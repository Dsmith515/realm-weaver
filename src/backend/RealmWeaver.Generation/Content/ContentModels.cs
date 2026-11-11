using RealmWeaver.Generation.Model;

namespace RealmWeaver.Generation.Content;

/// <summary>
/// Source of event, mob and difficulty templates. Milestone 2 uses <see cref="StaticContentCatalog"/>;
/// a later milestone backs this with database tables without changing the generator.
/// </summary>
public interface IContentCatalog
{
    IReadOnlyList<EventTemplate> EventTemplates { get; }

    IReadOnlyList<MobArchetype> MobArchetypes { get; }

    IReadOnlyList<DifficultyTemplate> DifficultyTemplates { get; }

    IReadOnlyList<TrapTemplate> TrapTemplates { get; }
}

[Flags]
public enum Environments
{
    None = 0,
    Field = 1,
    Dungeon = 2,
    Any = Field | Dungeon,
}

public enum EventCategory
{
    Encounter,
    Settlement,
    Social,
    Landmark,
    Hazard,

    /// <summary>A building or opening on the field that leads into the dungeon.</summary>
    Structure,
}

public enum MobRole
{
    Minion,
    Brute,
    Skirmisher,
    Caster,
    Leader,
    Boss,
}

public enum FootprintShape
{
    /// <summary>Any rectangle within the size limits.</summary>
    Area,

    /// <summary>A 1-tile-wide strip, horizontal or vertical (patrol routes).</summary>
    Line,
}

/// <summary>Size limits for an event's footprint, in tiles.</summary>
public sealed record FootprintRule(int MinSide, int MaxSide, FootprintShape Shape = FootprintShape.Area)
{
    /// <summary>
    /// When true the maximum side grows with the map (up to half the map's area), as for villages.
    /// </summary>
    public bool ScalesWithMap { get; init; }
}

public sealed record MobSlot(MobRole Role, int MinCount, int MaxCount);

public sealed record EventTemplate
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required EventCategory Category { get; init; }

    public required Environments Environments { get; init; }

    public required FootprintRule Footprint { get; init; }

    /// <summary>Relative chance of being chosen.</summary>
    public int Weight { get; init; } = 10;

    /// <summary>Restricts the template to these biomes. Empty means any biome.</summary>
    public IReadOnlyList<Biome> Biomes { get; init; } = [];

    public IReadOnlyList<MobSlot> MobSlots { get; init; } = [];

    public int MaxPerArea { get; init; } = int.MaxValue;

    /// <summary>Settlements and camps clear impassable terrain inside their footprint.</summary>
    public bool ClearsTerrain { get; init; }

    public string Description { get; init; } = "";

    /// <summary>Dungeon only: room styles this event can appear in. Empty means any.</summary>
    public IReadOnlyList<RoomStyle> AllowedStyles { get; init; } = [];

    /// <summary>Dungeon only: room styles where this event is three times as likely.</summary>
    public IReadOnlyList<RoomStyle> PreferredStyles { get; init; } = [];

    /// <summary>Dungeon only: how many connected rooms the event spreads across (settlements).</summary>
    public int MaxRooms { get; init; } = 1;

    /// <summary>Placed only in the boss room on the deepest floor.</summary>
    public bool IsBoss { get; init; }

    /// <summary>A field structure that holds the dungeon entrance. Set means the template is a host.</summary>
    public DungeonMaterial? HostMaterial { get; init; }

    public bool IsDungeonHost => HostMaterial is not null;
}

/// <summary>A trap kind; <see cref="Styles"/> limits it to rooms and corridors of matching style (empty means any).</summary>
public sealed record TrapTemplate(string Id, string Name, IReadOnlyList<RoomStyle> Styles);

/// <summary>
/// A kind of mob. Later milestones attach a user-authored stat block to each archetype id.
/// </summary>
public sealed record MobArchetype
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Mob type tags matched against the Coating, e.g. "beast", "undead".</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    public required IReadOnlyList<MobRole> Roles { get; init; }

    /// <summary>Restricts the archetype to these biomes. Empty means any biome.</summary>
    public IReadOnlyList<Biome> Biomes { get; init; } = [];

    public int MinTier { get; init; } = 1;

    public int MaxTier { get; init; } = 10;
}

/// <summary>A modifier applied to mobs to raise difficulty, e.g. "Elite".</summary>
public sealed record DifficultyTemplate(string Id, string Name, int MinThreat, string Summary);
