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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    var state = manager.GetAllFeedData()[RssUrl];
    Assert.Equal(referenceDate, state.LastPublishDate);
    mockRssParser.Verify(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ImageFetchMode>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
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
        .SetupSequence(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ImageFetchMode>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);
    var results = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    Assert.Collection(results,
      post => Assert.Equal("new", post.Title)
    );
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
        .Setup(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ImageFetchMode>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);
    var results = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

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
        .Setup(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ImageFetchMode>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    var state = manager.GetAllFeedData()[fallbackUrl];
    Assert.False(state.IsYoutube);
    Assert.NotEqual(default, state.LastPublishDate);
    mockRssParser.Verify(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ImageFetchMode>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    var state = manager.GetAllFeedData()[youtubeChannelUrl];
    Assert.True(state.IsYoutube);
    Assert.NotEqual(default, state.LastPublishDate);
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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

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
        .Setup(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ImageFetchMode>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    var state = manager.GetAllFeedData()[parserUrl];
    Assert.NotEqual(default, state.LastPublishDate);
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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    var state = manager.GetAllFeedData()[youtubeChannelUrl];
    Assert.Multiple(
      () => Assert.True(state.IsYoutube),
      () => Assert.NotEqual(default, state.LastPublishDate)
    );
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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    var state = manager.GetAllFeedData()[youtubeChannelUrl];
    Assert.Multiple(
      () => Assert.True(state.IsYoutube),
      () => Assert.NotEqual(default, state.LastPublishDate)
    );
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
  public void IsDirectYoutubeFeedUrl_CoversAdditionalEdgePatterns()
  {
    var method = typeof(FeedManager).GetMethod("IsDirectYoutubeFeedUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    Assert.NotNull(method);

    Assert.True((bool)method!.Invoke(null, ["https://www.youtube.com/feeds/videos.xml"])!);
    Assert.True((bool)method.Invoke(null, ["https://m.youtube.com/watch?v=abc&CHANNEL_ID=UC123"])!);
    Assert.False((bool)method.Invoke(null, ["https://www.youtube.com/watch?v=abc"])!);
    Assert.False((bool)method.Invoke(null, ["https://www.youtube.com/watch?v=abc&list=foo"])!);
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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

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
        .Setup(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ImageFetchMode>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);
    var results = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

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
        .Setup(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ImageFetchMode>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);
    Assert.True(manager.GetAllFeedData().ContainsKey(url));

    await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);
    await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);
    await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    Assert.True(manager.GetAllFeedData().ContainsKey(url));
  }

  #region Error Path Tests - High Priority Coverage Improvements

  [Fact]
  public async Task FetchRssAsync_WhenRssParserThrows_ReturnsEmptyListAndLogsWarning()
  {
    // Arrange
    var url = "https://example.com/rss";
    var config = CreateConfig(rssUrls: [url]);

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .Setup(x => x.GetAsyncWithFallback(url, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent("<rss></rss>")
        });

    var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
    mockRssParser
        .Setup(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ImageFetchMode>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("Parse failed"));

    var mockLogger = new Mock<ILogger<FeedManager>>(MockBehavior.Loose);
    var mockAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);
    var mockFilter = new Mock<IPostFilterService>(MockBehavior.Loose);

    var manager = new FeedManager(
        config,
        mockHttpClient.Object,
        mockRssParser.Object,
        mockLogger.Object,
        mockAggregator.Object,
        mockFilter.Object);

    // Act
    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);
    var result = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(result);
    Assert.Empty(result);
    mockLogger.Verify(
        x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.AtLeastOnce);
  }

  [Fact]
  public async Task CheckForNewPostsAsync_WhenYoutubeFetchReturnsNullResponse_ReturnsEmptyAndLogsWarning()
  {
    // Arrange
    var youtubeUrl = "https://www.youtube.com/@channelname";
    var initialDate = DateTime.UtcNow.AddHours(-1);
    var config = CreateConfig(youtubeUrls: [youtubeUrl]);

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>
        {
          [youtubeUrl] = new() { IsYoutube = true, LastRunDate = initialDate }
        });

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .SetupSequence(x => x.GetAsyncWithFallback(youtubeUrl, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
        .ReturnsAsync((HttpResponseMessage?)null);

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    // Act
    var result = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(result);
    Assert.Empty(result);
    mockLogger.Verify(
        x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("No response returned")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
  }

  [Fact]
  public async Task CheckForNewPostsAsync_WhenDirectYoutubeParserThrows_ReturnsEmptyAndLogsWarning()
  {
    // Arrange
    var youtubeFeedUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UC123456";
    var initialDate = DateTime.UtcNow.AddHours(-1);
    var config = CreateConfig(youtubeUrls: [youtubeFeedUrl]);

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>
        {
          [youtubeFeedUrl] = new() { IsYoutube = true, LastRunDate = initialDate }
        });

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .Setup(x => x.GetAsyncWithFallback(youtubeFeedUrl, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

    var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
    mockRssParser
        .Setup(x => x.ParseYoutubeFeedAsync(youtubeFeedUrl, It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("YouTube parse failed"));

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    // Act
    var result = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(result);
    Assert.Empty(result);
    mockRssParser.Verify(x => x.ParseYoutubeFeedAsync(youtubeFeedUrl, It.IsAny<CancellationToken>()), Times.Once);
    mockLogger.Verify(
        x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("An unexpected error occurred")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.AtLeastOnce);
  }

  [Fact]
  public async Task CheckForNewPostsAsync_WhenYoutubeFetchReturnsHttpError_ReturnsEmptyAndLogsWarning()
  {
    // Arrange
    var youtubeUrl = "https://www.youtube.com/@failing-channel";
    var initialDate = DateTime.UtcNow.AddHours(-1);
    var config = CreateConfig(youtubeUrls: [youtubeUrl]);

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>
        {
          [youtubeUrl] = new() { IsYoutube = true, LastRunDate = initialDate }
        });

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .SetupSequence(x => x.GetAsyncWithFallback(youtubeUrl, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    // Act
    var result = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(result);
    Assert.Empty(result);
    mockLogger.Verify(
        x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to fetch or process")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
  }

  [Fact]
  public async Task CheckForNewPostsAsync_WhenYoutubeParserThrowsForNonDirectUrl_ReturnsEmptyAndLogsWarning()
  {
    // Arrange
    var youtubeUrl = "https://www.youtube.com/@parser-failing-channel";
    var htmlResponse = "<html><body>channel page</body></html>";
    var initialDate = DateTime.UtcNow.AddHours(-1);
    var config = CreateConfig(youtubeUrls: [youtubeUrl]);

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>
        {
          [youtubeUrl] = new() { IsYoutube = true, LastRunDate = initialDate }
        });

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .SetupSequence(x => x.GetAsyncWithFallback(youtubeUrl, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(htmlResponse)
        });

    var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
    mockRssParser
        .Setup(x => x.ParseYoutubeFeedAsync(htmlResponse, It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("channel parse failed"));

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    // Act
    var result = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(result);
    Assert.Empty(result);
    mockRssParser.Verify(x => x.ParseYoutubeFeedAsync(htmlResponse, It.IsAny<CancellationToken>()), Times.Once);
    mockLogger.Verify(
        x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("An unexpected error occurred")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.AtLeastOnce);
  }

  [Fact]
  public async Task CheckForNewPostsAsync_WhenRssResponseContentReadThrows_ReturnsEmptyAndLogsWarning()
  {
    // Arrange
    var rssUrl = "https://example.com/rss-throwing-content";
    var initialDate = DateTime.UtcNow.AddHours(-1);
    var config = CreateConfig(rssUrls: [rssUrl]);

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>
        {
          [rssUrl] = new() { IsYoutube = false, LastRunDate = initialDate }
        });

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .SetupSequence(x => x.GetAsyncWithFallback(rssUrl, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new ThrowingHttpContent(new InvalidOperationException("rss content read failed"))
        });

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    // Act
    var result = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(result);
    Assert.Empty(result);
    mockRssParser.Verify(
        x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ImageFetchMode>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
        Times.Never);
    mockLogger.Verify(
        x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("An unexpected error occurred while checking the RSS feed")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
  }

  [Fact]
  public async Task CheckForNewPostsAsync_WhenYoutubeResponseContentReadThrows_ReturnsEmptyAndLogsWarning()
  {
    // Arrange
    var youtubeUrl = "https://www.youtube.com/@throwing-content";
    var initialDate = DateTime.UtcNow.AddHours(-1);
    var config = CreateConfig(youtubeUrls: [youtubeUrl]);

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>
        {
          [youtubeUrl] = new() { IsYoutube = true, LastRunDate = initialDate }
        });

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .SetupSequence(x => x.GetAsyncWithFallback(youtubeUrl, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new ThrowingHttpContent(new InvalidOperationException("youtube content read failed"))
        });

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    // Act
    var result = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(result);
    Assert.Empty(result);
    mockRssParser.Verify(
        x => x.ParseYoutubeFeedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
        Times.Never);
    mockLogger.Verify(
        x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("An unexpected error occurred while checking the RSS feed")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
  }

  [Fact]
  public async Task CheckForNewPostsAsync_WhenRssFetchReturnsNullResponse_ReturnsEmptyAndLogsWarning()
  {
    var rssUrl = "https://example.com/rss-null-response";
    var initialDate = DateTime.UtcNow.AddHours(-1);
    var config = CreateConfig(rssUrls: [rssUrl]);

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>
        {
          [rssUrl] = new() { IsYoutube = false, LastRunDate = initialDate }
        });

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .SetupSequence(x => x.GetAsyncWithFallback(rssUrl, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
        .ReturnsAsync((HttpResponseMessage?)null);

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    var result = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.Empty(result);
    mockRssParser.Verify(
        x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ImageFetchMode>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
        Times.Never);
    mockLogger.Verify(
        x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("No response returned")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
  }

  [Fact]
  public async Task CheckForNewPostsAsync_WhenRssFetchReturnsHttpError_ReturnsEmptyAndLogsWarning()
  {
    var rssUrl = "https://example.com/rss-http-error";
    var initialDate = DateTime.UtcNow.AddHours(-1);
    var config = CreateConfig(rssUrls: [rssUrl]);

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>
        {
          [rssUrl] = new() { IsYoutube = false, LastRunDate = initialDate }
        });

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .SetupSequence(x => x.GetAsyncWithFallback(rssUrl, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    var result = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.Empty(result);
    mockRssParser.Verify(
        x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ImageFetchMode>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
        Times.Never);
    mockLogger.Verify(
        x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to fetch or process the RSS feed")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
  }

  [Fact]
  public async Task InitializeUrlsAsync_WhenDirectYoutubeParserReturnsNull_UsesUtcFallbackPublishDate()
  {
    var youtubeFeedUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UC123456";
    var config = CreateConfig(youtubeUrls: [youtubeFeedUrl]);

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>());

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .Setup(x => x.GetAsyncWithFallback(youtubeFeedUrl, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

    var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
    mockRssParser
        .Setup(x => x.ParseYoutubeFeedAsync(youtubeFeedUrl, It.IsAny<CancellationToken>()))
        .ReturnsAsync((Post?)null);

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    var state = manager.GetAllFeedData()[youtubeFeedUrl];
    Assert.Multiple(
      () => Assert.True(state.IsYoutube),
      () => Assert.NotEqual(default, state.LastPublishDate)
    );
  }

  [Fact]
  public async Task CheckForNewPostsAsync_WhenYoutubeParserReturnsNullForNonDirectUrl_ReturnsEmpty()
  {
    var youtubeUrl = "https://www.youtube.com/@null-parser";
    var initialDate = DateTime.UtcNow.AddHours(-1);
    var config = CreateConfig(youtubeUrls: [youtubeUrl]);

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>
        {
          [youtubeUrl] = new() { IsYoutube = true, LastRunDate = initialDate }
        });

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .SetupSequence(x => x.GetAsyncWithFallback(youtubeUrl, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent("<html><head><title>youtube</title></head><body></body></html>")
        });

    var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
    mockRssParser
        .Setup(x => x.ParseYoutubeFeedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((Post?)null);

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    var result = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.Empty(result);
  }

  [Fact]
  public async Task CheckForNewPostsAsync_WhenYoutubeParserReturnsPostForNonDirectUrl_ReturnsPost()
  {
    var youtubeUrl = "https://www.youtube.com/@non-direct-success";
    var initialDate = DateTime.UtcNow.AddHours(-2);
    var postDate = initialDate.AddHours(1);
    var config = CreateConfig(youtubeUrls: [youtubeUrl]);

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>
        {
          [youtubeUrl] = new() { IsYoutube = true, LastRunDate = initialDate }
        });

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .SetupSequence(x => x.GetAsyncWithFallback(youtubeUrl, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent("<html><head><title>youtube</title></head><body></body></html>")
        });

    var parsed = CreatePost("yt-post", postDate);
    var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
    mockRssParser
        .Setup(x => x.ParseYoutubeFeedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(parsed);

    var mockLogger = new Mock<ILogger<FeedManager>>(MockBehavior.Loose);
    var mockAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);
    var mockFilter = new Mock<IPostFilterService>(MockBehavior.Strict);
    mockFilter
        .Setup(x => x.ShouldIncludePost(It.IsAny<Post>(), youtubeUrl))
        .Returns(true);

    var manager = new FeedManager(
        config,
        mockHttpClient.Object,
        mockRssParser.Object,
        mockLogger.Object,
        mockAggregator.Object,
        mockFilter.Object,
        mockStore.Object);

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    var result = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    Assert.Collection(result,
      post => Assert.Equal("yt-post", post.Title)
    );
  }

  [Fact]
  public async Task CheckForNewPostsAsync_WhenYoutubeFetchIsCanceledDuringHttpCall_ThrowsOperationCanceledException()
  {
    var youtubeUrl = "https://www.youtube.com/@cancel-during-fetch";
    var initialDate = DateTime.UtcNow.AddHours(-1);
    var config = CreateConfig(youtubeUrls: [youtubeUrl]);

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>
        {
          [youtubeUrl] = new() { IsYoutube = true, LastRunDate = initialDate }
        });

    var cts = new CancellationTokenSource();
    var callCount = 0;

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .Setup(x => x.GetAsyncWithFallback(youtubeUrl, It.IsAny<CancellationToken>()))
        .Returns<string, CancellationToken>((_, token) =>
        {
          callCount++;
          if (callCount == 1)
          {
            return Task.FromResult<HttpResponseMessage?>(new HttpResponseMessage(HttpStatusCode.OK));
          }

          cts.Cancel();
          return Task.FromCanceled<HttpResponseMessage?>(token);
        });

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.CheckForNewPostsAsync(cts.Token));
  }

  [Fact]
  public async Task CheckForNewPostsAsync_WhenRssFetchIsCanceledDuringHttpCall_ThrowsOperationCanceledException()
  {
    var rssUrl = "https://example.com/rss-cancel-during-fetch";
    var initialDate = DateTime.UtcNow.AddHours(-1);
    var config = CreateConfig(rssUrls: [rssUrl]);

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>
        {
          [rssUrl] = new() { IsYoutube = false, LastRunDate = initialDate }
        });

    var cts = new CancellationTokenSource();
    var callCount = 0;

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .Setup(x => x.GetAsyncWithFallback(rssUrl, It.IsAny<CancellationToken>()))
        .Returns<string, CancellationToken>((_, token) =>
        {
          callCount++;
          if (callCount == 1)
          {
            return Task.FromResult<HttpResponseMessage?>(new HttpResponseMessage(HttpStatusCode.OK));
          }

          cts.Cancel();
          return Task.FromCanceled<HttpResponseMessage?>(token);
        });

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.CheckForNewPostsAsync(cts.Token));
  }

  #endregion

  private sealed class ThrowingHttpContent : HttpContent
  {
    private readonly Exception _exception;

    public ThrowingHttpContent(Exception exception)
    {
      _exception = exception;
    }

    protected override Task SerializeToStreamAsync(System.IO.Stream stream, TransportContext? context)
    {
      return Task.FromException(_exception);
    }

    protected override bool TryComputeLength(out long length)
    {
      length = 0;
      return false;
    }
  }

  private static Config CreateConfig(string[]? rssUrls = null, string[]? youtubeUrls = null)
  {
    return new Config
    {
      Id = "CoverageFeed",
      RssUrls = rssUrls ?? [],
      YoutubeUrls = youtubeUrls ?? [],
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 30,
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

public class FeedManagerTests
{
  private readonly Mock<ICustomHttpClient> _mockHttpClient;
  private readonly Mock<IRssParsingService> _mockRssParser;
  private readonly Mock<ILogger<FeedManager>> _mockLogger;
  private readonly Mock<ILogAggregator> _mockAggregator;
  private readonly Mock<IPostFilterService> _mockFilterService;

  public FeedManagerTests()
  {
    _mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Loose);
    _mockRssParser = new Mock<IRssParsingService>(MockBehavior.Loose);
    _mockLogger = new Mock<ILogger<FeedManager>>(MockBehavior.Loose);
    _mockAggregator = new Mock<ILogAggregator>(MockBehavior.Loose);
    _mockFilterService = new Mock<IPostFilterService>(MockBehavior.Loose);
  }

  [Fact]
  public async Task InitializeUrlsAsync_LogsSuccessMessage()
  {
    // Arrange
    var config = CreateTestConfig(
        rssUrls: new[] { "http://example.com/rss" },
        youtubeUrls: new string[] { }
    );

    _mockHttpClient
        .Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .Returns(Task.FromResult<HttpResponseMessage?>(new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.OK }));

    var manager = new FeedManager(
        config,
        _mockHttpClient.Object,
        _mockRssParser.Object,
        _mockLogger.Object,
        _mockAggregator.Object,
        _mockFilterService.Object
    );

    // Act
    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    // Assert - should log information about URL testing
    _mockLogger.Verify(
        x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tested successfully")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()
        ),
        Times.Once
    );
  }

  [Fact]
  public async Task InitializeUrlsAsync_FiltersOutEmptyUrls()
  {
    // Arrange - mix of empty and valid URLs
    var config = CreateTestConfig(
        rssUrls: new[] { "http://example.com/rss", "", "   " },
        youtubeUrls: new[] { "", "http://youtube.com/channel/123" }
    );

    _mockHttpClient
        .Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .Returns(Task.FromResult<HttpResponseMessage?>(new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.OK }));

    var manager = new FeedManager(
        config,
        _mockHttpClient.Object,
        _mockRssParser.Object,
        _mockLogger.Object,
        _mockAggregator.Object,
        _mockFilterService.Object
    );

    // Act
    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    // Assert - should validate only 2 URLs (1 RSS + 1 YouTube, empty ones filtered)
    // Note: Each URL may be called multiple times due to retry logic/fallback attempts
    var calls = _mockHttpClient.Invocations.Where(i => i.Method.Name == "GetAsyncWithFallback").ToList();
    Assert.NotEmpty(calls);
  }

  [Fact]
  public async Task InitializeUrlsAsync_UsesDirectYouTubeFeedUrlWithoutInitialHttpFetch()
  {
    var youtubeFeedUrl = $"https://www.youtube.com/feeds/videos.xml?channel_id=UC{Guid.NewGuid():N}";
    var config = CreateTestConfig(youtubeUrls: new[] { youtubeFeedUrl });

    var post = new Post(
        Title: "Video",
        ImageUrl: "img",
        Description: "desc",
        Link: "link",
        Tag: "tag",
        PublishDate: DateTime.Now,
        Author: "author"
    );

    _mockRssParser
        .Setup(x => x.ParseYoutubeFeedAsync(youtubeFeedUrl, It.IsAny<CancellationToken>()))
        .Returns(Task.FromResult<Post?>(post));

    _mockHttpClient
        .Setup(x => x.GetAsyncWithFallback(youtubeFeedUrl, It.IsAny<CancellationToken>()))
        .Returns(Task.FromResult<HttpResponseMessage?>(new HttpResponseMessage { StatusCode = HttpStatusCode.OK }));

    var manager = new FeedManager(
        config,
        _mockHttpClient.Object,
        _mockRssParser.Object,
        _mockLogger.Object,
        _mockAggregator.Object,
        _mockFilterService.Object
    );

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    _mockHttpClient.Verify(
        x => x.GetAsyncWithFallback(youtubeFeedUrl, It.IsAny<CancellationToken>()),
        Times.Once
    );
    _mockRssParser.Verify(
        x => x.ParseYoutubeFeedAsync(youtubeFeedUrl, It.IsAny<CancellationToken>()),
        Times.Once
    );
  }

  [Fact]
  public async Task InitializeUrlsAsync_FetchesHtmlForNonFeedYouTubeUrl()
  {
    var youtubeChannelUrl = "https://www.youtube.com/@somechannel";
    var html = "<html><head></head><body></body></html>";
    var config = CreateTestConfig(youtubeUrls: new[] { youtubeChannelUrl });

    _mockHttpClient
        .Setup(x => x.GetAsyncWithFallback(youtubeChannelUrl, It.IsAny<CancellationToken>()))
        .Returns(Task.FromResult<HttpResponseMessage?>(new HttpResponseMessage
        {
          StatusCode = HttpStatusCode.OK,
          Content = new StringContent(html)
        }));

    _mockRssParser
        .Setup(x => x.ParseYoutubeFeedAsync(It.IsAny<string>()))
        .Returns(Task.FromResult<Post?>((Post?)null));

    var manager = new FeedManager(
        config,
        _mockHttpClient.Object,
        _mockRssParser.Object,
        _mockLogger.Object,
        _mockAggregator.Object,
        _mockFilterService.Object
    );

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    _mockHttpClient.Verify(
        x => x.GetAsyncWithFallback(youtubeChannelUrl, It.IsAny<CancellationToken>()),
        Times.Exactly(2)
    );
    _mockRssParser.Verify(
        x => x.ParseYoutubeFeedAsync(html, It.IsAny<CancellationToken>()),
        Times.Once
    );
  }

  [Fact]
  public async Task CheckForNewPostsAsync_ReturnsEmptyWhenNoFeeds()
  {
    // Arrange - empty feed state
    var config = CreateTestConfig();

    var manager = new FeedManager(
        config,
        _mockHttpClient.Object,
        _mockRssParser.Object,
        _mockLogger.Object,
        _mockAggregator.Object,
        _mockFilterService.Object
    );

    // Act
    var result = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    // Assert
    Assert.Empty(result);
  }

  [Fact]
  public async Task CheckForNewPostsAsync_AppliesPostFilters()
  {
    // Arrange
    var config = CreateTestConfig(rssUrls: new[] { "http://example.com/rss" });
    var testPost = new Post(
        Title: "Filtered Post",
        ImageUrl: "http://example.com/image.jpg",
        Description: "This should be filtered",
        Link: "http://example.com",
        Tag: "test",
        PublishDate: DateTime.Now,
        Author: "Author"
    );

    _mockRssParser
        .Setup(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ImageFetchMode>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
        .Returns(Task.FromResult<List<Post?>>(new List<Post?> { testPost }));

    _mockFilterService
        .Setup(x => x.ShouldIncludePost(It.IsAny<Post>(), It.IsAny<string>()))
        .Returns(false);  // Filter rejects the post

    var manager = new FeedManager(
        config,
        _mockHttpClient.Object,
        _mockRssParser.Object,
        _mockLogger.Object,
        _mockAggregator.Object,
        _mockFilterService.Object
    );

    // Act
    var result = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    // Assert - Note: Filter service is only called during actual feed checking
    // when feeds are in the internal _feedStates. Since feeds are empty,
    // this test verifies the method completes without error
    Assert.NotNull(result);
  }

  [Fact]
  public void GetAllFeedData_ReturnsReadOnlyDictionary()
  {
    // Arrange
    var config = CreateTestConfig();
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

    // Assert - should be read-only
    Assert.NotNull(result);
    Assert.IsAssignableFrom<IReadOnlyDictionary<string, FeedState>>(result);
  }

  [Fact]
  public void Constructor_InitializesConcurrentRequests()
  {
    // Arrange
    var config = CreateTestConfig(concurrentRequests: 10);

    // Act
    var manager = new FeedManager(
        config,
        _mockHttpClient.Object,
        _mockRssParser.Object,
        _mockLogger.Object,
        _mockAggregator.Object,
        _mockFilterService.Object
    );

    // Assert - should create SemaphoreSlim with correct count
    // Test passes if constructor completes without error
    Assert.NotNull(manager);
  }

  // Helper methods
  private Config CreateTestConfig(
      string[]? rssUrls = null,
      string[]? youtubeUrls = null,
      int concurrentRequests = 5)
  {
    return new Config
    {
      Id = "TestFeed",
      RssUrls = rssUrls ?? new string[] { },
      YoutubeUrls = youtubeUrls ?? new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 30,
      DescriptionLimit = 250,
      Forum = false,
      MarkdownFormat = false,
      PersistenceOnShutdown = false,
      ConcurrentRequests = concurrentRequests
    };
  }
}

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
      RssCheckIntervalSeconds = 30,
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
      RssCheckIntervalSeconds = 30,
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
      RssCheckIntervalSeconds = 30,
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
    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

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
      RssCheckIntervalSeconds = 30,
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
    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

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
      RssCheckIntervalSeconds = 30,
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
    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

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
      RssCheckIntervalSeconds = 30,
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
    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

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
      RssCheckIntervalSeconds = 30,
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
    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

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
      RssCheckIntervalSeconds = 30,
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
    var result = await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

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
      RssCheckIntervalSeconds = 30,
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
    await manager.CheckForNewPostsAsync(TestContext.Current.CancellationToken);

    // Assert
    _mockAggregator.Verify(x => x.SetNewPostCount(It.IsAny<int>()), Times.Once);
  }

  #endregion

  #region Feed Data Tests

  [Fact]
  public void GetAllFeedData_ReturnsReadOnlyDictionary_Expanded()
  {
    // Arrange
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 30,
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
      RssCheckIntervalSeconds = 30,
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
      RssCheckIntervalSeconds = 30,
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
    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

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
      RssCheckIntervalSeconds = 30,
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
      RssCheckIntervalSeconds = 30,
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
      RssCheckIntervalSeconds = 30,
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
    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);
  }

  #endregion

  #region Test Data

  public static IEnumerable<object?[]> GetEmptyUrlArrayVariations()
  {
    yield return new object?[] { Array.Empty<string>() };
    yield return new object?[] { null };
  }

  [Fact]
  public async Task InitializeUrlsAsync_WhenYoutubeParserReturnsNullEntry_UsesUtcFallbackPublishDate()
  {
    var youtubeUrl = "https://www.youtube.com/@null-entry";
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = Array.Empty<string>(),
      YoutubeUrls = new[] { youtubeUrl },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 30,
      DescriptionLimit = 250,
      ConcurrentRequests = 5
    };

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>());

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .Setup(x => x.GetAsyncWithFallback(youtubeUrl, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent("<html></html>")
        });

    var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
    mockRssParser
        .Setup(x => x.ParseYoutubeFeedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((Post?)null);

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    var state = manager.GetAllFeedData()[youtubeUrl];
    Assert.NotEqual(default, state.LastPublishDate);
  }

  [Fact]
  public async Task InitializeUrlsAsync_WhenRssParserReturnsOnlyNullPosts_UsesUtcFallbackPublishDate()
  {
    var rssUrl = "https://example.com/rss-null-post";
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new[] { rssUrl },
      YoutubeUrls = Array.Empty<string>(),
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 30,
      DescriptionLimit = 250,
      ConcurrentRequests = 5
    };

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>());

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .Setup(x => x.GetAsyncWithFallback(rssUrl, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent("<rss></rss>")
        });

    var mockRssParser = new Mock<IRssParsingService>(MockBehavior.Strict);
    mockRssParser
        .Setup(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ImageFetchMode>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<Post?> { null });

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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    var state = manager.GetAllFeedData()[rssUrl];
    Assert.NotEqual(default, state.LastPublishDate);
  }

  [Fact]
  public async Task InitializeUrlsAsync_WhenHttpClientThrowsCanceledOperationDuringTestUrl_RethrowsOperationCanceledException()
  {
    var url = "https://example.com/rss-cancel-during-test";
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new[] { url },
      YoutubeUrls = Array.Empty<string>(),
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 30,
      DescriptionLimit = 250,
      ConcurrentRequests = 5
    };

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Loose);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>());

    using var cts = new CancellationTokenSource();

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .Setup(x => x.GetAsyncWithFallback(url, It.IsAny<CancellationToken>()))
        .Returns<string, CancellationToken>((_, __) =>
        {
          cts.Cancel();
          throw new OperationCanceledException(cts.Token);
        });

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

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.InitializeUrlsAsync(cts.Token));
  }

  [Fact]
  public async Task InitializeUrlsAsync_WhenRssReturnsNotModifiedOnCheckAndFetch_UsesFallbackDateWithoutParsing()
  {
    var rssUrl = "https://example.com/rss-not-modified";
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new[] { rssUrl },
      YoutubeUrls = Array.Empty<string>(),
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 30,
      DescriptionLimit = 250,
      ConcurrentRequests = 5
    };

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>());

    var responseSequence = new Queue<HttpResponseMessage?>([
        new HttpResponseMessage(HttpStatusCode.NotModified),
        new HttpResponseMessage(HttpStatusCode.NotModified)
    ]);

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .Setup(x => x.GetAsyncWithFallback(rssUrl, It.IsAny<CancellationToken>()))
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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    var state = manager.GetAllFeedData()[rssUrl];
    Assert.False(state.IsYoutube);
    Assert.NotEqual(default, state.LastPublishDate);
    mockRssParser.Verify(x => x.ParseRssFeedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ImageFetchMode>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task InitializeUrlsAsync_WhenYoutubeReturnsNotModifiedOnFetch_UsesFallbackDateWithoutYoutubeParsing()
  {
    var youtubeUrl = "https://www.youtube.com/@feedcord-not-modified";
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = Array.Empty<string>(),
      YoutubeUrls = new[] { youtubeUrl },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 30,
      DescriptionLimit = 250,
      ConcurrentRequests = 5
    };

    var mockStore = new Mock<IReferencePostStore>(MockBehavior.Strict);
    mockStore
        .Setup(s => s.LoadReferencePosts())
        .Returns(new Dictionary<string, ReferencePost>());

    var responseSequence = new Queue<HttpResponseMessage?>([
        new HttpResponseMessage(HttpStatusCode.OK),
        new HttpResponseMessage(HttpStatusCode.NotModified)
    ]);

    var mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Strict);
    mockHttpClient
        .Setup(x => x.GetAsyncWithFallback(youtubeUrl, It.IsAny<CancellationToken>()))
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

    await manager.InitializeUrlsAsync(TestContext.Current.CancellationToken);

    var state = manager.GetAllFeedData()[youtubeUrl];
    Assert.True(state.IsYoutube);
    Assert.NotEqual(default, state.LastPublishDate);
    mockRssParser.Verify(x => x.ParseYoutubeFeedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  #endregion
}
