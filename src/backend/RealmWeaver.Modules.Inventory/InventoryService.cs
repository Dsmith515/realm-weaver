using Microsoft.EntityFrameworkCore;

using RealmWeaver.Infrastructure.Persistence;

using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Inventory.Persistence;

namespace RealmWeaver.Modules.Inventory;

internal sealed class InventoryService(InventoryDbContext db, ICatalogModule catalog, TimeProvider clock, StarterKitPicker starterKit)
    : IInventoryModule
{
    public async Task<PlayerDto?> GetPlayerAsync(Guid playerId, CancellationToken cancellationToken = default) =>
        await db.Players.AsNoTracking()
            .Where(p => p.Id == playerId)
            .Select(p => new PlayerDto(p.Id, p.Name, p.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PlayerDto>> ListPlayersAsync(CancellationToken cancellationToken) =>
        await db.Players.AsNoTracking()
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Select(p => new PlayerDto(p.Id, p.Name, p.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InventoryItemDto>?> GetInventoryAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        if (!await db.Players.AnyAsync(p => p.Id == playerId, cancellationToken))
        {
            return null;
        }

        var items = await db.Items.AsNoTracking()
            .Where(i => i.PlayerId == playerId && i.Quantity > 0)
            .ToListAsync(cancellationToken);

        // Join in memory through the Catalog contract; this module can't query catalog tables.
        var components = await catalog.GetComponentsAsync([.. items.Select(i => i.ComponentDefinitionId)], cancellationToken);
        return [.. items
            .Where(i => components.ContainsKey(i.ComponentDefinitionId))
            .Select(i => new InventoryItemDto(components[i.ComponentDefinitionId], i.Quantity, i.AcquiredAt))
            .OrderBy(i => i.Component.Type)
            .ThenBy(i => i.Component.Name, StringComparer.Ordinal)];
    }

    /// <summary>Creates a player and gives them the starter kit in the same SaveChanges.</summary>
    public async Task<PlayerDto> CreatePlayerAsync(string name, CancellationToken cancellationToken)
    {
        var now = clock.GetDatabaseUtcNow();
        var player = new Player { Id = Guid.CreateVersion7(), Name = name.Trim(), CreatedAt = now };

        var candidates = await catalog.GetStarterKitCandidatesAsync(cancellationToken);
        foreach (var (componentId, quantity) in starterKit.Pick(candidates))
        {
            player.Items.Add(new InventoryItem { ComponentDefinitionId = componentId, Quantity = quantity, AcquiredAt = now });
        }

        db.Players.Add(player);
        await db.SaveChangesAsync(cancellationToken);
        return new PlayerDto(player.Id, player.Name, player.CreatedAt);
    }

    /// <summary>
    /// Adds a component to a player's inventory as one atomic upsert, so concurrent grants never lose an update.
    /// Consumables stack. Needles and Maps are kept at quantity 1, and granting one the player owns changes nothing.
    /// The caller has already checked that the player and component exist.
    /// </summary>
    public async Task<InventoryItemDto> GrantAsync(Guid playerId, ComponentDto component, int quantity = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);

        var now = clock.GetDatabaseUtcNow();
        if (component.IsConsumable)
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO inventory.inventory_items (player_id, component_definition_id, quantity, acquired_at)
                VALUES ({playerId}, {component.Id}, {quantity}, {now})
                ON CONFLICT (player_id, component_definition_id)
                DO UPDATE SET quantity = inventory_items.quantity + EXCLUDED.quantity
                """,
                cancellationToken);
        }
        else
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO inventory.inventory_items (player_id, component_definition_id, quantity, acquired_at)
                VALUES ({playerId}, {component.Id}, 1, {now})
                ON CONFLICT (player_id, component_definition_id)
                DO UPDATE SET quantity = 1 WHERE inventory_items.quantity = 0
                """,
                cancellationToken);
        }

        var item = await db.Items.AsNoTracking()
            .FirstAsync(i => i.PlayerId == playerId && i.ComponentDefinitionId == component.Id, cancellationToken);
        return new InventoryItemDto(component, item.Quantity, item.AcquiredAt);
    }

    public async Task SpendOneEachAsync(Guid playerId, IReadOnlyCollection<Guid> componentIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(componentIds);
        var ids = componentIds.Distinct().ToList();
        var items = await db.Items
            .Where(i => i.PlayerId == playerId && ids.Contains(i.ComponentDefinitionId))
            .ToListAsync(cancellationToken);

        var missing = ids.Where(id => items.FirstOrDefault(i => i.ComponentDefinitionId == id) is not { Quantity: >= 1 }).ToList();
        if (missing.Count > 0)
        {
            throw new InsufficientComponentsException(missing);
        }

        foreach (var item in items)
        {
            item.Quantity--;
        }

        // UPDATE ... WHERE xmin = <loaded>: a concurrent spend of the same stack makes this throw.
        await db.SaveChangesAsync(cancellationToken);
    }
}
