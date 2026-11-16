using Microsoft.EntityFrameworkCore;

using RealmWeaver.Infrastructure.Persistence;

using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Inventory.Persistence;

namespace RealmWeaver.Modules.Inventory;

internal sealed class InventoryService(InventoryDbContext db, ICatalogModule catalog, TimeProvider clock, StarterKitPicker starterKit)
    : IInventoryModule
{
    public async Task<PlayerDto?> GetPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        var player = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playerId, cancellationToken);
        return player is null ? null : ToDto(player);
    }

    public const int MaxPlayerList = 200;

    /// <summary>Players by name, optionally only those whose name contains <paramref name="search"/>, at most <paramref name="limit"/>.</summary>
    public async Task<IReadOnlyList<PlayerDto>> ListPlayersAsync(string? search, int limit, CancellationToken cancellationToken)
    {
        var list = await Matching(search).OrderBy(p => p.Name).ThenBy(p => p.Id).Take(Math.Clamp(limit, 1, MaxPlayerList)).ToListAsync(cancellationToken);
        return [.. list.Select(ToDto)];
    }

    public async Task<PlayerPage> ListPlayersPageAsync(string? search, int page, int pageSize, IReadOnlyCollection<Guid> excludeIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(excludeIds);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPlayerList);
        var excluded = excludeIds.ToList();
        var players = Matching(search).Where(p => !excluded.Contains(p.Id));
        var total = await players.CountAsync(cancellationToken);
        var list = await players.OrderBy(p => p.Name).ThenBy(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PlayerPage([.. list.Select(ToDto)], page, pageSize, total);
    }

    public async Task<IReadOnlySet<Guid>> HoldersAsync(Guid componentId, IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playerIds);
        var ids = playerIds.ToList();
        var holders = await db.Items
            .Where(i => i.ComponentDefinitionId == componentId && i.Quantity > 0 && ids.Contains(i.PlayerId))
            .Select(i => i.PlayerId)
            .ToListAsync(cancellationToken);
        return holders.ToHashSet();
    }

    /// <summary>Players whose name contains <paramref name="search"/>; <c>%</c> and <c>_</c> are literal.</summary>
    private IQueryable<Persistence.Player> Matching(string? search)
    {
        var players = db.Players.AsNoTracking();
        if (string.IsNullOrWhiteSpace(search))
        {
            return players;
        }

        // Npgsql's two-argument ILike disables the escape character, so it's passed explicitly.
        var escaped = search.Trim().Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal).Replace("_", @"\_", StringComparison.Ordinal);
        return players.Where(p => EF.Functions.ILike(p.Name, $"%{escaped}%", @"\"));
    }

    /// <summary>Attunes a needle the player owns, or clears the attunement when <paramref name="needleId"/> is null.</summary>
    public async Task<AttuneOutcome> AttuneAsync(Guid playerId, Guid? needleId, CancellationToken cancellationToken)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, cancellationToken);
        if (player is null)
        {
            return AttuneOutcome.PlayerNotFound;
        }

        if (needleId is { } id)
        {
            var owns = await db.Items.AnyAsync(i => i.PlayerId == playerId && i.ComponentDefinitionId == id && i.Quantity > 0, cancellationToken);
            if (!owns || await catalog.GetComponentAsync(id, cancellationToken) is not { Type: ComponentType.Needle })
            {
                return AttuneOutcome.NotAnOwnedNeedle;
            }
        }

        player.AttunedNeedleId = needleId;
        await db.SaveChangesAsync(cancellationToken);
        return AttuneOutcome.Attuned;
    }

    private static PlayerDto ToDto(Player p) => new(p.Id, p.Name, p.CreatedAt, p.AttunedNeedleId);

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
            .Select(i => new InventoryItemDto(components[i.ComponentDefinitionId], i.Quantity, i.AcquiredAt, i.Notes))
            .OrderBy(i => i.Component.Type)
            .ThenBy(i => i.Component.Name, StringComparer.Ordinal)];
    }

    /// <summary>Creates a player and gives them the starter kit in the same SaveChanges.</summary>
    public async Task<PlayerDto> CreatePlayerAsync(string name, CancellationToken cancellationToken)
    {
        var now = clock.GetDatabaseUtcNow();
        var player = new Player { Id = Guid.CreateVersion7(), Name = name.Trim(), CreatedAt = now };

        var candidates = await catalog.GetStarterKitCandidatesAsync(cancellationToken);
        player.AttunedNeedleId = candidates.BeginnerNeedleId; // the starter needle is attuned until the player picks another
        foreach (var (componentId, quantity) in starterKit.Pick(candidates))
        {
            player.Items.Add(new InventoryItem { ComponentDefinitionId = componentId, Quantity = quantity, AcquiredAt = now });
        }

        db.Players.Add(player);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(player);
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

        // A Unique map has one holder. Checked here so every way of granting (assign, completion) obeys it.
        if (component.Map?.Flags is { Unique: true } && await IsHeldByOthersAsync(component.Id, playerId, cancellationToken))
        {
            throw new MapHeldElsewhereException($"{component.Name} is Unique and another player holds it.");
        }

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
        return new InventoryItemDto(component, item.Quantity, item.AcquiredAt, item.Notes);
    }

    /// <summary>Replaces the notes on an item the player owns. Returns null when they don't own it.</summary>
    public async Task<InventoryItemDto?> SetNotesAsync(Guid playerId, Guid componentId, string notes, CancellationToken cancellationToken)
    {
        var item = await db.Items.FirstOrDefaultAsync(
            i => i.PlayerId == playerId && i.ComponentDefinitionId == componentId && i.Quantity > 0, cancellationToken);
        var component = item is null ? null : await catalog.GetComponentAsync(componentId, cancellationToken);
        if (item is null || component is null)
        {
            return null;
        }

        item.Notes = notes.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return new InventoryItemDto(component, item.Quantity, item.AcquiredAt, item.Notes);
    }

    public Task<bool> IsHeldByOthersAsync(Guid componentId, Guid playerId, CancellationToken cancellationToken = default) =>
        db.Items.AnyAsync(i => i.ComponentDefinitionId == componentId && i.PlayerId != playerId && i.Quantity > 0, cancellationToken);

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

internal enum AttuneOutcome
{
    Attuned,
    PlayerNotFound,
    NotAnOwnedNeedle,
}
