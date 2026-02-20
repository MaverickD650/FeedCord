using Xunit;
using Moq;
using FeedCord.Infrastructure.Notifiers;
using FeedCord.Common;
using FeedCord.Core.Interfaces;
using FeedCord.Services.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace FeedCord.Tests.Infrastructure;

public class DiscordNotifierTests
{
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
        var mockHttpClient = new Mock<ICustomHttpClient>();
        var mockPayloadService = new Mock<IDiscordPayloadService>();
        mockPayloadService.Setup(x => x.BuildPayloadWithPost(It.IsAny<Post>())).Returns(new System.Net.Http.StringContent("{}"));
        mockPayloadService.Setup(x => x.BuildForumWithPost(It.IsAny<Post>())).Returns(new System.Net.Http.StringContent("{}"));
        mockHttpClient.Setup(x => x.PostAsyncWithFallback(It.IsAny<string>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var notifier = new DiscordNotifier(config, mockHttpClient.Object, mockPayloadService.Object);
        var posts = new List<Post> { new Post("title", "img", "desc", "link", "tag", System.DateTime.Now, "author") };

        // Act & Assert
        await notifier.SendNotificationsAsync(posts);
        mockHttpClient.Verify(x => x.PostAsyncWithFallback(It.IsAny<string>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendNotificationsAsync_ContinuesWithoutThrowingOnFailure()
    {
        // Arrange
        var config = new Config {
            Id = "TestFeed",
            RssUrls = new string[] { },
            YoutubeUrls = new string[] { },
            DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
            Forum = false
        };
        var mockHttpClient = new Mock<ICustomHttpClient>();
        var mockPayloadService = new Mock<IDiscordPayloadService>();
        mockPayloadService.Setup(x => x.BuildPayloadWithPost(It.IsAny<Post>())).Returns(new System.Net.Http.StringContent("{}"));
        mockPayloadService.Setup(x => x.BuildForumWithPost(It.IsAny<Post>())).Returns(new System.Net.Http.StringContent("{}"));
        mockHttpClient.Setup(x => x.PostAsyncWithFallback(It.IsAny<string>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(new System.Exception("fail"));
        var notifier = new DiscordNotifier(config, mockHttpClient.Object, mockPayloadService.Object);
        var posts = new List<Post> { new Post("title", "img", "desc", "link", "tag", System.DateTime.Now, "author") };

        // Act
        var exception = await Record.ExceptionAsync(() => notifier.SendNotificationsAsync(posts));

        // Assert
        Assert.Null(exception);
        mockHttpClient.Verify(x => x.PostAsyncWithFallback(It.IsAny<string>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<System.Net.Http.StringContent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
