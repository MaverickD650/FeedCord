using Xunit;
using Moq;
using FeedCord.Infrastructure.Workers;
using FeedCord.Common;
using FeedCord.Core.Interfaces;
using FeedCord.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace FeedCord.Tests.Infrastructure;

public class FeedWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_InitializesUrlsOnFirstRun()
    {
        // Arrange
        var mockLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
        var mockLogger = new Mock<ILogger<FeedWorker>>(MockBehavior.Loose);
        var mockFeedManager = new Mock<IFeedManager>(MockBehavior.Loose);
        var mockNotifier = new Mock<INotifier>(MockBehavior.Loose);
        var mockLogAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);
        var config = new Config {
            Id = "TestFeed",
            RssUrls = new string[] { },
            YoutubeUrls = new string[] { },
            DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
            RssCheckIntervalMinutes = 1,
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
        await Task.Delay(200); // Allow background loop to run
        cts.Cancel();
        try { await startTask; } catch { /* ignore cancellation */ }
        mockFeedManager.Verify(x => x.InitializeUrlsAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
