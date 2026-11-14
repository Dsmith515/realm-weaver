using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using RealmWeaver.Infrastructure.Http;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Inventory.Persistence;

namespace RealmWeaver.Modules.Inventory.Endpoints;

internal static class InventoryEndpoints
{
    public const int MaxSelfAddQuantity = 10;
    public const int MaxAssignQuantity = 99;

    public static void Map(IEndpointRouteBuilder app)
    {
        var players = app.MapGroup("/api/players").WithTags("Players");
        players.MapGet("/", ListPlayersAsync).WithName("ListPlayers");
        players.MapPost("/", CreatePlayerAsync).WithName("CreatePlayer")
            .WithDescription("Creates a player with the starter kit: the beginner needle, 2 threads ×3 and 2 coatings ×3.");
        players.MapGet("/{id:guid}", GetPlayerAsync).WithName("GetPlayer");
        players.MapGet("/{id:guid}/inventory", GetInventoryAsync).WithName("GetInventory");
        players.MapPost("/{id:guid}/inventory", SelfAddAsync).WithName("AddToOwnInventory")
            .WithDescription("A player adds a component to their own inventory. Allowed only when self-assign is on and the component is player-assignable.");
        players.MapPut("/{id:guid}/inventory/{componentId:guid}/notes", SetNotesAsync).WithName("SetInventoryNotes")
            .WithDescription("Replaces the player's notes on an item they own.");

        // Lives here, not in Catalog: granting is an inventory write, and Catalog must not depend on Inventory.
        app.MapPost("/api/components/{id:guid}/assign", AssignAsync).WithTags("Components").WithName("AssignComponent")
            .WithDescription("Admin: gives a component to a player, regardless of the self-assign rules.");
    }

    private static async Task<Ok<IReadOnlyList<PlayerDto>>> ListPlayersAsync(InventoryService inventory, CancellationToken cancellationToken) =>
        TypedResults.Ok(await inventory.ListPlayersAsync(cancellationToken));

    private static async Task<Results<Created<PlayerDto>, ValidationProblem>> CreatePlayerAsync(
        [FromBody] CreatePlayerInput input, InventoryService inventory, CancellationToken cancellationToken)
    {
        // Name is non-nullable in C#, but a JSON body can still omit it.
        var name = input.Name?.Trim() ?? string.Empty;
        var errors = new ValidationErrors();
        errors.Require(name.Length > 0, "name", "A name is required.");
        errors.Require(name.Length <= Player.MaxNameLength, "name", $"Use at most {Player.MaxNameLength} characters.");
        if (!errors.IsValid)
        {
            return Problems.Validation(errors.ToDictionary());
        }

        var player = await inventory.CreatePlayerAsync(name, cancellationToken);
        return TypedResults.Created($"/api/players/{player.Id}", player);
    }

    private static async Task<Results<Ok<PlayerDto>, ProblemHttpResult>> GetPlayerAsync(Guid id, InventoryService inventory, CancellationToken cancellationToken) =>
        await inventory.GetPlayerAsync(id, cancellationToken) is { } player
            ? TypedResults.Ok(player)
            : Problems.NotFound("Player", id);

    private static async Task<Results<Ok<IReadOnlyList<InventoryItemDto>>, ProblemHttpResult>> GetInventoryAsync(
        Guid id, InventoryService inventory, CancellationToken cancellationToken) =>
        await inventory.GetInventoryAsync(id, cancellationToken) is { } items
            ? TypedResults.Ok(items)
            : Problems.NotFound("Player", id);

    private static async Task<Results<Ok<InventoryItemDto>, ProblemHttpResult, ValidationProblem>> SelfAddAsync(
        Guid id, [FromBody] AddInventoryItemInput input, InventoryService inventory, ICatalogModule catalog, CancellationToken cancellationToken)
    {
        if (await inventory.GetPlayerAsync(id, cancellationToken) is null)
        {
            return Problems.NotFound("Player", id);
        }

        var component = await catalog.GetComponentAsync(input.ComponentId, cancellationToken);
        var errors = new ValidationErrors();
        errors.Require(component is { IsArchived: false }, "componentId", $"No component has id '{input.ComponentId}'.");
        if (component is not null)
        {
            var maxQuantity = component.IsConsumable ? MaxSelfAddQuantity : 1;
            errors.Require(input.Quantity >= 1 && input.Quantity <= maxQuantity, "quantity", $"Quantity must be between 1 and {maxQuantity}.");
        }

        if (!errors.IsValid)
        {
            return Problems.Validation(errors.ToDictionary());
        }

        var settings = await catalog.GetSettingsAsync(cancellationToken);
        if (!settings.PlayersCanSelfAssign)
        {
            return Problems.Forbidden("Self-assign is turned off.", "An admin has turned off adding components to your own inventory.");
        }

        if (!component!.IsPlayerAssignable)
        {
            return Problems.Forbidden("This component can't be self-assigned.", $"'{component.Name}' can only be given to players by an admin.");
        }

        return TypedResults.Ok(await inventory.GrantAsync(id, component, input.Quantity, cancellationToken));
    }

    private static async Task<Results<Ok<InventoryItemDto>, ProblemHttpResult, ValidationProblem>> SetNotesAsync(
        Guid id, Guid componentId, [FromBody] InventoryNotesInput input, InventoryService inventory, CancellationToken cancellationToken)
    {
        var notes = input.Notes ?? string.Empty;
        if (notes.Trim().Length > InventoryItem.MaxNotesLength)
        {
            return Problems.Validation(new Dictionary<string, string[]> { ["notes"] = [$"Use at most {InventoryItem.MaxNotesLength} characters."] });
        }

        return await inventory.SetNotesAsync(id, componentId, notes, cancellationToken) is { } item
            ? TypedResults.Ok(item)
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Item not found.", detail: $"Player '{id}' doesn't own component '{componentId}'.");
    }

    private static async Task<Results<Ok<InventoryItemDto>, ProblemHttpResult, ValidationProblem>> AssignAsync(
        Guid id, [FromBody] AssignComponentInput input, InventoryService inventory, ICatalogModule catalog, CancellationToken cancellationToken)
    {
        var component = await catalog.GetComponentAsync(id, cancellationToken);
        if (component is null)
        {
            return Problems.NotFound("Component", id);
        }

        var errors = new ValidationErrors();
        errors.Require(!component.IsArchived, "id", "Archived components can't be assigned.");
        errors.Require(await inventory.GetPlayerAsync(input.PlayerId, cancellationToken) is not null, "playerId", $"No player has id '{input.PlayerId}'.");
        var maxQuantity = component.IsConsumable ? MaxAssignQuantity : 1;
        errors.Require(input.Quantity >= 1 && input.Quantity <= maxQuantity, "quantity", $"Quantity must be between 1 and {maxQuantity}.");
        if (!errors.IsValid)
        {
            return Problems.Validation(errors.ToDictionary());
        }

        return TypedResults.Ok(await inventory.GrantAsync(input.PlayerId, component, input.Quantity, cancellationToken));
    }
}
