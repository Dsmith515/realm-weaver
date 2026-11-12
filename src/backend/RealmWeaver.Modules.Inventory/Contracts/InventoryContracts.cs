using RealmWeaver.Modules.Catalog.Contracts;

namespace RealmWeaver.Modules.Inventory.Contracts;

public sealed record PlayerDto(Guid Id, string Name, DateTimeOffset CreatedAt);

public sealed record CreatePlayerInput(string Name);

/// <summary>One stack in a player's inventory. Needles and Maps always have quantity 1.</summary>
public sealed record InventoryItemDto(ComponentDto Component, int Quantity, DateTimeOffset AcquiredAt);

/// <summary>Body for a player adding a component to their own inventory.</summary>
public sealed record AddInventoryItemInput(Guid ComponentId, int Quantity = 1);

/// <summary>Body for an admin giving a component to a player.</summary>
public sealed record AssignComponentInput(Guid PlayerId, int Quantity = 1);

/// <summary>The Inventory module's public contract for other modules.</summary>
public interface IInventoryModule
{
    Task<PlayerDto?> GetPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>Returns null when the player doesn't exist.</summary>
    Task<IReadOnlyList<InventoryItemDto>?> GetInventoryAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a component to the player's inventory. Consumables stack; owning a Needle or Map again changes nothing.
    /// Joins the caller's unit of work when one is running.
    /// </summary>
    Task<InventoryItemDto> GrantAsync(Guid playerId, ComponentDto component, int quantity = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes one of each component from the player's inventory, guarded by the rows' <c>xmin</c>.
    /// Throws <see cref="InsufficientComponentsException"/> when any is missing, or
    /// <c>DbUpdateConcurrencyException</c> when another request spent them first.
    /// </summary>
    Task SpendOneEachAsync(Guid playerId, IReadOnlyCollection<Guid> componentIds, CancellationToken cancellationToken = default);
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
