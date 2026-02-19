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
            _lastRunReference = CsvReader.LoadReferencePosts("feed_dump.csv");
            _rssParsingService = rssParsingService;
            _logger = logger;
            _logAggregator = logAggregator;
            _postFilterService = postFilterService;
            _feedStates = new ConcurrentDictionary<string, FeedState>();
            _instancedConcurrentRequests = new SemaphoreSlim(config.ConcurrentRequests);
        }
        public async Task<List<Post>> CheckForNewPostsAsync()
        {
            ConcurrentBag<Post> allNewPosts = new();

            var tasks = _feedStates.Select(async (feed) =>
                await CheckSingleFeedAsync(feed.Key, feed.Value, allNewPosts, _config.DescriptionLimit));

            await Task.WhenAll(tasks);

            _logAggregator.SetNewPostCount(allNewPosts.Count);

            return allNewPosts.ToList();
        }
        public async Task InitializeUrlsAsync()
        {
            var id = _config.Id;
            var validRssUrls = _config.RssUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToArray();

            var validYoutubeUrls = _config.YoutubeUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToArray();

            var rssCount = await GetSuccessCount(validRssUrls, false);
            var youtubeCount = await GetSuccessCount(validYoutubeUrls, true);
            var successCount = rssCount + youtubeCount;

            var totalUrls = validRssUrls.Length + validYoutubeUrls.Length;

            _logger.LogInformation("{id}: Tested successfully for {UrlCount} out of {TotalUrls} Urls in Configuration File", id, successCount, totalUrls);
        }

        public IReadOnlyDictionary<string, FeedState> GetAllFeedData()
        {
            return _feedStates;
        }
        private async Task<int> GetSuccessCount(string[] urls, bool isYoutube)
        {
            var successCount = 0;

            if (urls.Length == 0 || urls.Length == 1 && string.IsNullOrEmpty(urls[0]))
            {
                return successCount;
            }

            foreach (var url in urls)
            {
                var isSuccess = await TestUrlAsync(url);

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
                    var posts = await FetchYoutubeAsync(url);
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
                    var posts = await FetchRssAsync(url, _config.DescriptionLimit);
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
        private async Task<bool> TestUrlAsync(string url)
        {
            try
            {
                await _instancedConcurrentRequests.WaitAsync();

                var response = await _httpClient.GetAsyncWithFallback(url);

                if (response is null)
                {
                    _logAggregator.AddUrlResponse(url, -99);
                    return false;
                }

                _logAggregator.AddUrlResponse(url, (int)response.StatusCode);

                response.EnsureSuccessStatusCode();

                return true;
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
                _instancedConcurrentRequests.Release();
            }

            return false;
        }
        private async Task CheckSingleFeedAsync(string url, FeedState feedState, ConcurrentBag<Post> newPosts, int trim)
        {
            List<Post?> posts;

            try
            {
                await _instancedConcurrentRequests.WaitAsync();

                posts = feedState.IsYoutube ?
                    await FetchYoutubeAsync(url) :
                    await FetchRssAsync(url, trim);
            }
            catch (Exception ex)
            {
                HandleFeedError(url, feedState, ex);
                return;
            }
            finally
            {
                _instancedConcurrentRequests.Release();
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
        private async Task<List<Post?>> FetchYoutubeAsync(string url)
        {
            try
            {
                Post? post;

                //TODO --> BETTER HANDLING - TEMP FIX FOR INSERTING XML LINKS IN TO YOUTUBE - WE SKIP PARSING HTML
                if (url.Contains("xml"))
                {

                    post = await _rssParsingService.ParseYoutubeFeedAsync(url);

                    return post == null ? new List<Post?>() : new List<Post?> { post };
                }

                var response = await _httpClient.GetAsyncWithFallback(url);

                if (response is null)
                {
                    throw new Exception();
                }

                response!.EnsureSuccessStatusCode();

                var xmlContent = await GetResponseContentAsync(response);

                post = await _rssParsingService.ParseYoutubeFeedAsync(xmlContent);

                return post == null ? new List<Post?>() : new List<Post?> { post };

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

        private async Task<List<Post?>> FetchRssAsync(string url, int trim)
        {
            try
            {

                var response = await _httpClient.GetAsyncWithFallback(url);

                if (response is null)
                {
                    throw new Exception();
                }

                response.EnsureSuccessStatusCode();

                var xmlContent = await GetResponseContentAsync(response);

                return await _rssParsingService.ParseRssFeedAsync(xmlContent, trim);

            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Failed to fetch or process the RSS feed from {Url}: {Ex}", url, ex);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("An unexpected error occurred while checking the RSS feed from {Url}: {Ex}", url,
                    ex);
            }
            finally
            {

            }

            return new List<Post?>();
        }
        private async Task<string> GetResponseContentAsync(HttpResponseMessage response)
        {
            if (response.Content.Headers.ContentEncoding.Contains("gzip"))
            {
                await using var decompressedStream = new GZipStream(await response.Content.ReadAsStreamAsync(), CompressionMode.Decompress);
                using var reader = new StreamReader(decompressedStream, Encoding.UTF8);
                return await reader.ReadToEndAsync();
            }
            else
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
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
