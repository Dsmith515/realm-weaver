using Microsoft.AspNetCore.Http.HttpResults;

using RealmWeaver.Infrastructure.Http;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Realms.Contracts;

namespace RealmWeaver.Api;

/// <summary>
/// Everything about a map in one response: the map itself (seed, recipe, threat), the components it was woven
/// from, and a digest of its original realm (mobs, loot, resource nodes).
/// </summary>
public sealed record MapDetailsDto(
    ComponentDto Map,
    ComponentDto? Needle,
    ComponentDto? Thread,
    ComponentDto? Coating,
    RealmDigestDto? OriginalRealm);

/// <summary>
/// A read that spans modules, composed here from their public contracts only (ADR 0006): Catalog owns the map and
/// its components, Realms owns the generated document. Neither module knows about the other.
/// </summary>
internal static class MapDetailsEndpoints
{
    public static void MapMapDetails(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/maps/{id:guid}/details", GetAsync).WithTags("Maps").WithName("GetMapDetails");

    private static async Task<Results<Ok<MapDetailsDto>, ProblemHttpResult>> GetAsync(
        Guid id, ICatalogModule catalog, IRealmsModule realms, CancellationToken cancellationToken)
    {
        if (await catalog.GetComponentAsync(id, cancellationToken) is not { Map: { } details } map)
        {
            return Problems.NotFound("Map", id);
        }

        var parts = await catalog.GetComponentsAsync([details.NeedleId, details.ThreadId, details.CoatingId], cancellationToken);
        var original = await realms.GetOriginalDigestAsync(id, cancellationToken);
        return TypedResults.Ok(new MapDetailsDto(
            map,
            parts.GetValueOrDefault(details.NeedleId),
            parts.GetValueOrDefault(details.ThreadId),
            parts.GetValueOrDefault(details.CoatingId),
            original));
    }
}
