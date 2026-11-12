using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using RealmWeaver.Infrastructure.Persistence;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Catalog.Endpoints;
using RealmWeaver.Modules.Catalog.Persistence;

namespace RealmWeaver.Modules.Catalog;

/// <summary>Component definitions and the global self-assign setting. Owns the <c>catalog</c> schema.</summary>
public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<CatalogDbContext>(CatalogDbContext.Schema);
        services.AddScoped<CatalogService>();
        services.AddScoped<ICatalogModule>(sp => sp.GetRequiredService<CatalogService>());
        services.AddScoped<IModuleDatabaseInitializer, CatalogDatabaseInitializer>();
        return services;
    }

    public static IEndpointRouteBuilder MapCatalogModule(this IEndpointRouteBuilder app)
    {
        CatalogEndpoints.Map(app);
        return app;
    }
}
