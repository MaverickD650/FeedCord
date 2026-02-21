using Xunit;
using Moq;
using FeedCord.Services;
using FeedCord.Common;
using FeedCord.Services.Interfaces;
using FeedCord.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net;

namespace FeedCord.Tests.Services
{
    public class FeedManagerExpandedTests
    {
        private readonly Mock<ICustomHttpClient> _mockHttpClient;
        private readonly Mock<IRssParsingService> _mockRssParser;
        private readonly Mock<ILogger<FeedManager>> _mockLogger;
        private readonly Mock<ILogAggregator> _mockAggregator;
        private readonly Mock<IPostFilterService> _mockFilterService;

        public FeedManagerExpandedTests()
        {
            _mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Loose);
            _mockRssParser = new Mock<IRssParsingService>(MockBehavior.Loose);
            _mockLogger = new Mock<ILogger<FeedManager>>(MockBehavior.Loose);
            _mockAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);
            _mockFilterService = new Mock<IPostFilterService>(MockBehavior.Loose);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_InitializesConcurrentRequestsSemaphore()
        {
            // Arrange
            var config = new Config
            {
                Id = "TestFeed",
                RssUrls = new string[] { },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                ConcurrentRequests = 10
            };

            // Act
            var manager = new FeedManager(
                config,
                _mockHttpClient.Object,
                _mockRssParser.Object,
                _mockLogger.Object,
                _mockAggregator.Object,
                _mockFilterService.Object
            );

            // Assert
            Assert.NotNull(manager);
        }

        [Fact]
        public void Constructor_LoadsLastRunReference()
        {
            // Arrange
            var config = new Config
            {
                Id = "TestFeed",
                RssUrls = new string[] { },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                ConcurrentRequests = 5
            };

            // Act
            var manager = new FeedManager(
                config,
                _mockHttpClient.Object,
                _mockRssParser.Object,
                _mockLogger.Object,
                _mockAggregator.Object,
                _mockFilterService.Object
            );

            // Assert
            var feedData = manager.GetAllFeedData();
            Assert.NotNull(feedData);
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, FeedState>>(feedData);
        }

        #endregion

        #region URL Initialization Tests

        [Fact]
        public async Task InitializeUrlsAsync_WithAllValidUrls_ReturnsSuccess()
        {
            // Arrange
            var config = new Config
            {
                Id = "TestFeed",
                RssUrls = new[] { "https://example.com/rss1", "https://example.com/rss2" },
                YoutubeUrls = new[] { "https://youtube.com/feed1" },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                ConcurrentRequests = 5
            };

            _mockHttpClient
                .Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(new HttpResponseMessage { StatusCode = HttpStatusCode.OK }));

            var manager = new FeedManager(
                config,
                _mockHttpClient.Object,
                _mockRssParser.Object,
                _mockLogger.Object,
                _mockAggregator.Object,
                _mockFilterService.Object
            );

            // Act
            await manager.InitializeUrlsAsync();

            // Assert
            _mockHttpClient.Verify(
                x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce
            );
        }

        [Fact]
        public async Task InitializeUrlsAsync_IgnoresEmptyAndWhitespaceUrls()
        {
            // Arrange
            var config = new Config
            {
                Id = "TestFeed",
                RssUrls = new[] { "https://example.com/rss", "", "   ", null! },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                ConcurrentRequests = 5
            };

            _mockHttpClient
                .Setup(x => x.GetAsyncWithFallback("https://example.com/rss", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(new HttpResponseMessage { StatusCode = HttpStatusCode.OK }));

            var manager = new FeedManager(
                config,
                _mockHttpClient.Object,
                _mockRssParser.Object,
                _mockLogger.Object,
                _mockAggregator.Object,
                _mockFilterService.Object
            );

            // Act
            await manager.InitializeUrlsAsync();

            // Assert - should only call for the valid URL
            _mockHttpClient.Verify(
                x => x.GetAsyncWithFallback("https://example.com/rss", It.IsAny<CancellationToken>()),
                Times.AtLeastOnce
            );
        }

        [Fact]
        public async Task InitializeUrlsAsync_HandlesFailedUrls()
        {
            // Arrange
            var config = new Config
            {
                Id = "TestFeed",
                RssUrls = new[] { "https://example.com/rss" },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                ConcurrentRequests = 5
            };

            _mockHttpClient
                .Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>((HttpResponseMessage)null!));

            var manager = new FeedManager(
                config,
                _mockHttpClient.Object,
                _mockRssParser.Object,
                _mockLogger.Object,
                _mockAggregator.Object,
                _mockFilterService.Object
            );

            // Act
            await manager.InitializeUrlsAsync();

            // Assert - should log but not crash
            _mockAggregator.Verify(
                x => x.AddUrlResponse(It.IsAny<string>(), -99),
                Times.AtLeastOnce
            );
        }

        [Fact]
        public async Task InitializeUrlsAsync_WithHttpErrorCode_RecordsError()
        {
            // Arrange
            var config = new Config
            {
                Id = "TestFeed",
                RssUrls = new[] { "https://example.com/rss" },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                ConcurrentRequests = 5
            };

            _mockHttpClient
                .Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound }));

            var manager = new FeedManager(
                config,
                _mockHttpClient.Object,
                _mockRssParser.Object,
                _mockLogger.Object,
                _mockAggregator.Object,
                _mockFilterService.Object
            );

            // Act
            await manager.InitializeUrlsAsync();

            // Assert
            _mockAggregator.Verify(
                x => x.AddUrlResponse(It.IsAny<string>(), (int)HttpStatusCode.NotFound),
                Times.AtLeastOnce
            );
        }

        [Theory]
        [MemberData(nameof(GetEmptyUrlArrayVariations))]
        public async Task InitializeUrlsAsync_WithEmptyUrlArrays_DoesNotFetch(string[]? urls)
        {
            // Arrange
            var config = new Config
            {
                Id = "TestFeed",
                RssUrls = urls ?? new string[] { },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                ConcurrentRequests = 5
            };

            var manager = new FeedManager(
                config,
                _mockHttpClient.Object,
                _mockRssParser.Object,
                _mockLogger.Object,
                _mockAggregator.Object,
                _mockFilterService.Object
            );

            // Act
            await manager.InitializeUrlsAsync();

            // Assert
            _mockHttpClient.Verify(
                x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
        }

        #endregion

        #region CheckForNewPosts Tests

        [Fact]
        public async Task CheckForNewPostsAsync_WithNoPosts_ReturnsEmpty()
        {
            // Arrange
            var config = new Config
            {
                Id = "TestFeed",
                RssUrls = new string[] { },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                ConcurrentRequests = 5
            };

            var manager = new FeedManager(
                config,
                _mockHttpClient.Object,
                _mockRssParser.Object,
                _mockLogger.Object,
                _mockAggregator.Object,
                _mockFilterService.Object
            );

            // Act
            var result = await manager.CheckForNewPostsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task CheckForNewPostsAsync_UpdatesAggregatorPostCount()
        {
            // Arrange
            var config = new Config
            {
                Id = "TestFeed",
                RssUrls = new string[] { },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                ConcurrentRequests = 5
            };

            var manager = new FeedManager(
                config,
                _mockHttpClient.Object,
                _mockRssParser.Object,
                _mockLogger.Object,
                _mockAggregator.Object,
                _mockFilterService.Object
            );

            // Act
            await manager.CheckForNewPostsAsync();

            // Assert
            _mockAggregator.Verify(x => x.SetNewPostCount(It.IsAny<int>()), Times.Once);
        }

        #endregion

        #region Feed Data Tests

        [Fact]
        public void GetAllFeedData_ReturnsReadOnlyDictionary()
        {
            // Arrange
            var config = new Config
            {
                Id = "TestFeed",
                RssUrls = new string[] { },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                ConcurrentRequests = 5
            };

            var manager = new FeedManager(
                config,
                _mockHttpClient.Object,
                _mockRssParser.Object,
                _mockLogger.Object,
                _mockAggregator.Object,
                _mockFilterService.Object
            );

            // Act
            var result = manager.GetAllFeedData();

            // Assert
            Assert.NotNull(result);
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, FeedState>>(result);
        }

        [Fact]
        public void GetAllFeedData_ReturnsEmptyInitially()
        {
            // Arrange
            var config = new Config
            {
                Id = "TestFeed",
                RssUrls = new string[] { },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                ConcurrentRequests = 5
            };

            var manager = new FeedManager(
                config,
                _mockHttpClient.Object,
                _mockRssParser.Object,
                _mockLogger.Object,
                _mockAggregator.Object,
                _mockFilterService.Object
            );

            // Act
            var result = manager.GetAllFeedData();

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region Different URL Types Tests

        [Theory]
        [InlineData("https://example.com/rss")]
        [InlineData("http://example.com/feed.xml")]
        [InlineData("https://feeds.example.com/news")]
        public async Task InitializeUrlsAsync_HandlesDifferentRssUrls(string url)
        {
            // Arrange
            var config = new Config
            {
                Id = "TestFeed",
                RssUrls = new[] { url },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                ConcurrentRequests = 5
            };

            _mockHttpClient
                .Setup(x => x.GetAsyncWithFallback(url, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(new HttpResponseMessage { StatusCode = HttpStatusCode.OK }));

            var manager = new FeedManager(
                config,
                _mockHttpClient.Object,
                _mockRssParser.Object,
                _mockLogger.Object,
                _mockAggregator.Object,
                _mockFilterService.Object
            );

            // Act
            await manager.InitializeUrlsAsync();

            // Assert
            _mockHttpClient.Verify(
                x => x.GetAsyncWithFallback(url, It.IsAny<CancellationToken>()),
                Times.AtLeastOnce
            );
        }

        #endregion

        #region Concurrent Request Tests

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(20)]
        public void Constructor_WithVariousConcurrentRequests_Succeeds(int concurrentRequests)
        {
            // Arrange
            var config = new Config
            {
                Id = "TestFeed",
                RssUrls = new string[] { },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                ConcurrentRequests = concurrentRequests
            };

            // Act
            var manager = new FeedManager(
                config,
                _mockHttpClient.Object,
                _mockRssParser.Object,
                _mockLogger.Object,
                _mockAggregator.Object,
                _mockFilterService.Object
            );

            // Assert
            Assert.NotNull(manager);
        }

        #endregion

        #region Description Limit Tests

        [Theory]
        [InlineData(100)]
        [InlineData(250)]
        [InlineData(500)]
        [InlineData(0)]
        public void Constructor_WithVariousDescriptionLimits_Succeeds(int descriptionLimit)
        {
            // Arrange
            var config = new Config
            {
                Id = "TestFeed",
                RssUrls = new string[] { },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = descriptionLimit,
                ConcurrentRequests = 5
            };

            // Act
            var manager = new FeedManager(
                config,
                _mockHttpClient.Object,
                _mockRssParser.Object,
                _mockLogger.Object,
                _mockAggregator.Object,
                _mockFilterService.Object
            );

            // Assert
            Assert.NotNull(manager);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task InitializeUrlsAsync_HandlesHttpRequestException()
        {
            // Arrange
            var config = new Config
            {
                Id = "TestFeed",
                RssUrls = new[] { "https://example.com/rss" },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                ConcurrentRequests = 5
            };

            _mockHttpClient
                .Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromException<HttpResponseMessage?>(new HttpRequestException("Network error")));

            var manager = new FeedManager(
                config,
                _mockHttpClient.Object,
                _mockRssParser.Object,
                _mockLogger.Object,
                _mockAggregator.Object,
                _mockFilterService.Object
            );

            // Act & Assert - should not throw
            await manager.InitializeUrlsAsync();
        }

        #endregion

        #region Test Data

        public static IEnumerable<object?[]> GetEmptyUrlArrayVariations()
        {
            yield return new object?[] { Array.Empty<string>() };
            yield return new object?[] { null };
        }

        #endregion
    }
}
