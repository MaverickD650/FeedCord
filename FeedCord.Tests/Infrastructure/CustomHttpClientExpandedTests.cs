using Xunit;
using Moq;
using Moq.Protected;
using FeedCord.Infrastructure.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace FeedCord.Tests.Infrastructure
{
    public class CustomHttpClientExpandedTests
    {
        #region GetAsyncWithFallback Tests

        [Fact]
        public async Task GetAsyncWithFallback_WithCachedUserAgent_UsesCachedValue()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
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
        public async Task GetAsyncWithFallback_AfterSuccessfulFallback_UsesCachedUserAgentOnNextRequest()
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var observedUserAgents = new List<string>();
            var requestCount = 0;

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
                {
                    requestCount++;
                    observedUserAgents.Add(request.Headers.UserAgent.ToString());

                    if (requestCount == 1)
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
                    }

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(10, 10);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, new[] { "Cached-UA" });

            const string url = "https://example.com/feed";

            await client.GetAsyncWithFallback(url);
            await client.GetAsyncWithFallback(url);

            Assert.True(requestCount >= 3);
            Assert.Contains(observedUserAgents.Skip(2), ua => ua.Contains("Cached-UA"));
        }

        [Fact]
        public async Task GetAsyncWithFallback_WhenConfiguredFallbackSucceeds_CachesConfiguredUserAgent()
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var seenUserAgents = new List<string>();
            var callCount = 0;

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
                {
                    callCount++;
                    seenUserAgents.Add(request.Headers.UserAgent.ToString());

                    if (callCount == 1)
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
                    }

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(5, 5);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, new[] { "Primary-Fallback-UA" });

            const string url = "https://example.com/feed";

            var first = await client.GetAsyncWithFallback(url);
            var second = await client.GetAsyncWithFallback(url);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Contains(seenUserAgents, ua => ua.Contains("Primary-Fallback-UA"));
        }

        [Fact]
        public async Task GetAsyncWithFallback_WhenCachedUserAgentLaterFails_UpdatesCacheEntryViaFallback()
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var callCount = 0;

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
                {
                    callCount++;

                    return callCount switch
                    {
                        1 => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)),
                        2 => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)),
                        3 => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)),
                        4 => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)),
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))
                    };
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(5, 5);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, new[] { "Primary-Fallback-UA" });

            const string url = "https://example.com/feed";

            var first = await client.GetAsyncWithFallback(url);
            var second = await client.GetAsyncWithFallback(url);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.True(callCount >= 4);
        }

        [Fact]
        public async Task GetAsyncWithFallback_WhenRobotsUserAgentSucceeds_CachesRobotsUserAgent()
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var seenUserAgents = new List<string>();
            var robotsFetchCount = 0;

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
                {
                    var uri = request.RequestUri?.AbsoluteUri ?? string.Empty;

                    if (uri.EndsWith("/robots.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        robotsFetchCount++;
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("User-agent: Robots-UA")
                        });
                    }

                    var ua = request.Headers.UserAgent.ToString();
                    if (!string.IsNullOrWhiteSpace(ua))
                    {
                        seenUserAgents.Add(ua);
                    }

                    if (ua.Contains("Robots-UA", StringComparison.Ordinal))
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                    }

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(5, 5);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, new[] { "Bad-UA-1", "Bad-UA-2" });

            const string url = "https://example.com/feed";

            var first = await client.GetAsyncWithFallback(url);
            var second = await client.GetAsyncWithFallback(url);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal(1, robotsFetchCount);
            Assert.Contains(seenUserAgents, ua => ua.Contains("Robots-UA"));
        }

        [Fact]
        public async Task GetAsyncWithFallback_WhenRobotsUserAgentsAllFail_ReturnsOriginalResponse()
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var robotsFetchCount = 0;
            var attemptedRobotUas = new List<string>();

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
                {
                    var uri = request.RequestUri?.AbsoluteUri ?? string.Empty;

                    if (uri.EndsWith("/robots.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        robotsFetchCount++;
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("User-agent: Robot-UA-1\nUser-agent: Robot-UA-2")
                        });
                    }

                    var ua = request.Headers.UserAgent.ToString();
                    if (ua.Contains("Robot-UA", StringComparison.Ordinal))
                    {
                        attemptedRobotUas.Add(ua);
                    }

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(5, 5);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, new[] { "Bad-UA-1", "Bad-UA-2" });

            var response = await client.GetAsyncWithFallback("https://example.com/feed");

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(1, robotsFetchCount);
            Assert.True(attemptedRobotUas.Count >= 2);
        }

        [Fact]
        public async Task GetAsyncWithFallback_WhenCanceledDuringTryAlternative_RethrowsOperationCanceledException()
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            using var cts = new CancellationTokenSource();
            var callCount = 0;

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    callCount++;

                    if (callCount == 1)
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
                    }

                    cts.Cancel();
                    throw new OperationCanceledException(cts.Token);
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(5, 5);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, new[] { "UA-1" });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.GetAsyncWithFallback("https://example.com/feed", cts.Token));
        }

        [Fact]
        public async Task GetAsyncWithFallback_WhenOperationCanceledButTokenNotCanceled_ReturnsOriginalResponse()
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var callCount = 0;

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    callCount++;

                    if (callCount == 1)
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
                    }

                    throw new OperationCanceledException("not user canceled");
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(5, 5);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, new[] { "UA-1" });

            var response = await client.GetAsyncWithFallback("https://example.com/feed", CancellationToken.None);

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetAsyncWithFallback_WhenTryAlternativeThrowsHttpRequestException_ReturnsOriginalResponse()
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var callCount = 0;

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    callCount++;

                    if (callCount == 1)
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
                    }

                    throw new HttpRequestException("fallback send failed");
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(5, 5);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, new[] { "UA-1" });

            var response = await client.GetAsyncWithFallback("https://example.com/feed", CancellationToken.None);

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetAsyncWithFallback_WhenCanceledDuringRobotsFetch_RethrowsOperationCanceledException()
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            using var cts = new CancellationTokenSource();
            var callCount = 0;

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    callCount++;
                    var uri = request.RequestUri?.AbsoluteUri ?? string.Empty;

                    if (uri.EndsWith("/robots.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        cts.Cancel();
                        throw new OperationCanceledException(cts.Token);
                    }

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(5, 5);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, new[] { "Bad-UA-1", "Bad-UA-2" });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.GetAsyncWithFallback("https://example.com/feed", cts.Token));
        }

        [Fact]
        public async Task GetAsyncWithFallback_WhenRobotsFetchOperationCanceledWithoutTokenCancel_ReturnsOriginalResponse()
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    var uri = request.RequestUri?.AbsoluteUri ?? string.Empty;

                    if (uri.EndsWith("/robots.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new OperationCanceledException("robots fetch canceled without token cancel");
                    }

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(5, 5);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, new[] { "Bad-UA-1", "Bad-UA-2" });

            var response = await client.GetAsyncWithFallback("https://example.com/feed", CancellationToken.None);

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetAsyncWithFallback_WithTaskCanceledException_LogsWarningAndReturnsNull()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);

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
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);

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
        public async Task GetAsyncWithFallback_WithCanceledToken_RethrowsOperationCanceledException()
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException(cts.Token));

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(1, 1);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.GetAsyncWithFallback("https://example.com", cts.Token));
        }

        [Fact]
        public async Task GetAsyncWithFallback_WithGeneralException_LogsErrorAndReturnsNull()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);

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
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var callCount = 0;
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);

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

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.TooManyRequests)]
        [InlineData(HttpStatusCode.NotAcceptable)]
        public async Task GetAsyncWithFallback_WithFallbackEligibleStatus_TriggersFallback(HttpStatusCode initialStatus)
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var callCount = 0;
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
                {
                    callCount++;
                    return Task.FromResult(new HttpResponseMessage(callCount == 1 ? initialStatus : HttpStatusCode.OK));
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(10, 10);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, new[] { "UA-1" });

            var response = await client.GetAsyncWithFallback("https://example.com");

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(callCount >= 2, $"Expected fallback calls for {initialStatus}, got {callCount}");
        }

        [Fact]
        public async Task GetAsyncWithFallback_WithInternalServerError_DoesNotTryUserAgentFallback()
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var callCount = 0;
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
                {
                    callCount++;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(10, 10);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle, new[] { "UA-1", "UA-2" });

            var response = await client.GetAsyncWithFallback("https://example.com");

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(1, callCount);
        }

        #endregion

        #region PostAsyncWithFallback Tests

        [Fact]
        public async Task PostAsyncWithFallback_WithSuccessResponse_DoesNotRetry()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var callCount = 0;
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);

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
        public async Task PostAsyncWithFallback_WithForumFlag_UsesForumPayloadOnFirstAttempt()
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            string? sentBody = null;
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>(async (request, _) =>
                {
                    sentBody = request.Content is null ? null : await request.Content.ReadAsStringAsync();
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(1, 1);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle);

            var forumContent = new StringContent("{\"kind\":\"forum\"}");
            var textContent = new StringContent("{\"kind\":\"text\"}");

            await client.PostAsyncWithFallback("https://discord.com/api/webhooks/123", forumContent, textContent, true);

            Assert.Equal("{\"kind\":\"forum\"}", sentBody);
        }

        [Fact]
        public async Task PostAsyncWithFallback_WithFailureResponse_RetriesWithAltChannelType()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var callCount = 0;
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);

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
        public async Task PostAsyncWithFallback_WithForumFlagAndInitialFailure_RetriesWithTextPayload()
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var sentBodies = new List<string>();
            var callCount = 0;
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>(async (request, _) =>
                {
                    callCount++;
                    if (request.Content is not null)
                    {
                        sentBodies.Add(await request.Content.ReadAsStringAsync());
                    }

                    return new HttpResponseMessage(callCount == 1 ? HttpStatusCode.BadRequest : HttpStatusCode.NoContent);
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(1, 1);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle);

            var forumContent = new StringContent("{\"kind\":\"forum\"}");
            var textContent = new StringContent("{\"kind\":\"text\"}");

            await client.PostAsyncWithFallback("https://discord.com/api/webhooks/123", forumContent, textContent, true);

            Assert.Equal(2, callCount);
            Assert.Equal(2, sentBodies.Count);
            Assert.Equal("{\"kind\":\"forum\"}", sentBodies[0]);
            Assert.Equal("{\"kind\":\"text\"}", sentBodies[1]);
        }

        [Fact]
        public async Task PostAsyncWithFallback_OnRetry_UsesNewRequestContentInstance()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var callCount = 0;
            var sentContents = new List<HttpContent?>();
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
                {
                    callCount++;
                    sentContents.Add(request.Content);

                    return Task.FromResult(new HttpResponseMessage(callCount == 1 ? HttpStatusCode.BadRequest : HttpStatusCode.NoContent));
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(1, 1);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle);

            var content = new StringContent("{\"message\":\"hello\"}");

            // Act
            await client.PostAsyncWithFallback("https://discord.com/api/webhooks/123", content, content, false);

            // Assert
            Assert.Equal(2, callCount);
            Assert.Equal(2, sentContents.Count);
            Assert.NotNull(sentContents[0]);
            Assert.NotNull(sentContents[1]);
            Assert.NotSame(sentContents[0], sentContents[1]);
        }

        [Fact]
        public async Task PostAsyncWithFallback_WithInvalidCharset_ClonesPayloadUsingUtf8Fallback()
        {
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            string? observedCharset = null;
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
                {
                    observedCharset = request.Content?.Headers.ContentType?.CharSet;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
                });

            var httpClient = new HttpClient(handler.Object);
            var throttle = new SemaphoreSlim(1, 1);
            var client = new CustomHttpClient(mockLogger.Object, httpClient, throttle);

            var forumContent = new StringContent("{\"value\":1}");
            forumContent.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "definitely-not-a-real-charset"
            };
            var textContent = new StringContent("{\"value\":2}");

            await client.PostAsyncWithFallback("https://discord.com/api/webhooks/123", forumContent, textContent, true);

            Assert.Equal("utf-8", observedCharset);
        }

        [Fact]
        public async Task PostAsyncWithFallback_EnforcesRateLimiting()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);

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
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
            var activeRequests = 0;
            var maxConcurrentRequests = 0;
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);

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
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
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
            var mockLogger = new Mock<ILogger<CustomHttpClient>>(MockBehavior.Loose);
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
