using FeedCord.Common;
using FeedCord.Helpers;
using FeedCord.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using FeedCord.Core.Interfaces;
using FeedCord.Services.Helpers;

namespace FeedCord.Services
{
    public class FeedManager : IFeedManager
    {
        private readonly Config _config;
        private readonly SemaphoreSlim _instancedConcurrentRequests;
        private readonly ICustomHttpClient _httpClient;
        private readonly ILogAggregator _logAggregator;
        private readonly ILogger<FeedManager> _logger;
        private readonly IRssParsingService _rssParsingService;
        private readonly IPostFilterService _postFilterService;
        private readonly Dictionary<string, ReferencePost> _lastRunReference;
        private readonly ConcurrentDictionary<string, FeedState> _feedStates;

        public FeedManager(
            Config config,
            ICustomHttpClient httpClient,
            IRssParsingService rssParsingService,
            ILogger<FeedManager> logger,
            ILogAggregator logAggregator,
            IPostFilterService postFilterService)
        {
            _config = config;
            _httpClient = httpClient;
            var feedDumpPath = Path.Combine(AppContext.BaseDirectory, "feed_dump.csv");
            _lastRunReference = CsvReader.LoadReferencePosts(feedDumpPath);
            _rssParsingService = rssParsingService;
            _logger = logger;
            _logAggregator = logAggregator;
            _postFilterService = postFilterService;
            _feedStates = new ConcurrentDictionary<string, FeedState>();
            _instancedConcurrentRequests = new SemaphoreSlim(config.ConcurrentRequests);
        }
        public async Task<List<Post>> CheckForNewPostsAsync(CancellationToken cancellationToken = default)
        {
            ConcurrentBag<Post> allNewPosts = new();

            var tasks = _feedStates.Select(async (feed) =>
                await CheckSingleFeedAsync(feed.Key, feed.Value, allNewPosts, _config.DescriptionLimit, cancellationToken));

            await Task.WhenAll(tasks);

            _logAggregator.SetNewPostCount(allNewPosts.Count);

            return allNewPosts.ToList();
        }
        public async Task InitializeUrlsAsync(CancellationToken cancellationToken = default)
        {
            var id = _config.Id;
            var validRssUrls = _config.RssUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToArray();

            var validYoutubeUrls = _config.YoutubeUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToArray();

            var rssCount = await GetSuccessCount(validRssUrls, false, cancellationToken);
            var youtubeCount = await GetSuccessCount(validYoutubeUrls, true, cancellationToken);
            var successCount = rssCount + youtubeCount;

            var totalUrls = validRssUrls.Length + validYoutubeUrls.Length;

            _logger.LogInformation("{id}: Tested successfully for {UrlCount} out of {TotalUrls} Urls in Configuration File", id, successCount, totalUrls);
        }

        public IReadOnlyDictionary<string, FeedState> GetAllFeedData()
        {
            return _feedStates;
        }
        private async Task<int> GetSuccessCount(string[] urls, bool isYoutube, CancellationToken cancellationToken)
        {
            var successCount = 0;

            if (urls.Length == 0 || urls.Length == 1 && string.IsNullOrEmpty(urls[0]))
            {
                return successCount;
            }

            foreach (var url in urls)
            {
                var isSuccess = await TestUrlAsync(url, cancellationToken);

                if (!isSuccess)
                {
                    continue;
                }

                if (_lastRunReference.TryGetValue(url, out var value))
                {
                    _feedStates.TryAdd(url, new FeedState
                    {
                        IsYoutube = isYoutube,
                        LastPublishDate = value.LastRunDate,
                        ErrorCount = 0
                    });

                    successCount++;

                    continue;
                }

                bool successfulAdd;
                DateTime latestPublishDate;

                if (isYoutube)
                {
                    var posts = await FetchYoutubeAsync(url, cancellationToken);
                    latestPublishDate = posts?.FirstOrDefault()?.PublishDate ?? DateTime.Now;
                    successfulAdd = _feedStates.TryAdd(url, new FeedState
                    {
                        IsYoutube = true,
                        LastPublishDate = latestPublishDate,
                        ErrorCount = 0
                    });
                }
                else
                {
                    var posts = await FetchRssAsync(url, _config.DescriptionLimit, cancellationToken);
                    latestPublishDate = posts?.Max(p => p?.PublishDate) ?? DateTime.Now;
                    successfulAdd = _feedStates.TryAdd(url, new FeedState
                    {
                        IsYoutube = false,
                        LastPublishDate = latestPublishDate,
                        ErrorCount = 0
                    });
                }

                if (successfulAdd)
                {
                    successCount++;
                    _logger.LogInformation("Successfully initialized URL: {Url}", url);
                }

                else
                {
                    _logger.LogWarning("Failed to initialize URL: {Url}", url);
                }
            }

            return successCount;
        }
        private async Task<bool> TestUrlAsync(string url, CancellationToken cancellationToken)
        {
            var acquired = false;
            try
            {
                await _instancedConcurrentRequests.WaitAsync(cancellationToken);
                acquired = true;

                var response = await _httpClient.GetAsyncWithFallback(url, cancellationToken);

                if (response is null)
                {
                    _logAggregator.AddUrlResponse(url, -99);
                    return false;
                }

                _logAggregator.AddUrlResponse(url, (int)response.StatusCode);

                response.EnsureSuccessStatusCode();

                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logAggregator.AddUrlResponse(url, (int)(ex.StatusCode ?? System.Net.HttpStatusCode.BadRequest));
            }
            catch (Exception)
            {
                _logger.LogWarning("Failed to instantiate URL: {Url}", url);
            }
            finally
            {
                if (acquired)
                {
                    _instancedConcurrentRequests.Release();
                }
            }

            return false;
        }
        private async Task CheckSingleFeedAsync(string url, FeedState feedState, ConcurrentBag<Post> newPosts, int trim, CancellationToken cancellationToken)
        {
            List<Post?> posts;
            var acquired = false;

            try
            {
                await _instancedConcurrentRequests.WaitAsync(cancellationToken);
                acquired = true;

                posts = feedState.IsYoutube ?
                    await FetchYoutubeAsync(url, cancellationToken) :
                    await FetchRssAsync(url, trim, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                HandleFeedError(url, feedState, ex);
                return;
            }
            finally
            {
                if (acquired)
                {
                    _instancedConcurrentRequests.Release();
                }
            }

            var freshlyFetched = posts.Where(p => p?.PublishDate > feedState.LastPublishDate).ToList();

            if (freshlyFetched.Any())
            {
                feedState.LastPublishDate = freshlyFetched.Max(p => p!.PublishDate);
                feedState.ErrorCount = 0;

                foreach (var post in freshlyFetched)
                {
                    if (post is null)
                    {
                        _logger.LogWarning("Failed to parse a post from {Url}", url);
                        continue;
                    }

                    if (_postFilterService.ShouldIncludePost(post, url))
                    {
                        newPosts.Add(post);
                    }
                }
            }
            else
            {
                _logAggregator.AddLatestUrlPost(url, posts.OrderByDescending(p => p?.PublishDate).FirstOrDefault());
            }

        }
        private async Task<List<Post?>> FetchYoutubeAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                Post? post;

                if (IsDirectYoutubeFeedUrl(url))
                {
                    post = await _rssParsingService.ParseYoutubeFeedAsync(url);
                    return post == null ? new List<Post?>() : new List<Post?> { post };
                }

                var response = await _httpClient.GetAsyncWithFallback(url, cancellationToken);

                if (response is null)
                {
                    _logger.LogWarning("Failed to fetch YouTube feed from {Url}: No response returned.", url);
                    return new List<Post?>();
                }

                response!.EnsureSuccessStatusCode();

                var xmlContent = await GetResponseContentAsync(response, cancellationToken);

                post = await _rssParsingService.ParseYoutubeFeedAsync(xmlContent);

                return post == null ? new List<Post?>() : new List<Post?> { post };

            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(
                    "Failed to fetch or process the RSS feed from {Url}: Response Ended Prematurely - Skipping Url - Exception Message: {Ex}",
                    url, ex);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "An unexpected error occurred while checking the RSS feed from {Url} - Exception Message: {Ex}",
                    url, ex);
            }

            return new List<Post?>();
        }

        private static bool IsDirectYoutubeFeedUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (!uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (uri.AbsolutePath.Equals("/feeds/videos.xml", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var query = uri.Query;
            return query.Contains("channel_id=", StringComparison.OrdinalIgnoreCase)
                || query.Contains("playlist_id=", StringComparison.OrdinalIgnoreCase)
                || query.Contains("user=", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<List<Post?>> FetchRssAsync(string url, int trim, CancellationToken cancellationToken)
        {
            try
            {

                var response = await _httpClient.GetAsyncWithFallback(url, cancellationToken);

                if (response is null)
                {
                    _logger.LogWarning("Failed to fetch RSS feed from {Url}: No response returned.", url);
                    return new List<Post?>();
                }

                response.EnsureSuccessStatusCode();

                var xmlContent = await GetResponseContentAsync(response, cancellationToken);

                return await _rssParsingService.ParseRssFeedAsync(xmlContent, trim);

            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Failed to fetch or process the RSS feed from {Url}: {Ex}", url, SensitiveDataMasker.MaskException(ex));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("An unexpected error occurred while checking the RSS feed from {Url}: {Ex}", url,
                    SensitiveDataMasker.MaskException(ex));
            }
            finally
            {

            }

            return new List<Post?>();
        }
        private async Task<string> GetResponseContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.Content.Headers.ContentEncoding.Contains("gzip"))
            {
                await using var decompressedStream = new GZipStream(await response.Content.ReadAsStreamAsync(cancellationToken), CompressionMode.Decompress);
                using var reader = new StreamReader(decompressedStream, Encoding.UTF8);
                return await reader.ReadToEndAsync();
            }
            else
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                return EncodingExtractor.ConvertBytesByComparing(bytes, response.Content.Headers);
            }
        }

        private void HandleFeedError(string url, FeedState feedState, Exception ex)
        {
            feedState.ErrorCount++;
            _logger.LogError(ex, "Failed to fetch feed from {Url}. Error count: {ErrorCount}", url, feedState.ErrorCount);

            if (feedState.ErrorCount < 3 || !_config.EnableAutoRemove) return;

            _logger.LogWarning("Removing Url: {Url} after too many errors", url);
            var successRemove = _feedStates.TryRemove(url, out _);

            if (!successRemove)
            {
                _logger.LogWarning("Failed to remove Url: {Url}", url);
            }
        }


    }
}
