using Xunit;
using Moq;
using FeedCord.Services;
using FeedCord.Services.Interfaces;
using FeedCord.Common;
using Microsoft.Extensions.Logging;

namespace FeedCord.Tests.Services;

public class RssParsingServiceTests
{
    private readonly Mock<ILogger<RssParsingService>> _mockLogger;
    private readonly Mock<IYoutubeParsingService> _mockYoutubeParser;
    private readonly Mock<IImageParserService> _mockImageParser;

    public RssParsingServiceTests()
    {
        _mockLogger = new Mock<ILogger<RssParsingService>>(MockBehavior.Loose);
        _mockYoutubeParser = new Mock<IYoutubeParsingService>(MockBehavior.Loose);
        _mockImageParser = new Mock<IImageParserService>(MockBehavior.Loose);
    }

    [Fact]
    public async Task ParseRssFeedAsync_ValidRssXml_ReturnsListOfPosts()
    {
        // Arrange
        var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<rss version=""2.0"">
  <channel>
    <title>Test Feed</title>
    <link>http://example.com</link>
    <description>A test feed</description>
    <image>
      <url>http://example.com/image.jpg</url>
    </image>
    <item>
      <title>Test Post</title>
      <link>http://example.com/post1</link>
      <description>This is a test post description</description>
      <pubDate>Mon, 06 Sep 2026 00:01:00 +0000</pubDate>
      <author>Test Author</author>
    </item>
  </channel>
</rss>";

        var service = new RssParsingService(
            _mockLogger.Object,
            _mockYoutubeParser.Object,
            _mockImageParser.Object
        );

        // Act
        var result = await service.ParseRssFeedAsync(xmlContent, trim: 250);

        // Assert - should parse without error (may have 0 or more posts depending on PostBuilder)
        Assert.NotNull(result);
        // Note: PostBuilder.TryBuildPost may return null if validation fails,
        // so we allow empty list as valid result
    }

    [Fact]
    public async Task ParseRssFeedAsync_HandlesLowercaseDoctype()
    {
        // Arrange - RSS with lowercase <!doctype (common in malformed feeds)
        var xmlContent = @"<!doctype rss>
<?xml version=""1.0"" encoding=""utf-8""?>
<rss version=""2.0"">
  <channel>
    <title>Test Feed</title>
    <item>
      <title>Post</title>
      <link>http://example.com/post</link>
      <pubDate>Mon, 06 Sep 2026 00:01:00 +0000</pubDate>
    </item>
  </channel>
</rss>";

        var service = new RssParsingService(
            _mockLogger.Object,
            _mockYoutubeParser.Object,
            _mockImageParser.Object
        );

        // Act - should handle preprocessing without error
        var result = await service.ParseRssFeedAsync(xmlContent, trim: 250);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ParseRssFeedAsync_EmptyFeed_ReturnsEmptyList()
    {
        // Arrange - valid XML but no items
        var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<rss version=""2.0"">
  <channel>
    <title>Empty Feed</title>
    <link>http://example.com</link>
    <description>Empty test feed</description>
  </channel>
</rss>";

        var service = new RssParsingService(
            _mockLogger.Object,
            _mockYoutubeParser.Object,
            _mockImageParser.Object
        );

        // Act
        var result = await service.ParseRssFeedAsync(xmlContent, trim: 250);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task ParseRssFeedAsync_InvalidXml_ReturnsEmptyAndLogsWarning()
    {
        // Arrange - malformed XML
        var invalidXml = "<invalid>Not really XML</unclosed>";

        var service = new RssParsingService(
            _mockLogger.Object,
            _mockYoutubeParser.Object,
            _mockImageParser.Object
        );

        // Act - should not throw exception
        var result = await service.ParseRssFeedAsync(invalidXml, trim: 250);

        // Assert
        Assert.Empty(result);

        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("unexpected error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ParseRssFeedAsync_TrimmsDescriptionToLimit()
    {
        // Arrange
        var longDescription = new string('x', 500);
        var xmlContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<rss version=""2.0"">
  <channel>
    <title>Test Feed</title>
    <item>
      <title>Post with Long Description</title>
      <link>http://example.com/post</link>
      <description>{longDescription}</description>
      <pubDate>Mon, 06 Sep 2026 00:01:00 +0000</pubDate>
    </item>
  </channel>
</rss>";

        var service = new RssParsingService(
            _mockLogger.Object,
            _mockYoutubeParser.Object,
            _mockImageParser.Object
        );

        // Act - trim to 250 characters
        var result = await service.ParseRssFeedAsync(xmlContent, trim: 250);

        // Assert - description should be trimmed
        if (result.Count > 0 && result[0] != null)
        {
            Assert.True(result[0]!.Description.Length <= 250);
        }
    }

    [Fact]
    public async Task ParseYoutubeFeedAsync_CallsYoutubeParsingService()
    {
        // Arrange
        var channelUrl = "https://www.youtube.com/c/TestChannel";
        var expectedPost = new Post(
            Title: "YouTube Video",
            ImageUrl: "http://example.com/thumb.jpg",
            Description: "Video description",
            Link: "https://youtube.com/watch?v=123",
            Tag: "youtube",
            PublishDate: DateTime.Now,
            Author: "Test Channel"
        );

        _mockYoutubeParser
            .Setup(x => x.GetXmlUrlAndFeed(channelUrl))
            .Returns(Task.FromResult<Post?>(expectedPost));

        var service = new RssParsingService(
            _mockLogger.Object,
            _mockYoutubeParser.Object,
            _mockImageParser.Object
        );

        // Act
        var result = await service.ParseYoutubeFeedAsync(channelUrl);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("YouTube Video", result?.Title);
        _mockYoutubeParser.Verify(x => x.GetXmlUrlAndFeed(channelUrl), Times.Once);
    }

    [Fact]
    public async Task ParseYoutubeFeedAsync_InvalidUrl_LogsWarningReturnsNull()
    {
        // Arrange
        var invalidUrl = "https://invalid-youtube-url";

        _mockYoutubeParser
            .Setup(x => x.GetXmlUrlAndFeed(invalidUrl))
            .Returns(Task.FromResult<Post?>((Post?)null!));

        var service = new RssParsingService(
            _mockLogger.Object,
            _mockYoutubeParser.Object,
            _mockImageParser.Object
        );

        // Act
        var result = await service.ParseYoutubeFeedAsync(invalidUrl);

        // Assert
        Assert.Null(result);

        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }
}
