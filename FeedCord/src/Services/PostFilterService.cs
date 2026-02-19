using FeedCord.Common;
using FeedCord.Services.Helpers;
using FeedCord.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FeedCord.Services
{
    public class PostFilterService : IPostFilterService
    {
        private readonly ILogger<PostFilterService> _logger;
        private readonly List<PostFilters>? _postFilters;
        private readonly bool _hasFilterEnabled;
        private readonly bool _hasAllFilter;

        public PostFilterService(ILogger<PostFilterService> logger, Config config)
        {
            _logger = logger;
            _postFilters = config.PostFilters;
            _hasFilterEnabled = config.PostFilters?.Any() ?? false;

            // Check if there's an "all" filter
            _hasAllFilter = _hasFilterEnabled && _postFilters != null &&
                            _postFilters.Any(wf => wf.Url == "all");
        }

        public bool ShouldIncludePost(Post post, string feedUrl)
        {
            // If no filters are configured, include all posts
            if (!_hasFilterEnabled || _postFilters == null)
            {
                return true;
            }

            // Try to find a URL-specific filter
            var filter = _postFilters.FirstOrDefault(wf => wf.Url == feedUrl);

            if (filter != null)
            {
                // URL has specific filters - check if post matches
                var filterFound = FilterConfigs.GetFilterSuccess(post, filter.Filters.ToArray());

                if (filterFound)
                {
                    return true;
                }
                else
                {
                    _logger.LogInformation(
                        "A new post was omitted because it does not comply to the set filter: {Url}", feedUrl);
                    return false;
                }
            }

            // No URL-specific filter - check if there's an "all" filter
            if (_hasAllFilter)
            {
                var allFilter = _postFilters.FirstOrDefault(wf => wf.Url == "all");
                if (allFilter != null)
                {
                    var filterFound = FilterConfigs.GetFilterSuccess(post, allFilter.Filters.ToArray());

                    if (filterFound)
                    {
                        return true;
                    }
                    else
                    {
                        _logger.LogInformation(
                            "A new post was omitted because it does not comply to the set filter: {Url}", feedUrl);
                        return false;
                    }
                }
            }

            // No filters apply to this URL - include it
            return true;
        }
    }
}
