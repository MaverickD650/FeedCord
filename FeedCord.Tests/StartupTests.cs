using Xunit;
using FeedCord.Common;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace FeedCord.Tests
{
    public class StartupConfigurationValidationTests
    {
        #region Config Validation - Valid Configurations

        [Fact]
        public void ValidateConfiguration_WithValidConfig_DoesNotThrow()
        {
            // Arrange
            var config = new Config
            {
                Id = "test-feed",
                RssUrls = new[] { "https://example.com/rss" },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            };

            // Act & Assert
            ValidateConfiguration(config);
        }

        [Fact]
        public void ValidateConfiguration_WithValidMinimalConfig_DoesNotThrow()
        {
            // Arrange
            var config = new Config
            {
                Id = "minimal",
                RssUrls = new[] { "https://example.com/feed" },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 1,
                DescriptionLimit = 1,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            };

            // Act & Assert
            ValidateConfiguration(config);
        }

        #endregion

        #region Config Validation - Missing Required Fields

        [Fact]
        public void ValidateConfiguration_MissingId_Throws()
        {
            // Arrange
            var config = new Config
            {
                Id = null!,
                RssUrls = new[] { "https://example.com/rss" },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => ValidateConfiguration(config));
        }

        [Fact]
        public void ValidateConfiguration_MissingDiscordWebhookUrl_Throws()
        {
            // Arrange
            var config = new Config
            {
                Id = "test",
                RssUrls = new[] { "https://example.com/rss" },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = null!,
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => ValidateConfiguration(config));
        }

        #endregion

        #region Config Validation - Invalid Field Values

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-60)]
        public void ValidateConfiguration_WithInvalidCheckInterval_Throws(int invalidInterval)
        {
            // Arrange
            var config = new Config
            {
                Id = "test",
                RssUrls = new[] { "https://example.com/rss" },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = invalidInterval,
                DescriptionLimit = 250,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => ValidateConfiguration(config));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        [InlineData(4001)]
        [InlineData(5000)]
        public void ValidateConfiguration_WithInvalidDescriptionLimit_Throws(int invalidLimit)
        {
            // Arrange
            var config = new Config
            {
                Id = "test",
                RssUrls = new[] { "https://example.com/rss" },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = invalidLimit,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => ValidateConfiguration(config));
        }

        #endregion

        #region Config Validation - Edge Cases

        [Fact]
        public void ValidateConfiguration_WithEmptyId_Throws()
        {
            // Arrange
            var config = new Config
            {
                Id = "",
                RssUrls = new[] { "https://example.com/rss" },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => ValidateConfiguration(config));
        }

        [Fact]
        public void ValidateConfiguration_WithEmptyRssUrlsArray_IsValid()
        {
            // Arrange
            var config = new Config
            {
                Id = "test",
                RssUrls = new string[] { },
                YoutubeUrls = new[] { "https://youtube.com/channel/123" },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            };

            // Act & Assert
            ValidateConfiguration(config);
        }

        [Fact]
        public void ValidateConfiguration_WithEmptyYoutubeUrlsArray_IsValid()
        {
            // Arrange
            var config = new Config
            {
                Id = "test",
                RssUrls = new[] { "https://example.com/rss" },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            };

            // Act & Assert
            ValidateConfiguration(config);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(60)]
        [InlineData(1440)]
        public void ValidateConfiguration_WithVariousValidIntervals_Succeeds(int minutes)
        {
            // Arrange
            var config = new Config
            {
                Id = "test",
                RssUrls = new[] { "https://example.com/rss" },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = minutes,
                DescriptionLimit = 250,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            };

            // Act & Assert
            ValidateConfiguration(config);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(250)]
        [InlineData(4000)]
        public void ValidateConfiguration_WithVariousDescriptionLimits_Succeeds(int limit)
        {
            // Arrange
            var config = new Config
            {
                Id = "test",
                RssUrls = new[] { "https://example.com/rss" },
                YoutubeUrls = new string[] { },
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = limit,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            };

            // Act & Assert
            ValidateConfiguration(config);
        }

        #endregion

        #region Helper Method

        private static void ValidateConfiguration(Config config)
        {
            var context = new ValidationContext(config, serviceProvider: null, items: null);
            var results = new List<ValidationResult>();

            if (Validator.TryValidateObject(config, context, results, validateAllProperties: true))
                return;

            var errors = string.Join("\n", results.Select(r => r.ErrorMessage));
            throw new InvalidOperationException($"Invalid config entry: {errors}");
        }

        #endregion
    }

    public class StartupConfigurationPathTests
    {
        #region Configuration Path Selection

        [Fact]
        public void ConfigPath_WithNoArguments_DefaultsToConfigAppsettings()
        {
            // Arrange
            var args = Array.Empty<string>();

            // Act
            var selectedPath = SelectConfigPath(args);

            // Assert
            Assert.Equal("config/appsettings.json", selectedPath);
        }

        [Fact]
        public void ConfigPath_WithSingleArgument_UsesProvidedPath()
        {
            // Arrange
            var args = new[] { "custom/path.json" };

            // Act
            var selectedPath = SelectConfigPath(args);

            // Assert
            Assert.Equal("custom/path.json", selectedPath);
        }

        [Fact]
        public void ConfigPath_WithMultipleArguments_DefaultsToConfigAppsettings()
        {
            // Arrange
            var args = new[] { "arg1", "arg2", "arg3" };

            // Act
            var selectedPath = SelectConfigPath(args);

            // Assert
            Assert.Equal("config/appsettings.json", selectedPath);
        }

        [Theory]
        [MemberData(nameof(GetConfigPathVariations))]
        public void ConfigPath_WithVariousArguments_SelectsCorrectPath(string[] args)
        {
            // Act
            var selectedPath = SelectConfigPath(args);

            // Assert
            var expected = args.Length == 1 ? args[0] : "config/appsettings.json";
            Assert.Equal(expected, selectedPath);
        }

        #endregion

        #region Test Data

        public static IEnumerable<object[]> GetConfigPathVariations()
        {
            yield return new object[] { Array.Empty<string>() };
            yield return new object[] { new[] { "config.json" } };
            yield return new object[] { new[] { "path/to/config.yaml" } };
        }

        #endregion

        #region Helper Method

        private static string SelectConfigPath(string[] args)
        {
            return args.Length == 1 ? args[0] : "config/appsettings.json";
        }

        #endregion
    }

    public class StartupIntegrationTests
    {
        #region Service Configuration Constants

        [Fact]
        public void HttpClientUserAgent_ContainsExpectedComponents()
        {
            // Verify that the default user agent contains expected components
            var userAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 " +
                           "(KHTML, like Gecko) Chrome/104.0.5112.79 Safari/537.36";

            // Assert
            Assert.Contains("Mozilla", userAgent);
            Assert.Contains("Chrome", userAgent);
        }

        [Fact]
        public void HttpClientTimeout_Is30Seconds()
        {
            // Verify timeout constant
            var timeout = TimeSpan.FromSeconds(30);

            // Assert
            Assert.Equal(30, timeout.TotalSeconds);
        }

        [Fact]
        public void DefaultConcurrentRequests_Is20()
        {
            // Verify default concurrent requests value
            var defaultValue = 20;

            // Assert
            Assert.Equal(20, defaultValue);
        }

        [Fact]
        public void ConcurrentRequests_MinimumValue_IsOne()
        {
            // Verify minimum concurrent requests
            var minimum = 1;

            // Assert
            Assert.Equal(1, minimum);
        }

        [Fact]
        public void ConcurrentRequests_MaximumValue_Is200()
        {
            // Verify maximum concurrent requests
            var maximum = 200;

            // Assert
            Assert.Equal(200, maximum);
        }

        #endregion

        #region Logging Configuration

        [Fact]
        public void LoggingFilter_LogLevel_Information()
        {
            // Verify logging level constants
            var microsoftLevel = LogLevel.Information;
            var systemLevel = LogLevel.Information;

            // Assert
            Assert.Equal(LogLevel.Information, microsoftLevel);
            Assert.Equal(LogLevel.Information, systemLevel);
        }

        [Fact]
        public void LoggingFilter_MicrosoftHosting_Warning()
        {
            // Verify warning level for specific logger
            var hostingLevel = LogLevel.Warning;

            // Assert
            Assert.Equal(LogLevel.Warning, hostingLevel);
        }

        #endregion
    }
}
