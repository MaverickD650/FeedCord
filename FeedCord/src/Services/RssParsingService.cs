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

    public async Task<List<Post?>> ParseRssFeedAsync(
      string xmlContent,
      int trim,
      ImageFetchMode imageFetchMode,
      CancellationToken cancellationToken = default)
    {
      var xmlContenter = xmlContent.Replace("<!doctype", "<!DOCTYPE");

      try
      {
        var feed = FeedReader.ReadFromString(xmlContenter);

        List<Post?> posts = new();

        foreach (var post in feed.Items)
        {
          cancellationToken.ThrowIfCancellationRequested();
          var rawXml = GetRawXmlForItem(post);

            var imageLink = await _imageParserService
              .TryExtractImageLink(post.Link ?? string.Empty, rawXml, imageFetchMode)
              ?? feed.ImageUrl;

          var builtPost = PostBuilder.TryBuildPost(post, feed, trim, imageLink);

          posts.Add(builtPost);
        }

        if (posts.Count == 0)
        {
          return new List<Post?>();
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
      return feedItem.SpecificItem switch
      {
        CodeHollow.FeedReader.Feeds.Rss20FeedItem rssItem => rssItem.Element?.ToString() ?? string.Empty,
        CodeHollow.FeedReader.Feeds.AtomFeedItem atomItem => atomItem.Element?.ToString() ?? string.Empty,
        _ => string.Empty
      };
    }

  }
}
