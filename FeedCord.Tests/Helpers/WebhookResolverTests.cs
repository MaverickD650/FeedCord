using Xunit;
using FeedCord.Helpers;
using FeedCord.Common;

namespace FeedCord.Tests.Helpers;

public class WebhookResolverTests
{
    [Fact]
    public void ResolveWebhooks_DirectWebhookUrl_NoResolutionNeeded()
    {
        // Arrange
        var configs = new List<Config>
        {
            new Config
            {
                Id = "test1",
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                RssUrls = new string[] { },
                YoutubeUrls = new string[] { },
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            }
        };
        var loggedMessages = new List<string>();

        // Act
        WebhookResolver.ResolveWebhooks(configs, msg => loggedMessages.Add(msg));

        // Assert
        Assert.Equal("https://discord.com/api/webhooks/123/abc", configs[0].DiscordWebhookUrl);
        Assert.Empty(loggedMessages);  // No environment variable resolution
    }

    [Fact]
    public void ResolveWebhooks_EnvPrefixedUrl_ResolvesFromEnvironmentVariable()
    {
        // Arrange
        const string envVarName = "TEST_WEBHOOK_URL_FOR_RESOLUTION_123";
        const string expectedUrl = "https://discord.com/api/webhooks/999/xyz";

        Environment.SetEnvironmentVariable(envVarName, expectedUrl);
        try
        {
            var configs = new List<Config>
            {
                new Config
                {
                    Id = "test1",
                    DiscordWebhookUrl = $"env:{envVarName}",
                    RssUrls = new string[] { },
                    YoutubeUrls = new string[] { },
                    RssCheckIntervalMinutes = 30,
                    DescriptionLimit = 250,
                    Forum = false,
                    MarkdownFormat = false,
                    PersistenceOnShutdown = false
                }
            };
            var loggedMessages = new List<string>();

            // Act
            WebhookResolver.ResolveWebhooks(configs, msg => loggedMessages.Add(msg));

            // Assert
            Assert.Equal(expectedUrl, configs[0].DiscordWebhookUrl);
            Assert.Single(loggedMessages);
            Assert.Contains(envVarName, loggedMessages[0]);
            Assert.Contains("test1", loggedMessages[0]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void ResolveWebhooks_EnvPrefixWithWhitespace_ResolvesCorrectly()
    {
        // Arrange
        const string envVarName = "TEST_WEBHOOK_WITH_SPACES_123";
        const string expectedUrl = "https://discord.com/api/webhooks/888/def";

        Environment.SetEnvironmentVariable(envVarName, expectedUrl);
        try
        {
            var configs = new List<Config>
            {
                new Config
                {
                    Id = "test1",
                    DiscordWebhookUrl = $"env:  {envVarName}  ",  // Extra whitespace
                    RssUrls = new string[] { },
                    YoutubeUrls = new string[] { },
                    RssCheckIntervalMinutes = 30,
                    DescriptionLimit = 250,
                    Forum = false,
                    MarkdownFormat = false,
                    PersistenceOnShutdown = false
                }
            };

            // Act
            WebhookResolver.ResolveWebhooks(configs);

            // Assert
            Assert.Equal(expectedUrl, configs[0].DiscordWebhookUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void ResolveWebhooks_EnvPrefixCaseInsensitive_ResolvesCorrectly()
    {
        // Arrange
        const string envVarName = "TEST_WEBHOOK_CASE_INSENSITIVE_123";
        const string expectedUrl = "https://discord.com/api/webhooks/777/ghi";

        Environment.SetEnvironmentVariable(envVarName, expectedUrl);
        try
        {
            var configs = new List<Config>
            {
                new Config
                {
                    Id = "test1",
                    DiscordWebhookUrl = $"ENV:{envVarName}",  // Uppercase ENV
                    RssUrls = new string[] { },
                    YoutubeUrls = new string[] { },
                    RssCheckIntervalMinutes = 30,
                    DescriptionLimit = 250,
                    Forum = false,
                    MarkdownFormat = false,
                    PersistenceOnShutdown = false
                }
            };

            // Act
            WebhookResolver.ResolveWebhooks(configs);

            // Assert
            Assert.Equal(expectedUrl, configs[0].DiscordWebhookUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void ResolveWebhooks_EmptyEnvironmentVariable_ThrowsInvalidOperationException()
    {
        // Arrange
        const string envVarName = "TEST_WEBHOOK_EMPTY_123";
        Environment.SetEnvironmentVariable(envVarName, "");  // Empty but set
        try
        {
            var configs = new List<Config>
            {
                new Config
                {
                    Id = "test1",
                    DiscordWebhookUrl = $"env:{envVarName}",
                    RssUrls = new string[] { },
                    YoutubeUrls = new string[] { },
                    RssCheckIntervalMinutes = 30,
                    DescriptionLimit = 250,
                    Forum = false,
                    MarkdownFormat = false,
                    PersistenceOnShutdown = false
                }
            };

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                WebhookResolver.ResolveWebhooks(configs));
            Assert.Contains(envVarName, ex.Message);
            Assert.Contains("not set or is empty", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void ResolveWebhooks_UndefinedEnvironmentVariable_ThrowsInvalidOperationException()
    {
        // Arrange
        const string undefinedVar = "UNDEFINED_WEBHOOK_VAR_12345678";
        Environment.SetEnvironmentVariable(undefinedVar, null);  // Ensure it doesn't exist

        var configs = new List<Config>
        {
            new Config
            {
                Id = "test1",
                DiscordWebhookUrl = $"env:{undefinedVar}",
                RssUrls = new string[] { },
                YoutubeUrls = new string[] { },
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            }
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WebhookResolver.ResolveWebhooks(configs));
        Assert.Contains(undefinedVar, ex.Message);
        Assert.Contains("not set or is empty", ex.Message);
    }

    [Fact]
    public void ResolveWebhooks_MalformedEnvPrefix_ThrowsInvalidOperationException()
    {
        // Arrange
        var configs = new List<Config>
        {
            new Config
            {
                Id = "test1",
                DiscordWebhookUrl = "env:",  // No variable name
                RssUrls = new string[] { },
                YoutubeUrls = new string[] { },
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            }
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WebhookResolver.ResolveWebhooks(configs));
        Assert.Contains("malformed webhook reference", ex.Message);
        Assert.Contains("env:", ex.Message);
    }

    [Fact]
    public void ResolveWebhooks_MissingWebhookUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var configs = new List<Config>
        {
            new Config
            {
                Id = "test1",
                DiscordWebhookUrl = "",  // Empty webhook URL
                RssUrls = new string[] { },
                YoutubeUrls = new string[] { },
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            }
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WebhookResolver.ResolveWebhooks(configs));
        Assert.Contains("no DiscordWebhookUrl configured", ex.Message);
        Assert.Contains("test1", ex.Message);
    }

    [Fact]
    public void ResolveWebhooks_WhitespaceOnlyWebhookUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var configs = new List<Config>
        {
            new Config
            {
                Id = "test1",
                DiscordWebhookUrl = "   ",  // Only whitespace
                RssUrls = new string[] { },
                YoutubeUrls = new string[] { },
                RssCheckIntervalMinutes = 30,
                DescriptionLimit = 250,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false
            }
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WebhookResolver.ResolveWebhooks(configs));
        Assert.Contains("no DiscordWebhookUrl configured", ex.Message);
    }

    [Fact]
    public void ResolveWebhooks_NullConfigs_DoesNotThrow()
    {
        // Arrange & Act & Assert - should not throw
        WebhookResolver.ResolveWebhooks(null!);
    }

    [Fact]
    public void ResolveWebhooks_EmptyConfigsList_DoesNotThrow()
    {
        // Arrange
        var configs = new List<Config>();

        // Act & Assert - should not throw
        WebhookResolver.ResolveWebhooks(configs);
    }

    [Fact]
    public void ResolveWebhooks_MultipleConfigs_ResolvesAll()
    {
        // Arrange
        const string env1 = "TEST_WEBHOOK_MULTI_1";
        const string env2 = "TEST_WEBHOOK_MULTI_2";
        const string url1 = "https://discord.com/api/webhooks/111/aaa";
        const string url2 = "https://discord.com/api/webhooks/222/bbb";

        Environment.SetEnvironmentVariable(env1, url1);
        Environment.SetEnvironmentVariable(env2, url2);
        try
        {
            var configs = new List<Config>
            {
                new Config
                {
                    Id = "config1",
                    DiscordWebhookUrl = $"env:{env1}",
                    RssUrls = new string[] { },
                    YoutubeUrls = new string[] { },
                    RssCheckIntervalMinutes = 30,
                    DescriptionLimit = 250,
                    Forum = false,
                    MarkdownFormat = false,
                    PersistenceOnShutdown = false
                },
                new Config
                {
                    Id = "config2",
                    DiscordWebhookUrl = $"env:{env2}",
                    RssUrls = new string[] { },
                    YoutubeUrls = new string[] { },
                    RssCheckIntervalMinutes = 30,
                    DescriptionLimit = 250,
                    Forum = false,
                    MarkdownFormat = false,
                    PersistenceOnShutdown = false
                },
                new Config
                {
                    Id = "config3",
                    DiscordWebhookUrl = "https://direct.webhook.url",  // Direct URL
                    RssUrls = new string[] { },
                    YoutubeUrls = new string[] { },
                    RssCheckIntervalMinutes = 30,
                    DescriptionLimit = 250,
                    Forum = false,
                    MarkdownFormat = false,
                    PersistenceOnShutdown = false
                }
            };

            // Act
            WebhookResolver.ResolveWebhooks(configs);

            // Assert
            Assert.Equal(url1, configs[0].DiscordWebhookUrl);
            Assert.Equal(url2, configs[1].DiscordWebhookUrl);
            Assert.Equal("https://direct.webhook.url", configs[2].DiscordWebhookUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable(env1, null);
            Environment.SetEnvironmentVariable(env2, null);
        }
    }

    [Fact]
    public void ResolveWebhooks_WithLoggingAction_CallsLogOnResolvedWebhook()
    {
        // Arrange
        const string envVarName = "TEST_WEBHOOK_LOG_123";
        const string expectedUrl = "https://discord.com/api/webhooks/555/jkl";

        Environment.SetEnvironmentVariable(envVarName, expectedUrl);
        try
        {
            var configs = new List<Config>
            {
                new Config
                {
                    Id = "test1",
                    DiscordWebhookUrl = $"env:{envVarName}",
                    RssUrls = new string[] { },
                    YoutubeUrls = new string[] { },
                    RssCheckIntervalMinutes = 30,
                    DescriptionLimit = 250,
                    Forum = false,
                    MarkdownFormat = false,
                    PersistenceOnShutdown = false
                }
            };
            var loggedMessages = new List<string>();

            // Act
            WebhookResolver.ResolveWebhooks(configs, msg => loggedMessages.Add(msg));

            // Assert
            Assert.Single(loggedMessages);
            Assert.Contains("test1", loggedMessages[0]);
            Assert.Contains(envVarName, loggedMessages[0]);
            Assert.Contains("security", loggedMessages[0], System.StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void ResolveWebhooks_WithoutLoggingAction_DoesNotThrow()
    {
        // Arrange
        const string envVarName = "TEST_WEBHOOK_NOLOG_123";
        const string expectedUrl = "https://discord.com/api/webhooks/666/mno";

        Environment.SetEnvironmentVariable(envVarName, expectedUrl);
        try
        {
            var configs = new List<Config>
            {
                new Config
                {
                    Id = "test1",
                    DiscordWebhookUrl = $"env:{envVarName}",
                    RssUrls = new string[] { },
                    YoutubeUrls = new string[] { },
                    RssCheckIntervalMinutes = 30,
                    DescriptionLimit = 250,
                    Forum = false,
                    MarkdownFormat = false,
                    PersistenceOnShutdown = false
                }
            };

            // Act & Assert - should not throw when logAction is null
            WebhookResolver.ResolveWebhooks(configs, null);
            Assert.Equal(expectedUrl, configs[0].DiscordWebhookUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }
}
