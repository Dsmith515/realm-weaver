using RealmWeaver.Generation.Model;
using RealmWeaver.Modules.Catalog.Contracts;

namespace RealmWeaver.Modules.Catalog.Persistence;

// Entities are internal: other modules see only the DTOs in Contracts.

internal sealed class ComponentDefinition
{
    public Guid Id { get; set; }

    public ComponentType Type { get; set; }

    public required string Name { get; set; }

    public Rarity Rarity { get; set; }

    public int Rank { get; set; }

    public string Description { get; set; } = string.Empty;

    public bool IsPlayerAssignable { get; set; }

    public bool IsConsumable { get; set; }

    /// <summary>Archived components stay readable (players may own them) but are hidden from the catalog.</summary>
    public bool IsArchived { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Postgres <c>xmin</c>, used as the optimistic concurrency token.</summary>
    public uint Version { get; set; }

    // Type-specific data, one jsonb column per type. Exactly one is set, matching Type.
    public NeedleData? Needle { get; set; }

    public ThreadData? Thread { get; set; }

    public CoatingData? Coating { get; set; }

    public MapData? Map { get; set; }

    public static bool IsConsumableType(ComponentType type) => type is ComponentType.Thread or ComponentType.Coating;
}

internal sealed class NeedleData
{
    public int DifficultyTier { get; set; }

    public string Category { get; set; } = string.Empty;

    public List<string> Abilities { get; set; } = [];
}

internal sealed class ThreadData
{
    public Biome Biome { get; set; }

    public Element Element { get; set; }
}

internal sealed class CoatingData
{
    public List<string> MobTypes { get; set; } = [];

    public RealmKind RealmKind { get; set; }

    public int FloorCount { get; set; }
}

internal sealed class MapData
{
    public ulong Seed { get; set; }

    public Guid NeedleId { get; set; }

    public Guid ThreadId { get; set; }

    public Guid CoatingId { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public int EventCount { get; set; }

    public string GeneratorVersion { get; set; } = string.Empty;

    public Guid? CreatedByPlayerId { get; set; }

    /// <summary>A copy of the generation inputs (ADR 0006). Null only before the snapshot migration backfills it.</summary>
    public MapRecipeData? Recipe { get; set; }

    public int? Threat { get; set; }
}

internal sealed class MapRecipeData
{
    public RealmKind Kind { get; set; }

    public int FloorCount { get; set; }

    public Biome Biome { get; set; }

    public Element Element { get; set; }

    public List<string> MobTags { get; set; } = [];

    public int Difficulty { get; set; }
}

/// <summary>A single-row table of global settings.</summary>
internal sealed class CatalogSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public bool PlayersCanSelfAssign { get; set; }

    public uint Version { get; set; }
}
