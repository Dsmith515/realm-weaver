using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using RealmWeaver.Infrastructure.Persistence;
using RealmWeaver.Modules.Realms.Contracts;
using RealmWeaver.Modules.Realms.Endpoints;
using RealmWeaver.Modules.Realms.Persistence;

namespace RealmWeaver.Modules.Realms;

/// <summary>Generated realms and their exports. Owns the <c>realms</c> schema.</summary>
public static class RealmsModule
{
    public static IServiceCollection AddRealmsModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<RealmsDbContext>(RealmsDbContext.Schema);
        services.AddScoped<RealmsService>();
        services.AddScoped<IRealmsModule>(sp => sp.GetRequiredService<RealmsService>());
        services.AddScoped<IModuleDatabaseInitializer, RealmsDatabaseInitializer>();
        return services;
    }

    public static IEndpointRouteBuilder MapRealmsModule(this IEndpointRouteBuilder app)
    {
        RealmsEndpoints.Map(app);
        return app;
    }
}

internal sealed class RealmsDatabaseInitializer(RealmsDbContext db) : IModuleDatabaseInitializer
{
    public int Order => 300;

    public Task InitializeAsync(CancellationToken cancellationToken) => db.Database.MigrateAsync(cancellationToken);
}
