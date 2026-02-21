using FeedCord.Common;
using FeedCord.Core.Interfaces;
using FeedCord.Services;
using FeedCord.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Xunit;

namespace FeedCord.Tests.Services;

public class FeedManagerCoverageTests
{
    private const string RssUrl = "https://example.com/rss";

    [Fact]
    public async Task InitializeUrlsAsync_WithReferenceStoreSeed_UsesStoredLastPublishDate()
    {
        var referenceDate = new DateTime(2024, 06, 01, 12, 0, 0, DateTimeKind.Utc);
        var config = CreateConfig(rssUrls: [RssUrl]);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>
            {
                [RssUrl] = new() { IsYoutube = false, LastRunDate = referenceDate }
            });

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(RssUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
        var mockLogger = new Mock<ILogger<FeedManager>>(MockBehavior.Loose);
        var mockAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);
        var mockFilter = new Mock<IPostFilterService>(MockBehavior.Loose);

        var manager = new FeedManager(
            config,
            mockHttpClient.Object,
            mockRssParser.Object,
            mockLogger.Object,
            mockAggregator.Object,
            mockFilter.Object,
            mockStore.Object);

        await manager.InitializeUrlsAsync();

        var state = manager.GetAllFeedData()[RssUrl];
        Assert.Equal(referenceDate, state.LastPublishDate);
        mockRssParser.Verify(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckForNewPostsAsync_WithFreshPosts_AppliesFilterAndReturnsOnlyIncluded()
    {
        var initialDate = new DateTime(2024, 01, 01, 10, 0, 0, DateTimeKind.Utc);
        var freshDate = initialDate.AddMinutes(30);
        var config = CreateConfig(rssUrls: [RssUrl]);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Loose);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>());

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Loose);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(RssUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<rss></rss>")
            });

        var oldPost = CreatePost("old", initialDate);
        var newPost = CreatePost("new", freshDate);

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Loose);
        mockRssParser
            .SetupSequence(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([oldPost])
            .ReturnsAsync([oldPost, newPost]);

        var mockFilter = new Mock<IPostFilterService>(MockBehavior.Loose);
        mockFilter
            .Setup(x => x.ShouldIncludePost(It.IsAny<Post>(), RssUrl))
            .Returns<Post, string>((post, _) => post.Title == "new");

        var mockLogger = new Mock<ILogger<FeedManager>>(MockBehavior.Loose);
        var mockAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);

        var manager = new FeedManager(
            config,
            mockHttpClient.Object,
            mockRssParser.Object,
            mockLogger.Object,
            mockAggregator.Object,
            mockFilter.Object,
            mockStore.Object);

        await manager.InitializeUrlsAsync();
        var results = await manager.CheckForNewPostsAsync();

        Assert.Single(results);
        Assert.Equal("new", results[0].Title);
        mockFilter.Verify(x => x.ShouldIncludePost(It.IsAny<Post>(), RssUrl), Times.Once);
    }

    [Fact]
    public async Task CheckForNewPostsAsync_WithNoFreshPosts_AddsLatestPostToAggregator()
    {
        var referenceDate = new DateTime(2024, 03, 01, 10, 0, 0, DateTimeKind.Utc);
        var olderDate = referenceDate.AddMinutes(-10);
        var config = CreateConfig(rssUrls: [RssUrl]);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Loose);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>
            {
                [RssUrl] = new() { LastRunDate = referenceDate }
            });

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Loose);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(RssUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<rss></rss>")
            });

        var oldPost = CreatePost("old", olderDate);

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Loose);
        mockRssParser
            .Setup(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([oldPost]);

        var mockFilter = new Mock<IPostFilterService>(MockBehavior.Loose);
        var mockLogger = new Mock<ILogger<FeedManager>>(MockBehavior.Loose);
        var mockAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);

        var manager = new FeedManager(
            config,
            mockHttpClient.Object,
            mockRssParser.Object,
            mockLogger.Object,
            mockAggregator.Object,
            mockFilter.Object,
            mockStore.Object);

        await manager.InitializeUrlsAsync();
        var results = await manager.CheckForNewPostsAsync();

        Assert.Empty(results);
        mockAggregator.Verify(x => x.AddLatestUrlPost(RssUrl, It.Is<Post?>(p => p != null && p.Title == "old")), Times.Once);
    }

    [Fact]
    public async Task InitializeUrlsAsync_WhenHttpRequestExceptionHasNoStatus_RecordsBadRequest()
    {
        var config = CreateConfig(rssUrls: [RssUrl]);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Loose);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>());

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Loose);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(RssUrl, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Loose);
        var mockFilter = new Mock<IPostFilterService>(MockBehavior.Loose);
        var mockLogger = new Mock<ILogger<FeedManager>>(MockBehavior.Loose);
        var mockAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);

        var manager = new FeedManager(
            config,
            mockHttpClient.Object,
            mockRssParser.Object,
            mockLogger.Object,
            mockAggregator.Object,
            mockFilter.Object,
            mockStore.Object);

        await manager.InitializeUrlsAsync();

        mockAggregator.Verify(x => x.AddUrlResponse(RssUrl, (int)HttpStatusCode.BadRequest), Times.Once);
    }

    private static Config CreateConfig(string[]? rssUrls = null, string[]? youtubeUrls = null)
    {
        return new Config
        {
            Id = "CoverageFeed",
            RssUrls = rssUrls ?? [],
            YoutubeUrls = youtubeUrls ?? [],
            DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
            RssCheckIntervalMinutes = 30,
            DescriptionLimit = 250,
            Forum = false,
            MarkdownFormat = false,
            PersistenceOnShutdown = false,
            ConcurrentRequests = 5
        };
    }

    private static Post CreatePost(string title, DateTime publishDate)
    {
        return new Post(
            title,
            string.Empty,
            "desc",
            "https://example.com/post",
            "tag",
            publishDate,
            "author",
            []);
    }
}