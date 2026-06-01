using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Api.Health;

/// <summary>
/// Readiness probe: confirms the database is reachable with one cheap EXISTS query. Tagged
/// "ready" so it runs on <c>/health/ready</c> (deep) but never on <c>/health</c> (liveness) —
/// keeping liveness dependency-free so a slow or still-booting database can't trip a restart
/// loop in the orchestrator.
/// </summary>
internal sealed class DatabaseHealthCheck(IApplicationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await db.Companies.AnyAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database is not reachable.", ex);
        }
    }
}
