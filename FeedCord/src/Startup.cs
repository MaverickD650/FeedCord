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
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using System.Net;

namespace FeedCord
{
  public class Startup
  {
    internal static Func<string[], IHost> BuildHost { get; set; } = CreateApplication;
    internal static Action<IHost> RunHost { get; set; } = host => host.Run();

    public static void Initialize(string[] args)
    {
      var host = BuildHost(args);
      RunHost(host);
    }

    internal static IHost CreateApplication(string[] args)
    {
      var builder = WebApplication.CreateBuilder(args);
      SetupConfiguration(builder.Configuration, args);

      var hostBuilderContext = CreateHostBuilderContext(builder.Configuration);
      SetupLogging(hostBuilderContext, builder.Logging);
      SetupServices(hostBuilderContext, builder.Services);
      ConfigureObservability(builder);

      var app = builder.Build();
      MapObservabilityEndpoints(app);

      return app;
    }

    internal static HostBuilderContext CreateHostBuilderContext(IConfiguration configuration)
    {
      return new HostBuilderContext(new Dictionary<object, object>())
      {
        Configuration = configuration
      };
    }

    private static void ConfigureObservability(WebApplicationBuilder builder)
    {
      var observabilityOptions =
          builder.Configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ??
          new ObservabilityOptions();

      builder.WebHost.UseUrls(observabilityOptions.Urls);

      builder.Services.AddHealthChecks()
          .AddCheck<LivenessHealthCheck>("live", tags: new[] { "live" })
          .AddCheck<ReadinessHealthCheck>("ready", tags: new[] { "ready" });

      builder.Services.AddOpenTelemetry()
          .WithMetrics(metrics =>
          {
            metrics
                      .AddRuntimeInstrumentation()
                      .AddAspNetCoreInstrumentation()
                      .AddHttpClientInstrumentation()
                      .AddPrometheusExporter();
          });
    }

    private static void MapObservabilityEndpoints(WebApplication app)
    {
      var observabilityOptions =
          app.Configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ??
          new ObservabilityOptions();

      app.MapHealthChecks(observabilityOptions.LivenessPath, new HealthCheckOptions
      {
        Predicate = check => check.Tags.Contains("live")
      });

      app.MapHealthChecks(observabilityOptions.ReadinessPath, new HealthCheckOptions
      {
        Predicate = check => check.Tags.Contains("ready")
      });

      app.MapPrometheusScrapingEndpoint(observabilityOptions.MetricsPath);
    }

    internal static void SetupConfiguration(ConfigurationManager builder, string[] args)
    {
      builder.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
      AddConfigurationFile(builder, SelectConfigPath(args));
    }

    internal static void SetupConfiguration(HostBuilderContext ctx, IConfigurationBuilder builder, string[] args)
    {
      builder.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
      AddConfigurationFile(builder, SelectConfigPath(args));
    }

    internal static void AddConfigurationFile(IConfigurationBuilder builder, string configPath)
    {
      var extension = Path.GetExtension(configPath);

      if (string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(extension, ".yml", StringComparison.OrdinalIgnoreCase))
      {
        builder.AddYamlFile(configPath, optional: false, reloadOnChange: true);
        return;
      }

      if (string.IsNullOrWhiteSpace(extension) ||
          string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
      {
        builder.AddJsonFile(configPath, optional: false, reloadOnChange: true);
        return;
      }

      throw new InvalidOperationException(
          $"Unsupported configuration file extension '{extension}'. Supported extensions are: .json, .yaml, .yml.");
    }

    internal static string SelectConfigPath(string[] args)
    {
      return args.Length >= 1 ? args[0] : "config/appsettings.json";
    }

    internal static void SetupLogging(HostBuilderContext ctx, ILoggingBuilder logging)
    {
      logging.ClearProviders();
      logging.AddConsole(options => { options.FormatterName = "customlogsformatter"; })
             .AddConsoleFormatter<CustomLogsFormatter, ConsoleFormatterOptions>();

      logging.AddFilter("Microsoft", LogLevel.Information);
      logging.AddFilter("Microsoft.Hosting", LogLevel.Warning);
      logging.AddFilter("System", LogLevel.Information);
      logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
    }

    internal static void SetupServices(HostBuilderContext ctx, IServiceCollection services)
    {
      using var startupLoggerFactory = LoggerFactory.Create(logging => SetupLogging(ctx, logging));
      var startupLogger = startupLoggerFactory.CreateLogger<Startup>();

      var appOptions = GetValidatedOptions<AppOptions>(
          services,
          ctx.Configuration,
          AppOptions.SectionName,
          "Invalid app configuration");

      var httpOptions = GetValidatedOptions<HttpOptions>(
          services,
          ctx.Configuration,
          HttpOptions.SectionName,
          "Invalid HTTP configuration");

      _ = GetValidatedOptions<ObservabilityOptions>(
          services,
          ctx.Configuration,
          ObservabilityOptions.SectionName,
          "Invalid observability configuration");

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

    internal static void ValidateConfiguration(Config config)
    {
      var validator = new DataAnnotationValidateOptions<Config>(Options.DefaultName);
      var validationResult = validator.Validate(Options.DefaultName, config);

      if (validationResult.Succeeded)
      {
        return;
      }

      var errors = string.Join("\n", validationResult.Failures!);
      throw new InvalidOperationException($"Invalid config entry: {errors}");
    }

    private static TOptions GetValidatedOptions<TOptions>(
        IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        string validationErrorPrefix)
        where TOptions : class, new()
    {
      services.AddOptions<TOptions>()
          .Bind(configuration.GetSection(sectionName))
          .ValidateDataAnnotations()
          .ValidateOnStart();

      var options = configuration.GetSection(sectionName).Get<TOptions>() ?? new TOptions();
      var validator = new DataAnnotationValidateOptions<TOptions>(Options.DefaultName);
      var validationResult = validator.Validate(Options.DefaultName, options);

      if (validationResult.Succeeded)
      {
        return options;
      }

      var errors = string.Join("\n", validationResult.Failures!);
      throw new InvalidOperationException($"{validationErrorPrefix}: {errors}");
    }
  }
}


