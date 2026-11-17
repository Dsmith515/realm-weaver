using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

using RealmWeaver.Infrastructure.Health;

namespace RealmWeaver.Api.IntegrationTests;

public class PostgresHealthCheckTests
{
    [Fact]
    public async Task Check_is_unhealthy_when_postgres_is_unreachable()
    {
        // Port 1: nothing listens there, so the connection is refused.
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=x;Password=x;Timeout=2");
        var check = new PostgresHealthCheck(dataSource);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
