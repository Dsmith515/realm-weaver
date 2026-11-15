using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Export;
using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Objectives;
using RealmWeaver.Generation.Randomness;

namespace RealmWeaver.Generation.Tests;

/// <summary>Objective rules, checked over many seeds, kinds and biomes.</summary>
public class ObjectiveTests
{
    private static readonly Biome[] Biomes = Enum.GetValues<Biome>();
    private static readonly RealmKind[] Kinds = Enum.GetValues<RealmKind>();
    private static readonly Dictionary<string, EventTemplate> Templates = StaticContentCatalog.Default.EventTemplates.ToDictionary(t => t.Id);

    public static TheoryData<ulong> Seeds()
    {
        var data = new TheoryData<ulong>();
        for (ulong seed = 1; seed <= 150; seed++)
        {
            data.Add(seed * 7919);
        }

        return data;
    }

    private static GenerationRequest RequestFor(ulong seed) => new()
    {
        Seed = seed,
        Kind = Kinds[(int)(seed % (ulong)Kinds.Length)],
        Biome = Biomes[(int)(seed / 3 % (ulong)Biomes.Length)],
        FloorCount = (int)(seed % 3) + 1,
        Difficulty = (int)(seed % 10) + 1,
        Width = 20 + (int)(seed % 50),
        Height = 20 + (int)(seed % 40),
        MobTags = (seed % 4) switch { 0 => ["undead"], 1 => ["humanoid"], 2 => ["goblinoid"], _ => [] },
    };

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Every_realm_gets_one_to_three_valid_objectives(ulong seed)
    {
        var realm = RealmGenerator.Generate(RequestFor(seed));

        Assert.InRange(realm.Objectives.Count, GeneratorLimits.MinObjectives, GeneratorLimits.MaxObjectives);
        Assert.Equal(realm.Objectives.Count, realm.Objectives.Select(o => o.Key).Distinct().Count());
        Assert.True(realm.Objectives.Count(o => o.Scope == ObjectiveScope.Realm) <= 1, "At most one realm-wide objective.");
        Assert.Equal(
            realm.Objectives.Count(o => o.Scope == ObjectiveScope.Event),
            realm.Objectives.Where(o => o.Scope == ObjectiveScope.Event).Select(o => (o.AreaId, o.EventId)).Distinct().Count());

        for (var i = 0; i < realm.Objectives.Count; i++)
        {
            var objective = realm.Objectives[i];
            Assert.Equal($"O{i + 1}", objective.Id);
            if (objective.Scope == ObjectiveScope.Realm)
            {
                Assert.Null(objective.EventId);
                continue;
            }

            // An event objective points at a real event whose template offers that hook.
            var area = Assert.Single(realm.Areas, a => a.Id == objective.AreaId);
            var placed = Assert.Single(area.Events, e => e.Id == objective.EventId);
            Assert.Contains(Templates[placed.TemplateId].ObjectiveHooks, h => h.Id == objective.TemplateId);
            Assert.Equal(placed.Location, objective.Location);
            if (objective.TemplateId == "settle-the-dispute")
            {
                Assert.NotEqual(SettlementPopulation.Abandoned, placed.Population);
            }
        }
    }

    [Fact]
    public void Objectives_are_deterministic()
    {
        var request = RequestFor(1234) with { Kind = RealmKind.FieldWithDungeon, FloorCount = 3 };
        Assert.Equal(RealmGenerator.Generate(request).Objectives, RealmGenerator.Generate(request).Objectives);
    }

    [Fact]
    public void Charting_every_floor_is_only_offered_with_dungeon_floors()
    {
        var fields = Enumerable.Range(1, 80).Select(i => RealmGenerator.Generate(RequestFor((ulong)i) with { Kind = RealmKind.Field }));
        Assert.DoesNotContain(fields.SelectMany(r => r.Objectives), o => o.TemplateId == "chart-every-floor");

        var dungeon = RealmGenerator.Generate(RequestFor(5) with { Kind = RealmKind.Dungeon });
        Assert.Contains(ObjectivePicker.Candidates(dungeon.Areas, StaticContentCatalog.Default), o => o.TemplateId == "chart-every-floor");
    }

    [Fact]
    public void A_boss_chamber_offers_defeat_the_guardian()
    {
        var dungeon = RealmGenerator.Generate(RequestFor(77) with { Kind = RealmKind.Dungeon, FloorCount = 2, Difficulty = 6 });
        var boss = dungeon.Areas[^1].Events.Single(e => e.TemplateId == "boss-chamber");

        var candidate = Assert.Single(ObjectivePicker.Candidates(dungeon.Areas, StaticContentCatalog.Default), o => o.TemplateId == "defeat-the-guardian");
        Assert.Equal(boss.Id, candidate.EventId);
        Assert.Equal(dungeon.Areas[^1].Id, candidate.AreaId);
    }

    [Fact]
    public void Picking_never_offers_more_than_the_pool_holds()
    {
        // A pool of one realm-wide objective: no events with hooks at all.
        var area = new RealmArea
        {
            Id = "field", Kind = AreaKind.Field, Name = "Empty", Width = 15, Height = 15, TerrainRows = [], Events = [],
        };

        for (ulong seed = 1; seed <= 30; seed++)
        {
            var picked = ObjectivePicker.Pick([area], StaticContentCatalog.Default, new SeededRandom(seed));
            var only = Assert.Single(picked);
            Assert.Equal("clear-the-realm", only.TemplateId);
        }
    }

    [Fact]
    public void Picks_spread_across_one_two_and_three_objectives()
    {
        var counts = Enumerable.Range(1, 120)
            .Select(i => RealmGenerator.Generate(RequestFor((ulong)i * 31) with { Kind = RealmKind.FieldWithDungeon, FloorCount = 2 }).Objectives.Count)
            .ToHashSet();

        Assert.Equal([1, 2, 3], counts.Order());
    }

    [Fact]
    public void The_placement_sheet_lists_the_objectives()
    {
        var realm = RealmGenerator.Generate(RequestFor(42) with { Kind = RealmKind.FieldWithDungeon });
        var markdown = MarkdownExporter.Build(realm);

        Assert.Contains("## Objectives (complete any one)", markdown, StringComparison.Ordinal);
        Assert.All(realm.Objectives, o => Assert.Contains(o.Title, markdown, StringComparison.Ordinal));
    }

    [Fact]
    public void Documents_from_before_objectives_still_load()
    {
        // Objectives are the document's last property, so cutting them off leaves a 0.4.0-shaped document.
        var json = RealmJson.Serialize(RealmGenerator.Generate(RequestFor(9)));
        var legacy = json[..json.IndexOf("\"objectives\"", StringComparison.Ordinal)].TrimEnd().TrimEnd(',') + "\n}";

        Assert.Empty(RealmJson.Deserialize(legacy).Objectives);
    }
}
