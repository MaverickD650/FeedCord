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

    [Fact]
    public async Task GetAsyncWithFallback_UsesConfiguredFallbackUserAgent()
    {
        var mockLogger = new Mock<ILogger<CustomHttpClient>>();
        var observedUserAgents = new List<string>();
        var callCount = 0;

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                observedUserAgents.Add(request.Headers.UserAgent.ToString());
                callCount++;
                var statusCode = callCount == 1
                    ? System.Net.HttpStatusCode.Forbidden
                    : System.Net.HttpStatusCode.OK;
                return Task.FromResult(new HttpResponseMessage(statusCode));
            });

        var httpClient = new HttpClient(handler.Object);
        var throttle = new SemaphoreSlim(1, 1);
        var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, new[] { "My-Custom-UA" });

        var response = await client.GetAsyncWithFallback("http://example.com");

        Assert.NotNull(response);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("My-Custom-UA", observedUserAgents);
    }

    [Fact]
    public async Task GetAsyncWithFallback_UsesDefaultFallbackUserAgentsWhenConfigMissing()
    {
        var mockLogger = new Mock<ILogger<CustomHttpClient>>();
        var observedUserAgents = new List<string>();
        var callCount = 0;

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                observedUserAgents.Add(request.Headers.UserAgent.ToString());
                callCount++;
                var statusCode = callCount < 3
                    ? System.Net.HttpStatusCode.Forbidden
                    : System.Net.HttpStatusCode.OK;
                return Task.FromResult(new HttpResponseMessage(statusCode));
            });

        var httpClient = new HttpClient(handler.Object);
        var throttle = new SemaphoreSlim(1, 1);
        var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle);

        var response = await client.GetAsyncWithFallback("http://example.com");

        Assert.NotNull(response);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(observedUserAgents, ua => ua.Contains("Mozilla/5.0"));
        Assert.Contains(observedUserAgents, ua => ua.Contains("FeedFetcher-Google"));
    }
}
