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

    [Fact]
    public async Task InitializeUrlsAsync_WithDuplicateRssUrls_HitsTryAddFailurePath()
    {
        var duplicateUrl = "https://example.com/duplicate-rss";
        var config = CreateConfig(rssUrls: [duplicateUrl, duplicateUrl]);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Loose);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>());

        var responseSequence = new Queue<HttpResponseMessage?>([
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<rss></rss>") },
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<rss></rss>") }
        ]);

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(duplicateUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => responseSequence.Dequeue());

        var post = CreatePost("post", new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc));

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
        mockRssParser
            .Setup(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([post]);

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

        Assert.Single(manager.GetAllFeedData());
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to initialize URL")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InitializeUrlsAsync_WithYoutubeUserQuery_UsesDirectYoutubeParsingPath()
    {
        var youtubeUserUrl = "https://www.youtube.com/watch?user=channel-user";
        var config = CreateConfig(youtubeUrls: [youtubeUserUrl]);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Loose);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>());

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(youtubeUserUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var parsed = CreatePost("video", new DateTime(2024, 04, 01, 12, 0, 0, DateTimeKind.Utc));
        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
        mockRssParser
            .Setup(x => x.ParseYoutubeFeedAsync(youtubeUserUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parsed);

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

        var state = manager.GetAllFeedData()[youtubeUserUrl];
        Assert.True(state.IsYoutube);
        Assert.Equal(parsed.PublishDate, state.LastPublishDate);
        mockRssParser.Verify(x => x.ParseYoutubeFeedAsync(youtubeUserUrl, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitializeUrlsAsync_WhenCanceled_ThrowsOperationCanceledException()
    {
        var config = CreateConfig(rssUrls: [RssUrl]);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Loose);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>());

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Loose);
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

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.InitializeUrlsAsync(cts.Token));
    }

    [Fact]
    public async Task CheckForNewPostsAsync_WhenCanceled_ThrowsOperationCanceledException()
    {
        var config = CreateConfig(rssUrls: [RssUrl]);
        var referenceDate = new DateTime(2024, 05, 01, 0, 0, 0, DateTimeKind.Utc);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>
            {
                [RssUrl] = new() { LastRunDate = referenceDate }
            });

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(RssUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
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

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.CheckForNewPostsAsync(cts.Token));
    }

    [Fact]
    public async Task InitializeUrlsAsync_WhenRssFetchFailsAfterTestUrl_UsesUtcFallbackPublishDate()
    {
        var fallbackUrl = "https://example.com/rss-fallback";
        var config = CreateConfig(rssUrls: [fallbackUrl]);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>());

        var responseSequence = new Queue<HttpResponseMessage?>([
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.NotFound)
        ]);

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(fallbackUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => responseSequence.Dequeue());

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
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

        var state = manager.GetAllFeedData()[fallbackUrl];
        Assert.False(state.IsYoutube);
        Assert.True(state.LastPublishDate > DateTime.UtcNow.AddMinutes(-1));
        mockRssParser.Verify(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitializeUrlsAsync_WhenYoutubeFetchReturnsNullResponse_UsesUtcFallbackPublishDate()
    {
        var youtubeChannelUrl = "https://www.youtube.com/@feedcord";
        var config = CreateConfig(youtubeUrls: [youtubeChannelUrl]);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>());

        var responseSequence = new Queue<HttpResponseMessage?>([
            new HttpResponseMessage(HttpStatusCode.OK),
            null
        ]);

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(youtubeChannelUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => responseSequence.Dequeue());

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
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

        var state = manager.GetAllFeedData()[youtubeChannelUrl];
        Assert.True(state.IsYoutube);
        Assert.True(state.LastPublishDate > DateTime.UtcNow.AddMinutes(-1));
        mockRssParser.Verify(x => x.ParseYoutubeFeedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitializeUrlsAsync_WhenUrlTestThrowsUnexpectedException_LogsInstantiationWarning()
    {
        var config = CreateConfig(rssUrls: [RssUrl]);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>());

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(RssUrl, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unexpected"));

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
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

        Assert.Empty(manager.GetAllFeedData());
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to instantiate URL")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InitializeUrlsAsync_WhenRssParserThrows_ReturnsEmptyAndUsesUtcFallback()
    {
        var parserUrl = "https://example.com/rss-parser-throws";
        var config = CreateConfig(rssUrls: [parserUrl]);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>());

        var responseSequence = new Queue<HttpResponseMessage?>([
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<rss></rss>") }
        ]);

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(parserUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => responseSequence.Dequeue());

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
        mockRssParser
            .Setup(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("parse failure"));

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

        var state = manager.GetAllFeedData()[parserUrl];
        Assert.True(state.LastPublishDate > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task InitializeUrlsAsync_WhenYoutubeFetchReturnsHttpError_UsesUtcFallbackPublishDate()
    {
        var youtubeChannelUrl = "https://www.youtube.com/@error-feed";
        var config = CreateConfig(youtubeUrls: [youtubeChannelUrl]);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>());

        var responseSequence = new Queue<HttpResponseMessage?>([
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
        ]);

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(youtubeChannelUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => responseSequence.Dequeue());

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
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

        var state = manager.GetAllFeedData()[youtubeChannelUrl];
        Assert.True(state.IsYoutube);
        Assert.True(state.LastPublishDate > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task InitializeUrlsAsync_WhenYoutubeParserThrows_UsesUtcFallbackPublishDate()
    {
        var youtubeChannelUrl = "https://www.youtube.com/@parser-throws";
        var config = CreateConfig(youtubeUrls: [youtubeChannelUrl]);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>());

        var responseSequence = new Queue<HttpResponseMessage?>([
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body>youtube</body></html>")
            }
        ]);

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(youtubeChannelUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => responseSequence.Dequeue());

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
        mockRssParser
            .Setup(x => x.ParseYoutubeFeedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("youtube parse failure"));

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

        var state = manager.GetAllFeedData()[youtubeChannelUrl];
        Assert.True(state.IsYoutube);
        Assert.True(state.LastPublishDate > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void IsDirectYoutubeFeedUrl_CoversExpectedUrlPatterns()
    {
        var method = typeof(FeedManager).GetMethod("IsDirectYoutubeFeedUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        Assert.False((bool)method!.Invoke(null, ["not-a-url"])!);
        Assert.False((bool)method.Invoke(null, ["https://vimeo.com/12345"])!);
        Assert.True((bool)method.Invoke(null, ["https://www.youtube.com/feeds/videos.xml?channel_id=abc"])!);
        Assert.True((bool)method.Invoke(null, ["https://www.youtube.com/watch?playlist_id=abc"])!);
        Assert.True((bool)method.Invoke(null, ["https://www.youtube.com/watch?user=abc"])!);
    }

    [Fact]
    public async Task HandleFeedError_WithAutoRemoveDisabled_IncrementsErrorWithoutRemoving()
    {
        var config = CreateConfig(rssUrls: [RssUrl]);
        config.EnableAutoRemove = false;

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>
            {
                [RssUrl] = new() { LastRunDate = DateTime.UtcNow.AddHours(-1) }
            });

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(RssUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
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

        var state = manager.GetAllFeedData()[RssUrl];
        var handleFeedError = typeof(FeedManager).GetMethod("HandleFeedError", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(handleFeedError);

        handleFeedError!.Invoke(manager, [RssUrl, state, new Exception("boom")]);

        Assert.Equal(1, state.ErrorCount);
        Assert.True(manager.GetAllFeedData().ContainsKey(RssUrl));
    }

    [Fact]
    public async Task HandleFeedError_WithAutoRemoveEnabled_RemovesUrlAfterThirdError()
    {
        var config = CreateConfig(rssUrls: [RssUrl]);
        config.EnableAutoRemove = true;

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>
            {
                [RssUrl] = new() { LastRunDate = DateTime.UtcNow.AddHours(-1) }
            });

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(RssUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
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

        var state = manager.GetAllFeedData()[RssUrl];
        var handleFeedError = typeof(FeedManager).GetMethod("HandleFeedError", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(handleFeedError);

        handleFeedError!.Invoke(manager, [RssUrl, state, new Exception("e1")]);
        handleFeedError.Invoke(manager, [RssUrl, state, new Exception("e2")]);
        handleFeedError.Invoke(manager, [RssUrl, state, new Exception("e3")]);

        Assert.False(manager.GetAllFeedData().ContainsKey(RssUrl));
    }

    [Fact]
    public void HandleFeedError_WhenRemoveFails_LogsFailureWarning()
    {
        var config = CreateConfig();
        config.EnableAutoRemove = true;

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>());

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
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

        var orphanState = new FeedState { ErrorCount = 2, LastPublishDate = DateTime.UtcNow, IsYoutube = false };
        var handleFeedError = typeof(FeedManager).GetMethod("HandleFeedError", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(handleFeedError);

        handleFeedError!.Invoke(manager, [RssUrl, orphanState, new Exception("boom")]);

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to remove Url")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InitializeUrlsAsync_WithYoutubeChannelIdQuery_UsesDirectYoutubePath()
    {
        var youtubeChannelQueryUrl = "https://www.youtube.com/watch?v=1&channel_id=UC123";
        var config = CreateConfig(youtubeUrls: [youtubeChannelQueryUrl]);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>());

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(youtubeChannelQueryUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var video = CreatePost("channel-video", new DateTime(2024, 07, 01, 12, 0, 0, DateTimeKind.Utc));
        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
        mockRssParser
            .Setup(x => x.ParseYoutubeFeedAsync(youtubeChannelQueryUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(video);

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

        var state = manager.GetAllFeedData()[youtubeChannelQueryUrl];
        Assert.True(state.IsYoutube);
        Assert.Equal(video.PublishDate, state.LastPublishDate);
    }

    [Fact]
    public async Task CheckForNewPostsAsync_WhenRssParserReturnsNullPost_AggregatesNullLatestPost()
    {
        var config = CreateConfig(rssUrls: [RssUrl]);
        var referenceDate = new DateTime(2024, 08, 01, 10, 0, 0, DateTimeKind.Utc);

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>
            {
                [RssUrl] = new() { LastRunDate = referenceDate }
            });

        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(RssUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<rss></rss>")
            });

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
        mockRssParser
            .Setup(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([null]);

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
        mockAggregator.Verify(x => x.AddLatestUrlPost(RssUrl, null), Times.Once);
    }

    [Fact]
    public async Task CheckForNewPostsAsync_WhenRssFetchKeepsFailing_FeedIsNotAutoRemoved()
    {
        var url = "https://example.com/rss-errors";
        var config = CreateConfig(rssUrls: [url]);
        config.EnableAutoRemove = true;

        var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
        mockStore
            .Setup(s => s.LoadReferencePosts())
            .Returns(new Dictionary<string, ReferencePost>
            {
                [url] = new() { LastRunDate = DateTime.UtcNow.AddHours(-2) }
            });

        var initResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
        mockHttpClient
            .Setup(x => x.GetAsyncWithFallback(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(initResponse);

        var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
        mockRssParser
            .Setup(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("parse boom"));

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
        Assert.True(manager.GetAllFeedData().ContainsKey(url));

        await manager.CheckForNewPostsAsync();
        await manager.CheckForNewPostsAsync();
        await manager.CheckForNewPostsAsync();

        Assert.True(manager.GetAllFeedData().ContainsKey(url));
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
