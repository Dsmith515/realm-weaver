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
}
