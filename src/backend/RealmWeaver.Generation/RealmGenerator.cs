using System.Globalization;
using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Dungeon;
using RealmWeaver.Generation.Events;
using RealmWeaver.Generation.Field;
using RealmWeaver.Generation.Grid;
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

        var root = new SeededRandom(request.Seed);
        var (width, height) = ResolveSize(request, root.Fork("size"));
        var areas = new List<RealmArea>();
        DungeonTheme? theme = null;

        switch (request.Kind)
        {
            case RealmKind.Field:
                areas.Add(GenerateField(root, request, catalog, width, height, host: null).Area);
                break;

            case RealmKind.FieldWithDungeon:
            {
                var host = PickHost(root.Fork("host"), request.Biome, catalog);
                var (field, hostEvent) = GenerateField(root, request, catalog, width, height, host);
                areas.Add(field);
                theme = ThemeFor(host, request.Biome);
                var entrance = field.Features.Single(f => f.Kind == FeatureKind.DungeonEntrance).Position;
                areas.AddRange(GenerateFloors(root, request, catalog, theme, width, height, entrance, hostEvent!.Footprint,
                    $"Stairs up to the {host.Name.ToLowerInvariant()} on the field ({entrance})"));
                break;
            }

            case RealmKind.Dungeon:
            {
                var host = PickHost(root.Fork("host"), request.Biome, catalog);
                theme = ThemeFor(host, request.Biome);
                areas.AddRange(GenerateFloors(root, request, catalog, theme, width, height, entrance: null, focus: null,
                    "Portal arrival point (the needle points to the way out)"));
                break;
            }

            default:
                throw new RealmGenerationException($"Unknown realm kind {request.Kind}.");
        }

        return new RealmDocument
        {
            GeneratorVersion = GeneratorLimits.Version,
            Seed = request.Seed,
            Request = request,
            Threat = request.Threat,
            Theme = theme,
            Areas = areas,
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

    private static (RealmArea Area, PlacedEvent? Host) GenerateField(
        SeededRandom root, GenerationRequest request, IContentCatalog catalog, int width, int height, EventTemplate? host)
    {
        // Terrain depends only on the seed, biome and size; encounters also depend on the variant.
        var terrain = FieldTerrainGenerator.Generate(root.Fork("field/terrain"), request.Biome, width, height);
        var eventRng = root.Fork($"field/events/{request.EncounterVariant}");
        var count = EventBudget.Resolve(terrain.Area, request.EventCount, eventRng.Fork("count"));

        // Spawn and host structure use the layout stream so they stay put when encounters are rerolled.
        var placement = new FieldEventPlacer(terrain, catalog, request).Place(count, eventRng, root.Fork("field/layout"), host);

        var features = new List<PlacedFeature>();
        if (placement.Host is { } hostEvent)
        {
            var center = hostEvent.Footprint.Center;
            features.Add(new PlacedFeature("X1", FeatureKind.DungeonEntrance, center, $"Way down into the {hostEvent.Name.ToLowerInvariant()}'s depths"));
        }

        var area = new RealmArea
        {
            Id = "field",
            Kind = AreaKind.Field,
            Name = $"{request.Biome} field",
            Width = width,
            Height = height,
            TerrainRows = terrain.ToRows(),
            Spawn = placement.Spawn,
            Events = placement.Events,
            Features = features,
        };

        return (area, placement.Host);
    }

    private static List<RealmArea> GenerateFloors(
        SeededRandom root, GenerationRequest request, IContentCatalog catalog, DungeonTheme theme,
        int width, int height, GridPoint? entrance, GridRect? focus, string entranceDescription)
    {
        var floors = new List<RealmArea>();
        for (var number = 1; number <= request.FloorCount; number++)
        {
            var ctx = new FloorContext(request, catalog, theme, width, height, number, request.FloorCount, entrance, focus, entranceDescription);
            var floor = DungeonFloorGenerator.Generate(ctx, root.Fork("dungeon/floor/" + number.ToString(CultureInfo.InvariantCulture)));
            floors.Add(floor);

            // The next floor starts where this floor's stairs go down.
            if (floor.Features.FirstOrDefault(f => f.Kind == FeatureKind.StairsDown) is { } stairs)
            {
                entrance = stairs.Position;
                focus = null;
                entranceDescription = $"Stairs up to floor {number}";
            }
        }

        return floors;
    }

    private static EventTemplate PickHost(SeededRandom rng, Biome biome, IContentCatalog catalog)
    {
        var hosts = catalog.EventTemplates
            .Where(t => t.IsDungeonHost && (t.Biomes.Count == 0 || t.Biomes.Contains(biome)))
            .ToList();
        if (hosts.Count == 0)
        {
            throw new RealmGenerationException($"The content catalog has no dungeon host structure for the {biome} biome.");
        }

        return rng.PickWeighted(hosts, t => t.Weight);
    }

    private static DungeonTheme ThemeFor(EventTemplate host, Biome biome)
    {
        var primary = host.HostMaterial!.Value;
        var branches = DungeonMaterials.BranchesFor(biome).Where(m => m != primary).ToList();
        return new DungeonTheme(host.Id, host.Name, primary, branches);
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
