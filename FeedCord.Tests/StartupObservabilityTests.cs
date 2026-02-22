using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FeedCord.Tests;

public class StartupObservabilityTests
{
  [Fact]
  public async Task CreateApplication_WithoutObservabilityPaths_UsesDefaultObservabilityEndpoints()
  {
    var port = GetFreeTcpPort();
    var fallbackUrls = $"http://127.0.0.1:{port}";
    var tempConfigPath = Path.Combine(Path.GetTempPath(), $"feedcord-observability-defaults-{Guid.NewGuid():N}.json");

    var config = new
    {
      Instances = Array.Empty<object>(),
      Observability = new
      {
        Urls = fallbackUrls,
      }
    };

    await File.WriteAllTextAsync(tempConfigPath, JsonSerializer.Serialize(config), TestContext.Current.CancellationToken);

    IHost? host = null;
    try
    {
      host = Startup.CreateApplication(new[] { tempConfigPath });
      await host.StartAsync(TestContext.Current.CancellationToken);

      using var httpClient = new HttpClient { BaseAddress = new Uri(fallbackUrls) };

      var liveness = await httpClient.GetAsync("/health/live", TestContext.Current.CancellationToken);
      var readiness = await httpClient.GetAsync("/health/ready", TestContext.Current.CancellationToken);
      var metrics = await httpClient.GetAsync("/metrics", TestContext.Current.CancellationToken);

      Assert.True(liveness.IsSuccessStatusCode, "Default liveness endpoint should return success.");
      Assert.True(readiness.IsSuccessStatusCode, "Default readiness endpoint should return success.");
      Assert.True(metrics.IsSuccessStatusCode, "Default metrics endpoint should return success.");
    }
    finally
    {
      if (host is not null)
      {
        await host.StopAsync(TestContext.Current.CancellationToken);
        host.Dispose();
      }

      if (File.Exists(tempConfigPath))
      {
        File.Delete(tempConfigPath);
      }
    }
  }

  [Fact]
  public async Task CreateApplication_WithObservabilityEndpoints_ExposesMetricsAndHealthPaths()
  {
    var port = GetFreeTcpPort();
    var tempConfigPath = Path.Combine(Path.GetTempPath(), $"feedcord-observability-{Guid.NewGuid():N}.json");
    var observabilityUrls = $"http://127.0.0.1:{port}";

    var config = new
    {
      Instances = Array.Empty<object>(),
      Observability = new
      {
        Urls = observabilityUrls,
        MetricsPath = "/metrics-test",
        LivenessPath = "/health/live-test",
        ReadinessPath = "/health/ready-test",
      }
    };

    await File.WriteAllTextAsync(tempConfigPath, JsonSerializer.Serialize(config), TestContext.Current.CancellationToken);

    IHost? host = null;
    try
    {
      host = Startup.CreateApplication(new[] { tempConfigPath });

      await host.StartAsync(TestContext.Current.CancellationToken);

      using var httpClient = new HttpClient { BaseAddress = new Uri(observabilityUrls) };

      var liveness = await httpClient.GetAsync("/health/live-test", TestContext.Current.CancellationToken);
      var readiness = await httpClient.GetAsync("/health/ready-test", TestContext.Current.CancellationToken);
      var metrics = await httpClient.GetAsync("/metrics-test", TestContext.Current.CancellationToken);

      Assert.True(liveness.IsSuccessStatusCode, "Liveness endpoint should return success.");
      Assert.True(readiness.IsSuccessStatusCode, "Readiness endpoint should return success.");
      Assert.True(metrics.IsSuccessStatusCode, "Metrics endpoint should return success.");
    }
    finally
    {
      if (host is not null)
      {
        await host.StopAsync(TestContext.Current.CancellationToken);
        host.Dispose();
      }

      if (File.Exists(tempConfigPath))
      {
        File.Delete(tempConfigPath);
      }
    }
  }

  [Fact]
  public async Task CreateApplication_WithYamlConfig_ExposesMetricsAndHealthPaths()
  {
    var port = GetFreeTcpPort();
    var tempConfigPath = Path.Combine(Path.GetTempPath(), $"feedcord-observability-{Guid.NewGuid():N}.yaml");
    var observabilityUrls = $"http://127.0.0.1:{port}";

    var yamlConfig = $"""
Instances: []
Observability:
  Urls: {observabilityUrls}
  MetricsPath: /metrics-yaml
  LivenessPath: /health/live-yaml
  ReadinessPath: /health/ready-yaml
""";

    await File.WriteAllTextAsync(tempConfigPath, yamlConfig, TestContext.Current.CancellationToken);

    IHost? host = null;
    try
    {
      host = Startup.CreateApplication(new[] { tempConfigPath });

      await host.StartAsync(TestContext.Current.CancellationToken);

      using var httpClient = new HttpClient { BaseAddress = new Uri(observabilityUrls) };

      var liveness = await httpClient.GetAsync("/health/live-yaml", TestContext.Current.CancellationToken);
      var readiness = await httpClient.GetAsync("/health/ready-yaml", TestContext.Current.CancellationToken);
      var metrics = await httpClient.GetAsync("/metrics-yaml", TestContext.Current.CancellationToken);

      Assert.True(liveness.IsSuccessStatusCode, "YAML liveness endpoint should return success.");
      Assert.True(readiness.IsSuccessStatusCode, "YAML readiness endpoint should return success.");
      Assert.True(metrics.IsSuccessStatusCode, "YAML metrics endpoint should return success.");
    }
    finally
    {
      if (host is not null)
      {
        await host.StopAsync(TestContext.Current.CancellationToken);
        host.Dispose();
      }

      if (File.Exists(tempConfigPath))
      {
        File.Delete(tempConfigPath);
      }
    }
  }

  private static int GetFreeTcpPort()
  {
    using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    listener.Start();
    return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
  }
}
