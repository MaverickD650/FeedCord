using Microsoft.Extensions.Logging;
using CodeHollow.FeedReader;
using FeedCord.Common;
using FeedCord.Services.Helpers;
using FeedCord.Services.Interfaces;
using FeedCord.Helpers;

namespace FeedCord.Services
{
    public class RssParsingService : IRssParsingService
    {
        private readonly ILogger<RssParsingService> _logger;
        private readonly IYoutubeParsingService _youtubeParsingService;
        private readonly IImageParserService _imageParserService;

        public RssParsingService(
            ILogger<RssParsingService> logger,
            IYoutubeParsingService youtubeParsingService,
            IImageParserService imageParserService)
        {
            _logger = logger;
            _youtubeParsingService = youtubeParsingService;
            _imageParserService = imageParserService;
        }

        public async Task<List<Post?>> ParseRssFeedAsync(string xmlContent, int trim, CancellationToken cancellationToken = default)
        {
            var xmlContenter = xmlContent.Replace("<!doctype", "<!DOCTYPE");

            try
            {
                var feed = FeedReader.ReadFromString(xmlContenter);

                var latestPost = feed.Items.FirstOrDefault();

                if (latestPost is null)
                    return new List<Post?>();

                var feedItems = feed.Items.ToList();

                List<Post?> posts = new();

                foreach (var post in feedItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var rawXml = GetRawXmlForItem(post);

                    // Temporary, don't extract images from post url
                    var imageLink = feed.ImageUrl;

                    // var imageLink = await _imageParserService
                    //     .TryExtractImageLink(post.Link, rawXml, cancellationToken)
                    //                 ?? feed.ImageUrl;

                    var builtPost = PostBuilder.TryBuildPost(post, feed, trim, imageLink);

                    posts.Add(builtPost);
                }

                return posts;

            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("An unexpected error occurred while parsing the RSS feed: {Ex}", SensitiveDataMasker.MaskException(ex));
                return new List<Post?>();
            }
        }

        public async Task<Post?> ParseYoutubeFeedAsync(string channelUrl, CancellationToken cancellationToken = default)
        {
            var youtubePost = await _youtubeParsingService.GetXmlUrlAndFeed(channelUrl, cancellationToken);

            if (youtubePost is null)
                _logger.LogWarning("Failed to parse Youtube Feed from url: {ChannelUrl} - Try directly feeding the xml formatted Url, otherwise could be a malformed feed", channelUrl);

            return youtubePost;
        }

        private string GetRawXmlForItem(FeedItem feedItem)
        {
            if (feedItem.SpecificItem is CodeHollow.FeedReader.Feeds.Rss20FeedItem rssItem)
            {
                return rssItem.Element?.ToString() ?? "";
            }
            else if (feedItem.SpecificItem is CodeHollow.FeedReader.Feeds.AtomFeedItem atomItem)
            {
                return atomItem.Element?.ToString() ?? "";
            }

            return "";
        }

    }
}
