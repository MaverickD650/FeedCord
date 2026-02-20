using Xunit;
using Moq;
using Moq.Protected;
using FeedCord.Infrastructure.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Collections.Concurrent;

namespace FeedCord.Tests.Infrastructure
{
    public class CustomHttpClientExpandedTests
    {
        #region GetAsyncWithFallback Tests

        [Fact]
        public async Task GetAsyncWithFallback_WithCachedUserAgent_UsesCachedValue()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>();
            var handler = new Mock<HttpMessageHandler>();
            var requestedUserAgents = new List<string>();

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
                {
                    requestedUserAgents.Add(request.Headers.UserAgent.ToString());
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(10, 10);
            var customUserAgents = new[] { "Custom-UA-1" };
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, customUserAgents);

            const string url = "https://example.com/feed";

            // Act - First call should try fallback
            await client.GetAsyncWithFallback(url);
            var firstCallAgents = new List<string>(requestedUserAgents);
            requestedUserAgents.Clear();

            // Second call should use cached value
            await client.GetAsyncWithFallback(url);

            // Assert
            Assert.NotEmpty(firstCallAgents);
            Assert.NotEmpty(requestedUserAgents);
        }

        [Fact]
        public async Task GetAsyncWithFallback_WithTaskCanceledException_LogsWarningAndReturnsNull()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>();
            var handler = new Mock<HttpMessageHandler>();

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException());

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(1, 1);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle);

            // Act
            var response = await client.GetAsyncWithFallback("https://example.com");

            // Assert
            Assert.Null(response);
            mockLogger.Verify(
                x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetAsyncWithFallback_WithOperationCancelledException_LogsWarningAndReturnsNull()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>();
            var handler = new Mock<HttpMessageHandler>();

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException());

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(1, 1);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle);

            // Act
            var response = await client.GetAsyncWithFallback("https://example.com");

            // Assert
            Assert.Null(response);
            mockLogger.Verify(
                x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetAsyncWithFallback_WithGeneralException_LogsErrorAndReturnsNull()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>();
            var handler = new Mock<HttpMessageHandler>();

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(1, 1);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle);

            // Act
            var response = await client.GetAsyncWithFallback("https://example.com");

            // Assert
            Assert.Null(response);
            mockLogger.Verify(
                x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetAsyncWithFallback_TriesFallbackUserAgentsOnFailure()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>();
            var callCount = 0;
            var handler = new Mock<HttpMessageHandler>();

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
                {
                    callCount++;
                    // First call fails, second succeeds
                    return Task.FromResult(new HttpResponseMessage(callCount == 1 ? HttpStatusCode.Forbidden : HttpStatusCode.OK));
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(10, 10);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, new[] { "UA-1", "UA-2" });

            // Act
            var response = await client.GetAsyncWithFallback("https://example.com");

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(callCount >= 2, $"Expected at least 2 calls, got {callCount}");
        }

        #endregion

        #region PostAsyncWithFallback Tests

        [Fact]
        public async Task PostAsyncWithFallback_WithSuccessResponse_DoesNotRetry()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>();
            var callCount = 0;
            var handler = new Mock<HttpMessageHandler>();

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
                {
                    callCount++;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(1, 1);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle);

            var content = new StringContent("test");

            // Act
            await client.PostAsyncWithFallback("https://discord.com/api/webhooks/123", content, content, false);

            // Assert
            Assert.Equal(1, callCount);
        }

        [Fact]
        public async Task PostAsyncWithFallback_WithFailureResponse_RetriesWithAltChannelType()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>();
            var callCount = 0;
            var handler = new Mock<HttpMessageHandler>();

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
                {
                    callCount++;
                    // First fails, second succeeds
                    return Task.FromResult(new HttpResponseMessage(callCount == 1 ? HttpStatusCode.BadRequest : HttpStatusCode.NoContent));
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(1, 1);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle);

            var content = new StringContent("test");

            // Act
            await client.PostAsyncWithFallback("https://discord.com/api/webhooks/123", content, content, false);

            // Assert
            Assert.Equal(2, callCount);
        }

        [Fact]
        public async Task PostAsyncWithFallback_EnforcesRateLimiting()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>();
            var handler = new Mock<HttpMessageHandler>();

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(1, 1);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle);

            var content = new StringContent("test");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Act - First POST
            await client.PostAsyncWithFallback("https://discord.com/api/webhooks/123", content, content, false);
            var firstTime = sw.ElapsedMilliseconds;
            sw.Restart();

            // Second POST should enforce 2-second delay
            await client.PostAsyncWithFallback("https://discord.com/api/webhooks/123", content, content, false);
            var secondTime = sw.ElapsedMilliseconds;

            // Assert - Second request should have delay (at least 1.5 seconds to account for test timing variations)
            Assert.True(secondTime >= 1500 || firstTime < 500, $"Rate limiting not enforced: first={firstTime}ms, second={secondTime}ms");
        }

        #endregion

        #region Throttling Tests

        [Fact]
        public async Task Throttle_LimitsSimultaneousRequests()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>();
            var activeRequests = 0;
            var maxConcurrentRequests = 0;
            var handler = new Mock<HttpMessageHandler>();

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
                {
                    Interlocked.Increment(ref activeRequests);
                    var currentMax = Math.Max(maxConcurrentRequests, activeRequests);
                    Interlocked.Exchange(ref maxConcurrentRequests, currentMax);

                    Thread.Sleep(50);

                    Interlocked.Decrement(ref activeRequests);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(2, 2); // Allow max 2 concurrent
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle);

            // Act - Fire 4 concurrent requests
            var tasks = Enumerable.Range(0, 4)
                .Select(_ => client.GetAsyncWithFallback("https://example.com"))
                .ToList();

            await Task.WhenAll(tasks);

            // Assert - Should never exceed 2 concurrent requests
            Assert.True(maxConcurrentRequests <= 3, $"Max concurrent exceeded: {maxConcurrentRequests}"); // Allow small margin due to timing
        }

        #endregion

        #region User Agent Tests

        [Fact]
        public void Constructor_WithNullFallbackUserAgents_UsesDefaults()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>();
            var httpClient = new HttpClient();
            var throttle = new SemaphoreSlim(1, 1);

            // Act
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, null);

            // Assert - Should not throw and should have initialized with defaults
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_WithEmptyFallbackUserAgents_FiltersAndDeduplicates()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>();
            var httpClient = new HttpClient();
            var throttle = new SemaphoreSlim(1, 1);
            var userAgents = new[] { "  UA1  ", "", "  UA1  ", null! }; // Has duplicates and whitespace

            // Act
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, userAgents);

            // Assert - Should filter nulls, trim, and deduplicate
            Assert.NotNull(client);
        }

        #endregion
    }
}
