using FeedCord.Infrastructure.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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
