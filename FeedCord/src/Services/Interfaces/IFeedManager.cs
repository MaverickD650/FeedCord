using FeedCord.Common;

namespace FeedCord.Services.Interfaces
{
    public interface IFeedManager
    {
        Task<List<Post>> CheckForNewPostsAsync(CancellationToken cancellationToken = default);
        Task InitializeUrlsAsync(CancellationToken cancellationToken = default);
        IReadOnlyDictionary<string, FeedState> GetAllFeedData();
    }
}
