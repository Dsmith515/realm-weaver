using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using RealmWeaver.Infrastructure.Persistence;
using RealmWeaver.Modules.Rituals.Endpoints;
using RealmWeaver.Modules.Rituals.Persistence;

namespace RealmWeaver.Modules.Rituals;

/// <summary>Ritual preview and the ritual transaction. Owns the <c>rituals</c> schema.</summary>
public static class RitualsModule
{
    public static IServiceCollection AddRitualsModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<RitualsDbContext>(RitualsDbContext.Schema);
        services.AddScoped(sp => ActivatorUtilities.CreateInstance<RitualResolver>(sp, Random.Shared));
        services.AddScoped<RitualService>();
        services.AddScoped<IModuleDatabaseInitializer, RitualsDatabaseInitializer>();
        return services;
    }

    public static IEndpointRouteBuilder MapRitualsModule(this IEndpointRouteBuilder app)
    {
        RitualsEndpoints.Map(app);
        return app;
    }
}

internal sealed class RitualsDatabaseInitializer(RitualsDbContext db) : IModuleDatabaseInitializer
{
    public int Order => 400;

    public Task InitializeAsync(CancellationToken cancellationToken) => db.Database.MigrateAsync(cancellationToken);
}
