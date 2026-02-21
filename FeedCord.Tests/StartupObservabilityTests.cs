using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FeedCord.Tests;

public class StartupObservabilityTests
{
    [Fact]
    public async Task CreateHostBuilder_WithoutObservabilitySection_UsesDefaultObservabilityEndpoints()
    {
        var port = GetFreeTcpPort();
        var fallbackUrls = $"http://127.0.0.1:{port}";
        var tempConfigPath = Path.Combine(Path.GetTempPath(), $"feedcord-observability-defaults-{Guid.NewGuid():N}.json");

        var config = new
        {
            Instances = Array.Empty<object>()
        };

        await File.WriteAllTextAsync(tempConfigPath, JsonSerializer.Serialize(config));

        IHost? host = null;
        try
        {
            var hostBuilder = (IHostBuilder)InvokeStartupPrivateMethod("CreateHostBuilder", (object)new[] { tempConfigPath })!;
            hostBuilder.ConfigureWebHost(webBuilder => webBuilder.UseUrls(fallbackUrls));

            host = hostBuilder.Build();
            await host.StartAsync();

            using var httpClient = new HttpClient { BaseAddress = new Uri(fallbackUrls) };

            var liveness = await httpClient.GetAsync("/health/live");
            var readiness = await httpClient.GetAsync("/health/ready");
            var metrics = await httpClient.GetAsync("/metrics");

            Assert.True(liveness.IsSuccessStatusCode, "Default liveness endpoint should return success.");
            Assert.True(readiness.IsSuccessStatusCode, "Default readiness endpoint should return success.");
            Assert.True(metrics.IsSuccessStatusCode, "Default metrics endpoint should return success.");
        }
        finally
        {
            if (host is not null)
            {
                await host.StopAsync();
                host.Dispose();
            }

            if (File.Exists(tempConfigPath))
            {
                File.Delete(tempConfigPath);
            }
        }
    }

    [Fact]
    public async Task CreateHostBuilder_WithObservabilityEndpoints_ExposesMetricsAndHealthPaths()
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

        await File.WriteAllTextAsync(tempConfigPath, JsonSerializer.Serialize(config));

        IHost? host = null;
        try
        {
            var hostBuilder = (IHostBuilder)InvokeStartupPrivateMethod("CreateHostBuilder", (object)new[] { tempConfigPath })!;
            host = hostBuilder.Build();

            await host.StartAsync();

            using var httpClient = new HttpClient { BaseAddress = new Uri(observabilityUrls) };

            var liveness = await httpClient.GetAsync("/health/live-test");
            var readiness = await httpClient.GetAsync("/health/ready-test");
            var metrics = await httpClient.GetAsync("/metrics-test");

            Assert.True(liveness.IsSuccessStatusCode, "Liveness endpoint should return success.");
            Assert.True(readiness.IsSuccessStatusCode, "Readiness endpoint should return success.");
            Assert.True(metrics.IsSuccessStatusCode, "Metrics endpoint should return success.");
        }
        finally
        {
            if (host is not null)
            {
                await host.StopAsync();
                host.Dispose();
            }

            if (File.Exists(tempConfigPath))
            {
                File.Delete(tempConfigPath);
            }
        }
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

    private static int GetFreeTcpPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }
}
