using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Export;
using RealmWeaver.Generation.Field;
using RealmWeaver.Generation.Grid;
using RealmWeaver.Generation.Model;

namespace RealmWeaver.Generation.Tests;

/// <summary>
/// Property-style tests: generate many realms and check the rules that must hold for every seed.
/// </summary>
public class FieldInvariantTests
{
    private static readonly Biome[] Biomes = Enum.GetValues<Biome>();

    public static TheoryData<ulong> Seeds()
    {
        var data = new TheoryData<ulong>();
        for (ulong seed = 1; seed <= 200; seed++)
        {
            data.Add(seed * 7919);
        }

        return data;
    }

    private static GenerationRequest RequestFor(ulong seed) => new()
    {
        Seed = seed,
        Biome = Biomes[(int)(seed % (ulong)Biomes.Length)],
        MobTags = (seed % 3) switch { 0 => ["beast"], 1 => ["undead", "bandit"], _ => [] },
        Difficulty = (int)(seed % 10) + 1,
    };

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Random_realms_satisfy_all_field_rules(ulong seed)
    {
        var realm = RealmGenerator.Generate(RequestFor(seed));
        AssertFieldRules(realm.Areas.Single());
    }

    [Theory]
    [InlineData(15, 15, 6)]
    [InlineData(15, 15, null)]
    [InlineData(100, 100, 60)]
    [InlineData(100, 100, 5)]
    [InlineData(100, 15, null)]
    [InlineData(40, 60, 20)]
    public void Manual_sizes_and_counts_are_honored(int width, int height, int? events)
    {
        foreach (var biome in Biomes)
        {
            var request = new GenerationRequest { Seed = 2024, Width = width, Height = height, EventCount = events, Biome = biome };
            var area = RealmGenerator.Generate(request).Areas.Single();

            Assert.Equal(width, area.Width);
            Assert.Equal(height, area.Height);
            if (events is { } expected)
            {
                Assert.Equal(expected, area.Events.Count);
            }

            AssertFieldRules(area);
        }
    }

    [Fact]
    public void Same_request_produces_identical_realms()
    {
        var request = RequestFor(31337);
        Assert.Equal(RealmJson.Serialize(RealmGenerator.Generate(request)), RealmJson.Serialize(RealmGenerator.Generate(request)));
    }

    [Fact]
    public void Encounter_variant_changes_events_but_keeps_terrain()
    {
        var original = RealmGenerator.Generate(RequestFor(555)).Areas.Single();
        var variant = RealmGenerator.Generate(RequestFor(555) with { EncounterVariant = 1, DangerModifier = 2 }).Areas.Single();

        Assert.Equal(original.Width, variant.Width);
        Assert.Equal(original.Height, variant.Height);

        // Settlements and camps may clear tiles under their own footprint; everywhere else terrain is identical.
        var cleared = original.Events.Concat(variant.Events).Select(e => e.Footprint).ToList();
        for (var y = 0; y < original.Height; y++)
        {
            for (var x = 0; x < original.Width; x++)
            {
                var p = new GridPoint(x, y);
                if (!cleared.Any(r => r.Contains(p)))
                {
                    Assert.Equal(original.TerrainRows[y][x], variant.TerrainRows[y][x]);
                }
            }
        }

        Assert.NotEqual(
            original.Events.Select(e => (e.TemplateId, e.Footprint)),
            variant.Events.Select(e => (e.TemplateId, e.Footprint)));
    }

    [Fact]
    public void Changing_event_count_does_not_change_map_size()
    {
        var few = RealmGenerator.Generate(RequestFor(77) with { EventCount = 5 }).Areas.Single();
        var many = RealmGenerator.Generate(RequestFor(77) with { EventCount = 6 }).Areas.Single();
        Assert.Equal((few.Width, few.Height), (many.Width, many.Height));
    }

    [Fact]
    public void Higher_threat_unlocks_difficulty_templates()
    {
        var easy = RealmGenerator.Generate(new GenerationRequest { Seed = 9, Width = 80, Height = 80, EventCount = 40, Difficulty = 1 });
        var hard = RealmGenerator.Generate(new GenerationRequest { Seed = 9, Width = 80, Height = 80, EventCount = 40, Difficulty = 10, DangerModifier = 2 });

        Assert.All(easy.Areas[0].Events, e => Assert.Empty(e.DifficultyTemplateIds));
        Assert.Contains(hard.Areas[0].Events, e => e.DifficultyTemplateIds.Count > 0);
    }

    [Fact]
    public void Mobs_come_from_the_catalog_and_prefer_the_coating_tags()
    {
        var catalog = StaticContentCatalog.Default;
        var realm = RealmGenerator.Generate(new GenerationRequest { Seed = 4, Width = 60, Height = 60, EventCount = 25, MobTags = ["undead"] });
        var mobs = realm.Areas[0].Events.SelectMany(e => e.Mobs).ToList();

        Assert.NotEmpty(mobs);
        Assert.All(mobs, m => Assert.Contains(catalog.MobArchetypes, a => a.Id == m.ArchetypeId));
        Assert.All(mobs, m => Assert.Contains("undead", catalog.MobArchetypes.Single(a => a.Id == m.ArchetypeId).Tags));
    }

    [Theory]
    [InlineData(14, 50)]
    [InlineData(101, 50)]
    public void Invalid_sizes_are_rejected(int width, int height) =>
        Assert.Throws<RealmGenerationException>(() => RealmGenerator.Generate(new GenerationRequest { Seed = 1, Width = width, Height = height }));

    private static void AssertFieldRules(RealmArea area)
    {
        var map = TerrainMap.FromRows(area.TerrainRows);
        var limits = (Min: GeneratorLimits.MinEvents, Max: GeneratorLimits.AbsoluteMaxEvents);

        Assert.InRange(area.Width, GeneratorLimits.MinSize, GeneratorLimits.MaxSize);
        Assert.InRange(area.Height, GeneratorLimits.MinSize, GeneratorLimits.MaxSize);
        Assert.InRange(area.Events.Count, limits.Min, limits.Max);

        // Every passable tile is reachable from every other.
        Assert.Single(Connectivity.PassableRegions(map));

        // Spawn exists, is passable, and is at least 5 tiles from every event.
        var spawn = Assert.NotNull(area.Spawn);
        Assert.True(map.IsPassable(spawn), $"spawn {spawn} is not passable");
        Assert.All(area.Events, e => Assert.True(
            e.Footprint.ChebyshevDistanceTo(spawn) >= GeneratorLimits.SpawnClearance,
            $"{e.Id} at {e.Location} is closer than {GeneratorLimits.SpawnClearance} to spawn {spawn}"));

        foreach (var e in area.Events)
        {
            // In bounds, passable, and no larger than half the map.
            Assert.True(e.Footprint.X >= 0 && e.Footprint.Y >= 0 && e.Footprint.Right < area.Width && e.Footprint.Bottom < area.Height);
            Assert.True(e.Footprint.Area <= area.Width * area.Height / 2, $"{e.Id} covers more than half the map");
            Assert.All(e.Footprint.Tiles(), p => Assert.True(map.IsPassable(p), $"{e.Id} has impassable tile {p}"));
        }

        // No overlaps (and at least a one-tile gap).
        for (var i = 0; i < area.Events.Count; i++)
        {
            for (var j = i + 1; j < area.Events.Count; j++)
            {
                var a = area.Events[i].Footprint;
                var b = area.Events[j].Footprint;
                var paddedA = new GridRect(a.X - 1, a.Y - 1, a.Width + 2, a.Height + 2);
                Assert.False(paddedA.Intersects(b), $"{area.Events[i].Id} and {area.Events[j].Id} touch or overlap");
            }
        }

        Assert.Equal(area.Events.Count, area.Events.Select(e => e.Id).Distinct().Count());
    }
}
