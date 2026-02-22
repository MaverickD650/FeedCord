using Xunit;
using Moq;
using FeedCord.Infrastructure.Notifiers;
using FeedCord.Common;
using FeedCord.Core.Interfaces;
using FeedCord.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FeedCord.Tests.Infrastructure;

public class DiscordNotifierTests
{
  [Fact]
  public async Task SendNotificationsAsync_UsesForumPrimaryAndPayloadFallback_WhenForumEnabled()
  {
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      Forum = true
    };

    var post = new Post("title", "img", "desc", "link", "tag", System.DateTime.Now, "author");
    var primaryPayload = new System.Net.Http.StringContent("{\"type\":\"forum\"}");
    var fallbackPayload = new System.Net.Http.StringContent("{\"type\":\"webhook\"}");

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    var mockPayloadService = new Mock<IDiscordPayloadService>(MockBehavior.Strict);

    mockPayloadService
        .Setup(x => x.BuildForumWithPost(post))
        .Returns(primaryPayload);
    mockPayloadService
        .Setup(x => x.BuildPayloadWithPost(post))
        .Returns(fallbackPayload);

    mockHttpClient
        .Setup(x => x.PostAsyncWithFallback(config.DiscordWebhookUrl, primaryPayload, fallbackPayload, true, It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var notifier = new DiscordNotifier(config, mockHttpClient.Object, mockPayloadService.Object);

    await notifier.SendNotificationsAsync(new List<Post> { post }, TestContext.Current.CancellationToken);

    mockPayloadService.Verify(x => x.BuildForumWithPost(post), Times.Once);
    mockPayloadService.Verify(x => x.BuildPayloadWithPost(post), Times.Once);
    mockHttpClient.Verify(x => x.PostAsyncWithFallback(config.DiscordWebhookUrl, primaryPayload, fallbackPayload, true, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task SendNotificationsAsync_SendsAllPostsWithoutException()
  {
    // Arrange
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      Forum = false
    };
    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Loose);
    var mockPayloadService = new Mock<IDiscordPayloadService>(MockBehavior.Loose);
    mockPayloadService.Setup(x => x.BuildPayloadWithPost(It.IsAny<Post>())).Returns(new System.Net.Http.StringContent("{}"));
    mockPayloadService.Setup(x => x.BuildForumWithPost(It.IsAny<Post>())).Returns(new System.Net.Http.StringContent("{}"));
    mockHttpClient.Setup(x => x.PostAsyncWithFallback(It.IsAny<string>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    var notifier = new DiscordNotifier(config, mockHttpClient.Object, mockPayloadService.Object);
    var posts = new List<Post> { new Post("title", "img", "desc", "link", "tag", System.DateTime.Now, "author") };

    // Act & Assert
    await notifier.SendNotificationsAsync(posts, TestContext.Current.CancellationToken);
    mockHttpClient.Verify(x => x.PostAsyncWithFallback(It.IsAny<string>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task SendNotificationsAsync_ContinuesWithoutThrowingOnFailure()
  {
    // Arrange
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      Forum = false
    };
    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Loose);
    var mockPayloadService = new Mock<IDiscordPayloadService>(MockBehavior.Loose);
    mockPayloadService.Setup(x => x.BuildPayloadWithPost(It.IsAny<Post>())).Returns(new System.Net.Http.StringContent("{}"));
    mockPayloadService.Setup(x => x.BuildForumWithPost(It.IsAny<Post>())).Returns(new System.Net.Http.StringContent("{}"));
    mockHttpClient.Setup(x => x.PostAsyncWithFallback(It.IsAny<string>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.FromException(new System.Exception("fail")));
    var notifier = new DiscordNotifier(config, mockHttpClient.Object, mockPayloadService.Object);
    var posts = new List<Post> { new Post("title", "img", "desc", "link", "tag", System.DateTime.Now, "author") };

    // Act & Assert
    await notifier.SendNotificationsAsync(posts, TestContext.Current.CancellationToken);
    mockHttpClient.Verify(x => x.PostAsyncWithFallback(It.IsAny<string>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task SendNotificationsAsync_RethrowsOperationCanceledException()
  {
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      Forum = false
    };

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Loose);
    var mockPayloadService = new Mock<IDiscordPayloadService>(MockBehavior.Loose);
    mockPayloadService.Setup(x => x.BuildPayloadWithPost(It.IsAny<Post>())).Returns(new System.Net.Http.StringContent("{}"));
    mockPayloadService.Setup(x => x.BuildForumWithPost(It.IsAny<Post>())).Returns(new System.Net.Http.StringContent("{}"));
    mockHttpClient
        .Setup(x => x.PostAsyncWithFallback(It.IsAny<string>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new OperationCanceledException("cancelled"));

    var notifier = new DiscordNotifier(config, mockHttpClient.Object, mockPayloadService.Object);
    var posts = new List<Post> { new Post("title", "img", "desc", "link", "tag", System.DateTime.Now, "author") };

    await Assert.ThrowsAsync<OperationCanceledException>(() => notifier.SendNotificationsAsync(posts, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task SendNotificationsAsync_LogsError_WhenFailureOccursAndLoggerProvided()
  {
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      Forum = false
    };

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Loose);
    var mockPayloadService = new Mock<IDiscordPayloadService>(MockBehavior.Loose);
    var mockLogger = new Mock<ILogger<DiscordNotifier>>(MockBehavior.Loose);

    mockPayloadService.Setup(x => x.BuildPayloadWithPost(It.IsAny<Post>())).Returns(new System.Net.Http.StringContent("{}"));
    mockPayloadService.Setup(x => x.BuildForumWithPost(It.IsAny<Post>())).Returns(new System.Net.Http.StringContent("{}"));
    mockHttpClient
        .Setup(x => x.PostAsyncWithFallback(It.IsAny<string>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new System.Exception("fail"));

    var notifier = new DiscordNotifier(config, mockHttpClient.Object, mockPayloadService.Object, mockLogger.Object);
    var posts = new List<Post> { new Post("title", "img", "desc", "link", "tag", System.DateTime.Now, "author") };

    await notifier.SendNotificationsAsync(posts, TestContext.Current.CancellationToken);

    mockLogger.Verify(
        x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send notification for post")),
            It.IsAny<System.Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
  }
}
