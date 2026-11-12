using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Npgsql;

namespace RealmWeaver.Infrastructure.Persistence;

/// <summary>
/// Every module owns its own DbContext, Postgres schema and migration history (see ADR 0004).
/// In Phase 1 they all point at the same database, and within one request (DI scope) they share one
/// connection, so <see cref="UnitOfWork"/> can run a ritual across modules in one transaction (ADR 0005).
/// </summary>
public static class ModuleDbContextRegistration
{
    public const string ConnectionStringName = "RealmWeaver";

    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : DbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        // The connection string is read when the data source is first built, not at registration time,
        // so test hosts can override it after Program.cs has run.
        services.TryAddSingleton(provider =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            return NpgsqlDataSource.Create(connectionString);
        });
        services.TryAddScoped(provider => provider.GetRequiredService<NpgsqlDataSource>().CreateConnection());
        services.TryAddScoped<UnitOfWork>();

        services.AddDbContextFactory<TContext>(
            (provider, options) => options
                .UseNpgsql(provider.GetRequiredService<NpgsqlConnection>(), npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
                .UseSnakeCaseNamingConvention(),
            ServiceLifetime.Scoped);

        // Contexts are created through the factory and attached to the scope's unit of work, so they join
        // its transaction whether they were created before or during it. DI disposes them with the scope.
        services.AddScoped(provider =>
        {
            var context = provider.GetRequiredService<IDbContextFactory<TContext>>().CreateDbContext();
            provider.GetRequiredService<UnitOfWork>().Attach(context);
            return context;
        });

        return services;
    }
}
