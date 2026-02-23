using FeedCord.Common;

namespace FeedCord.Services.Interfaces
{
  public interface IRssParsingService
  {
    Task<List<Post?>> ParseRssFeedAsync(string xmlContent, int trim, ImageFetchMode imageFetchMode, DateTime? minPublishDate = null, CancellationToken cancellationToken = default);
    Task<Post?> ParseYoutubeFeedAsync(string channelUrl, CancellationToken cancellationToken = default);
  }
}
