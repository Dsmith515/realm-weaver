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
    Guid PlayerId,
    Guid MapComponentId,
    int DangerModifier,
    int EncounterVariant,
    RealmDocument Document,
    RealmComponentsDto? Components = null);

/// <summary>A component a realm was woven from, as it was named then.</summary>
public sealed record ComponentRefDto(Guid Id, string Name);

/// <summary>The map and the components in the slots when the realm was woven. Null for realms from before this was recorded.</summary>
public sealed record RealmComponentsDto(string MapName, ComponentRefDto Needle, ComponentRefDto Thread, ComponentRefDto Coating);

public sealed record RealmSummaryDto(
    Guid Id,
    Guid PlayerId,
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
    RealmComponentsDto? Components = null)
{
    /// <summary>A mirror: a recorded map woven with different components. Same layout, rerolled encounters.</summary>
    public bool IsMirror => EncounterVariant != 0;
}

/// <summary>One exportable map in a realm: the field, or a dungeon floor.</summary>
public sealed record RealmAreaSummaryDto(string Id, AreaKind Kind, string Name, int FloorNumber);

public sealed record RealmDto(RealmSummaryDto Summary, RealmDocument Document);

/// <summary>The Realms module's public contract for other modules.</summary>
public interface IRealmsModule
{
    /// <summary>Stores a realm. Joins the caller's unit of work when one is running.</summary>
    Task<RealmSummaryDto> SaveAsync(NewRealm realm, CancellationToken cancellationToken = default);

    Task<RealmSummaryDto?> GetSummaryAsync(Guid realmId, CancellationToken cancellationToken = default);
}
