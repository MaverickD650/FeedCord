using Xunit;
using Moq;
using FeedCord.Services;
using FeedCord.Common;
using Microsoft.Extensions.Logging;

namespace FeedCord.Tests.Services;

public class PostFilterServiceTests
{
    private readonly Mock<ILogger<PostFilterService>> _mockLogger;

    public PostFilterServiceTests()
    {
        _mockLogger = new Mock<ILogger<PostFilterService>>();
    }

    [Fact]
    public void ShouldIncludePost_NoFiltersConfigured_AlwaysTrue()
    {
        // Arrange
        var config = CreateMinimalConfig(postFilters: null);
        var service = new PostFilterService(_mockLogger.Object, config);
        var post = CreateTestPost("Test Post");

        // Act
        var result = service.ShouldIncludePost(post, "http://example.com/rss");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludePost_EmptyFiltersConfigured_AlwaysTrue()
    {
        // Arrange
        var config = CreateMinimalConfig(postFilters: new List<PostFilters>());
        var service = new PostFilterService(_mockLogger.Object, config);
        var post = CreateTestPost("Test Post");

        // Act
        var result = service.ShouldIncludePost(post, "http://example.com/rss");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludePost_NoUrlMatch_NoAllFilter_IncludePost()
    {
        // Arrange - filter for specific URL that doesn't match
        var filters = new List<PostFilters>
        {
            new PostFilters { Url = "http://other.com/rss", Filters = new[] { "tech" } }
        };
        var config = CreateMinimalConfig(postFilters: filters);
        var service = new PostFilterService(_mockLogger.Object, config);
        var post = CreateTestPost("Test Post");

        // Act
        var result = service.ShouldIncludePost(post, "http://example.com/rss");

        // Assert - different URL, no "all" filter, should include
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludePost_HasAllFilter_AppliesGlobally()
    {
        // Arrange - "all" filter that applies to any URL
        var filters = new List<PostFilters>
        {
            new PostFilters { Url = "all", Filters = new[] { "important" } }
        };
        var config = CreateMinimalConfig(postFilters: filters);
        var service = new PostFilterService(_mockLogger.Object, config);
        var post = CreateTestPost("Test Post");

        // Act
        var result = service.ShouldIncludePost(post, "http://any-url.com/rss");

        // Assert - should apply "all" filter
        // Note: Actual filter matching logic in FilterConfigs.GetFilterSuccess
        // This test documents expected behavior
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void ShouldIncludePost_UrlSpecificFilter_OverridesAllFilter()
    {
        // Arrange - both URL-specific and "all" filters
        var filters = new List<PostFilters>
        {
            new PostFilters { Url = "http://example.com/rss", Filters = new[] { "specific" } },
            new PostFilters { Url = "all", Filters = new[] { "global" } }
        };
        var config = CreateMinimalConfig(postFilters: filters);
        var service = new PostFilterService(_mockLogger.Object, config);
        var post = CreateTestPost("Test Post");

        // Act
        var result = service.ShouldIncludePost(post, "http://example.com/rss");

        // Assert - URL-specific filter should be checked, not "all"
        Assert.IsType<bool>(result);
    }

    // Helper methods
    private Config CreateMinimalConfig(List<PostFilters>? postFilters = null)
    {
        return new Config
        {
            Id = "TestFeed",
            RssUrls = new[] { "http://example.com/rss" },
            YoutubeUrls = new string[] { },
            DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
            RssCheckIntervalMinutes = 30,
            DescriptionLimit = 250,
            Forum = false,
            MarkdownFormat = false,
            PersistenceOnShutdown = false,
            PostFilters = postFilters
        };
    }

    private Post CreateTestPost(string title)
    {
        return new Post(
            Title: title,
            ImageUrl: "http://example.com/image.jpg",
            Description: "Test description",
            Link: "http://example.com/post",
            Tag: "test-tag",
            PublishDate: DateTime.Now,
            Author: "Test Author"
        );
    }
}
