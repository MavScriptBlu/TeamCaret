using CCOS.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CCOS.Api.Health;

/// <summary>
/// The API's health check (CAR-84): healthy when the database can be reached. GET /health answers
/// "Healthy" (200) or "Unhealthy" (503), so a host can tell whether the API is up and working.
/// </summary>
public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    /// <summary>
    /// Tries to open a connection to the database. CanConnectAsync returns false instead of throwing
    /// when the database can't be reached.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy("The database can be reached.")
            : HealthCheckResult.Unhealthy("The database can't be reached.");
    }
}
