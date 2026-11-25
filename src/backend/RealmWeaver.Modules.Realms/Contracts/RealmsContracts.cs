using System.Text.Json.Serialization;

using RealmWeaver.Generation.Model;

namespace RealmWeaver.Modules.Realms.Contracts;

/// <summary>
/// A generated realm to store. The document is the immutable snapshot of what the generator produced.
/// <see cref="Components"/> copies the map and component names at weaving time (ADR 0006), so listing realms
/// never calls the Catalog module.
/// </summary>
public sealed record NewRealm(
    Guid Id,
    Guid? PlayerId,
    Guid MapComponentId,
    int DangerModifier,
    int EncounterVariant,
    RealmDocument Document,
    RealmComponentsDto? Components = null);

/// <summary>A component a realm was woven from, as it was named then.</summary>
public sealed record ComponentRefDto(Guid Id, string Name);

/// <summary>
/// The map and the components in the slots when the realm was woven. Null for realms from before this was recorded.
/// <see cref="AttunedNeedle"/> is the weaver's attuned needle at that moment: the abilities and boon the party brings.
/// </summary>
public sealed record RealmComponentsDto(
    string MapName,
    ComponentRefDto Needle,
    ComponentRefDto Thread,
    ComponentRefDto Coating,
    AttunedNeedleDto? AttunedNeedle = null);

/// <summary>A copy of the attuned needle's name, abilities and boon (a Catalog <c>NeedleBoon</c> name) at weaving time.</summary>
public sealed record AttunedNeedleDto(Guid Id, string Name, IReadOnlyList<string> Abilities, string? Boon);

public sealed record RealmSummaryDto(
    Guid Id,
    Guid? PlayerId,
    Guid MapComponentId,
    RealmKind Kind,
    Biome Biome,
    int Width,
    int Height,
    int FloorCount,
    int Threat,
    int DangerModifier,
    int EncounterVariant,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    ulong Seed,
    string GeneratorVersion,
    DateTimeOffset CreatedAt,
    IReadOnlyList<RealmAreaSummaryDto> Areas,
    RealmComponentsDto? Components = null,
    RealmObjective? PinnedObjective = null,
    int CompletionCount = 0)
{
    /// <summary>A mirror: a recorded map woven with different components. Same layout, rerolled encounters.</summary>
    public bool IsMirror => EncounterVariant != 0;
}

/// <summary>One exportable map in a realm: the field, or a dungeon floor.</summary>
public sealed record RealmAreaSummaryDto(string Id, AreaKind Kind, string Name, int FloorNumber);

public sealed record RealmDto(RealmSummaryDto Summary, RealmDocument Document);

/// <summary>
/// The GM marking an objective complete for a group of players. A realm can be completed any number of times, by the
/// same party or a new one, since every map can be visited again (ADR 0011). Completions belong to the realm's map, so
/// every run woven from it shares one history; <see cref="IsFirst"/> marks the earliest.
/// </summary>
public sealed record RealmCompletionDto(
    Guid Id,
    Guid RealmId,
    RealmObjective Objective,
    DateTimeOffset CompletedAt,
    IReadOnlyList<CompletionPlayerDto> Players,
    bool IsFirst);

/// <summary>
/// A player in a completion, with their name as it was then (ADR 0006: Realms never calls Inventory) and whether the GM
/// handed them the map at that moment.
/// </summary>
public sealed record CompletionPlayerDto(Guid Id, string Name, bool MapGiven);

/// <summary>A player who completed at least one objective on this map, across every run woven from it.</summary>
public sealed record CompleterDto(Guid Id, string Name, int Completions, DateTimeOffset FirstCompletedAt, bool InFirstCompletion);

/// <summary>A map's completion history, newest first, and everyone who has completed something on it.</summary>
public sealed record RealmCompletionsDto(IReadOnlyList<RealmCompletionDto> Completions, IReadOnlyList<CompleterDto> Players);

public enum CompletionResult
{
    Completed,
    NotFound,

    /// <summary>The key isn't one of the realm's rolled objectives or its GM pin.</summary>
    UnknownObjective,
}

public sealed record RealmCompletionResult(CompletionResult Result, RealmCompletionDto? Completion = null);

/// <summary>A page of realm summaries.</summary>
public sealed record RealmSummaryPage(IReadOnlyList<RealmSummaryDto> Items, int Page, int PageSize, int TotalCount);

/// <summary>Body for a GM pinning an objective: the <see cref="RealmObjective.Key"/> of a rolled or candidate objective.</summary>
public sealed record PinObjectiveInput(string Key);

/// <summary>The Realms module's public contract for other modules.</summary>
public interface IRealmsModule
{
    /// <summary>Stores a realm. Joins the caller's unit of work when one is running.</summary>
    Task<RealmSummaryDto> SaveAsync(NewRealm realm, CancellationToken cancellationToken = default);

    Task<RealmSummaryDto?> GetSummaryAsync(Guid realmId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that <paramref name="players"/> completed one of the realm's rolled objectives (or its pin). Repeatable:
    /// each call adds a completion. Joins the caller's unit of work when one is running.
    /// </summary>
    Task<RealmCompletionResult> CompleteAsync(Guid realmId, string objectiveKey, IReadOnlyList<CompletionPlayerDto> players, CancellationToken cancellationToken = default);

    /// <summary>The completion history of the realm's map, newest first, with everyone who completed something. Null when the realm doesn't exist.</summary>
    Task<RealmCompletionsDto?> GetCompletionsAsync(Guid realmId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The first non-mirror realm opened from a map, with a digest of its mobs, loot and resource nodes.
    /// Null when no such realm is stored (for example a map the GM created by hand).
    /// </summary>
    Task<RealmDigestDto?> GetOriginalDigestAsync(Guid mapComponentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The realm as a player sees it (<see cref="RealmBriefingDto"/>). The caller decides whether the viewer holds the
    /// realm's map, since that's the Inventory module's knowledge; holding it adds the kinds of things to find.
    /// Null when the realm doesn't exist.
    /// </summary>
    Task<RealmBriefingDto?> GetBriefingAsync(Guid realmId, bool holdsMap, CancellationToken cancellationToken = default);
}

/// <summary>A map's original realm and what it holds.</summary>
public sealed record RealmDigestDto(Guid RealmId, int Threat, Generation.Export.RealmDigest Digest);

/// <summary>
/// A realm as a player sees it before the run: what kind of place it is, how dangerous, and what the party can aim for,
/// with no map, no locations and nothing a GM keeps to themselves. A player who holds the realm's map also gets
/// <see cref="Finds"/> and the <see cref="Areas"/> to draw its map by. Still a preview (ADR 0012): the API doesn't check who is asking yet, but the shape is the one a
/// signed-in player will be sent.
/// </summary>
public sealed record RealmBriefingDto(
    Guid RealmId,
    string? MapName,
    RealmKind Kind,
    Biome Biome,
    Element Element,
    int Threat,
    int DangerModifier,
    int FloorCount,
    bool IsMirror,
    IReadOnlyList<string> WovenFrom,
    int CompletionCount,
    IReadOnlyList<BriefingObjectiveDto> Objectives,
    bool HoldsMap,
    BriefingFindsDto? Finds = null,
    IReadOnlyList<RealmAreaSummaryDto>? Areas = null);

/// <summary>
/// An objective as a player sees it: what to do and a hint, never where. <see cref="Id"/> is "O1".."O3", or "P" for a
/// GM pin that isn't one of the rolled objectives; <see cref="IsPinned"/> marks the GM's pick either way.
/// </summary>
public sealed record BriefingObjectiveDto(string Id, string Title, string Hint, bool IsPinned);

/// <summary>The kinds of creatures, loot and resource nodes in the realm, by name only: no counts, no locations.</summary>
public sealed record BriefingFindsDto(IReadOnlyList<string> Creatures, IReadOnlyList<string> Loot, IReadOnlyList<string> ResourceNodes);
