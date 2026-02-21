using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using Microsoft.Extensions.Logging;
using FeedCord.Services.Interfaces;
using System.Collections.Concurrent;
using FeedCord.Helpers;

namespace FeedCord.Infrastructure.Http
{
    public class CustomHttpClient : ICustomHttpClient
    {
        private static readonly string[] DefaultFallbackUserAgents =
        {
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.79 Safari/537.36",
            "FeedFetcher-Google"
        };

        private readonly HttpClient _innerClient;
        private readonly ILogger<CustomHttpClient> _logger;
        private readonly SemaphoreSlim _throttle;
        private readonly ConcurrentDictionary<string, string> _userAgentCache;
        private readonly IReadOnlyList<string> _fallbackUserAgents;
        // Rate limiting fields
        private readonly SemaphoreSlim _rateLimiter = new SemaphoreSlim(1, 1);
        private DateTime _lastPostTime = DateTime.MinValue;
        private readonly TimeSpan _minPostInterval = TimeSpan.FromSeconds(2);
        public CustomHttpClient(
            ILogger<CustomHttpClient> logger,
            HttpClient innerClient,
            SemaphoreSlim throttle,
            IEnumerable<string>? fallbackUserAgents = null)
        {
            _logger = logger;
            _throttle = throttle;
            _innerClient = innerClient;
            _userAgentCache = new ConcurrentDictionary<string, string>();
            _fallbackUserAgents = (fallbackUserAgents ?? DefaultFallbackUserAgents)
                .Where(ua => !string.IsNullOrWhiteSpace(ua))
                .Select(ua => ua.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        public async Task<HttpResponseMessage?> GetAsyncWithFallback(string url, CancellationToken cancellationToken = default)
        {
            HttpResponseMessage? response = null;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                if (_userAgentCache.ContainsKey(url))
                {
                    request.Headers.UserAgent.ParseAdd(_userAgentCache.GetValueOrDefault(url, ""));
                }

                response = await SendGetAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode && ShouldTryAlternative(response.StatusCode))
                {
                    response = await TryAlternativeAsync(url, response, cancellationToken);
                }

                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning("Request to {Url} was canceled: {Ex}", url, SensitiveDataMasker.MaskException(ex));
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning("Operation was canceled for {Url}: {Ex}", url, SensitiveDataMasker.MaskException(ex));
            }
            catch (Exception ex)
            {
                _logger.LogError("An error occurred while processing the request for {Url}: {Ex}", url, SensitiveDataMasker.MaskException(ex));
            }

            return response;
        }


        public async Task PostAsyncWithFallback(string url, StringContent forumChannelContent, StringContent textChannelContent, bool isForum, CancellationToken cancellationToken = default)
        {
            await _rateLimiter.WaitAsync(cancellationToken);
            try
            {
                // Enforce 1 request per 2 seconds
                var now = DateTime.UtcNow;
                var waitTime = _minPostInterval - (now - _lastPostTime);
                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);
                }
                _lastPostTime = DateTime.UtcNow;

                var response = await PostWithThrottleAsync(url, isForum ? forumChannelContent : textChannelContent, cancellationToken);

                if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Discord POST failed. Status: {StatusCode}, Body: {Body}", response.StatusCode, responseBody);

                    response = await PostWithThrottleAsync(url, !isForum ? forumChannelContent : textChannelContent, cancellationToken);

                    if (response.StatusCode == HttpStatusCode.NoContent)
                    {
                        _logger.LogWarning(
                            "Successfully posted to Discord Channel after switching channel type - Change Forum Property in Config!!");
                    }
                    else
                    {
                        var fallbackResponseBody = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Failed to post to Discord Channel after fallback. Status: {StatusCode}, Body: {Body}", response.StatusCode, fallbackResponseBody);
                    }
                }
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        private async Task<HttpResponseMessage> TryAlternativeAsync(string url, HttpResponseMessage oldResponse, CancellationToken cancellationToken)
        {
            var uri = new Uri(url);
            var baseUrl = uri.GetLeftPart(UriPartial.Authority);

            HttpRequestMessage request;
            HttpResponseMessage response;

            try
            {
                foreach (var fallbackUserAgent in _fallbackUserAgents)
                {
                    request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.UserAgent.ParseAdd(fallbackUserAgent);

                    response = await SendGetAsync(request, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    _userAgentCache[url] = fallbackUserAgent;
                    return response;
                }

                var robotsUrl = new Uri(new Uri(baseUrl), "/robots.txt").AbsoluteUri;
                var userAgents = await GetRobotsUserAgentsAsync(robotsUrl, cancellationToken);

                if (userAgents.Count > 0)
                {
                    foreach (var userAgent in userAgents)
                    {
                        request = new HttpRequestMessage(HttpMethod.Get, url);
                        request.Headers.UserAgent.ParseAdd(userAgent);
                        request.Headers.Add("Accept", "*/*");
                        response = await SendGetAsync(request, cancellationToken);
                        if (response.IsSuccessStatusCode)
                        {
                            _userAgentCache[url] = userAgent;
                            return response;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to fetch RSS Feed after fallback attempts: {Url} - {E}", url, SensitiveDataMasker.MaskException(e));
            }
            return oldResponse;
        }

        private static bool ShouldTryAlternative(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.Forbidden
                   || statusCode == HttpStatusCode.Unauthorized
                   || statusCode == HttpStatusCode.TooManyRequests
                   || statusCode == HttpStatusCode.NotAcceptable;
        }

        private async Task<string> FetchRobotsContentAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                await _throttle.WaitAsync(cancellationToken);
                return await _innerClient.GetStringAsync(url, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                _throttle.Release();
            }
        }

        private async Task<HttpResponseMessage> SendGetAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await _throttle.WaitAsync(cancellationToken);
            try
            {
                return await _innerClient.SendAsync(request, cancellationToken);
            }
            finally
            {
                _throttle.Release();
            }
        }

        private async Task<HttpResponseMessage> PostWithThrottleAsync(string url, StringContent content, CancellationToken cancellationToken)
        {
            await _throttle.WaitAsync(cancellationToken);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = await CloneStringContentAsync(content, cancellationToken)
                };

                return await _innerClient.SendAsync(request, cancellationToken);
            }
            finally
            {
                _throttle.Release();
            }
        }

        private static async Task<StringContent> CloneStringContentAsync(StringContent content, CancellationToken cancellationToken)
        {
            var mediaType = content.Headers.ContentType?.MediaType ?? "application/json";
            var payloadBytes = await content.ReadAsByteArrayAsync(cancellationToken);

            var charset = content.Headers.ContentType?.CharSet;
            Encoding encoding;

            try
            {
                encoding = string.IsNullOrWhiteSpace(charset)
                    ? Encoding.UTF8
                    : Encoding.GetEncoding(charset);
            }
            catch
            {
                encoding = Encoding.UTF8;
            }

            var payload = encoding.GetString(payloadBytes);

            var clone = new StringContent(payload, encoding, mediaType);

            foreach (var header in content.Headers)
            {
                if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }

        private async Task<List<string>> GetRobotsUserAgentsAsync(string url, CancellationToken cancellationToken)
        {
            var userAgents = new List<string>();

            var robotsContent = await FetchRobotsContentAsync(url, cancellationToken);

            if (robotsContent == string.Empty)
                return userAgents.OrderByDescending(x => x).Distinct().ToList();

            var pattern = @"User-agent:\s*(?<agent>.+)";
            var regex = new Regex(pattern);

            var matches = regex.Matches(robotsContent);

            foreach (Match match in matches)
            {
                var userAgent = match.Groups["agent"].Value.Trim();
                if (!string.IsNullOrEmpty(userAgent))
                {
                    userAgents.Add(userAgent);
                }
            }

            return userAgents.OrderByDescending(x => x).Distinct().ToList();
        }
    }
}
