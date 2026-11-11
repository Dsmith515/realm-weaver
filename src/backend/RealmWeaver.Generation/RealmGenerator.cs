using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Events;
using RealmWeaver.Generation.Field;
using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Randomness;

namespace RealmWeaver.Generation;

/// <summary>
/// Entry point: turns a <see cref="GenerationRequest"/> into a <see cref="RealmDocument"/>.
/// Pure and deterministic: the same request always produces the same document.
/// </summary>
public static class RealmGenerator
{
    public static RealmDocument Generate(GenerationRequest request, IContentCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        catalog ??= StaticContentCatalog.Default;

        if (request.Kind != RealmKind.Field)
        {
            throw new RealmGenerationException($"Realm kind {request.Kind} is not supported yet (dungeons arrive in Milestone 3).");
        }

        var root = new SeededRandom(request.Seed);
        var (width, height) = ResolveSize(request, root.Fork("size"));

        // Terrain depends only on the seed, biome and size; encounters also depend on the variant.
        var terrain = FieldTerrainGenerator.Generate(root.Fork("field/terrain"), request.Biome, width, height);
        var eventRng = root.Fork($"field/events/{request.EncounterVariant}");
        var count = EventBudget.Resolve(terrain.Area, request.EventCount, eventRng.Fork("count"));
        var placement = new FieldEventPlacer(terrain, catalog, request).Place(count, eventRng);

        var field = new RealmArea
        {
            Id = "field",
            Kind = AreaKind.Field,
            Name = $"{request.Biome} field",
            Width = width,
            Height = height,
            TerrainRows = terrain.ToRows(),
            Spawn = placement.Spawn,
            Events = placement.Events,
        };

        return new RealmDocument
        {
            GeneratorVersion = GeneratorLimits.Version,
            Seed = request.Seed,
            Request = request,
            Threat = request.Threat,
            Areas = [field],
        };
    }

    public static (int Width, int Height) ResolveSize(GenerationRequest request, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rng);

        var width = request.Width ?? rng.NextInclusive(GeneratorLimits.MinSize, GeneratorLimits.MaxSize);

        // A random height stays within 25% of the width so random maps are not long thin strips.
        var height = request.Height ?? Math.Clamp(
            rng.NextInclusive((int)(width * 0.75), (int)Math.Ceiling(width * 1.25)),
            GeneratorLimits.MinSize,
            GeneratorLimits.MaxSize);

        return (width, height);
    }

    private static void Validate(GenerationRequest request)
    {
        static void CheckSize(int? value, string name)
        {
            if (value is { } v && (v < GeneratorLimits.MinSize || v > GeneratorLimits.MaxSize))
            {
                throw new RealmGenerationException($"{name} must be between {GeneratorLimits.MinSize} and {GeneratorLimits.MaxSize}, but was {v}.");
            }
        }

        CheckSize(request.Width, nameof(request.Width));
        CheckSize(request.Height, nameof(request.Height));

        if (request.Difficulty is < GeneratorLimits.MinDifficulty or > GeneratorLimits.MaxDifficulty)
        {
            throw new RealmGenerationException($"Difficulty must be between {GeneratorLimits.MinDifficulty} and {GeneratorLimits.MaxDifficulty}.");
        }

        if (request.DangerModifier < 0)
        {
            throw new RealmGenerationException("Danger modifier cannot be negative.");
        }

        if (request.FloorCount < 1 || request.FloorCount > GeneratorLimits.MaxFloors)
        {
            throw new RealmGenerationException($"Floor count must be between 1 and {GeneratorLimits.MaxFloors}.");
        }
    }
}
