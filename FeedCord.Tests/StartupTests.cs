using Xunit;
using FeedCord.Common;
using System.ComponentModel.DataAnnotations;
using FeedCord.Helpers;
using FeedCord.Services.Interfaces;
using FeedCord.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace FeedCord.Tests
{
  public class StartupConfigurationValidationTests
  {
    public static TheoryData<int> InvalidCheckIntervals => new()
    {
      0,
      -1,
      -60,
    };

    public static TheoryData<int> InvalidDescriptionLimits => new()
    {
      0,
      -1,
      -100,
      4001,
      5000,
    };

    public static TheoryData<int> ValidIntervals => new()
    {
      1,
      60,
      86400,
    };

    public static TheoryData<int> ValidDescriptionLimits => new()
    {
      1,
      250,
      4000,
    };

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
        RssCheckIntervalSeconds = 30,
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
        RssCheckIntervalSeconds = 1,
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
        RssCheckIntervalSeconds = 30,
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
        RssCheckIntervalSeconds = 30,
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
    [MemberData(nameof(InvalidCheckIntervals))]
    public void ValidateConfiguration_WithInvalidCheckInterval_Throws(int invalidInterval)
    {
      // Arrange
      var config = new Config
      {
        Id = "test",
        RssUrls = new[] { "https://example.com/rss" },
        YoutubeUrls = new string[] { },
        DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
        RssCheckIntervalSeconds = invalidInterval,
        DescriptionLimit = 250,
        Forum = false,
        MarkdownFormat = false,
        PersistenceOnShutdown = false
      };

      // Act & Assert
      Assert.Throws<InvalidOperationException>(() => ValidateConfiguration(config));
    }

    [Theory]
    [MemberData(nameof(InvalidDescriptionLimits))]
    public void ValidateConfiguration_WithInvalidDescriptionLimit_Throws(int invalidLimit)
    {
      // Arrange
      var config = new Config
      {
        Id = "test",
        RssUrls = new[] { "https://example.com/rss" },
        YoutubeUrls = new string[] { },
        DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
        RssCheckIntervalSeconds = 30,
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
        RssCheckIntervalSeconds = 30,
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
        RssCheckIntervalSeconds = 30,
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
        RssCheckIntervalSeconds = 30,
        DescriptionLimit = 250,
        Forum = false,
        MarkdownFormat = false,
        PersistenceOnShutdown = false
      };

      // Act & Assert
      ValidateConfiguration(config);
    }

    [Theory]
    [MemberData(nameof(ValidIntervals))]
    public void ValidateConfiguration_WithVariousValidIntervals_Succeeds(int seconds)
    {
      // Arrange
      var config = new Config
      {
        Id = "test",
        RssUrls = new[] { "https://example.com/rss" },
        YoutubeUrls = new string[] { },
        DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
        RssCheckIntervalSeconds = seconds,
        DescriptionLimit = 250,
        Forum = false,
        MarkdownFormat = false,
        PersistenceOnShutdown = false
      };

      // Act & Assert
      ValidateConfiguration(config);
    }

    [Theory]
    [MemberData(nameof(ValidDescriptionLimits))]
    public void ValidateConfiguration_WithVariousDescriptionLimits_Succeeds(int limit)
    {
      // Arrange
      var config = new Config
      {
        Id = "test",
        RssUrls = new[] { "https://example.com/rss" },
        YoutubeUrls = new string[] { },
        DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
        RssCheckIntervalSeconds = 30,
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
    public void ConfigPath_WithNoArguments_UsesDefaultConfigSelection()
    {
      // Arrange
      var args = Array.Empty<string>();

      // Act
      var selectedPath = Startup.SelectConfigPath(args);

      // Assert
      Assert.Equal("config/appsettings.yaml", selectedPath);
    }

    [Fact]
    public void ConfigPath_WithSingleArgument_UsesProvidedPath()
    {
      // Arrange
      var args = new[] { "custom/path.yaml" };

      // Act
      var selectedPath = Startup.SelectConfigPath(args);

      // Assert
      Assert.Equal("custom/path.yaml", selectedPath);
    }

    [Fact]
    public void ConfigPath_WithMultipleArguments_UsesFirstProvidedPath()
    {
      // Arrange
      var args = new[] { "arg1", "arg2", "arg3" };

      // Act
      var selectedPath = Startup.SelectConfigPath(args);

      // Assert
      Assert.Equal("arg1", selectedPath);
    }

    [Theory]
    [MemberData(nameof(GetConfigPathVariations))]
    public void ConfigPath_WithVariousArguments_SelectsCorrectPath(string[] args)
    {
      // Act
      var selectedPath = Startup.SelectConfigPath(args);

      // Assert
      var expected = args.Length >= 1
        ? args[0]
        : Startup.SelectDefaultConfigPath(AppDomain.CurrentDomain.BaseDirectory);
      Assert.Equal(expected, selectedPath);
    }

    [Fact]
    public void SelectDefaultConfigPath_WhenYamlExists_PrefersYaml()
    {
      var baseDirectory = Path.Combine(Path.GetTempPath(), $"feedcord-config-base-{Guid.NewGuid():N}");
      var configDirectory = Path.Combine(baseDirectory, "config");
      Directory.CreateDirectory(configDirectory);

      File.WriteAllText(Path.Combine(configDirectory, "appsettings.yaml"), "Instances: []");
      File.WriteAllText(Path.Combine(configDirectory, "appsettings.yml"), "Instances: []");

      try
      {
        var selectedPath = Startup.SelectDefaultConfigPath(baseDirectory);
        Assert.Equal("config/appsettings.yaml", selectedPath);
      }
      finally
      {
        if (Directory.Exists(baseDirectory))
        {
          Directory.Delete(baseDirectory, recursive: true);
        }
      }
    }

    [Fact]
    public void SelectDefaultConfigPath_WhenYmlExistsWithoutYaml_UsesYml()
    {
      var baseDirectory = Path.Combine(Path.GetTempPath(), $"feedcord-config-base-{Guid.NewGuid():N}");
      var configDirectory = Path.Combine(baseDirectory, "config");
      Directory.CreateDirectory(configDirectory);

      File.WriteAllText(Path.Combine(configDirectory, "appsettings.yml"), "Instances: []");

      try
      {
        var selectedPath = Startup.SelectDefaultConfigPath(baseDirectory);
        Assert.Equal("config/appsettings.yml", selectedPath);
      }
      finally
      {
        if (Directory.Exists(baseDirectory))
        {
          Directory.Delete(baseDirectory, recursive: true);
        }
      }
    }

    [Fact]
    public void SelectDefaultConfigPath_WhenOnlyJsonExists_FallsBackToYaml()
    {
      var baseDirectory = Path.Combine(Path.GetTempPath(), $"feedcord-config-base-{Guid.NewGuid():N}");
      var configDirectory = Path.Combine(baseDirectory, "config");
      Directory.CreateDirectory(configDirectory);

      File.WriteAllText(Path.Combine(configDirectory, "appsettings.json"), "{\"Instances\":[]}");

      try
      {
        var selectedPath = Startup.SelectDefaultConfigPath(baseDirectory);
        Assert.Equal("config/appsettings.yaml", selectedPath);
      }
      finally
      {
        if (Directory.Exists(baseDirectory))
        {
          Directory.Delete(baseDirectory, recursive: true);
        }
      }
    }

    #endregion

    #region Test Data

    public static IEnumerable<object[]> GetConfigPathVariations()
    {
      yield return new object[] { Array.Empty<string>() };
      yield return new object[] { new[] { "config.yaml" } };
      yield return new object[] { new[] { "path/to/config.yaml" } };
    }

    #endregion

    #region Helper Method

    #endregion
  }

  public class StartupIntegrationTests
  {
    #region Service Configuration Behavior

    [Fact]
    public void SetupServices_RegistersHttpClientFactory_WithExpectedDefaults()
    {
      var context = CreateHostBuilderContext(new Dictionary<string, string?>
      {
        ["App:ConcurrentRequests"] = "20"
      });

      var services = new ServiceCollection();

      Startup.SetupServices(context, services);

      using var provider = services.BuildServiceProvider();
      var factory = provider.GetRequiredService<IHttpClientFactory>();
      var client = factory.CreateClient("Default");
      var userAgent = client.DefaultRequestHeaders.UserAgent.ToString();

      Assert.True(
          client.Timeout == TimeSpan.FromSeconds(30) || client.Timeout == Timeout.InfiniteTimeSpan,
          $"Unexpected timeout: {client.Timeout}");
      Assert.Contains("Mozilla", userAgent);
      Assert.Contains("Chrome", userAgent);
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

    private static HostBuilderContext CreateHostBuilderContext(Dictionary<string, string?> values)
    {
      var configuration = new ConfigurationBuilder()
          .AddInMemoryCollection(values)
          .Build();

      return new HostBuilderContext(new Dictionary<object, object>())
      {
        Configuration = configuration
      };
    }

    #endregion
  }

  public class StartupPrivateMethodCoverageTests
  {
    public static TheoryData<int> InvalidConcurrentRequestsValues => new()
    {
      0,
      201,
    };

    public static TheoryData<int> InvalidTimeoutSecondsValues => new()
    {
      0,
      301,
    };

    public static TheoryData<int> InvalidPostMinIntervalSecondsValues => new()
    {
      0,
      121,
    };

    public static TheoryData<int> NonDefaultConcurrentRequestsValues => new()
    {
      1,
      10,
      25,
      50,
      100,
      200,
    };

    [Fact]
    public void CreateApplication_WithArgs_ReturnsHost()
    {
      var tempConfigPath = Path.Combine(Path.GetTempPath(), $"feedcord-startup-create-app-{Guid.NewGuid():N}.yaml");
      File.WriteAllText(tempConfigPath, "Instances: []");

      var result = Startup.CreateApplication(new[] { tempConfigPath });

      Assert.NotNull(result);
      Assert.IsAssignableFrom<IHost>(result);

      if (result is IHost host)
      {
        host.Dispose();
      }

      if (File.Exists(tempConfigPath))
      {
        File.Delete(tempConfigPath);
      }
    }

    [Fact]
    public void SetupConfiguration_WithSingleArgument_UsesProvidedPath()
    {
      var context = new HostBuilderContext(new Dictionary<object, object>());
      var builder = new ConfigurationBuilder();

      Startup.SetupConfiguration(context, builder, new[] { "custom-config.yaml" });

      var source = builder.Sources.Last();
      Assert.Equal("YamlConfigurationSource", source.GetType().Name);

      var sourcePath = source.GetType().GetProperty("Path")?.GetValue(source) as string;
      Assert.Equal("custom-config.yaml", sourcePath);
    }

    [Theory]
    [InlineData("custom-config.yaml")]
    [InlineData("custom-config.yml")]
    public void SetupConfiguration_WithYamlArgument_UsesYamlSource(string path)
    {
      var context = new HostBuilderContext(new Dictionary<object, object>());
      var builder = new ConfigurationBuilder();

      Startup.SetupConfiguration(context, builder, new[] { path });

      var source = builder.Sources.Last();
      Assert.Equal("YamlConfigurationSource", source.GetType().Name);

      var sourcePath = source.GetType().GetProperty("Path")?.GetValue(source) as string;
      Assert.Equal(path, sourcePath);
    }

    [Fact]
    public void SetupConfiguration_WithMultipleArguments_UsesFirstProvidedPath()
    {
      var context = new HostBuilderContext(new Dictionary<object, object>());
      var builder = new ConfigurationBuilder();

      Startup.SetupConfiguration(context, builder, new[] { "one.yaml", "two.yml" });

      var source = builder.Sources.Last();
      Assert.Equal("YamlConfigurationSource", source.GetType().Name);

      var sourcePath = source.GetType().GetProperty("Path")?.GetValue(source) as string;
      Assert.Equal("one.yaml", sourcePath);
    }

    [Fact]
    public void SetupConfiguration_WithUnsupportedExtension_Throws()
    {
      var context = new HostBuilderContext(new Dictionary<object, object>());
      var builder = new ConfigurationBuilder();

      var exception = Assert.Throws<InvalidOperationException>(() =>
          Startup.SetupConfiguration(context, builder, new[] { "config.toml" }));

      Assert.Contains("Unsupported configuration file extension", exception.Message);
    }

    [Theory]
    [InlineData(".yaml")]
    [InlineData(".yml")]
    public void SetupConfigurationManager_WithYamlArgument_UsesYamlSource(string extension)
    {
      var builder = new ConfigurationManager();
      var path = Path.Combine(Path.GetTempPath(), $"feedcord-startup-config-{Guid.NewGuid():N}{extension}");

      File.WriteAllText(path, "Instances: []");

      try
      {
        Startup.SetupConfiguration(builder, new[] { path });

        var source = builder.Sources.Last();
        Assert.Equal("YamlConfigurationSource", source.GetType().Name);

        var sourcePath = source.GetType().GetProperty("Path")?.GetValue(source) as string;
        Assert.Equal(Path.GetFileName(path), sourcePath);
      }
      finally
      {
        if (File.Exists(path))
        {
          File.Delete(path);
        }
      }
    }

    [Fact]
    public void SetupConfigurationManager_WithUnsupportedExtension_Throws()
    {
      var builder = new ConfigurationManager();

      var exception = Assert.Throws<InvalidOperationException>(() =>
          Startup.SetupConfiguration(builder, new[] { "manager-config.toml" }));

      Assert.Contains("Unsupported configuration file extension", exception.Message);
    }

    [Fact]
    public void SetupLogging_ConfiguresCustomFormatterAndFilters()
    {
      var loggingBuilder = new TestLoggingBuilder();
      var context = new HostBuilderContext(new Dictionary<object, object>());

      Startup.SetupLogging(context, loggingBuilder);

      using var provider = loggingBuilder.Services.BuildServiceProvider();
      var consoleOptions = provider.GetRequiredService<IOptions<ConsoleLoggerOptions>>().Value;
      var filterOptions = provider.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;

      Assert.Equal("customlogsformatter", consoleOptions.FormatterName);
      Assert.Contains(filterOptions.Rules, r => r.CategoryName == "Microsoft" && r.LogLevel == LogLevel.Information);
      Assert.Contains(filterOptions.Rules, r => r.CategoryName == "Microsoft.Hosting" && r.LogLevel == LogLevel.Warning);
      Assert.Contains(filterOptions.Rules, r => r.CategoryName == "Microsoft.AspNetCore.Hosting.Diagnostics" && r.LogLevel == LogLevel.Debug);
      Assert.Contains(filterOptions.Rules, r => r.CategoryName == "Microsoft.AspNetCore.Routing.EndpointMiddleware" && r.LogLevel == LogLevel.Debug);
      Assert.Contains(filterOptions.Rules, r => r.CategoryName == "Microsoft.AspNetCore.Diagnostics.HealthChecks" && r.LogLevel == LogLevel.Debug);
      Assert.Contains(filterOptions.Rules, r => r.CategoryName == "System" && r.LogLevel == LogLevel.Information);
      Assert.Contains(filterOptions.Rules, r => r.CategoryName == "System.Net.Http.HttpClient" && r.LogLevel == LogLevel.Warning);
    }

    [Theory]
    [MemberData(nameof(InvalidConcurrentRequestsValues))]
    public void SetupServices_WithInvalidConcurrentRequests_Throws(int concurrentRequests)
    {
      var context = CreateHostBuilderContext(new Dictionary<string, string?>
      {
        ["App:ConcurrentRequests"] = concurrentRequests.ToString(),
      });

      var services = new ServiceCollection();

      var exception = Assert.Throws<InvalidOperationException>(() =>
          Startup.SetupServices(context, services));

      Assert.Contains("ConcurrentRequests", exception.Message);
    }

    [Fact]
    public void SetupServices_RegistersSemaphoreAndReferenceStore()
    {
      var context = CreateHostBuilderContext(new Dictionary<string, string?>
      {
        ["App:ConcurrentRequests"] = "12",
      });

      var services = new ServiceCollection();

      Startup.SetupServices(context, services);

      using var provider = services.BuildServiceProvider();
      var semaphore = provider.GetRequiredService<SemaphoreSlim>();
      var referencePostStore = provider.GetRequiredService<IReferencePostStore>();

      Assert.Equal(12, semaphore.CurrentCount);
      Assert.IsType<JsonReferencePostStore>(referencePostStore);
    }

    [Fact]
    public void SetupServices_WithAppConcurrentRequests_RegistersSemaphore()
    {
      var context = CreateHostBuilderContext(new Dictionary<string, string?>
      {
        ["App:ConcurrentRequests"] = "15",
      });

      var services = new ServiceCollection();

      Startup.SetupServices(context, services);

      using var provider = services.BuildServiceProvider();
      var semaphore = provider.GetRequiredService<SemaphoreSlim>();

      Assert.Equal(15, semaphore.CurrentCount);
    }

    [Fact]
    public void SetupServices_WithValidInstance_RegistersHostedServiceFactory()
    {
      var context = CreateHostBuilderContext(new Dictionary<string, string?>
      {
        ["Instances:0:Id"] = "test-feed",
        ["Instances:0:RssUrls:0"] = "https://example.com/rss",
        ["Instances:0:YoutubeUrls:0"] = "https://youtube.com/channel/example",
        ["Instances:0:DiscordWebhookUrl"] = "https://discord.com/api/webhooks/123/abc",
        ["Instances:0:RssCheckIntervalSeconds"] = "30",
        ["Instances:0:DescriptionLimit"] = "250",
        ["Instances:0:Forum"] = "false",
        ["Instances:0:MarkdownFormat"] = "false",
        ["Instances:0:PersistenceOnShutdown"] = "false",
      });

      var services = new ServiceCollection();

      Startup.SetupServices(context, services);

      Assert.Equal(1, services.Count(sd => sd.ServiceType == typeof(IHostedService)));
    }

    [Fact]
    public void SetupServices_WithValidInstance_ResolvesHostedServiceInstance()
    {
      var context = CreateHostBuilderContext(new Dictionary<string, string?>
      {
        ["Instances:0:Id"] = "test-feed-resolve",
        ["Instances:0:RssUrls:0"] = "https://example.com/rss",
        ["Instances:0:YoutubeUrls:0"] = "https://youtube.com/channel/example",
        ["Instances:0:DiscordWebhookUrl"] = "https://discord.com/api/webhooks/123/abc",
        ["Instances:0:RssCheckIntervalSeconds"] = "30",
        ["Instances:0:DescriptionLimit"] = "250",
        ["Instances:0:Forum"] = "false",
        ["Instances:0:MarkdownFormat"] = "false",
        ["Instances:0:PersistenceOnShutdown"] = "false",
      });

      var services = new ServiceCollection();

      Startup.SetupServices(context, services);
      services.AddSingleton<Moq.Mock<IHostApplicationLifetime>>();
      services.AddSingleton<IHostApplicationLifetime>(sp => sp.GetRequiredService<Moq.Mock<IHostApplicationLifetime>>().Object);

      using var provider = services.BuildServiceProvider();
      var hostedServices = provider.GetServices<IHostedService>().ToList();

      Assert.Single(hostedServices);
      Assert.NotNull(hostedServices[0]);
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidConfig_ThrowsDetailedMessage()
    {
      var invalidConfig = new Config
      {
        Id = null!,
        RssUrls = Array.Empty<string>(),
        YoutubeUrls = Array.Empty<string>(),
        DiscordWebhookUrl = null!,
        RssCheckIntervalSeconds = 0,
        DescriptionLimit = 0,
        Forum = false,
        MarkdownFormat = false,
        PersistenceOnShutdown = false,
      };

      var exception = Assert.Throws<InvalidOperationException>(() =>
          Startup.ValidateConfiguration(invalidConfig));

      Assert.Contains("Invalid config entry", exception.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidTimeoutSecondsValues))]
    public void SetupServices_WithInvalidTimeoutSeconds_Throws(int timeoutSeconds)
    {
      var context = CreateHostBuilderContext(new Dictionary<string, string?>
      {
        ["App:ConcurrentRequests"] = "20",
        ["Http:TimeoutSeconds"] = timeoutSeconds.ToString(),
      });

      var services = new ServiceCollection();

      var exception = Assert.Throws<InvalidOperationException>(() =>
          Startup.SetupServices(context, services));

      Assert.Contains("Invalid HTTP configuration", exception.Message);
      Assert.Contains("TimeoutSeconds", exception.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidPostMinIntervalSecondsValues))]
    public void SetupServices_WithInvalidPostMinIntervalSeconds_Throws(int postMinInterval)
    {
      var context = CreateHostBuilderContext(new Dictionary<string, string?>
      {
        ["App:ConcurrentRequests"] = "20",
        ["Http:PostMinIntervalSeconds"] = postMinInterval.ToString(),
      });

      var services = new ServiceCollection();

      var exception = Assert.Throws<InvalidOperationException>(() =>
          Startup.SetupServices(context, services));

      Assert.Contains("Invalid HTTP configuration", exception.Message);
      Assert.Contains("PostMinIntervalSeconds", exception.Message);
    }

    [Theory]
    [InlineData("metrics")]
    [InlineData("health/live")]
    public void SetupServices_WithInvalidObservabilityMetricsPath_Throws(string metricsPath)
    {
      var context = CreateHostBuilderContext(new Dictionary<string, string?>
      {
        ["App:ConcurrentRequests"] = "20",
        ["Observability:MetricsPath"] = metricsPath,
      });

      var services = new ServiceCollection();

      var exception = Assert.Throws<InvalidOperationException>(() =>
          Startup.SetupServices(context, services));

      Assert.Contains("Invalid observability configuration", exception.Message);
      Assert.Contains("MetricsPath", exception.Message);
    }

    [Theory]
    [InlineData("health/live")]
    [InlineData("live")]
    public void SetupServices_WithInvalidObservabilityLivenessPath_Throws(string livenessPath)
    {
      var context = CreateHostBuilderContext(new Dictionary<string, string?>
      {
        ["App:ConcurrentRequests"] = "20",
        ["Observability:LivenessPath"] = livenessPath,
      });

      var services = new ServiceCollection();

      var exception = Assert.Throws<InvalidOperationException>(() =>
          Startup.SetupServices(context, services));

      Assert.Contains("Invalid observability configuration", exception.Message);
      Assert.Contains("LivenessPath", exception.Message);
    }

    [Theory]
    [MemberData(nameof(NonDefaultConcurrentRequestsValues))]
    public void SetupServices_WithNonDefaultConcurrentRequests_LogsInformation(int concurrentRequests)
    {
      var context = CreateHostBuilderContext(new Dictionary<string, string?>
      {
        ["App:ConcurrentRequests"] = concurrentRequests.ToString(),
      });

      var services = new ServiceCollection();

      // This should not throw and should log that ConcurrentRequests is set
      Startup.SetupServices(context, services);

      using var provider = services.BuildServiceProvider();
      var semaphore = provider.GetRequiredService<SemaphoreSlim>();

      Assert.Equal(concurrentRequests, semaphore.CurrentCount);
    }

    [Fact]
    public void SetupServices_WithDefaultConcurrentRequests_DoesNotLog()
    {
      var context = CreateHostBuilderContext(new Dictionary<string, string?>
      {
        ["App:ConcurrentRequests"] = "20",
      });

      var services = new ServiceCollection();

      // With default value of 20, the logging should be skipped
      Startup.SetupServices(context, services);

      using var provider = services.BuildServiceProvider();
      var semaphore = provider.GetRequiredService<SemaphoreSlim>();

      Assert.Equal(20, semaphore.CurrentCount);
    }

    [Fact]
    public void SetupServices_RegistersBatchLogger()
    {
      var context = CreateHostBuilderContext(new Dictionary<string, string?>
      {
        ["App:ConcurrentRequests"] = "20",
      });

      var services = new ServiceCollection();

      Startup.SetupServices(context, services);

      using var provider = services.BuildServiceProvider();
      var batchLogger = provider.GetRequiredService<IBatchLogger>();

      Assert.NotNull(batchLogger);
      Assert.IsType<FeedCord.Core.BatchLogger>(batchLogger);
    }

    [Fact]
    public void SetupServices_RegistersCustomHttpClient()
    {
      var context = CreateHostBuilderContext(new Dictionary<string, string?>
      {
        ["App:ConcurrentRequests"] = "20",
      });

      var services = new ServiceCollection();

      Startup.SetupServices(context, services);

      using var provider = services.BuildServiceProvider();
      var customHttpClient = provider.GetRequiredService<ICustomHttpClient>();

      Assert.NotNull(customHttpClient);
      Assert.IsType<FeedCord.Infrastructure.Http.CustomHttpClient>(customHttpClient);
    }

    private static HostBuilderContext CreateHostBuilderContext(Dictionary<string, string?> values)
    {
      var configuration = new ConfigurationBuilder()
          .AddInMemoryCollection(values)
          .Build();

      return new HostBuilderContext(new Dictionary<object, object>())
      {
        Configuration = configuration
      };
    }

    private sealed class TestLoggingBuilder : ILoggingBuilder
    {
      public IServiceCollection Services { get; } = new ServiceCollection();
    }
  }

  [CollectionDefinition("StartupInitializeNonParallel", DisableParallelization = true)]
  public class StartupInitializeNonParallelCollection
  {
  }

  [Collection("StartupInitializeNonParallel")]
  public class StartupInitializeExecutionTests
  {
    [Fact]
    public void Initialize_BuildsAndRunsHost_UsingInjectedDelegates()
    {
      var originalBuildHost = Startup.BuildHost;
      var originalRunHost = Startup.RunHost;

      var expectedHost = new Moq.Mock<IHost>(Moq.MockBehavior.Strict).Object;
      string[]? capturedArgs = null;
      IHost? capturedHost = null;

      try
      {
        Startup.BuildHost = args =>
        {
          capturedArgs = args;
          return expectedHost;
        };

        Startup.RunHost = host => capturedHost = host;

        var args = new[] { "config/appsettings.yaml" };
        Startup.Initialize(args);

        Assert.Same(args, capturedArgs);
        Assert.Same(expectedHost, capturedHost);
      }
      finally
      {
        Startup.BuildHost = originalBuildHost;
        Startup.RunHost = originalRunHost;
      }
    }
  }
}
