using FeedCord.Common;
using FeedCord.Core.Interfaces;
using FeedCord.Services.Interfaces;

namespace FeedCord.Infrastructure.Workers
{
  public class FeedWorker : BackgroundService
  {
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogAggregator _logAggregator;
    private readonly ILogger<FeedWorker> _logger;
    private readonly IFeedManager _feedManager;
    private readonly INotifier _notifier;
    private readonly IReferencePostStore _referencePostStore;

    private readonly bool _persistent;
    private readonly string _id;
    private readonly int _delayTime;
    private bool _isInitialized;


    public FeedWorker(
        IHostApplicationLifetime lifetime,
        ILogger<FeedWorker> logger,
        IFeedManager feedManager,
        INotifier notifier,
        Config config,
        ILogAggregator logAggregator,
        IReferencePostStore? referencePostStore = null)
    {
      _lifetime = lifetime;
      _logger = logger;
      _feedManager = feedManager;
      _notifier = notifier;
      _delayTime = config.RssCheckIntervalSeconds;
      _id = config.Id;
      _isInitialized = false;
      _persistent = config.PersistenceOnShutdown;
      _logAggregator = logAggregator;
      _referencePostStore = referencePostStore ?? new NoOpReferencePostStore();
        logger.LogInformation("{id} Created with check interval {Interval} seconds",
          _id, config.RssCheckIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      _lifetime.ApplicationStopping.Register(OnShutdown);

      try
      {
        while (!stoppingToken.IsCancellationRequested)
        {
          _logAggregator.SetStartTime(DateTime.UtcNow);

          try
          {
            await RunRoutineBackgroundProcessAsync(stoppingToken);
          }
          catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
          {
            break;
          }
          catch (Exception e)
          {
            _logger.LogCritical("Critical Error in Background Process: {E}", e);
            throw;
          }

          _logAggregator.SetEndTime(DateTime.UtcNow);
          await _logAggregator.SendToBatchAsync();
          await Task.Delay(TimeSpan.FromSeconds(_delayTime), stoppingToken);
        }
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        _logger.LogInformation("{id}: Feed worker stopped gracefully.", _id);
      }
    }

    private async Task RunRoutineBackgroundProcessAsync(CancellationToken stoppingToken)
    {
      if (!_isInitialized)
      {
        _logger.LogInformation("{id}: Initializing Url Checks..", _id);
        await _feedManager.InitializeUrlsAsync(stoppingToken);
        _isInitialized = true;
      }

      var posts = await _feedManager.CheckForNewPostsAsync(stoppingToken);

      if (posts.Count > 0)
      {
        _logger.LogInformation("{id}: Found {PostCount} new posts..", _id, posts.Count);
        await _notifier.SendNotificationsAsync(posts, stoppingToken);
      }
    }

    private void OnShutdown()
    {
      if (!_persistent) return;

      var data = _feedManager.GetAllFeedData();
      _referencePostStore.SaveReferencePosts(data);
    }

    private sealed class NoOpReferencePostStore : IReferencePostStore
    {
      public Dictionary<string, ReferencePost> LoadReferencePosts() => [];
      public void SaveReferencePosts(IReadOnlyDictionary<string, FeedState> data)
      {
      }
    }
  }
}
