using System.Text.Json.Serialization;
using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Grid;

namespace RealmWeaver.Generation.Model;

/// <summary>The complete, serializable output of one generation. Stored as a snapshot by the Realms module.</summary>
public sealed record RealmDocument
{
    public required string GeneratorVersion { get; init; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public required ulong Seed { get; init; }

    public required GenerationRequest Request { get; init; }

    public required int Threat { get; init; }

    /// <summary>The field and/or dungeon floors, in travel order.</summary>
    public required IReadOnlyList<RealmArea> Areas { get; init; }
}

/// <summary>A single map: the field, or one dungeon floor.</summary>
public sealed record RealmArea
{
    public required string Id { get; init; }

    public required AreaKind Kind { get; init; }

    public required string Name { get; init; }

    /// <summary>0 for the field; 1..N for dungeon floors.</summary>
    public int FloorNumber { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    /// <summary>One string per row; one <see cref="TerrainInfo.Code"/> character per tile.</summary>
    public required IReadOnlyList<string> TerrainRows { get; init; }

    public GridPoint? Spawn { get; init; }

    public required IReadOnlyList<PlacedEvent> Events { get; init; }

    /// <summary>Notable non-event things: traps, chests, secret doors, stairs, dungeon entrances.</summary>
    public IReadOnlyList<PlacedFeature> Features { get; init; } = [];
}

/// <summary>An encounter or event occupying one or more tiles.</summary>
public sealed record PlacedEvent
{
    /// <summary>Short id shown on the map, e.g. "E7".</summary>
    public required string Id { get; init; }

    /// <summary>The <see cref="EventTemplate.Id"/> used, so content can later move to database tables.</summary>
    public required string TemplateId { get; init; }

    public required string Name { get; init; }

    public required EventCategory Category { get; init; }

    public required GridRect Footprint { get; init; }

    public string Location => TileLabels.Range(Footprint);

    public IReadOnlyList<MobGroup> Mobs { get; init; } = [];

    /// <summary>Ids of <see cref="DifficultyTemplate"/>s to apply to the mobs.</summary>
    public IReadOnlyList<string> DifficultyTemplateIds { get; init; } = [];

    public string? Notes { get; init; }
}

/// <summary>A number of mobs of one archetype in an event.</summary>
public sealed record MobGroup(string ArchetypeId, string Name, MobRole Role, int Count);

public enum FeatureKind
{
    Trap,
    Chest,
    SecretDoor,
    StairsDown,
    StairsUp,
    DungeonEntrance,
    Landmark,
}

public sealed record PlacedFeature(string Id, FeatureKind Kind, GridPoint Position, string Description)
{
    public string Location => TileLabels.Label(Position);
}
