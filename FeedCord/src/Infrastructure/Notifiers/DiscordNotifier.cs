using FeedCord.Common;
using FeedCord.Core.Interfaces;
using FeedCord.Services.Interfaces;

namespace FeedCord.Infrastructure.Notifiers
{
  public class DiscordNotifier : INotifier
  {
    private readonly ICustomHttpClient _httpClient;
    private readonly IDiscordPayloadService _discordPayloadService;
    private readonly ILogger<DiscordNotifier>? _logger;
    private readonly string _webhook;
    private readonly bool _forum;
    public DiscordNotifier(Config config, ICustomHttpClient httpClient, IDiscordPayloadService discordPayloadService, ILogger<DiscordNotifier>? logger = null)
    {
      _httpClient = httpClient;
      _discordPayloadService = discordPayloadService;
      _logger = logger;
      _webhook = config.DiscordWebhookUrl;
      _forum = config.Forum;
    }
    public async Task SendNotificationsAsync(List<Post> newPosts, CancellationToken cancellationToken = default)
    {
      foreach (var post in newPosts)
      {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
          // Build appropriate payload based on configured channel type
          // CustomHttpClient.PostAsyncWithFallback handles fallback to opposite type if needed
          var primaryPayload = _forum
              ? _discordPayloadService.BuildForumWithPost(post)
              : _discordPayloadService.BuildPayloadWithPost(post);

          var fallbackPayload = _forum
              ? _discordPayloadService.BuildPayloadWithPost(post)
              : _discordPayloadService.BuildForumWithPost(post);

          await _httpClient.PostAsyncWithFallback(_webhook, primaryPayload, fallbackPayload, _forum, cancellationToken);
        }
        catch (OperationCanceledException)
        {
          throw;
        }
        catch (Exception ex)
        {
          _logger?.LogError(ex, "Failed to send notification for post: {PostTitle}", post.Title);
        }
      }
    }
  }
}
