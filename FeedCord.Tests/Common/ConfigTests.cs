using Xunit;
using System.ComponentModel.DataAnnotations;
using FeedCord.Common;

namespace FeedCord.Tests.Common;

public class ConfigTests
{
  [Fact]
  public void Config_AllRequiredPropertiesSet_IsValid()
  {
    // Arrange
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new[] { "http://example.com/rss" },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalMinutes = 30,
      DescriptionLimit = 250,
      Forum = false,
      MarkdownFormat = false,
      PersistenceOnShutdown = true
    };

    // Act
    var results = ValidateModel(config);

    // Assert
    Assert.Empty(results);
  }

  [Fact]
  public void Config_IdRequired_FailsValidationWhenMissing()
  {
    // Arrange
    var config = new Config
    {
      Id = "",  // Empty is invalid
      RssUrls = new[] { "http://example.com/rss" },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalMinutes = 30,
      DescriptionLimit = 250,
      Forum = false,
      MarkdownFormat = false,
      PersistenceOnShutdown = false
    };

    // Act
    var results = ValidateModel(config);

    // Assert
    Assert.NotEmpty(results);
    Assert.Contains(results, r => r.MemberNames.Contains("Id"));
  }

  [Fact]
  public void Config_RssUrlsRequired_CanBeEmpty()
  {
    // Arrange
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },  // Empty array is allowed
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalMinutes = 30,
      DescriptionLimit = 250,
      Forum = false,
      MarkdownFormat = false,
      PersistenceOnShutdown = false
    };

    // Act
    var results = ValidateModel(config);

    // Assert - should not fail on empty RssUrls
    var rssUrlErrors = results.Where(r => r.MemberNames.Contains("RssUrls")).ToList();
    Assert.Empty(rssUrlErrors);
  }

  [Fact]
  public void Config_YoutubeUrlsRequired_CanBeEmpty()
  {
    // Arrange
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new[] { "http://example.com/rss" },
      YoutubeUrls = new string[] { },  // Empty array is allowed
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalMinutes = 30,
      DescriptionLimit = 250,
      Forum = false,
      MarkdownFormat = false,
      PersistenceOnShutdown = false
    };

    // Act
    var results = ValidateModel(config);

    // Assert - should not fail on empty YoutubeUrls
    var youtubeErrors = results.Where(r => r.MemberNames.Contains("YoutubeUrls")).ToList();
    Assert.Empty(youtubeErrors);
  }

  [Fact]
  public void Config_DiscordWebhookUrlRequired()
  {
    // Arrange
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new[] { "http://example.com/rss" },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "",  // Empty is invalid
      RssCheckIntervalMinutes = 30,
      DescriptionLimit = 250,
      Forum = false,
      MarkdownFormat = false,
      PersistenceOnShutdown = false
    };

    // Act
    var results = ValidateModel(config);

    // Assert
    Assert.NotEmpty(results);
    Assert.Contains(results, r => r.MemberNames.Contains("DiscordWebhookUrl"));
  }

  [Fact]
  public void Config_ConcurrentRequestsDefaultsToFive()
  {
    // Arrange
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalMinutes = 30,
      DescriptionLimit = 250,
      Forum = false,
      MarkdownFormat = false,
      PersistenceOnShutdown = false
      // ConcurrentRequests not explicitly set
    };

    // Assert - default value should be 5
    Assert.Equal(5, config.ConcurrentRequests);
  }

  [Fact]
  public void Config_ConcurrentRequestsCanBeConfigured()
  {
    // Arrange
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalMinutes = 30,
      DescriptionLimit = 250,
      Forum = false,
      MarkdownFormat = false,
      PersistenceOnShutdown = false,
      ConcurrentRequests = 10
    };

    // Assert
    Assert.Equal(10, config.ConcurrentRequests);
  }

  [Fact]
  public void Config_ColorPreservesIntValue()
  {
    // Arrange
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalMinutes = 30,
      DescriptionLimit = 250,
      Forum = false,
      MarkdownFormat = false,
      PersistenceOnShutdown = false,
      Color = 8411391
    };

    // Assert - Color should be stored as-is
    Assert.Equal(8411391, config.Color);
  }

  [Fact]
  public void Config_AllPropertiesOptionalExceptRequired()
  {
    // Arrange - minimal valid config
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalMinutes = 30,
      DescriptionLimit = 250,
      Forum = false,
      MarkdownFormat = false,
      PersistenceOnShutdown = false
      // No optional properties set
    };

    // Act
    var results = ValidateModel(config);

    // Assert - should be valid with no optional properties
    Assert.Empty(results);
    Assert.Null(config.Username);
    Assert.Null(config.AvatarUrl);
    Assert.Null(config.AuthorIcon);
    Assert.Null(config.AuthorName);
    Assert.Null(config.AuthorUrl);
    Assert.Null(config.FallbackImage);
    Assert.Null(config.FooterImage);
    Assert.Null(config.PostFilters);
    Assert.Null(config.Pings);
  }

  // Helper method
  private static List<ValidationResult> ValidateModel(object model)
  {
    var results = new List<ValidationResult>();
    var context = new ValidationContext(model, null, null);
    Validator.TryValidateObject(model, context, results, true);
    return results;
  }
}
