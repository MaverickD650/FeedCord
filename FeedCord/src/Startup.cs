using FeedCord.Common;
using FeedCord.Helpers;
using FeedCord.Core;
using FeedCord.Services;
using FeedCord.Core.Interfaces;
using FeedCord.Core.Factories;
using FeedCord.Infrastructure.Http;
using FeedCord.Services.Factories;
using FeedCord.Infrastructure.Factories;
using FeedCord.Infrastructure.Health;
using FeedCord.Services.Interfaces;
using FeedCord.Infrastructure.Parsers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using OpenTelemetry.Metrics;
using System.Net;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace FeedCord
{
    public class Startup
    {
        internal static Func<string[], IHost> BuildHost { get; set; } = args => CreateHostBuilder(args).Build();
        internal static Action<IHost> RunHost { get; set; } = host => host.Run();

        public static void Initialize(string[] args)
        {
            var host = BuildHost(args);
            RunHost(host);
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((ctx, builder) =>
                {
                    SetupConfiguration(ctx, builder, args);
                })
                .ConfigureLogging(SetupLogging)
                .ConfigureWebHostDefaults(ConfigureWebHost)
                .ConfigureServices(SetupServices);
        }

        private static void ConfigureWebHost(IWebHostBuilder webBuilder)
        {
            webBuilder.ConfigureServices((ctx, services) =>
            {
                var observabilityOptions =
                    ctx.Configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ??
                    new ObservabilityOptions();

                webBuilder.UseUrls(observabilityOptions.Urls);

                services.AddHealthChecks()
                    .AddCheck<LivenessHealthCheck>("live", tags: new[] { "live" })
                    .AddCheck<ReadinessHealthCheck>("ready", tags: new[] { "ready" });

                services.AddOpenTelemetry()
                    .WithMetrics(metrics =>
                    {
                        metrics
                            .AddRuntimeInstrumentation()
                            .AddAspNetCoreInstrumentation()
                            .AddHttpClientInstrumentation()
                            .AddPrometheusExporter();
                    });
            });

            webBuilder.Configure((ctx, app) =>
            {
                var observabilityOptions =
                    ctx.Configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ??
                    new ObservabilityOptions();

                app.UseRouting();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapHealthChecks(observabilityOptions.LivenessPath, new HealthCheckOptions
                    {
                        Predicate = check => check.Tags.Contains("live")
                    });

                    endpoints.MapHealthChecks(observabilityOptions.ReadinessPath, new HealthCheckOptions
                    {
                        Predicate = check => check.Tags.Contains("ready")
                    });

                    endpoints.MapPrometheusScrapingEndpoint(observabilityOptions.MetricsPath);
                });
            });
        }

        private static void SetupConfiguration(HostBuilderContext ctx, IConfigurationBuilder builder, string[] args)
        {
            builder.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
            builder.AddJsonFile(args.Length >= 1 ? args[0] : "config/appsettings.json", optional: false,
                reloadOnChange: true);
        }

        private static void SetupLogging(HostBuilderContext ctx, ILoggingBuilder logging)
        {
            logging.ClearProviders();
            logging.AddConsole(options => { options.FormatterName = "customlogsformatter"; })
                   .AddConsoleFormatter<CustomLogsFormatter, ConsoleFormatterOptions>();

            logging.AddFilter("Microsoft", LogLevel.Information);
            logging.AddFilter("Microsoft.Hosting", LogLevel.Warning);
            logging.AddFilter("System", LogLevel.Information);
            logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        }

        private static void SetupServices(HostBuilderContext ctx, IServiceCollection services)
        {
            using var startupLoggerFactory = LoggerFactory.Create(logging => SetupLogging(ctx, logging));
            var startupLogger = startupLoggerFactory.CreateLogger<Startup>();

            var appOptions = ctx.Configuration.GetSection(AppOptions.SectionName).Get<AppOptions>() ?? new AppOptions();

            var appOptionsContext = new ValidationContext(appOptions, serviceProvider: null, items: null);
            var appOptionsValidationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(appOptions, appOptionsContext, appOptionsValidationResults, validateAllProperties: true))
            {
                var appErrors = string.Join("\n", appOptionsValidationResults.Select(r => r.ErrorMessage));
                throw new InvalidOperationException($"Invalid app configuration: {appErrors}");
            }

            var httpOptions = ctx.Configuration.GetSection(HttpOptions.SectionName).Get<HttpOptions>() ?? new HttpOptions();

            var httpOptionsContext = new ValidationContext(httpOptions, serviceProvider: null, items: null);
            var httpOptionsValidationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(httpOptions, httpOptionsContext, httpOptionsValidationResults, validateAllProperties: true))
            {
                var httpErrors = string.Join("\n", httpOptionsValidationResults.Select(r => r.ErrorMessage));
                throw new InvalidOperationException($"Invalid HTTP configuration: {httpErrors}");
            }

            var observabilityOptions =
                ctx.Configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ??
                new ObservabilityOptions();

            var observabilityContext = new ValidationContext(observabilityOptions, serviceProvider: null, items: null);
            var observabilityValidationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(observabilityOptions, observabilityContext, observabilityValidationResults, validateAllProperties: true))
            {
                var observabilityErrors = string.Join("\n", observabilityValidationResults.Select(r => r.ErrorMessage));
                throw new InvalidOperationException($"Invalid observability configuration: {observabilityErrors}");
            }

            var fallbackUserAgents = httpOptions.FallbackUserAgents;

            services.AddHttpClient("Default", httpClient =>
            {
                httpClient.Timeout = TimeSpan.FromSeconds(httpOptions.TimeoutSeconds);
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(httpOptions.DefaultUserAgent);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            })
            .AddStandardResilienceHandler();

            var concurrentRequests = appOptions.ConcurrentRequests;

            if (concurrentRequests != 20)
            {
                startupLogger.LogInformation("Blanket Concurrent Requests set to: {ConcurrentRequests}",
                    concurrentRequests);
            }

            services.AddSingleton(new SemaphoreSlim(concurrentRequests));

            services.AddSingleton<IBatchLogger, BatchLogger>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<BatchLogger>>();
                return new BatchLogger(logger);
            });

            services.AddTransient<ICustomHttpClient, CustomHttpClient>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient("Default");
                var logger = sp.GetRequiredService<ILogger<CustomHttpClient>>();
                var throttle = sp.GetRequiredService<SemaphoreSlim>();

                return new CustomHttpClient(
                    logger,
                    httpClient,
                    throttle,
                    fallbackUserAgents,
                    httpOptions.PostMinIntervalSeconds);
            });

            services.AddTransient<ILogAggregatorFactory, LogAggregatorFactory>();
            services.AddTransient<IFeedWorkerFactory, FeedWorkerFactory>();
            services.AddTransient<IFeedManagerFactory, FeedManagerFactory>();
            services.AddTransient<INotifierFactory, NotifierFactory>();
            services.AddTransient<IDiscordPayloadServiceFactory, DiscordPayloadServiceFactory>();
            services.AddTransient<IRssParsingService, RssParsingService>();
            services.AddTransient<IImageParserService, ImageParserService>();
            services.AddTransient<IYoutubeParsingService, YoutubeParsingService>();
            services.AddTransient<IDiscordPayloadService, DiscordPayloadService>();
            services.AddSingleton<IReferencePostStore>(sp =>
            {
                var path = Path.Combine(AppContext.BaseDirectory, "feed_dump.json");
                var logger = sp.GetRequiredService<ILogger<JsonReferencePostStore>>();
                return new JsonReferencePostStore(path, logger);
            });

            var configs = ctx.Configuration.GetSection("Instances")
                .Get<List<Config>>() ?? new List<Config>();

            startupLogger.LogInformation("Number of configurations loaded: {ConfigCount}", configs.Count);

            // Resolve webhook URLs from environment variables if prefixed with "env:"
            WebhookResolver.ResolveWebhooks(configs, msg => startupLogger.LogInformation("{Message}", msg));

            foreach (var c in configs)
            {
                startupLogger.LogInformation("Validating & Registering Background Service {ServiceId}", c.Id);

                ValidateConfiguration(c);

                services.AddSingleton<IHostedService>(sp =>
                {
                    var feedManagerFactory = sp.GetRequiredService<IFeedManagerFactory>();
                    var feedWorkerFactory = sp.GetRequiredService<IFeedWorkerFactory>();
                    var notifierFactory = sp.GetRequiredService<INotifierFactory>();
                    var discordPayloadServiceFactory = sp.GetRequiredService<IDiscordPayloadServiceFactory>();
                    var logAggregatorFactory = sp.GetRequiredService<ILogAggregatorFactory>();

                    var logAggregator = logAggregatorFactory.Create(c);
                    var feedManager = feedManagerFactory.Create(c, logAggregator);
                    var discordPayloadService = discordPayloadServiceFactory.Create(c);
                    var notifier = notifierFactory.Create(c, discordPayloadService);

                    return feedWorkerFactory.Create(c, logAggregator, feedManager, notifier);
                });
            }
        }

        private static void ValidateConfiguration(Config config)
        {
            var context = new ValidationContext(config, serviceProvider: null, items: null);
            var results = new List<ValidationResult>();

            if (Validator.TryValidateObject(config, context, results, validateAllProperties: true))
                return;

            var errors = string.Join("\n", results.Select(r => r.ErrorMessage));
            throw new InvalidOperationException($"Invalid config entry: {errors}");
        }
    }
}


