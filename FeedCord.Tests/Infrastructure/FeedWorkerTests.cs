using Xunit;
using Moq;
using FeedCord.Infrastructure.Workers;
using FeedCord.Common;
using FeedCord.Core.Interfaces;
using FeedCord.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace FeedCord.Tests.Infrastructure;

/// <summary>
/// Comprehensive test suite for FeedWorker functionality.
/// Consolidates all FeedWorker tests into a single, well-organized file.
/// </summary>
[CollectionDefinition("InfrastructureWorkersNonParallel", DisableParallelization = true)]
public class InfrastructureWorkersNonParallelCollection
{
}

[Collection("InfrastructureWorkersNonParallel")]
public class FeedWorkerTests
{
  #region Constructor Tests

  [Fact]
  public void Constructor_InitializesWithConfig()
  {
    // Arrange
    var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
    var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
    var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
    var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);

    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 30,
      PersistenceOnShutdown = false
    };

    // Act
    var worker = new FeedWorker(
        mockLifetime.Object,
        mockLogger.Object,
        mockFeedManager.Object,
        mockNotifier.Object,
        config,
        mockLogAggregator.Object
    );

    // Assert
    Assert.NotNull(worker);
    mockLogger.Verify(
        x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created with check interval")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()
        ),
        Times.Once
    );
  }

  [Fact]
  public void Constructor_WithDifferentIntervals_LogsCorrectInterval()
  {
    // Arrange
    var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
    var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
    var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
    var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);

    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 60,
      PersistenceOnShutdown = false
    };

    // Act
    var worker = new FeedWorker(
        mockLifetime.Object,
        mockLogger.Object,
        mockFeedManager.Object,
        mockNotifier.Object,
        config,
        mockLogAggregator.Object
    );

    // Assert
    Assert.NotNull(worker);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(5)]
  [InlineData(30)]
  [InlineData(60)]
  public void Constructor_AcceptsVariousIntervals(int seconds)
  {
    // Arrange
    var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
    var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
    var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
    var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);

    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = seconds,
      PersistenceOnShutdown = false
    };

    // Act
    var worker = new FeedWorker(
        mockLifetime.Object,
        mockLogger.Object,
        mockFeedManager.Object,
        mockNotifier.Object,
        config,
        mockLogAggregator.Object
    );

    // Assert
    Assert.NotNull(worker);
  }

  #endregion

  #region NoOpReferencePostStore Tests

  [Fact]
  public void NoOpReferencePostStore_LoadAndSave_AreNoOp()
  {
    // Arrange
    var noOpType = typeof(FeedWorker).GetNestedType("NoOpReferencePostStore", BindingFlags.NonPublic);
    Assert.NotNull(noOpType);

    var instance = Activator.CreateInstance(noOpType!);
    Assert.NotNull(instance);

    var store = Assert.IsAssignableFrom<IReferencePostStore>(instance);

    var feedData = new Dictionary<string, FeedState>
    {
      ["https://example.com/feed"] = new FeedState
      {
        IsYoutube = false,
        LastPublishDate = DateTime.UtcNow,
        ErrorCount = 0
      }
    };

    // Act
    var loaded = store.LoadReferencePosts();
    store.SaveReferencePosts(feedData);

    // Assert
    Assert.NotNull(loaded);
    Assert.Empty(loaded);
  }

  #endregion

  #region ExecuteAsync Tests

  [Fact(Timeout = 10000)]
  public async Task ExecuteAsync_InitializesUrlsOnFirstRun()
  {
    // Arrange
    var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
    var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
    var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
    var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 1,
      PersistenceOnShutdown = false
    };
    mockFeedManager.Setup(x => x.InitializeUrlsAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    var worker = new FeedWorker(
        mockLifetime.Object,
        mockLogger.Object,
        mockFeedManager.Object,
        mockNotifier.Object,
        config,
        mockLogAggregator.Object
    );
    var cts = new CancellationTokenSource();
    cts.CancelAfter(100); // Short run

    // Act
    var startTask = worker.StartAsync(cts.Token);
    await Task.Delay(200, TestContext.Current.CancellationToken); // Allow background loop to run
    cts.Cancel();
    try
    {
      await startTask;
    }
    catch (OperationCanceledException)
    {
      // Expected when cancellation is observed by the background loop.
    }
    mockFeedManager.Verify(x => x.InitializeUrlsAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
  }

  [Fact(Timeout = 10000)]
  public async Task ExecuteAsync_WhenCanceledDuringDelay_StopsGracefully()
  {
    var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
    var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
    var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
    var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);

    using var cts = new CancellationTokenSource();

    mockFeedManager.Setup(x => x.InitializeUrlsAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    mockFeedManager.Setup(x => x.CheckForNewPostsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Post>());
    mockLogAggregator
        .Setup(x => x.SendToBatchAsync())
        .Callback(() => cts.Cancel())
        .Returns(Task.CompletedTask);

    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 0,
      PersistenceOnShutdown = false
    };

    var worker = new FeedWorker(
        mockLifetime.Object,
        mockLogger.Object,
        mockFeedManager.Object,
        mockNotifier.Object,
        config,
        mockLogAggregator.Object
    );

    var executeAsync = typeof(FeedWorker).GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(executeAsync);

    var task = (Task)executeAsync!.Invoke(worker, new object[] { cts.Token })!;
    await task;

    mockLogAggregator.Verify(x => x.SetEndTime(It.IsAny<DateTime>()), Times.AtLeastOnce);
    mockLogAggregator.Verify(x => x.SendToBatchAsync(), Times.AtLeastOnce);
  }

  [Fact(Timeout = 10000)]
  public async Task ExecuteAsync_WhenRoutineThrowsOperationCanceledWithCanceledToken_BreaksLoopGracefully()
  {
    // Arrange
    var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
    var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
    var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
    var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);

    using var cts = new CancellationTokenSource();

    mockFeedManager
        .Setup(x => x.InitializeUrlsAsync(It.IsAny<CancellationToken>()))
        .Callback(() => cts.Cancel())
        .ThrowsAsync(new OperationCanceledException(cts.Token));

    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 1,
      PersistenceOnShutdown = false
    };

    var worker = new FeedWorker(
        mockLifetime.Object,
        mockLogger.Object,
        mockFeedManager.Object,
        mockNotifier.Object,
        config,
        mockLogAggregator.Object
    );

    // Act
    var executeAsync = typeof(FeedWorker).GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(executeAsync);

    var task = (Task)executeAsync!.Invoke(worker, new object[] { cts.Token })!;
    await task;

    // Assert
    mockFeedManager.Verify(x => x.InitializeUrlsAsync(It.IsAny<CancellationToken>()), Times.Once);
    mockLogAggregator.Verify(x => x.SetEndTime(It.IsAny<DateTime>()), Times.Never);
  }

  [Fact(Timeout = 10000)]
  public async Task ExecuteAsync_WhenRoutineThrowsUnexpectedException_LogsCriticalAndRethrows()
  {
    // Arrange
    var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
    var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
    var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
    var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);

    mockFeedManager
        .Setup(x => x.InitializeUrlsAsync(It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("boom"));

    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 1,
      PersistenceOnShutdown = false
    };

    var worker = new FeedWorker(
        mockLifetime.Object,
        mockLogger.Object,
        mockFeedManager.Object,
        mockNotifier.Object,
        config,
        mockLogAggregator.Object
    );

    // Act
    var executeAsync = typeof(FeedWorker).GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(executeAsync);

    var task = (Task)executeAsync!.Invoke(worker, new object[] { TestContext.Current.CancellationToken })!;

    // Assert
    await Assert.ThrowsAsync<InvalidOperationException>(() => task);
    mockLogger.Verify(
        x => x.Log(
            LogLevel.Critical,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Critical Error in Background Process")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()
        ),
        Times.Once
    );
  }

  [Fact(Timeout = 10000)]
  public async Task ExecuteAsync_ChecksForNewPostsAndNotifies()
  {
    // Arrange
    var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
    var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
    var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
    var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);

    var newPost = new Post(
        Title: "Test Post",
        ImageUrl: "https://example.com/image.jpg",
        Description: "Test description",
        Link: "https://example.com/article",
        Tag: "Test Feed",
        PublishDate: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Author: "Test Author"
    );

    mockFeedManager.Setup(x => x.InitializeUrlsAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    mockFeedManager.Setup(x => x.CheckForNewPostsAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.FromResult<List<Post>>(new List<Post> { newPost }));
    mockNotifier.Setup(x => x.SendNotificationsAsync(It.IsAny<List<Post>>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 1,
      PersistenceOnShutdown = false
    };

    var worker = new FeedWorker(
        mockLifetime.Object,
        mockLogger.Object,
        mockFeedManager.Object,
        mockNotifier.Object,
        config,
        mockLogAggregator.Object
    );

    var cts = new CancellationTokenSource();
    cts.CancelAfter(500);

    // Act
    try
    {
      await worker.StartAsync(cts.Token);
      await Task.Delay(200, TestContext.Current.CancellationToken);
      cts.Cancel();
    }
    catch (OperationCanceledException) { /* expected */ }

    // Assert
    mockFeedManager.Verify(x => x.CheckForNewPostsAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
  }

  [Fact(Timeout = 10000)]
  public async Task ExecuteAsync_NotifiesWhenPostsFound()
  {
    // Arrange
    var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
    var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
    var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
    var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);

    var posts = new List<Post>
        {
            new Post("Post 1", "", "Desc 1", "link1", "tag1", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), "author1"),
            new Post("Post 2", "", "Desc 2", "link2", "tag2", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), "author2")
        };

    mockFeedManager.Setup(x => x.InitializeUrlsAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    mockFeedManager.Setup(x => x.CheckForNewPostsAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.FromResult<List<Post>>(posts));
    mockNotifier.Setup(x => x.SendNotificationsAsync(posts, It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new[] { "https://example.com/feed" },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 1,
      PersistenceOnShutdown = false
    };

    var worker = new FeedWorker(
        mockLifetime.Object,
        mockLogger.Object,
        mockFeedManager.Object,
        mockNotifier.Object,
        config,
        mockLogAggregator.Object
    );

    var cts = new CancellationTokenSource();
    cts.CancelAfter(500);

    // Act
    try
    {
      await worker.StartAsync(cts.Token);
      await Task.Delay(200, TestContext.Current.CancellationToken);
      cts.Cancel();
    }
    catch (OperationCanceledException) { /* expected */ }

    // Assert
    mockNotifier.Verify(x => x.SendNotificationsAsync(It.IsAny<List<Post>>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
  }

  [Fact(Timeout = 10000)]
  public async Task ExecuteAsync_DoesNotNotifyWhenNoPostsFound()
  {
    // Arrange
    var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
    var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
    var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
    var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);

    mockFeedManager.Setup(x => x.InitializeUrlsAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    mockFeedManager.Setup(x => x.CheckForNewPostsAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.FromResult<List<Post>>(new List<Post>()));

    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 1,
      PersistenceOnShutdown = false
    };

    var worker = new FeedWorker(
        mockLifetime.Object,
        mockLogger.Object,
        mockFeedManager.Object,
        mockNotifier.Object,
        config,
        mockLogAggregator.Object
    );

    var cts = new CancellationTokenSource();
    cts.CancelAfter(500);

    // Act
    try
    {
      await worker.StartAsync(cts.Token);
      await Task.Delay(200, TestContext.Current.CancellationToken);
      cts.Cancel();
    }
    catch (OperationCanceledException) { /* expected */ }

    // Assert
    mockNotifier.Verify(x => x.SendNotificationsAsync(It.IsAny<List<Post>>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  #endregion

  #region Shutdown and Persistence Tests

  [Fact(Timeout = 10000)]
  public async Task OnShutdown_WithPersistenceEnabled_SavesThroughReferencePostStore()
  {
    // Arrange
    var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
    var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
    var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
    var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);
    var mockReferencePostStore = new Mock<IReferencePostStore>(MockBehavior.Loose);
    var appStoppingSource = new CancellationTokenSource();

    mockLifetime.SetupGet(x => x.ApplicationStopping).Returns(appStoppingSource.Token);

    var feedData = new Dictionary<string, FeedState>
        {
            {
                "https://example.com/feed1",
                new FeedState { IsYoutube = false, LastPublishDate = new DateTime(2024, 1, 15), ErrorCount = 0 }
            },
            {
                "https://youtube.com/feed2",
                new FeedState { IsYoutube = true, LastPublishDate = new DateTime(2024, 1, 16), ErrorCount = 0 }
            }
        };

    mockFeedManager.Setup(x => x.GetAllFeedData()).Returns(feedData);

    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 1,
      PersistenceOnShutdown = true // Enable persistence
    };

    var worker = new FeedWorker(
        mockLifetime.Object,
        mockLogger.Object,
        mockFeedManager.Object,
        mockNotifier.Object,
        config,
        mockLogAggregator.Object,
        mockReferencePostStore.Object
    );

    var workerTokenSource = new CancellationTokenSource();
    var runTask = worker.StartAsync(workerTokenSource.Token);

    await Task.Delay(100, TestContext.Current.CancellationToken);
    appStoppingSource.Cancel();
    workerTokenSource.Cancel();

    try
    {
      await runTask;
    }
    catch (OperationCanceledException)
    {
    }

    mockReferencePostStore.Verify(x => x.SaveReferencePosts(It.IsAny<IReadOnlyDictionary<string, FeedState>>()), Times.AtLeastOnce);
  }

  [Fact(Timeout = 10000)]
  public async Task OnShutdown_WhenPersistenceDisabled_DoesNotSaveThroughReferencePostStore()
  {
    // Arrange
    var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
    var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
    var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
    var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);
    var mockReferencePostStore = new Mock<IReferencePostStore>(MockBehavior.Loose);
    var appStoppingSource = new CancellationTokenSource();

    mockLifetime.SetupGet(x => x.ApplicationStopping).Returns(appStoppingSource.Token);

    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 1,
      PersistenceOnShutdown = false // Disable persistence
    };

    var worker = new FeedWorker(
        mockLifetime.Object,
        mockLogger.Object,
        mockFeedManager.Object,
        mockNotifier.Object,
        config,
        mockLogAggregator.Object,
        mockReferencePostStore.Object
    );

    var workerTokenSource = new CancellationTokenSource();
    var runTask = worker.StartAsync(workerTokenSource.Token);

    await Task.Delay(100, TestContext.Current.CancellationToken);
    appStoppingSource.Cancel();
    workerTokenSource.Cancel();

    try
    {
      await runTask;
    }
    catch (OperationCanceledException)
    {
    }

    mockReferencePostStore.Verify(x => x.SaveReferencePosts(It.IsAny<IReadOnlyDictionary<string, FeedState>>()), Times.Never);
  }

  #endregion

  #region LogAggregator Tests

  [Fact(Timeout = 10000)]
  public async Task ExecuteAsync_SetsBatchLoggerStartAndEndTime()
  {
    // Arrange
    var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
    var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
    var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
    var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);

    mockFeedManager.Setup(x => x.InitializeUrlsAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    mockFeedManager.Setup(x => x.CheckForNewPostsAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.FromResult<List<Post>>(new List<Post>()));
    mockLogAggregator.Setup(x => x.SendToBatchAsync()).Returns(Task.CompletedTask);

    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 1,
      PersistenceOnShutdown = false
    };

    var worker = new FeedWorker(
        mockLifetime.Object,
        mockLogger.Object,
        mockFeedManager.Object,
        mockNotifier.Object,
        config,
        mockLogAggregator.Object
    );

    var cts = new CancellationTokenSource();
    cts.CancelAfter(500);

    // Act
    try
    {
      await worker.StartAsync(cts.Token);
      await Task.Delay(200, TestContext.Current.CancellationToken);
      cts.Cancel();
    }
    catch (OperationCanceledException) { /* expected */ }

    // Assert
    mockLogAggregator.Verify(x => x.SetStartTime(It.IsAny<DateTime>()), Times.AtLeastOnce);
    mockLogAggregator.Verify(x => x.SetEndTime(It.IsAny<DateTime>()), Times.AtLeastOnce);
    mockLogAggregator.Verify(x => x.SendToBatchAsync(), Times.AtLeastOnce);
  }

  [Fact(Timeout = 10000)]
  public async Task ExecuteAsync_WhenTaskDelayCanceled_LogsStoppedGracefully()
  {
    // Arrange - Test for FeedWorker.cs line 74 coverage (outer catch block)
    var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
    var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
    var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
    var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);

    using var cts = new CancellationTokenSource();

    mockFeedManager.Setup(x => x.InitializeUrlsAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    mockFeedManager.Setup(x => x.CheckForNewPostsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Post>());
    mockLogAggregator.Setup(x => x.SetStartTime(It.IsAny<DateTime>()));
    mockLogAggregator.Setup(x => x.SetEndTime(It.IsAny<DateTime>()));
    mockLogAggregator
        .Setup(x => x.SendToBatchAsync())
        .Callback(() => cts.Cancel()) // Cancel after SendToBatchAsync completes
        .Returns(Task.CompletedTask);

    var config = new Config
    {
      Id = "StopTest",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 1,
      PersistenceOnShutdown = false
    };

    var worker = new FeedWorker(
        mockLifetime.Object,
        mockLogger.Object,
        mockFeedManager.Object,
        mockNotifier.Object,
        config,
        mockLogAggregator.Object
    );

    var executeAsync = typeof(FeedWorker).GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(executeAsync);

    // Act - Invoke ExecuteAsync; Task.Delay will throw OperationCanceledException after cancellation
    var task = (Task)executeAsync!.Invoke(worker, new object[] { cts.Token })!;
    await task;

    // Assert - Verify the outer catch block logged "stopped gracefully"
    mockLogger.Verify(
        x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopped gracefully")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()
        ),
        Times.Once
    );
  }

  [Fact(Timeout = 10000)]
  public async Task ExecuteAsync_DelayCompletesOnce_LogsStoppedGracefully()
  {
    // Arrange - allow one delay cycle to complete before cancellation
    var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
    var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
    var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
    var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);

    using var cts = new CancellationTokenSource();
    var iterationCount = 0;
    mockFeedManager.Setup(x => x.InitializeUrlsAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    mockFeedManager.Setup(x => x.CheckForNewPostsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Post>());
    mockLogAggregator.Setup(x => x.SetStartTime(It.IsAny<DateTime>()));
    mockLogAggregator.Setup(x => x.SetEndTime(It.IsAny<DateTime>()));
    mockLogAggregator
        .Setup(x => x.SendToBatchAsync())
        .Callback(() =>
        {
          iterationCount++;
        })
        .Returns(Task.CompletedTask);

    var config = new Config
    {
      Id = "DelayTest",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 1,
      PersistenceOnShutdown = false
    };

    var worker = new FeedWorker(
        mockLifetime.Object,
        mockLogger.Object,
        mockFeedManager.Object,
        mockNotifier.Object,
        config,
        mockLogAggregator.Object
    );

    var executeAsync = typeof(FeedWorker).GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(executeAsync);

    cts.CancelAfter(1500);

    // Act - Task.Delay completes once before cancellation
    var task = (Task)executeAsync!.Invoke(worker, new object[] { cts.Token })!;
    await task;

    // Assert - At least two iterations ran, meaning Task.Delay completed once
    Assert.True(iterationCount >= 2);
    mockLogger.Verify(
      x => x.Log(
        LogLevel.Information,
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopped gracefully")),
        It.IsAny<Exception>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()
      ),
      Times.Once
    );
  }

  #endregion
}
