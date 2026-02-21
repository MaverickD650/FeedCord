using FeedCord.Common;

namespace FeedCord.Services.Interfaces
{
    public interface IReferencePostStore
    {
        Dictionary<string, ReferencePost> LoadReferencePosts();
        void SaveReferencePosts(IReadOnlyDictionary<string, FeedState> data);
    }
}
