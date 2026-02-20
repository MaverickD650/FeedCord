using Xunit;
using Moq;
using FeedCord.Infrastructure.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq.Protected;

namespace FeedCord.Tests.Infrastructure;

public class CustomHttpClientTests
{
    [Fact]
    public async Task GetAsyncWithFallback_ReturnsResponseOnSuccess()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<CustomHttpClient>>();
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var httpClient = new HttpClient(handler.Object);
        var throttle = new SemaphoreSlim(1, 1);
        var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle);

        // Act
        var response = await client.GetAsyncWithFallback("http://example.com");

        // Assert
        Assert.NotNull(response);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
