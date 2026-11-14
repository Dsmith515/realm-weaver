using System.Text.Json.Serialization;

using RealmWeaver.Generation.Model;

namespace RealmWeaver.Modules.Catalog.Contracts;

/// <summary>Thimbles arrive in Phase 3 as a fifth, optional type.</summary>
public enum ComponentType
{
    Needle,
    Thread,
    Coating,
    Map,
}

public enum Rarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
}

/// <summary>Needle: difficulty, and the compass inside the realm. Never consumed.</summary>
public sealed record NeedleDetails(int DifficultyTier, string Category, IReadOnlyList<string> Abilities);

/// <summary>Thread: the realm's biome and element. Consumed by a ritual.</summary>
public sealed record ThreadDetails(Biome Biome, Element Element);

/// <summary>Coating: mob types and realm shape. Consumed by a ritual.</summary>
public sealed record CoatingDetails(IReadOnlyList<string> MobTypes, RealmKind RealmKind, int FloorCount);

/// <summary>
/// The generation inputs a map was woven from, copied when the map is created (ADR 0006). Rebuilding the map uses
/// these, not the components' current values, so editing a component never re-shapes an existing map.
/// New inputs (a Thimble in Phase 3) are added here as optional members.
/// </summary>
public sealed record MapRecipe(
    RealmKind Kind,
    int FloorCount,
    Biome Biome,
    Element Element,
    IReadOnlyList<string> MobTags,
    int Difficulty);

/// <summary>
/// Map: a realm already visited. Its seed and recipe rebuild the same realm.
/// <see cref="Recipe"/> is filled in from the referenced components when it's left out of a request.
/// <see cref="Threat"/> is known only for maps a ritual created.
/// </summary>
public sealed record MapDetails(
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    ulong Seed,
    Guid NeedleId,
    Guid ThreadId,
    Guid CoatingId,
    int Width,
    int Height,
    int EventCount,
    string GeneratorVersion,
    Guid? CreatedByPlayerId,
    MapRecipe? Recipe = null,
    int? Threat = null);

public sealed record ComponentDto(
    Guid Id,
    ComponentType Type,
    string Name,
    Rarity Rarity,
    int Rank,
    string Description,
    bool IsPlayerAssignable,
    bool IsConsumable,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    NeedleDetails? Needle,
    ThreadDetails? Thread,
    CoatingDetails? Coating,
    MapDetails? Map,
    int UsedByMapCount = 0);

/// <summary>
/// The last word a component's name must end with, per type, for example "Wax" or "Lacquer" for a Coating.
/// Map names drop the literal type word ("Beginner's Needle" → "Beginner's").
/// </summary>
public sealed record NameSuffixesDto(IReadOnlyList<string> Needle, IReadOnlyList<string> Thread, IReadOnlyList<string> Coating);

/// <summary>
/// Body for creating or replacing a component. Exactly one details object must be set, matching <see cref="Type"/>.
/// <c>IsConsumable</c> is not settable: Threads and Coatings are consumed, everything else isn't.
/// </summary>
public sealed record ComponentInput(
    ComponentType Type,
    string Name,
    Rarity Rarity,
    int Rank,
    string? Description,
    bool IsPlayerAssignable,
    NeedleDetails? Needle = null,
    ThreadDetails? Thread = null,
    CoatingDetails? Coating = null,
    MapDetails? Map = null);

public sealed record ComponentQuery(
    ComponentType? Type = null,
    Rarity? Rarity = null,
    Biome? Biome = null,
    string? Search = null,
    bool? Assignable = null,
    bool IncludeArchived = false,
    int Page = 1,
    int PageSize = 50);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>
/// Global admin settings. A player may add a component to their own inventory only when
/// <see cref="PlayersCanSelfAssign"/> is on and the component is <c>IsPlayerAssignable</c>.
/// </summary>
public sealed record CatalogSettingsDto(bool PlayersCanSelfAssign);

/// <summary>What a new player's starter kit is drawn from.</summary>
public sealed record StarterKitCandidates(Guid BeginnerNeedleId, IReadOnlyList<Guid> ThreadIds, IReadOnlyList<Guid> CoatingIds);

/// <summary>
/// A Map component created by a ritual. It appears in the admin catalog like any other component.
/// The caller chooses the id up front, so a retried or replayed step always refers to the same map.
/// </summary>
public sealed record NewMap(Guid Id, string Name, string Description, MapDetails Details);

/// <summary>The Catalog module's public contract for other modules. In Phase 2 this becomes an HTTP or message client.</summary>
public interface ICatalogModule
{
    Task<ComponentDto?> GetComponentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Looks up many components at once. Unknown ids are left out of the result.</summary>
    Task<IReadOnlyDictionary<Guid, ComponentDto>> GetComponentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Every component of one type, for picking at random (GM Quick Weave).</summary>
    Task<IReadOnlyList<ComponentDto>> ListByTypeAsync(ComponentType type, bool includeArchived, CancellationToken cancellationToken = default);

    Task<CatalogSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<StarterKitCandidates> GetStarterKitCandidatesAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a Map component. Joins the caller's unit of work when one is running.</summary>
    Task<ComponentDto> CreateMapAsync(NewMap map, CancellationToken cancellationToken = default);
}
