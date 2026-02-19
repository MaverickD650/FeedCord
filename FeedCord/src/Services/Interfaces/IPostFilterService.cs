using FeedCord.Common;

namespace FeedCord.Services.Interfaces
{
    public interface IPostFilterService
    {
        /// <summary>
        /// Determines whether a post should be included based on configured filters for the given feed URL.
        /// </summary>
        /// <param name="post">The post to evaluate</param>
        /// <param name="feedUrl">The URL of the feed the post came from</param>
        /// <returns>True if the post should be included, false if it should be filtered out</returns>
        bool ShouldIncludePost(Post post, string feedUrl);
    }
}
