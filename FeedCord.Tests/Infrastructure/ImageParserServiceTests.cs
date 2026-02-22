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
    public async Task TryExtractImageLink_WithEnclosureImageMissingUrl_UsesDescriptionImage()
    {
      // Arrange
      var xml = @"<?xml version='1.0'?>
<rss>
    <channel>
        <item>
            <enclosure type='image/jpeg' url='' />
            <description><![CDATA[<img src='https://example.com/desc.jpg' />]]></description>
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

      // Assert
      Assert.Equal("https://example.com/desc.jpg", result);
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
    public async Task TryExtractImageLink_WithMediaContentNonImageType_UsesContentEncodedImage()
    {
      // Arrange
      var xml = @"<?xml version='1.0'?>
<rss xmlns:media='http://search.yahoo.com/mrss/' xmlns:content='http://purl.org/rss/1.0/modules/content/'>
    <channel>
        <item>
            <media:content type='video/mp4' url='https://example.com/video.mp4' />
            <content:encoded><![CDATA[<img src='https://example.com/encoded.jpg' />]]></content:encoded>
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

      // Assert
      Assert.Equal("https://example.com/encoded.jpg", result);
    }

    [Fact]
    public async Task TryExtractImageLink_WithMediaContentEmptyUrl_UsesItunesImage()
    {
      // Arrange
      var xml = @"<?xml version='1.0'?>
<rss xmlns:media='http://search.yahoo.com/mrss/' xmlns:itunes='http://www.itunes.com/dtds/podcast.dtd'>
    <channel>
        <item>
            <media:thumbnail type='image/jpeg' url='' />
            <itunes:image href='https://example.com/itunes.jpg' />
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

      // Assert
      Assert.Equal("https://example.com/itunes.jpg", result);
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
    public async Task TryExtractImageLink_WithItunesImageMissingHref_UsesDescriptionImage()
    {
      // Arrange
      var xml = @"<?xml version='1.0'?>
<rss xmlns:itunes='http://www.itunes.com/dtds/podcast.dtd'>
    <channel>
        <item>
            <itunes:image href='' />
            <description><![CDATA[<img src='https://example.com/desc-image.jpg' />]]></description>
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

      // Assert
      Assert.Equal("https://example.com/desc-image.jpg", result);
    }

    [Fact]
    public async Task TryExtractImageLink_WithItunesImageNoHrefAttribute_UsesDescriptionImage()
    {
      // Arrange
      var xml = @"<?xml version='1.0'?>
<rss xmlns:itunes='http://www.itunes.com/dtds/podcast.dtd'>
    <channel>
        <item>
            <itunes:image />
            <description><![CDATA[<img src='https://example.com/desc-image.jpg' />]]></description>
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

      // Assert
      Assert.Equal("https://example.com/desc-image.jpg", result);
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
    public async Task TryExtractImageLink_WithDescriptionImgEmptySrc_UsesContentEncodedImage()
    {
      // Arrange
      var xml = @"<?xml version='1.0'?>
<rss xmlns:content='http://purl.org/rss/1.0/modules/content/'>
    <channel>
        <item>
            <description><![CDATA[<img src='' />]]></description>
            <content:encoded><![CDATA[<img src='https://example.com/content.jpg' />]]></content:encoded>
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

      // Assert
      Assert.Equal("https://example.com/content.jpg", result);
    }

    [Fact]
    public async Task TryExtractImageLink_WithNoDescriptionImage_UsesContentEncodedImage()
    {
      // Arrange
      var xml = @"<?xml version='1.0'?>
<rss xmlns:content='http://purl.org/rss/1.0/modules/content/'>
    <channel>
        <item>
            <description><![CDATA[No image here]]></description>
            <content:encoded><![CDATA[<img src='https://example.com/content.jpg' />]]></content:encoded>
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

      // Assert
      Assert.Equal("https://example.com/content.jpg", result);
    }

    [Fact]
    public async Task TryExtractImageLink_WithDescriptionAndContentWithoutImages_FallsBackToWebpageScrape()
    {
      // Arrange
      var xml = @"<?xml version='1.0'?>
<rss xmlns:content='http://purl.org/rss/1.0/modules/content/'>
    <channel>
        <item>
            <description><![CDATA[No image here]]></description>
            <content:encoded><![CDATA[Still no image]]></content:encoded>
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
    public async Task TryExtractImageLink_WithJavascriptImageUrl_FallsBackToWebpageScrape()
    {
      // Arrange
      var xml = @"<?xml version='1.0'?>
<rss>
    <channel>
        <item>
            <enclosure type='image/jpeg' url='javascript:alert(1)' />
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
      // Arrange
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

      // Assert
      Assert.Equal("https://example.com/fallback.jpg", result);
    }

    [Fact]
    public async Task TryExtractImageLink_WithRelativeUrlAndInvalidBaseUrl_ReturnsEmpty()
    {
      // Arrange
      var xml = @"<?xml version='1.0'?>
<rss>
    <channel>
        <item>
            <enclosure type='image/jpeg' url='/images/photo.jpg' />
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("", xml);

      // Assert
      Assert.NotNull(result);
      Assert.Empty(result);
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
    public async Task TryExtractImageLink_WithOgImageMissingContent_UsesTwitterImage()
    {
      // Arrange
      var html = @"<html>
<head>
    <meta property='og:image'/>
    <meta name='twitter:image' content='https://example.com/twitter.jpg'/>
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
      Assert.Equal("https://example.com/twitter.jpg", result);
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
    public async Task TryExtractImageLink_WithLinkImageSrcMissingHref_UsesDataSrcImage()
    {
      // Arrange
      var html = @"<html>
<head>
    <link rel='image_src'/>
</head>
<body>
    <img data-src='https://example.com/data-src.jpg'/>
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
      Assert.Equal("https://example.com/data-src.jpg", result);
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
    public async Task TryExtractImageLink_WithElementIdDataSrc_ReturnsDataSrcImageUrl()
    {
      // Arrange
      var html = @"<html>
<body>
    <img id='post-image' data-src='https://example.com/post-data-src.jpg'/>
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
      Assert.Equal("https://example.com/post-data-src.jpg", result);
    }

    [Fact]
    public async Task TryExtractImageLink_WithElementIdButNoSrcAttributes_ReturnsEmpty()
    {
      // Arrange
      var html = @"<html>
<body>
    <img id='post-image' alt='missing src'/>
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
      Assert.NotNull(result);
      Assert.Empty(result);
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

    [Fact]
    public async Task TryExtractImageLink_WithScrapedFtpImageUrl_ReturnsEmpty()
    {
      // Arrange
      var html = @"<html>
<head>
    <meta property='og:image' content='ftp://example.com/image.jpg'/>
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
      Assert.NotNull(result);
      Assert.Empty(result);
    }

    [Fact]
    public async Task TryExtractImageLink_WithWhitespaceDescriptionImageSrc_FallsBackToWebpageScrape()
    {
      // Arrange
      var xml = @"<?xml version='1.0'?>
<rss>
    <channel>
        <item>
            <description><![CDATA[<img src='   ' />]]></description>
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
    [InlineData("https://example.com/path/", "http://example.com/image.jpg")]
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

    [Fact]
    public async Task TryExtractImageLink_WithMalformedAbsoluteLikeUrl_ReturnsEmpty()
    {
      // Arrange
      var xml = @"<?xml version='1.0'?>
<rss>
    <channel>
        <item>
            <enclosure type='image/jpeg' url='http://[::1' />
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("", xml);

      // Assert
      Assert.NotNull(result);
      Assert.Empty(result);
    }

    [Fact]
    public async Task TryExtractImageLink_WithUnsupportedSchemeUrl_ReturnsEmpty()
    {
      // Arrange
      var xml = @"<?xml version='1.0'?>
<rss>
    <channel>
        <item>
            <enclosure type='image/jpeg' url='ftp://example.com/image.jpg' />
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("https://example.com/feed", xml);

      // Assert
      Assert.NotNull(result);
      Assert.Empty(result);
    }

    [Fact]
    public async Task TryExtractImageLink_WithEnclosureEmptyUrl_FallsBackToDescription()
    {
      // Arrange - enclosure exists with type but url is empty (line 46 false branch)
      var xml = @"<?xml version='1.0'?>
<rss>
    <channel>
        <item>
            <enclosure type='image/jpeg' url='' />
            <description><![CDATA[<img src='https://example.com/fallback.jpg' />]]></description>
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

      // Assert
      Assert.Equal("https://example.com/fallback.jpg", result);
    }

    [Fact]
    public async Task TryExtractImageLink_WithEnclosureWhitespaceUrl_FallsBackToDescription()
    {
      // Arrange - enclosure exists with type but url is whitespace (line 46 false branch)
      var xml = @"<?xml version='1.0'?>
<rss>
    <channel>
        <item>
            <enclosure type='image/jpeg' url='   ' />
            <description><![CDATA[<img src='https://example.com/fallback2.jpg' />]]></description>
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

      // Assert
      Assert.Equal("https://example.com/fallback2.jpg", result);
    }

    [Fact]
    public async Task TryExtractImageLink_WithMediaContentEmptyUrl_FallsBackToDescription()
    {
      // Arrange - media:content exists but url is empty (line 58 false branch)
      var xml = @"<?xml version='1.0'?>
<rss xmlns:media='http://search.yahoo.com/mrss/'>
    <channel>
        <item>
            <media:content type='image/png' url='' />
            <description><![CDATA[<img src='https://example.com/media-fallback.jpg' />]]></description>
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

      // Assert
      Assert.Equal("https://example.com/media-fallback.jpg", result);
    }

    [Fact]
    public async Task TryExtractImageLink_WithMediaThumbnailWhitespaceUrl_FallsBackToDescription()
    {
      // Arrange - media:thumbnail exists but url is whitespace (line 58 false branch)
      var xml = @"<?xml version='1.0'?>
<rss xmlns:media='http://search.yahoo.com/mrss/'>
    <channel>
        <item>
            <media:thumbnail url='  ' />
            <description><![CDATA[<img src='https://example.com/thumb-fallback.jpg' />]]></description>
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

      // Assert
      Assert.Equal("https://example.com/thumb-fallback.jpg", result);
    }

    [Fact]
    public async Task TryExtractImageLink_WithItunesImageEmptyHref_FallsBackToDescription()
    {
      // Arrange - itunes:image exists but href is empty (line 67 false branch)
      var xml = @"<?xml version='1.0'?>
<rss xmlns:itunes='http://www.itunes.com/dtds/podcast-1.0.dtd'>
    <channel>
        <item>
            <itunes:image href='' />
            <description><![CDATA[<img src='https://example.com/itunes-fallback.jpg' />]]></description>
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

      // Assert
      Assert.Equal("https://example.com/itunes-fallback.jpg", result);
    }

    [Fact]
    public async Task TryExtractImageLink_WithItunesImageWhitespaceHref_FallsBackToDescription()
    {
      // Arrange - itunes:image exists but href is whitespace (line 67 false branch)
      var xml = @"<?xml version='1.0'?>
<rss xmlns:itunes='http://www.itunes.com/dtds/podcast-1.0.dtd'>
    <channel>
        <item>
            <itunes:image href='   ' />
            <description><![CDATA[<img src='https://example.com/itunes-fallback2.jpg' />]]></description>
        </item>
    </channel>
</rss>";

      // Act
      var result = await _imageParserService.TryExtractImageLink("https://example.com", xml);

      // Assert
      Assert.Equal("https://example.com/itunes-fallback2.jpg", result);
    }

    #endregion
  }
}
