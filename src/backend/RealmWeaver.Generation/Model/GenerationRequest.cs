using System.Text.Json.Serialization;

namespace RealmWeaver.Generation.Model;

/// <summary>Hard limits shared by the generator, API validation and UI.</summary>
public static class GeneratorLimits
{
    public const string Version = "0.2.0";
    public const int MinSize = 15;
    public const int MaxSize = 100;
    public const int MinEvents = 5;
    public const int DefaultMaxEvents = 50;
    public const int AbsoluteMaxEvents = 60;
    public const int SpawnClearance = 5;
    public const int MaxFloors = 3;
    public const int MinDifficulty = 1;
    public const int MaxDifficulty = 10;
}

/// <summary>
/// Everything the generator needs. The Ritual module builds this from the Needle (difficulty),
/// Thread (biome, element), Coating (mob tags, kind, floors) and Map (seed) components.
/// </summary>
public sealed record GenerationRequest
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public required ulong Seed { get; init; }

    public RealmKind Kind { get; init; } = RealmKind.Field;

    public Biome Biome { get; init; } = Biome.Plains;

    public Element Element { get; init; } = Element.None;

    /// <summary>Mob type tags from the Coating, e.g. "beast", "undead". Empty means any.</summary>
    public IReadOnlyList<string> MobTags { get; init; } = [];

    /// <summary>Needle difficulty tier, 1-10.</summary>
    public int Difficulty { get; init; } = 1;

    /// <summary>Extra danger, e.g. from using a Map with mismatched components.</summary>
    public int DangerModifier { get; init; }

    /// <summary>
    /// Changes encounters without changing terrain. A mismatched Map keeps its seed but uses a new variant.
    /// </summary>
    public int EncounterVariant { get; init; }

    /// <summary>Manual width; random 15-100 when null.</summary>
    public int? Width { get; init; }

    /// <summary>Manual height; random 15-100 when null.</summary>
    public int? Height { get; init; }

    /// <summary>Manual event count (5-60, capped by map size); scaled random when null.</summary>
    public int? EventCount { get; init; }

    /// <summary>Dungeon floors (1-<see cref="GeneratorLimits.MaxFloors"/>). Used from Milestone 3.</summary>
    public int FloorCount { get; init; } = 1;

    /// <summary>Danger level used to pick mob counts and difficulty templates.</summary>
    [JsonIgnore]
    public int Threat => Difficulty + DangerModifier;
}

public sealed class RealmGenerationException : Exception
{
    public RealmGenerationException()
    {
    }

    public RealmGenerationException(string message)
        : base(message)
    {
    }

    public RealmGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
