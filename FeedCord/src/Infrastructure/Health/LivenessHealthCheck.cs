using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FeedCord.Infrastructure.Health
{
  public class LivenessHealthCheck : IHealthCheck
  {
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
      return Task.FromResult(HealthCheckResult.Healthy("Service is running."));
    }
  }
}
