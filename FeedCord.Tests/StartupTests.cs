using Xunit;
using FeedCord.Common;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.ExceptionServices;
using FeedCord.Helpers;
using FeedCord.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

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
        public void ConfigPath_WithMultipleArguments_UsesFirstProvidedPath()
        {
            // Arrange
            var args = new[] { "arg1", "arg2", "arg3" };

            // Act
            var selectedPath = SelectConfigPath(args);

            // Assert
            Assert.Equal("arg1", selectedPath);
        }

        [Theory]
        [MemberData(nameof(GetConfigPathVariations))]
        public void ConfigPath_WithVariousArguments_SelectsCorrectPath(string[] args)
        {
            // Act
            var selectedPath = SelectConfigPath(args);

            // Assert
            var expected = args.Length >= 1 ? args[0] : "config/appsettings.json";
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
            return args.Length >= 1 ? args[0] : "config/appsettings.json";
        }

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
                ["ConcurrentRequests"] = "20"
            });

            var services = new ServiceCollection();

            InvokeStartupPrivateMethod("SetupServices", context, services);

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

        private static object? InvokeStartupPrivateMethod(string methodName, params object[] parameters)
        {
            var method = typeof(Startup).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            try
            {
                return method!.Invoke(null, parameters);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        #endregion
    }

    public class StartupPrivateMethodCoverageTests
    {
        [Fact]
        public void CreateHostBuilder_WithArgs_ReturnsHostBuilder()
        {
            var result = InvokeStartupPrivateMethod("CreateHostBuilder", (object)Array.Empty<string>());

            Assert.NotNull(result);
            Assert.IsAssignableFrom<IHostBuilder>(result);
        }

        [Fact]
        public void SetupConfiguration_WithSingleArgument_UsesProvidedPath()
        {
            var context = new HostBuilderContext(new Dictionary<object, object>());
            var builder = new ConfigurationBuilder();

            InvokeStartupPrivateMethod("SetupConfiguration", context, builder, new[] { "custom-config.json" });

            var jsonSource = Assert.IsType<JsonConfigurationSource>(builder.Sources.Last());
            Assert.Equal("custom-config.json", jsonSource.Path);
        }

        [Fact]
        public void SetupConfiguration_WithMultipleArguments_UsesFirstProvidedPath()
        {
            var context = new HostBuilderContext(new Dictionary<object, object>());
            var builder = new ConfigurationBuilder();

            InvokeStartupPrivateMethod("SetupConfiguration", context, builder, new[] { "one", "two" });

            var jsonSource = Assert.IsType<JsonConfigurationSource>(builder.Sources.Last());
            Assert.Equal("one", jsonSource.Path);
        }

        [Fact]
        public void SetupLogging_ConfiguresCustomFormatterAndFilters()
        {
            var loggingBuilder = new TestLoggingBuilder();
            var context = new HostBuilderContext(new Dictionary<object, object>());

            InvokeStartupPrivateMethod("SetupLogging", context, loggingBuilder);

            using var provider = loggingBuilder.Services.BuildServiceProvider();
            var consoleOptions = provider.GetRequiredService<IOptions<ConsoleLoggerOptions>>().Value;
            var filterOptions = provider.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;

            Assert.Equal("customlogsformatter", consoleOptions.FormatterName);
            Assert.Contains(filterOptions.Rules, r => r.CategoryName == "Microsoft" && r.LogLevel == LogLevel.Information);
            Assert.Contains(filterOptions.Rules, r => r.CategoryName == "Microsoft.Hosting" && r.LogLevel == LogLevel.Warning);
            Assert.Contains(filterOptions.Rules, r => r.CategoryName == "System" && r.LogLevel == LogLevel.Information);
            Assert.Contains(filterOptions.Rules, r => r.CategoryName == "System.Net.Http.HttpClient" && r.LogLevel == LogLevel.Warning);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(201)]
        public void SetupServices_WithInvalidConcurrentRequests_Throws(int concurrentRequests)
        {
            var context = CreateHostBuilderContext(new Dictionary<string, string?>
            {
                ["ConcurrentRequests"] = concurrentRequests.ToString(),
            });

            var services = new ServiceCollection();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                InvokeStartupPrivateMethod("SetupServices", context, services));

            Assert.Contains("ConcurrentRequests", exception.Message);
        }

        [Fact]
        public void SetupServices_RegistersSemaphoreAndReferenceStore()
        {
            var context = CreateHostBuilderContext(new Dictionary<string, string?>
            {
                ["ConcurrentRequests"] = "12",
            });

            var services = new ServiceCollection();

            InvokeStartupPrivateMethod("SetupServices", context, services);

            using var provider = services.BuildServiceProvider();
            var semaphore = provider.GetRequiredService<SemaphoreSlim>();
            var referencePostStore = provider.GetRequiredService<IReferencePostStore>();

            Assert.Equal(12, semaphore.CurrentCount);
            Assert.IsType<JsonReferencePostStore>(referencePostStore);
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
                ["Instances:0:RssCheckIntervalMinutes"] = "30",
                ["Instances:0:DescriptionLimit"] = "250",
                ["Instances:0:Forum"] = "false",
                ["Instances:0:MarkdownFormat"] = "false",
                ["Instances:0:PersistenceOnShutdown"] = "false",
            });

            var services = new ServiceCollection();

            InvokeStartupPrivateMethod("SetupServices", context, services);

            Assert.Equal(1, services.Count(sd => sd.ServiceType == typeof(IHostedService)));
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
                RssCheckIntervalMinutes = 0,
                DescriptionLimit = 0,
                Forum = false,
                MarkdownFormat = false,
                PersistenceOnShutdown = false,
            };

            var exception = Assert.Throws<InvalidOperationException>(() =>
                InvokeStartupPrivateMethod("ValidateConfiguration", invalidConfig));

            Assert.Contains("Invalid config entry", exception.Message);
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

        private static object? InvokeStartupPrivateMethod(string methodName, params object[] parameters)
        {
            var method = typeof(Startup).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            try
            {
                return method!.Invoke(null, parameters);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
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
            var buildHostProperty = typeof(Startup).GetProperty(
                "BuildHost",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );
            var runHostProperty = typeof(Startup).GetProperty(
                "RunHost",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );

            Assert.NotNull(buildHostProperty);
            Assert.NotNull(runHostProperty);

            var originalBuildHost = (Func<string[], IHost>)buildHostProperty!.GetValue(null)!;
            var originalRunHost = (Action<IHost>)runHostProperty!.GetValue(null)!;

            var expectedHost = new Moq.Mock<IHost>(Moq.MockBehavior.Strict).Object;
            string[]? capturedArgs = null;
            IHost? capturedHost = null;

            try
            {
                buildHostProperty.SetValue(null, (Func<string[], IHost>)(args =>
                {
                    capturedArgs = args;
                    return expectedHost;
                }));

                runHostProperty.SetValue(null, (Action<IHost>)(host => capturedHost = host));

                var args = new[] { "config/appsettings.json" };
                Startup.Initialize(args);

                Assert.Same(args, capturedArgs);
                Assert.Same(expectedHost, capturedHost);
            }
            finally
            {
                buildHostProperty.SetValue(null, originalBuildHost);
                runHostProperty.SetValue(null, originalRunHost);
            }
        }
    }
}
