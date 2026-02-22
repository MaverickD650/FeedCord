using FeedCord.Infrastructure.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FeedCord.Tests.Infrastructure;

public class LivenessHealthCheckTests
{
  [Fact]
  public async Task CheckHealthAsync_ReturnsHealthy()
  {
    var check = new LivenessHealthCheck();
    var context = new HealthCheckContext();

    var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

    Assert.Equal(HealthStatus.Healthy, result.Status);
  }
}

public class ReadinessHealthCheckTests
{
  [Fact]
  public async Task CheckHealthAsync_WhenApplicationNotStarted_ReturnsUnhealthy()
  {
    using var startedCts = new CancellationTokenSource();
    var lifetime = new TestHostApplicationLifetime(startedCts.Token);
    var check = new ReadinessHealthCheck(lifetime);
    var context = new HealthCheckContext();

    var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

    Assert.Equal(HealthStatus.Unhealthy, result.Status);
  }

  [Fact]
  public async Task CheckHealthAsync_WhenApplicationStarted_ReturnsHealthy()
  {
    using var startedCts = new CancellationTokenSource();
    startedCts.Cancel();

    var lifetime = new TestHostApplicationLifetime(startedCts.Token);
    var check = new ReadinessHealthCheck(lifetime);
    var context = new HealthCheckContext();

    var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

    Assert.Equal(HealthStatus.Healthy, result.Status);
  }

  private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
  {
    public TestHostApplicationLifetime(CancellationToken applicationStarted)
    {
      ApplicationStarted = applicationStarted;
    }

    public CancellationToken ApplicationStarted { get; }

    public CancellationToken ApplicationStopping { get; } = CancellationToken.None;

    public CancellationToken ApplicationStopped { get; } = CancellationToken.None;

    public void StopApplication()
    {
    }
  }
}
