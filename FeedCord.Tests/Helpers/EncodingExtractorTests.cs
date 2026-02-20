using Xunit;
using Moq;
using FeedCord.Services.Helpers;
using System.Net.Http.Headers;
using System.Text;

namespace FeedCord.Tests.Helpers;

public class EncodingExtractorTests
{
    private HttpContentHeaders CreateMockHeaders(string? charSet = null)
    {
        var mockContent = new Mock<HttpContent>();
        var headers = mockContent.Object.Headers;
        if (!string.IsNullOrEmpty(charSet))
        {
            headers.ContentType = new MediaTypeHeaderValue("application/xml") { CharSet = charSet };
        }
        return headers;
    }

    [Fact]
    public void ConvertBytesByComparing_ValidUtf8Bytes_ReturnsCorrectString()
    {
        // Arrange
        var content = "Hello, World!";
        var bytes = Encoding.UTF8.GetBytes(content);
        var headers = CreateMockHeaders("utf-8");

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void ConvertBytesByComparing_Iso88591Encoding_ReturnsCorrectString()
    {
        // Arrange
        var content = "Café";
        var bytes = Encoding.GetEncoding("iso-8859-1").GetBytes(content);
        var headers = CreateMockHeaders("iso-8859-1");

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void ConvertBytesByComparing_NoCharSetSpecified_DefaultsToUtf8()
    {
        // Arrange
        var content = "Default encoding test";
        var bytes = Encoding.UTF8.GetBytes(content);
        var headers = CreateMockHeaders(null);  // No charset

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void ConvertBytesByComparing_XmlDeclaredEncoding_PreferredOverServerDeclared()
    {
        // Arrange
        var xmlWithEncoding = "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?><root>Café</root>";
        var bytes = Encoding.GetEncoding("iso-8859-1").GetBytes(xmlWithEncoding);
        var headers = CreateMockHeaders("utf-8");  // Different from XML declaration

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        // Should prefer XML declaration over server header
        Assert.NotNull(result);
        Assert.Contains("Café", result);
    }

    [Fact]
    public void ConvertBytesByComparing_NullHeaders_DefaultsToUtf8()
    {
        // Arrange
        var content = "Test content";
        var bytes = Encoding.UTF8.GetBytes(content);

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, (System.Net.Http.Headers.HttpContentHeaders?)null);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void ConvertBytesByComparing_InvalidEncodingName_FallsBackToUtf8()
    {
        // Arrange
        var content = "Fallback test";
        var bytes = Encoding.UTF8.GetBytes(content);
        var headers = CreateMockHeaders("invalid-encoding-xyz");

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void ConvertBytesByComparing_EmptyCharSet_DefaultsToUtf8()
    {
        // Arrange
        var content = "Test content";
        var bytes = Encoding.UTF8.GetBytes(content);
        var headers = CreateMockHeaders("");  // Empty charset

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void ConvertBytesByComparing_XmlEncodingWithDoubleQuotes_ReturnsCorrectString()
    {
        // Arrange
        var xmlWithEncoding = "<?xml version=\"1.0\" encoding=\"utf-16\"?><root>Test</root>";
        var bytes = Encoding.GetEncoding("utf-16").GetBytes(xmlWithEncoding);
        var headers = CreateMockHeaders(null);

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        Assert.Contains("Test", result);
    }

    [Fact]
    public void ConvertBytesByComparing_XmlEncodingWithSingleQuotes_ReturnsCorrectString()
    {
        // Arrange
        var xmlWithEncoding = "<?xml version='1.0' encoding='iso-8859-1'?><root>Café</root>";
        var bytes = Encoding.GetEncoding("iso-8859-1").GetBytes(xmlWithEncoding);
        var headers = CreateMockHeaders(null);

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        Assert.Contains("Café", result);
    }

    [Fact]
    public void ConvertBytesByComparing_LargeXmlContent_ReadsFirstBytesForEncoding()
    {
        // Arrange
        var xmlHeader = "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?>";
        var largeContent = new string('x', 10000);  // Large content
        var xmlContent = xmlHeader + largeContent;
        var bytes = Encoding.GetEncoding("iso-8859-1").GetBytes(xmlContent);
        var headers = CreateMockHeaders(null);

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("xxx", result);
    }

    [Fact]
    public void ConvertBytesByComparing_NoXmlDeclaration_UsesServerDeclaredEncoding()
    {
        // Arrange
        var content = "Content without XML declaration";
        var bytes = Encoding.GetEncoding("iso-8859-1").GetBytes(content);
        var headers = CreateMockHeaders("iso-8859-1");

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void ConvertBytesByComparing_XmlEncodingWithWhitespace_ParsesToCorrectEncoding()
    {
        // Arrange
        var xmlWithWhitespace = "<?xml version='1.0' encoding = 'iso-8859-1' ?><root>Café</root>";
        var bytes = Encoding.GetEncoding("iso-8859-1").GetBytes(xmlWithWhitespace);
        var headers = CreateMockHeaders(null);

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        Assert.Contains("Café", result);
    }

    [Fact]
    public void ConvertBytesByComparing_CaseInsensitiveXmlEncoding_IsHandled()
    {
        // Arrange
        var xmlWithUppercase = "<?xml version=\"1.0\" ENCODING=\"utf-8\"?><root>Test</root>";
        var bytes = Encoding.UTF8.GetBytes(xmlWithUppercase);
        var headers = CreateMockHeaders(null);

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        Assert.Contains("Test", result);
    }

    [Fact]
    public void ConvertBytesByComparing_MalformedXmlEncoding_FallsBackToServerOrUtf8()
    {
        // Arrange
        var xmlWithMalformedEncoding = "<?xml version=\"1.0\" encoding=\"utf-8-invalid\"?><root>Test</root>";
        var bytes = Encoding.UTF8.GetBytes(xmlWithMalformedEncoding);
        var headers = CreateMockHeaders("utf-8");

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        Assert.Contains("Test", result);
    }

    [Fact]
    public void ConvertBytesByComparing_EmptyBytes_ReturnsEmptyString()
    {
        // Arrange
        var bytes = new byte[] { };
        var headers = CreateMockHeaders(null);

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ConvertBytesByComparing_UtfBomBytes_HandlesCorrectly()
    {
        // Arrange
        var content = "BOM Test";
        var utf8WithBom = new UTF8Encoding(true);  // true = emit BOM
        var bytes = utf8WithBom.GetBytes(content);
        var headers = CreateMockHeaders("utf-8");

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        // UTF8 BOM should be handled transparently
        Assert.Contains("BOM Test", result);
    }

    [Fact]
    public void ConvertBytesByComparing_SpecialCharacters_PreservedCorrectly()
    {
        // Arrange
        var content = "Emoji: 🎉 Special: € ¥ £";
        var bytes = Encoding.UTF8.GetBytes(content);
        var headers = CreateMockHeaders("utf-8");

        // Act
        var result = EncodingExtractor.ConvertBytesByComparing(bytes, headers);

        // Assert
        Assert.Equal(content, result);
    }
}
