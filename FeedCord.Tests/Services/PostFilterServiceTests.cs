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
    _mockLogger = new Mock<ILogger<PostFilterService>>(MockBehavior.Loose);
  }

  public static IEnumerable<object?[]> NoFilterConfigurations()
  {
    yield return new object?[] { null };
    yield return new object?[] { new List<PostFilters>() };
  }

  [Theory]
  [MemberData(nameof(NoFilterConfigurations))]
  public void ShouldIncludePost_NoActiveFilters_AlwaysReturnsTrue(List<PostFilters>? postFilters)
  {
    var service = new PostFilterService(_mockLogger.Object, CreateMinimalConfig(postFilters));

    var result = service.ShouldIncludePost(CreateTestPost("Any title"), "http://example.com/rss");

    Assert.True(result);
  }

  [Fact]
  public void ShouldIncludePost_UrlSpecificFilter_MatchingPost_ReturnsTrue()
  {
    var filters = new List<PostFilters>
        {
            new() { Url = "http://example.com/rss", Filters = new[] { "breaking" } }
        };
    var service = new PostFilterService(_mockLogger.Object, CreateMinimalConfig(filters));

    var result = service.ShouldIncludePost(CreateTestPost("Breaking: update"), "http://example.com/rss");

    Assert.True(result);
  }

  [Fact]
  public void ShouldIncludePost_UrlSpecificFilter_NonMatchingPost_ReturnsFalse()
  {
    var filters = new List<PostFilters>
        {
            new() { Url = "http://example.com/rss", Filters = new[] { "breaking" } }
        };
    var service = new PostFilterService(_mockLogger.Object, CreateMinimalConfig(filters));

    var result = service.ShouldIncludePost(CreateTestPost("Daily digest"), "http://example.com/rss");

    Assert.False(result);
  }

  [Fact]
  public void ShouldIncludePost_NoUrlSpecificFilter_AllFilterMatches_ReturnsTrue()
  {
    var filters = new List<PostFilters>
        {
            new() { Url = "all", Filters = new[] { "important" } }
        };
    var service = new PostFilterService(_mockLogger.Object, CreateMinimalConfig(filters));

    var result = service.ShouldIncludePost(CreateTestPost("Important release notes"), "http://unmatched.com/rss");

    Assert.True(result);
  }

  [Fact]
  public void ShouldIncludePost_NoUrlSpecificFilter_AllFilterDoesNotMatch_ReturnsFalse()
  {
    var filters = new List<PostFilters>
        {
            new() { Url = "all", Filters = new[] { "important" } }
        };
    var service = new PostFilterService(_mockLogger.Object, CreateMinimalConfig(filters));

    var result = service.ShouldIncludePost(CreateTestPost("General post"), "http://unmatched.com/rss");

    Assert.False(result);
  }

  [Fact]
  public void ShouldIncludePost_UrlSpecificFilter_DoesNotFallBackToAllFilter()
  {
    var filters = new List<PostFilters>
        {
            new() { Url = "http://example.com/rss", Filters = new[] { "specific" } },
            new() { Url = "all", Filters = new[] { "global" } }
        };
    var service = new PostFilterService(_mockLogger.Object, CreateMinimalConfig(filters));

    var result = service.ShouldIncludePost(CreateTestPost("Global announcement"), "http://example.com/rss");

    Assert.False(result);
  }

  [Fact]
  public void ShouldIncludePost_NoMatchingSpecificAndNoAllFilter_ReturnsTrue()
  {
    var filters = new List<PostFilters>
        {
            new() { Url = "http://other.com/rss", Filters = new[] { "breaking" } }
        };
    var service = new PostFilterService(_mockLogger.Object, CreateMinimalConfig(filters));

    var result = service.ShouldIncludePost(CreateTestPost("Any title"), "http://example.com/rss");

    Assert.True(result);
  }

  [Fact]
  public void ShouldIncludePost_UrlSpecificLabelFilter_MatchingLabel_ReturnsTrue()
  {
    var filters = new List<PostFilters>
        {
            new() { Url = "http://example.com/rss", Filters = new[] { "label:breaking" } }
        };
    var service = new PostFilterService(_mockLogger.Object, CreateMinimalConfig(filters));

    var labeledPost = new Post(
        Title: "Daily digest",
        ImageUrl: "http://example.com/image.jpg",
        Description: "Summary",
        Link: "http://example.com/post",
        Tag: "news",
        PublishDate: DateTime.Now,
        Author: "Author",
        Labels: new[] { "breaking", "release" }
    );

    var result = service.ShouldIncludePost(labeledPost, "http://example.com/rss");

    Assert.True(result);
  }

  [Fact]
  public void ShouldIncludePost_AllFilterRemovedAfterInitialization_ReturnsTrueWhenNoApplicableFiltersRemain()
  {
    var filters = new List<PostFilters>
        {
            new() { Url = "all", Filters = new[] { "important" } }
        };

    var service = new PostFilterService(_mockLogger.Object, CreateMinimalConfig(filters));

    filters.Clear();

    var result = service.ShouldIncludePost(CreateTestPost("General post"), "http://unmatched.com/rss");

    Assert.True(result);
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
      RssCheckIntervalSeconds = 30,
      DescriptionLimit = 250,
      Forum = false,
      MarkdownFormat = false,
      PersistenceOnShutdown = false,
      PostFilters = postFilters
    };
  }

  private static Post CreateTestPost(string title)
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
