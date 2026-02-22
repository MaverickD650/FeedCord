

namespace FeedCord.Services.Interfaces
{
  public interface ICustomHttpClient
  {
    Task<HttpResponseMessage?> GetAsyncWithFallback(string url, CancellationToken cancellationToken = default);
    Task PostAsyncWithFallback(string url, StringContent forumChannelContent, StringContent textChannelContent, bool isForum, CancellationToken cancellationToken = default);
  }
}
