using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

namespace RealmWeaver.Infrastructure.Health;

/// <summary>Readiness: can we reach Postgres and run a trivial query?</summary>
public sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (NpgsqlException exception)
        {
            return HealthCheckResult.Unhealthy("Postgres is unreachable.", exception);
        }
    }
}
