using Microsoft.Extensions.Diagnostics.HealthChecks;
using SafyaClinic.Infrastructure.Data;

namespace SafyaClinic.Web.Startup;

/// <summary>Healthy when the database answers (F3a). Details are logged, never shown.</summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly SafyaDbContext _db;

    public DatabaseHealthCheck(SafyaDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database reachable.")
                : HealthCheckResult.Unhealthy("Database not reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database not reachable.", ex);
        }
    }
}