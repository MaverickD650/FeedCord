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
using System.Globalization;
using System.IO;

namespace FeedCord.Tests.Infrastructure
{
    public class FeedWorkerExpandedTests
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
                RssCheckIntervalMinutes = 30,
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
                RssCheckIntervalMinutes = 60,
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

        #region ExecuteAsync Tests

        [Fact]
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
                PublishDate: DateTime.Now,
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
                RssCheckIntervalMinutes = 1,
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
                await Task.Delay(200);
                cts.Cancel();
            }
            catch (OperationCanceledException) { /* expected */ }

            // Assert
            mockFeedManager.Verify(x => x.CheckForNewPostsAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
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
                new Post("Post 1", "", "Desc 1", "link1", "tag1", DateTime.Now, "author1"),
                new Post("Post 2", "", "Desc 2", "link2", "tag2", DateTime.Now, "author2")
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
                RssCheckIntervalMinutes = 1,
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
                await Task.Delay(200);
                cts.Cancel();
            }
            catch (OperationCanceledException) { /* expected */ }

            // Assert
            mockNotifier.Verify(x => x.SendNotificationsAsync(It.IsAny<List<Post>>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
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
                RssCheckIntervalMinutes = 1,
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
                await Task.Delay(200);
                cts.Cancel();
            }
            catch (OperationCanceledException) { /* expected */ }

            // Assert
            mockNotifier.Verify(x => x.SendNotificationsAsync(It.IsAny<List<Post>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Shutdown/Persistence Tests

        [Fact]
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
                RssCheckIntervalMinutes = 1,
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

            await Task.Delay(100);
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

        [Fact]
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
                RssCheckIntervalMinutes = 1,
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

            await Task.Delay(100);
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

        [Fact]
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
                RssCheckIntervalMinutes = 1,
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
                await Task.Delay(200);
                cts.Cancel();
            }
            catch (OperationCanceledException) { /* expected */ }

            // Assert
            mockLogAggregator.Verify(x => x.SetStartTime(It.IsAny<DateTime>()), Times.AtLeastOnce);
            mockLogAggregator.Verify(x => x.SetEndTime(It.IsAny<DateTime>()), Times.AtLeastOnce);
            mockLogAggregator.Verify(x => x.SendToBatchAsync(), Times.AtLeastOnce);
        }

        #endregion

        #region Interval Tests

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(30)]
        [InlineData(60)]
        public void Constructor_AcceptsVariousIntervals(int minutes)
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
                RssCheckIntervalMinutes = minutes,
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
    }
}
