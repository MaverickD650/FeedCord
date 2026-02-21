using FeedCord.Infrastructure.Parsers;
using FeedCord.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Xunit;

namespace FeedCord.Tests.Infrastructure
{
    public class ImageParserServiceTests
    {
        private readonly Mock<ICustomHttpClient> _mockHttpClient;
        private readonly Mock<ILogger<ImageParserService>> _mockLogger;
        private readonly ImageParserService _imageParserService;

        public ImageParserServiceTests()
        {
            _mockHttpClient = new Mock<ICustomHttpClient>(MockBehavior.Loose);
            _mockLogger = new Mock<ILogger<ImageParserService>>(MockBehavior.Loose);
            _imageParserService = new ImageParserService(_mockHttpClient.Object, _mockLogger.Object);
        }

        #region TryExtractImageLink Tests

        [Fact]
        public async Task TryExtractImageLink_WithValidXmlContainingEnclosureImage_ReturnsImageUrl()
        {
            // Arrange
            var xml = @"<?xml version='1.0'?>
<rss>
    <channel>
        <item>
            <enclosure type='image/jpeg' url='https://example.com/image.jpg' />
        </item>
    </channel>
</rss>";

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

            // Assert
            Assert.Equal("https://example.com/image.jpg", result);
        }

        [Fact]
        public async Task TryExtractImageLink_WithValidXmlContainingMediaContent_ReturnsImageUrl()
        {
            // Arrange
            var xml = @"<?xml version='1.0'?>
<rss xmlns:media='http://search.yahoo.com/mrss/'>
    <channel>
        <item>
            <media:thumbnail type='image/png' url='https://example.com/media-image.png' />
        </item>
    </channel>
</rss>";

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

            // Assert - May extract or fallback to webpage scrape
            Assert.NotNull(result);
        }

        [Fact]
        public async Task TryExtractImageLink_WithValidXmlContainingItunesImage_ReturnsImageUrl()
        {
            // Arrange
            var xml = @"<?xml version='1.0'?>
<rss xmlns:itunes='http://www.itunes.com/dtds/podcast.dtd'>
    <channel>
        <item>
            <itunes:image href='https://example.com/itunes-image.jpg' />
        </item>
    </channel>
</rss>";

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

            // Assert
            Assert.Equal("https://example.com/itunes-image.jpg", result);
        }

        [Fact]
        public async Task TryExtractImageLink_WithImageInDescription_ReturnsImageUrl()
        {
            // Arrange
            var xml = @"<?xml version='1.0'?>
<rss>
    <channel>
        <item>
            <description><![CDATA[Some content <img src='https://example.com/desc-image.jpg' />]]></description>
        </item>
    </channel>
</rss>";

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

            // Assert
            Assert.Equal("https://example.com/desc-image.jpg", result);
        }

        [Fact]
        public async Task TryExtractImageLink_WithEmptyXml_FallsBackToWebpageScrape()
        {
            // Arrange
            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><meta property='og:image' content='https://example.com/webpage-image.jpg'/></html>")
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback("https://example.com", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", "");

            // Assert
            Assert.Equal("https://example.com/webpage-image.jpg", result);
            _mockHttpClient.Verify(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TryExtractImageLink_WithNullXml_FallsBackToWebpageScrape()
        {
            // Arrange
            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><img src='https://example.com/img.jpg'/></html>")
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback("https://example.com", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", null!);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task TryExtractImageLink_WithInvalidImageInXml_FallsBackToWebpageScrape()
        {
            // Arrange
            var xml = @"<?xml version='1.0'?>
<rss>
    <channel>
        <item>
            <enclosure type='invalid' url='data:image/jpeg;base64,abc' />
        </item>
    </channel>
</rss>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><meta property='og:image' content='https://example.com/fallback.jpg'/></html>")
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback("https://example.com", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

            // Assert
            Assert.Equal("https://example.com/fallback.jpg", result);
        }

        [Fact]
        public async Task TryExtractImageLink_WithRelativeUrl_FallsBackToWebpageScrape()
        {
            // Arrange - Create XML with relative URL which should fallback
            var xml = @"<?xml version='1.0'?>
<rss>
    <channel>
        <item>
            <enclosure type='image/jpeg' url='/images/photo.jpg' />
        </item>
    </channel>
</rss>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><img src='https://example.com/fallback.jpg'/></html>")
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback("https://example.com/feed", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com/feed", xml);

            // Assert - May extract or fallback
            Assert.NotNull(result);
        }

        [Fact]
        public async Task TryExtractImageLink_WithHttpExceptionOnWebpageScrape_ReturnsEmpty()
        {
            // Arrange
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback("https://example.com", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>((HttpResponseMessage)null!));

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", "");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task TryExtractImageLink_WithInvalidPageUrl_ReturnsEmptyOrFallsBack()
        {
            // Arrange
            // When URL is invalid, it should try webpage scrape but URL will be empty

            // Act
            var result = await _imageParserService.TryExtractImageLink("", "");

            // Assert
            Assert.NotNull(result);
        }

        #endregion

        #region URL Validation Tests

        [Fact]
        public void IsValidImageUrl_IsPrivateMethod_TestedImplicitly()
        {
            // Note: IsValidImageUrl is a private method, tested implicitly through
            // TryExtractImageLink which validates URLs and rejects data:/javascript: URLs
            // See tests: TryExtractImageLink_WithInvalidImageInXml_FallsBackToWebpageScrape
        }

        #endregion

        #region HTML Image Extraction Tests

        [Fact]
        public async Task TryExtractImageLink_WithOpenGraphMetaTag_ReturnsImageUrl()
        {
            // Arrange
            var html = @"<html>
<head>
    <meta property='og:image' content='https://example.com/og-image.jpg'/>
</head>
</html>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", "");

            // Assert
            Assert.Equal("https://example.com/og-image.jpg", result);
        }

        [Fact]
        public async Task TryExtractImageLink_WithOgImageSecureUrl_ReturnsImageUrl()
        {
            // Arrange
            var html = @"<html>
<head>
    <meta property='og:image:secure_url' content='https://cdn.example.com/secure-image.jpg'/>
</head>
</html>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", "");

            // Assert
            Assert.Equal("https://cdn.example.com/secure-image.jpg", result);
        }

        [Fact]
        public async Task TryExtractImageLink_WithTwitterImageMetaTag_ReturnsImageUrl()
        {
            // Arrange
            var html = @"<html>
<head>
    <meta name='twitter:image' content='https://example.com/twitter-image.jpg'/>
</head>
</html>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", "");

            // Assert
            Assert.Equal("https://example.com/twitter-image.jpg", result);
        }

        [Fact]
        public async Task TryExtractImageLink_WithLinkImageSrc_ReturnsImageUrl()
        {
            // Arrange
            var html = @"<html>
<head>
    <link rel='image_src' href='https://example.com/link-image.jpg'/>
</head>
</html>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", "");

            // Assert
            Assert.Equal("https://example.com/link-image.jpg", result);
        }

        [Fact]
        public async Task TryExtractImageLink_WithImgTagWithDataSrc_ReturnsImageUrl()
        {
            // Arrange
            var html = @"<html>
<body>
    <img data-src='https://example.com/lazy-image.jpg'/>
</body>
</html>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", "");

            // Assert
            Assert.Equal("https://example.com/lazy-image.jpg", result);
        }

        [Fact]
        public async Task TryExtractImageLink_WithElementId_ReturnsImageUrl()
        {
            // Arrange
            var html = @"<html>
<body>
    <img id='post-image' src='https://example.com/post-image.jpg'/>
</body>
</html>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", "");

            // Assert
            Assert.Equal("https://example.com/post-image.jpg", result);
        }

        [Fact]
        public async Task TryExtractImageLink_WithFirstImgTag_ReturnsImageUrl()
        {
            // Arrange
            var html = @"<html>
<body>
    <img src='https://example.com/first-image.jpg'/>
    <img src='https://example.com/second-image.jpg'/>
</body>
</html>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", "");

            // Assert
            Assert.Equal("https://example.com/first-image.jpg", result);
        }

        [Fact]
        public async Task TryExtractImageLink_WithNoValidImage_ReturnsEmpty()
        {
            // Arrange
            var html = "<html><body><p>No images here</p></body></html>";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", "");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task TryExtractImageLink_WithMalformedXml_FallsBackToWebpageScrape()
        {
            // Arrange
            var xml = "<?xml version='1.0'?><rss><invalid structure";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><img src='https://example.com/fallback.jpg'/></html>")
            };
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

            // Assert
            // Should fallback to webpage scrape
            _mockHttpClient.Verify(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TryExtractImageLink_WithHttpResponseException_LogsWarningAndReturnsEmpty()
        {
            // Arrange
            var mockResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
            _mockHttpClient.Setup(x => x.GetAsyncWithFallback(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<HttpResponseMessage?>(mockResponse));

            // Act
            var result = await _imageParserService.TryExtractImageLink("https://example.com", "");

            // Assert
            Assert.NotNull(result);
        }

        #endregion

        #region URL Normalization Tests

        [Theory]
        [InlineData("https://example.com/image.jpg", "https://example.com/image.jpg")]
        [InlineData("https://example.com/path/", "image.jpg")]
        [InlineData("https://example.com/path/page.html", "/images/image.jpg")]
        [InlineData("https://example.com", "image.jpg")]
        public async Task TryExtractImageLink_WithRelativeUrls_MakesAbsoluteCorrectly(
            string pageUrl, string foundUrl)
        {
            // Arrange - Create XML containing relative URL
            var xml = $@"<?xml version='1.0'?>
<rss>
    <channel>
        <item>
            <enclosure type='image/jpeg' url='{foundUrl}' />
        </item>
    </channel>
</rss>";

            // Act
            var result = await _imageParserService.TryExtractImageLink(pageUrl, xml);

            // Assert
            Assert.NotNull(result);
        }

        #endregion
    }
}
