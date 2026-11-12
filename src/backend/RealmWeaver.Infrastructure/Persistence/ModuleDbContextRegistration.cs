using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RealmWeaver.Infrastructure.Persistence;

/// <summary>
/// Every module owns its own DbContext, Postgres schema and migration history (see ADR 0004).
/// In Phase 1 they all point at the same database, so a ritual can still run in one transaction.
/// </summary>
public static class ModuleDbContextRegistration
{
    public const string ConnectionStringName = "RealmWeaver";

    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : DbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        // The connection string is read when the context is first built, not at registration time,
        // so test hosts can override it after Program.cs has run.
        services.AddDbContext<TContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

            options
                .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
                .UseSnakeCaseNamingConvention();
        });

        return services;
    }
}
