using FeedCord.Common;
using FeedCord.Infrastructure.Parsers;
using FeedCord.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Xunit;

namespace FeedCord.Tests.Infrastructure
{
    public class YoutubeParsingServiceTests
    {
        private readonly Mock<ICustomHttpClient> _mockHttpClient;
        private readonly Mock<ILogger<YoutubeParsingService>> _mockLogger;
        private readonly YoutubeParsingService _youtubeParsingService;

        public YoutubeParsingServiceTests()
        {
            _mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Loose);
            _mockLogger = new Mock<ILogger<YoutubeParsingService>>(MockBehavior.Loose);
            _youtubeParsingService = new YoutubeParsingService(_mockHttpClient.Object, _mockLogger.Object);
        }

        #region GetXmlUrlAndFeed Tests

        [Fact]
        public async Task GetXmlUrlAndFeed_WithDirectXmlUrl_ExtractsRecentPost()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var validFeed = CreateValidAtomFeed();

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(validFeed)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Video Title", result.Title);
            Assert.Equal("Test Channel", result.Tag);
            Assert.NotEmpty(result.Link);
        }

        [Fact]
        public async Task GetXmlUrlAndFeed_WithHtmlContainingRssLink_ExtractsAndFetchesFeed()
        {
            // Arrange
            var channelUrl = "https://www.youtube.com/c/testchannel";
            var html = @"<html>
<head>
    <link rel='alternate' type='application/rss+xml' href='https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx'/>
</head>
</html>";

            var validFeed = CreateValidAtomFeed();
            var htmlResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            };

            var feedResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(validFeed)
            };

            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(channelUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(htmlResponse));

            _mockHttpClient.Setup(x => x.GetAsyncWithFallback("https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(feedResponse));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(html);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Video Title", result.Title);
        }

        [Fact]
        public async Task GetXmlUrlAndFeed_WithHtmlMissingRssLink_ReturnsNull()
        {
            // Arrange
            var html = @"<html>
<head>
    <title>No RSS Link Here</title>
</head>
</html>";

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(html);

            // Assert
            Assert.Null(result);
            _mockLogger.Verify(
                x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetXmlUrlAndFeed_WithHtmlRssLinkButEmptyHref_ReturnsNullWithoutHttpCall()
        {
            // Arrange
            var html = @"<html>
<head>
    <link rel='alternate' type='application/rss+xml' href=''/>
</head>
</html>";

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(html);

            // Assert
            Assert.Null(result);
            _mockHttpClient.Verify(
                x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GetXmlUrlAndFeed_WithEmptyInput_ReturnsNull()
        {
            // Arrange
            var input = "";

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(input);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetXmlUrlAndFeed_WithHttpExceptionDuringFeedFetch_ReturnsNull()
        {
            // Arrange
            var xml = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xml, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>((HttpResponseMessage)null!));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xml);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region GetRecentPost Tests

        [Fact]
        public async Task GetRecentPost_WithValidFeed_ExtractsAllFields()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var validFeed = CreateValidAtomFeed();

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(validFeed)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Video Title", result.Title);
            Assert.Equal("Test Channel", result.Tag);
            Assert.NotEmpty(result.Link);
            Assert.NotEmpty(result.ImageUrl);
            Assert.NotEmpty(result.Author);
            Assert.True(result.PublishDate > DateTime.MinValue);
        }

        [Fact]
        public async Task GetRecentPost_WithAsynchronouslyCompletedHttpCall_ExtractsFields()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var validFeed = CreateValidAtomFeed();
            var completion = new TaskCompletionSource<HttpResponseMessage?>(TaskCreationOptions.RunContinuationsAsynchronously);

            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(completion.Task);

            // Act
            var resultTask = _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);
            await Task.Yield();

            completion.SetResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(validFeed)
            });

            var result = await resultTask;

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Video Title", result.Title);
            Assert.Equal("Test Channel", result.Tag);
        }

        [Fact]
        public async Task GetRecentPost_WithMissingOptionalFields_StillReturnsPost()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var feedWithMissingFields = @"<?xml version='1.0'?>
<feed xmlns='http://www.w3.org/2005/Atom' xmlns:media='http://search.yahoo.com/mrss/'>
    <title>Test Channel</title>
    <entry>
        <title>Video Without Author</title>
        <link href='https://www.youtube.com/watch?v=dQw4w9WgXcQ'/>
    </entry>
</feed>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feedWithMissingFields)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Video Without Author", result.Title);
            // Tag might be empty if not parsed
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetRecentPost_WithMissingTitleLinkThumbnailAndAuthorName_UsesEmptyFallbacks()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var sparseFeed = @"<?xml version='1.0'?>
<feed xmlns='http://www.w3.org/2005/Atom' xmlns:media='http://search.yahoo.com/mrss/'>
    <entry>
        <published>2024-01-15T10:30:00Z</published>
        <author></author>
    </entry>
</feed>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sparseFeed)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(string.Empty, result.Tag);
            Assert.Equal(string.Empty, result.Title);
            Assert.Equal(string.Empty, result.Link);
            Assert.Equal(string.Empty, result.ImageUrl);
            Assert.Equal(string.Empty, result.Author);
        }

        [Fact]
        public async Task GetRecentPost_WithLinkElementMissingHref_UsesEmptyLink()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var feed = @"<?xml version='1.0'?>
<feed xmlns='http://www.w3.org/2005/Atom' xmlns:media='http://search.yahoo.com/mrss/'>
    <title>Test Channel</title>
    <entry>
        <title>Video Title</title>
        <link />
        <published>2024-01-15T10:30:00Z</published>
    </entry>
</feed>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feed)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(string.Empty, result.Link);
        }

        [Fact]
        public async Task GetRecentPost_WithThumbnailMissingUrlAttribute_UsesEmptyThumbnail()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var feed = @"<?xml version='1.0'?>
<feed xmlns='http://www.w3.org/2005/Atom' xmlns:media='http://search.yahoo.com/mrss/'>
    <title>Test Channel</title>
    <entry>
        <title>Video Title</title>
        <link href='https://www.youtube.com/watch?v=test'/>
        <media:group>
            <media:thumbnail />
        </media:group>
        <published>2024-01-15T10:30:00Z</published>
    </entry>
</feed>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feed)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(string.Empty, result.ImageUrl);
        }

        [Fact]
        public async Task GetRecentPost_WithMediaGroupButNoThumbnailElement_UsesEmptyThumbnail()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var feed = @"<?xml version='1.0'?>
<feed xmlns='http://www.w3.org/2005/Atom' xmlns:media='http://search.yahoo.com/mrss/'>
    <title>Test Channel</title>
    <entry>
        <title>Video Title</title>
        <link href='https://www.youtube.com/watch?v=test'/>
        <media:group>
        </media:group>
        <published>2024-01-15T10:30:00Z</published>
    </entry>
</feed>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feed)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(string.Empty, result.ImageUrl);
        }
        [Fact]
        public async Task GetRecentPost_WithNoEntryInFeed_ReturnsNull()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var feedWithoutEntry = @"<?xml version='1.0'?>
<feed xmlns='http://www.w3.org/2005/Atom'>
    <title>Test Channel</title>
</feed>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feedWithoutEntry)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetRecentPost_WithMalformedXml_ReturnsNull()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var malformedXml = "<?xml version='1.0'?><feed><unclosed>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(malformedXml)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.Null(result);
            _mockLogger.Verify(
                x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetRecentPost_WithNullResponse_ReturnsNull()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>((HttpResponseMessage)null!));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetRecentPost_WithFailedStatusCode_ReturnsNull()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var mockResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetRecentPost_WithEmptyXmlUrl_ReturnsNull()
        {
            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed("");

            // Assert
            Assert.Null(result);
            _mockHttpClient.Verify(
                x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        #endregion

        #region Error Handling & Logging Tests

        [Fact]
        public async Task GetXmlUrlAndFeed_WhenHttpFails_LogsErrorAndReturnsNull()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var exception = new HttpRequestException("Network error");

            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromException<HttpResponseMessage?>(exception));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.Null(result);
            _mockLogger.Verify(
                x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetXmlUrlAndFeed_WithNullRoot_ReturnsNull()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var emptyXml = "";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(emptyXml)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetXmlUrlAndFeed_WithXmlDeclarationOnly_ReturnsNull()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var declarationOnlyXml = "<?xml version='1.0' encoding='utf-8'?>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(declarationOnlyXml)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Date Parsing Tests

        [Fact]
        public async Task GetRecentPost_ParsesPublishedDateCorrectly()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var expectedDate = new DateTime(2024, 1, 15, 10, 30, 0);
            var feed = $@"<?xml version='1.0'?>
<feed xmlns='http://www.w3.org/2005/Atom' xmlns:media='http://search.yahoo.com/mrss/'>
    <title>Test Channel</title>
    <entry>
        <title>Test Video</title>
        <link href='https://www.youtube.com/watch?v=test'/>
        <published>2024-01-15T10:30:00Z</published>
        <author><name>Test Author</name></author>
        <media:group>
            <media:thumbnail url='https://example.com/thumb.jpg'/>
        </media:group>
    </entry>
</feed>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feed)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.NotNull(result);
            Assert.InRange(result.PublishDate, expectedDate.AddSeconds(-1), expectedDate.AddSeconds(1));
        }

        [Fact]
        public async Task GetRecentPost_WithInvalidPublishedDate_UsesMinValueWithoutThrowing()
        {
            // Arrange
            var xmlUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxx";
            var feed = @"<?xml version='1.0'?>
<feed xmlns='http://www.w3.org/2005/Atom' xmlns:media='http://search.yahoo.com/mrss/'>
    <title>Test Channel</title>
    <entry>
        <title>Test Video</title>
        <link href='https://www.youtube.com/watch?v=test'/>
        <published>not-a-valid-date</published>
        <author><name>Test Author</name></author>
        <media:group>
            <media:thumbnail url='https://example.com/thumb.jpg'/>
        </media:group>
    </entry>
</feed>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feed)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(xmlUrl, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _youtubeParsingService.GetXmlUrlAndFeed(xmlUrl);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(DateTime.MinValue, result.PublishDate);
        }

        #endregion

        // Helper method to create a valid Atom feed
        private string CreateValidAtomFeed()
        {
            return @"<?xml version='1.0'?>
<feed xmlns='http://www.w3.org/2005/Atom' xmlns:media='http://search.yahoo.com/mrss/'>
    <title>Test Channel</title>
    <entry>
        <title>Test Video Title</title>
        <link href='https://www.youtube.com/watch?v=dQw4w9WgXcQ'/>
        <published>2024-01-15T10:30:00Z</published>
        <author><name>Test Author</name></author>
        <media:group>
            <media:thumbnail url='https://i.ytimg.com/vi/dQw4w9WgXcQ/default.jpg'/>
        </media:group>
    </entry>
</feed>";
        }
    }
}
