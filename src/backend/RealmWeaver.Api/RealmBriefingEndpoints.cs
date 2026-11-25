using Microsoft.AspNetCore.Http.HttpResults;

using RealmWeaver.Infrastructure.Http;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Realms.Contracts;

namespace RealmWeaver.Api;

/// <summary>
/// The player's view of a realm (Milestone 8i): a briefing with no map, no locations and no GM detail. Composed from the
/// Realms contract (the realm, trimmed by the module that owns it) and the Inventory contract (whether the player holds
/// the realm's map), like the roster.
/// </summary>
internal static class RealmBriefingEndpoints
{
    public static void MapRealmBriefing(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/realms/{id:guid}/briefing", GetAsync).WithTags("Realms").WithName("GetRealmBriefing")
            .WithDescription("The realm as a player sees it: kind, threat, components and objectives (title and hint only, no locations). When playerId holds the realm's map, the kinds of creatures, loot and resource nodes are included. A preview, not access control (ADR 0012).");

    private static async Task<Results<Ok<RealmBriefingDto>, ProblemHttpResult>> GetAsync(
        Guid id,
        IRealmsModule realms,
        IInventoryModule inventory,
        CancellationToken cancellationToken,
        Guid? playerId = null)
    {
        if (await realms.GetSummaryAsync(id, cancellationToken) is not { } summary)
        {
            return Problems.NotFound("Realm", id);
        }

        var holdsMap = playerId is { } player
            && (await inventory.HoldersAsync(summary.MapComponentId, [player], cancellationToken)).Contains(player);
        return await realms.GetBriefingAsync(id, holdsMap, cancellationToken) is { } briefing
            ? TypedResults.Ok(briefing)
            : Problems.NotFound("Realm", id);
    }
}
