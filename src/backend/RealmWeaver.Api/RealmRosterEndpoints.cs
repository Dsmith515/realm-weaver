using Microsoft.AspNetCore.Http.HttpResults;

using RealmWeaver.Infrastructure.Http;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Realms.Contracts;

namespace RealmWeaver.Api;

/// <summary>
/// A player as the GM sees them on a realm: how many objectives they've completed on its map, and whether they hold
/// the map.
/// </summary>
public sealed record RosterPlayerDto(Guid Id, string Name, int Completions, bool HoldsMap, bool IsWeaver);

/// <summary>
/// A page of players, and the player who wove the realm (null for GM weaves) whatever the page, search or filter, so
/// the GM can start from them.
/// </summary>
public sealed record RealmRosterDto(IReadOnlyList<RosterPlayerDto> Items, int Page, int PageSize, int TotalCount, RosterPlayerDto? Weaver);

/// <summary>
/// The players a GM can complete a realm for or hand its map to, a page at a time (ADR 0011). Composed from the
/// Inventory (players, holders) and Realms (completions) contracts, like the map details.
/// </summary>
internal static class RealmRosterEndpoints
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    public static void MapRealmRoster(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/realms/{id:guid}/roster", GetAsync).WithTags("Realms").WithName("GetRealmRoster")
            .WithDescription("Players for the GM's completion table, by name. By default players who have completed an objective here and hold the map are left out; all=true lists everyone. search matches any part of the name.");

    private static async Task<Results<Ok<RealmRosterDto>, ProblemHttpResult, ValidationProblem>> GetAsync(
        Guid id,
        IRealmsModule realms,
        IInventoryModule inventory,
        CancellationToken cancellationToken,
        string? search = null,
        bool all = false,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
        {
            return Problems.Validation(new Dictionary<string, string[]>
            {
                ["pageSize"] = [$"Page must be at least 1 and page size between 1 and {MaxPageSize}."],
            });
        }

        if (await realms.GetSummaryAsync(id, cancellationToken) is not { } summary
            || await realms.GetCompletionsAsync(id, cancellationToken) is not { } completions)
        {
            return Problems.NotFound("Realm", id);
        }

        var counts = completions.Players.ToDictionary(p => p.Id, p => p.Completions);
        var completersHolding = await inventory.HoldersAsync(summary.MapComponentId, [.. counts.Keys], cancellationToken);
        var players = await inventory.ListPlayersPageAsync(search, page, pageSize, all ? [] : [.. completersHolding], cancellationToken);
        var weaver = summary.PlayerId is { } weaverId ? await inventory.GetPlayerAsync(weaverId, cancellationToken) : null;
        var holders = await inventory.HoldersAsync(
            summary.MapComponentId, [.. players.Items.Select(p => p.Id), .. weaver is null ? Array.Empty<Guid>() : [weaver.Id]], cancellationToken);

        RosterPlayerDto Row(PlayerDto p) => new(p.Id, p.Name, counts.GetValueOrDefault(p.Id), holders.Contains(p.Id), p.Id == summary.PlayerId);
        return TypedResults.Ok(new RealmRosterDto([.. players.Items.Select(Row)], players.Page, players.PageSize, players.TotalCount, weaver is null ? null : Row(weaver)));
    }
}
