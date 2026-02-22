using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FeedCord.Tests;

public class StartupObservabilityTests
{
  [Theory(Timeout = 10000)]
  [InlineData("default-json", "/health/live", "/health/ready", "/metrics")]
  [InlineData("custom-json", "/health/live-test", "/health/ready-test", "/metrics-test")]
  [InlineData("custom-yaml", "/health/live-yaml", "/health/ready-yaml", "/metrics-yaml")]
  public async Task CreateApplication_ExposesExpectedObservabilityEndpoints(
      string configVariant,
      string expectedLivenessPath,
      string expectedReadinessPath,
      string expectedMetricsPath)
  {
    var port = GetFreeTcpPort();
    var observabilityUrls = $"http://127.0.0.1:{port}";
    var extension = configVariant == "custom-yaml" ? "yaml" : "json";
    var tempConfigPath = Path.Combine(Path.GetTempPath(), $"feedcord-observability-{configVariant}-{Guid.NewGuid():N}.{extension}");

    if (configVariant == "default-json")
    {
      var config = new
      {
        Instances = Array.Empty<object>(),
        Observability = new
        {
          Urls = observabilityUrls,
        }
      };

      await File.WriteAllTextAsync(tempConfigPath, JsonSerializer.Serialize(config), TestContext.Current.CancellationToken);
    }
    else if (configVariant == "custom-json")
    {
      var config = new
      {
        Instances = Array.Empty<object>(),
        Observability = new
        {
          Urls = observabilityUrls,
          MetricsPath = expectedMetricsPath,
          LivenessPath = expectedLivenessPath,
          ReadinessPath = expectedReadinessPath,
        }
      };

      await File.WriteAllTextAsync(tempConfigPath, JsonSerializer.Serialize(config), TestContext.Current.CancellationToken);
    }
    else
    {
      var yamlConfig = $"""
Instances: []
Observability:
  Urls: {observabilityUrls}
  MetricsPath: {expectedMetricsPath}
  LivenessPath: {expectedLivenessPath}
  ReadinessPath: {expectedReadinessPath}
""";

      await File.WriteAllTextAsync(tempConfigPath, yamlConfig, TestContext.Current.CancellationToken);
    }

    IHost? host = null;
    try
    {
      host = Startup.CreateApplication(new[] { tempConfigPath });
      await host.StartAsync(TestContext.Current.CancellationToken);

      using var httpClient = new HttpClient { BaseAddress = new Uri(observabilityUrls) };

      var liveness = await httpClient.GetAsync(expectedLivenessPath, TestContext.Current.CancellationToken);
      var readiness = await httpClient.GetAsync(expectedReadinessPath, TestContext.Current.CancellationToken);
      var metrics = await httpClient.GetAsync(expectedMetricsPath, TestContext.Current.CancellationToken);

      Assert.Multiple(
        () => Assert.True(liveness.IsSuccessStatusCode, $"Liveness endpoint '{expectedLivenessPath}' should return success."),
        () => Assert.True(readiness.IsSuccessStatusCode, $"Readiness endpoint '{expectedReadinessPath}' should return success."),
        () => Assert.True(metrics.IsSuccessStatusCode, $"Metrics endpoint '{expectedMetricsPath}' should return success.")
      );
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
