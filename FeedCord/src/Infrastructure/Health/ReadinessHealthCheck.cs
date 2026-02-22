using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FeedCord.Infrastructure.Health
{
  public class ReadinessHealthCheck : IHealthCheck
  {
    private readonly IHostApplicationLifetime _applicationLifetime;

    public ReadinessHealthCheck(IHostApplicationLifetime applicationLifetime)
    {
      _applicationLifetime = applicationLifetime;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
      if (_applicationLifetime.ApplicationStarted.IsCancellationRequested)
      {
        return Task.FromResult(HealthCheckResult.Healthy("Service startup completed."));
      }

      return Task.FromResult(HealthCheckResult.Unhealthy("Service is still starting."));
    }
  }
}
