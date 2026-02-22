using Xunit;
using FeedCord.Helpers;
using FeedCord.Common;

namespace FeedCord.Tests.Helpers;

public class WebhookResolverTests
{
  public static IEnumerable<object?[]> EmptyConfigInputs()
  {
    yield return new object?[] { null };
    yield return new object?[] { new List<Config>() };
  }

  [Theory]
  [MemberData(nameof(EmptyConfigInputs))]
  public void ResolveWebhooks_NullOrEmptyConfigs_DoesNotThrow(List<Config>? configs)
  {
    WebhookResolver.ResolveWebhooks(configs!);
  }

  [Fact]
  public void ResolveWebhooks_DirectWebhookUrl_NoResolutionNeeded()
  {
    var configs = new List<Config> { CreateConfig("test1", "https://discord.com/api/webhooks/123/abc") };
    var loggedMessages = new List<string>();

    WebhookResolver.ResolveWebhooks(configs, msg => loggedMessages.Add(msg));

    Assert.Equal("https://discord.com/api/webhooks/123/abc", configs[0].DiscordWebhookUrl);
    Assert.Empty(loggedMessages);
  }

  [Theory]
  [InlineData("env:{0}")]
  [InlineData("env:  {0}  ")]
  [InlineData("ENV:{0}")]
  public void ResolveWebhooks_EnvPrefixedUrl_ResolvesFromEnvironmentVariable(string envReferencePattern)
  {
    var envVarName = $"TEST_WEBHOOK_URL_{Guid.NewGuid():N}";
    const string expectedUrl = "https://discord.com/api/webhooks/999/xyz";

    Environment.SetEnvironmentVariable(envVarName, expectedUrl);
    try
    {
      var configs = new List<Config> { CreateConfig("test1", string.Format(envReferencePattern, envVarName)) };
      var loggedMessages = new List<string>();

      WebhookResolver.ResolveWebhooks(configs, msg => loggedMessages.Add(msg));

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

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void ResolveWebhooks_MissingOrEmptyEnvironmentVariable_ThrowsInvalidOperationException(string? envValue)
  {
    var envVarName = $"TEST_WEBHOOK_MISSING_{Guid.NewGuid():N}";
    Environment.SetEnvironmentVariable(envVarName, envValue);
    try
    {
      var configs = new List<Config> { CreateConfig("test1", $"env:{envVarName}") };

      var ex = Assert.Throws<InvalidOperationException>(() => WebhookResolver.ResolveWebhooks(configs));
      Assert.Contains(envVarName, ex.Message);
      Assert.Contains("not set or is empty", ex.Message);
    }
    finally
    {
      Environment.SetEnvironmentVariable(envVarName, null);
    }
  }

  [Theory]
  [InlineData("env:")]
  [InlineData("env:   ")]
  public void ResolveWebhooks_MalformedEnvPrefix_ThrowsInvalidOperationException(string malformedReference)
  {
    var configs = new List<Config> { CreateConfig("test1", malformedReference) };

    var ex = Assert.Throws<InvalidOperationException>(() => WebhookResolver.ResolveWebhooks(configs));
    Assert.Contains("malformed webhook reference", ex.Message);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void ResolveWebhooks_MissingWebhookUrl_ThrowsInvalidOperationException(string webhookUrl)
  {
    var configs = new List<Config> { CreateConfig("test1", webhookUrl) };

    var ex = Assert.Throws<InvalidOperationException>(() => WebhookResolver.ResolveWebhooks(configs));
    Assert.Contains("no DiscordWebhookUrl configured", ex.Message);
    Assert.Contains("test1", ex.Message);
  }

  [Fact]
  public void ResolveWebhooks_MultipleConfigs_ResolvesMixedReferences()
  {
    var env1 = $"TEST_WEBHOOK_MULTI_1_{Guid.NewGuid():N}";
    var env2 = $"TEST_WEBHOOK_MULTI_2_{Guid.NewGuid():N}";
    const string url1 = "https://discord.com/api/webhooks/111/aaa";
    const string url2 = "https://discord.com/api/webhooks/222/bbb";

    Environment.SetEnvironmentVariable(env1, url1);
    Environment.SetEnvironmentVariable(env2, url2);
    try
    {
      var configs = new List<Config>
            {
                CreateConfig("config1", $"env:{env1}"),
                CreateConfig("config2", $"env:{env2}"),
                CreateConfig("config3", "https://direct.webhook.url")
            };

      WebhookResolver.ResolveWebhooks(configs);

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
    var envVarName = $"TEST_WEBHOOK_LOG_{Guid.NewGuid():N}";
    const string expectedUrl = "https://discord.com/api/webhooks/555/jkl";

    Environment.SetEnvironmentVariable(envVarName, expectedUrl);
    try
    {
      var configs = new List<Config> { CreateConfig("test1", $"env:{envVarName}") };
      var loggedMessages = new List<string>();

      WebhookResolver.ResolveWebhooks(configs, msg => loggedMessages.Add(msg));

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
    var envVarName = $"TEST_WEBHOOK_NOLOG_{Guid.NewGuid():N}";
    const string expectedUrl = "https://discord.com/api/webhooks/666/mno";

    Environment.SetEnvironmentVariable(envVarName, expectedUrl);
    try
    {
      var configs = new List<Config> { CreateConfig("test1", $"env:{envVarName}") };

      WebhookResolver.ResolveWebhooks(configs, null);
      Assert.Equal(expectedUrl, configs[0].DiscordWebhookUrl);
    }
    finally
    {
      Environment.SetEnvironmentVariable(envVarName, null);
    }
  }

  private static Config CreateConfig(string id, string webhookUrl)
  {
    return new Config
    {
      Id = id,
      DiscordWebhookUrl = webhookUrl,
      RssUrls = Array.Empty<string>(),
      YoutubeUrls = Array.Empty<string>(),
      RssCheckIntervalMinutes = 30,
      DescriptionLimit = 250,
      Forum = false,
      MarkdownFormat = false,
      PersistenceOnShutdown = false
    };
  }
}
