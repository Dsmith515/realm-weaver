using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using RealmWeaver.Infrastructure.Persistence;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Inventory.Endpoints;
using RealmWeaver.Modules.Inventory.Persistence;

namespace RealmWeaver.Modules.Inventory;

/// <summary>Players and the components they own. Owns the <c>inventory</c> schema.</summary>
public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<InventoryDbContext>(InventoryDbContext.Schema);
        services.AddSingleton(_ => new StarterKitPicker(Random.Shared));
        services.AddScoped<InventoryService>();
        services.AddScoped<IInventoryModule>(sp => sp.GetRequiredService<InventoryService>());
        services.AddScoped<IModuleDatabaseInitializer, InventoryDatabaseInitializer>();
        return services;
    }

    public static IEndpointRouteBuilder MapInventoryModule(this IEndpointRouteBuilder app)
    {
        InventoryEndpoints.Map(app);
        return app;
    }
}

/// <summary>Runs after the catalog is seeded, because the demo player's starter kit comes from it.</summary>
internal sealed class InventoryDatabaseInitializer(InventoryDbContext db, InventoryService inventory) : IModuleDatabaseInitializer
{
    public const string DemoPlayerName = "Wanderer";

    public int Order => 200;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await db.Database.MigrateAsync(cancellationToken);

        if (!await db.Players.AnyAsync(cancellationToken))
        {
            await inventory.CreatePlayerAsync(DemoPlayerName, cancellationToken);
        }
    }
}
