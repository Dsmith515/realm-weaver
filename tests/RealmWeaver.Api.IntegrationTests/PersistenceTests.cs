using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

using RealmWeaver.Api.IntegrationTests.Infrastructure;
using RealmWeaver.Modules.Catalog;
using RealmWeaver.Modules.Inventory;
using RealmWeaver.Modules.Realms;
using RealmWeaver.Modules.Rituals;

namespace RealmWeaver.Api.IntegrationTests;

/// <summary>Checks the module persistence rules from ADR 0004 against the real model and database.</summary>
[Collection(PostgresApiTests.Name)]
public class PersistenceTests(RealmWeaverApiFactory factory)
{
    private static readonly Assembly[] ModuleAssemblies = [typeof(CatalogModule).Assembly, typeof(InventoryModule).Assembly, typeof(RealmsModule).Assembly, typeof(RitualsModule).Assembly];

    private static IEnumerable<Type> DbContextTypes() =>
        ModuleAssemblies.SelectMany(a => a.GetTypes()).Where(t => t.IsSubclassOf(typeof(DbContext)));

    [Fact]
    public void Every_module_has_exactly_one_DbContext()
    {
        Assert.All(ModuleAssemblies, a => Assert.Single(a.GetTypes(), t => t.IsSubclassOf(typeof(DbContext))));
    }

    [Fact]
    public void No_model_change_is_missing_a_migration()
    {
        using var scope = factory.Services.CreateScope();
        foreach (var contextType in DbContextTypes())
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
            Assert.False(context.Database.HasPendingModelChanges(), $"{contextType.Name} has model changes without a migration. Run dotnet ef migrations add.");
        }
    }

    [Fact]
    public void Each_module_keeps_its_tables_in_its_own_schema()
    {
        using var scope = factory.Services.CreateScope();
        var schemasByContext = new Dictionary<string, string?>();
        foreach (var contextType in DbContextTypes())
        {
            var model = ((DbContext)scope.ServiceProvider.GetRequiredService(contextType)).Model;
            var schema = model.GetDefaultSchema();
            Assert.False(string.IsNullOrEmpty(schema));
            Assert.All(
                model.GetEntityTypes().Where(e => e.GetTableName() is not null),
                e => Assert.Equal(schema, e.GetSchema()));
            schemasByContext[contextType.Name] = schema;
        }

        Assert.Equal(schemasByContext.Count, schemasByContext.Values.Distinct().Count());
    }

    [Fact]
    public void Entities_and_DbContexts_are_not_visible_to_other_modules()
    {
        var persistenceTypes = ModuleAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Namespace?.Contains(".Persistence", StringComparison.Ordinal) == true)
            .Where(t => !t.IsSubclassOf(typeof(Migration)) && !t.Name.EndsWith("ModelSnapshot", StringComparison.Ordinal))
            .Where(t => !t.IsNested && !t.Name.StartsWith('<'));

        Assert.All(persistenceTypes, t => Assert.False(t.IsPublic, $"{t.FullName} should be internal."));
    }

    [Fact]
    public async Task Inventory_rows_use_xmin_for_optimistic_concurrency()
    {
        // Two requests load the same stack; the second save must fail rather than overwrite the first.
        var inventoryContext = DbContextTypes().Single(t => t.Name == "InventoryDbContext");
        await using var scope1 = factory.Services.CreateAsyncScope();
        await using var scope2 = factory.Services.CreateAsyncScope();
        var first = (DbContext)scope1.ServiceProvider.GetRequiredService(inventoryContext);
        var second = (DbContext)scope2.ServiceProvider.GetRequiredService(inventoryContext);
        var itemType = first.Model.GetEntityTypes().Single(e => e.ClrType.Name == "InventoryItem");

        var key = await FirstItemKeyAsync(first, itemType.ClrType);
        var mine = await first.FindAsync(itemType.ClrType, key);
        var theirs = await second.FindAsync(itemType.ClrType, key);
        Assert.NotNull(mine);
        Assert.NotNull(theirs);

        first.Entry(mine).Property("Quantity").CurrentValue = (int)first.Entry(mine).Property("Quantity").CurrentValue! + 1;
        await first.SaveChangesAsync();

        second.Entry(theirs).Property("Quantity").CurrentValue = 0;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    private static async Task<object[]> FirstItemKeyAsync(DbContext context, Type itemType)
    {
        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT player_id, component_definition_id FROM inventory.inventory_items LIMIT 1";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), $"No {itemType.Name} rows to test with.");
            return [reader.GetGuid(0), reader.GetGuid(1)];
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
