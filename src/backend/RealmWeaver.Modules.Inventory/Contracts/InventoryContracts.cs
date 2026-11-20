using RealmWeaver.Modules.Catalog.Contracts;

namespace RealmWeaver.Modules.Inventory.Contracts;

/// <summary>
/// A player. <see cref="AttunedNeedleId"/> is the one needle that supplies abilities and its boon during an adventure;
/// it's separate from the needle used to weave, and the realm sheet shows it to the GM.
/// </summary>
public sealed record PlayerDto(Guid Id, string Name, DateTimeOffset CreatedAt, Guid? AttunedNeedleId = null);

/// <summary>Body for attuning one of the player's own needles.</summary>
public sealed record AttuneNeedleInput(Guid NeedleId);

public sealed record CreatePlayerInput(string Name);

/// <summary>One stack in a player's inventory. Needles and Maps always have quantity 1.</summary>
public sealed record InventoryItemDto(ComponentDto Component, int Quantity, DateTimeOffset AcquiredAt, string Notes = "");

/// <summary>Body for replacing a player's notes on an item they own. Empty clears them.</summary>
public sealed record InventoryNotesInput(string Notes);

/// <summary>Body for a player adding a component to their own inventory.</summary>
public sealed record AddInventoryItemInput(Guid ComponentId, int Quantity = 1);

/// <summary>Body for the GM giving a component to a player.</summary>
public sealed record AssignComponentInput(Guid PlayerId, int Quantity = 1);

/// <summary>One page of players by name.</summary>
public sealed record PlayerPage(IReadOnlyList<PlayerDto> Items, int Page, int PageSize, int TotalCount);

/// <summary>The Inventory module's public contract for other modules.</summary>
public interface IInventoryModule
{
    Task<PlayerDto?> GetPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Players by name, a page at a time. <paramref name="search"/> matches any part of the name (case-insensitive,
    /// <c>%</c> and <c>_</c> literal). <paramref name="excludeIds"/> are left out of the page and the count.
    /// </summary>
    Task<PlayerPage> ListPlayersPageAsync(string? search, int page, int pageSize, IReadOnlyCollection<Guid> excludeIds, CancellationToken cancellationToken = default);

    /// <summary>Which of <paramref name="playerIds"/> hold <paramref name="componentId"/>.</summary>
    Task<IReadOnlySet<Guid>> HoldersAsync(Guid componentId, IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default);

    /// <summary>Returns null when the player doesn't exist.</summary>
    Task<IReadOnlyList<InventoryItemDto>?> GetInventoryAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a component to the player's inventory. Consumables stack; owning a Needle or Map again changes nothing.
    /// Throws <see cref="MapHeldElsewhereException"/> for a Unique map another player holds.
    /// Joins the caller's unit of work when one is running.
    /// </summary>
    Task<InventoryItemDto> GrantAsync(Guid playerId, ComponentDto component, int quantity = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes one of each component from the player's inventory, guarded by the rows' <c>xmin</c>.
    /// Throws <see cref="InsufficientComponentsException"/> when any is missing, or
    /// <c>DbUpdateConcurrencyException</c> when another request spent them first.
    /// </summary>
    Task SpendOneEachAsync(Guid playerId, IReadOnlyCollection<Guid> componentIds, CancellationToken cancellationToken = default);

    /// <summary>Whether any player other than <paramref name="playerId"/> holds the component.</summary>
    Task<bool> IsHeldByOthersAsync(Guid componentId, Guid playerId, CancellationToken cancellationToken = default);
}

/// <summary>A Unique map is already held by another player, so it can't be granted.</summary>
public sealed class MapHeldElsewhereException : Exception
{
    public MapHeldElsewhereException()
    {
    }

    public MapHeldElsewhereException(string message)
        : base(message)
    {
    }

    public MapHeldElsewhereException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The player doesn't have (enough of) these components.</summary>
public sealed class InsufficientComponentsException : Exception
{
    public InsufficientComponentsException()
    {
    }

    public InsufficientComponentsException(string message)
        : base(message)
    {
    }

    public InsufficientComponentsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public InsufficientComponentsException(IReadOnlyList<Guid> componentIds)
        : base($"Not enough of: {string.Join(", ", componentIds)}.")
    {
        ComponentIds = componentIds;
    }

    public IReadOnlyList<Guid> ComponentIds { get; } = [];
}
