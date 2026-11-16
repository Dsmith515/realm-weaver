using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Events;
using RealmWeaver.Generation.Export;
using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Randomness;

namespace RealmWeaver.Generation.Tests;

public class SettlementTests
{
    private static readonly EventTemplate Village = StaticContentCatalog.Default.EventTemplates.Single(t => t.Id == "village");

    private static GenerationRequest Goblins(ulong seed, SettlementPopulation? population = null, bool structures = true) => new()
    {
        Seed = seed,
        Width = 40,
        Height = 17,
        Biome = Biome.Tundra,
        MobTags = ["goblinoid"],
        Difficulty = 2,
        SettlementPopulation = population,
        IncludeStructures = structures,
    };

    /// <summary>Settlement events from many 40x17 goblin realms.</summary>
    private static List<PlacedEvent> Settlements(SettlementPopulation? population = null, bool structures = true) =>
        Enumerable.Range(1, 120)
            .Select(i => RealmGenerator.Generate(Goblins((ulong)i * 104729, population, structures)))
            .SelectMany(r => r.Areas.SelectMany(a => a.Events))
            .Where(e => e.Category == EventCategory.Settlement)
            .ToList();

    private static int Total(IEnumerable<MobGroup> groups) => groups.Sum(g => g.Count);

    [Fact]
    public void Settlement_residents_grow_with_footprint_size()
    {
        var resolver = new MobResolver(StaticContentCatalog.Default, Goblins(1));

        var small = Total(resolver.ResolveSettlement(Village, 36, SettlementPopulation.Modest, new SeededRandom(5)));
        var large = Total(resolver.ResolveSettlement(Village, 196, SettlementPopulation.Modest, new SeededRandom(5)));

        Assert.True(large > small, $"A 14x14 village has {large} mobs, a 6x6 village has {small}.");
        Assert.True(large >= 10, $"A 14x14 Modest village should hold about 10 mobs, not {large}.");
    }

    [Fact]
    public void Busier_settlements_hold_more_residents()
    {
        var resolver = new MobResolver(StaticContentCatalog.Default, Goblins(1));
        int Count(SettlementPopulation p) => Total(resolver.ResolveSettlement(Village, 150, p, new SeededRandom(9)));

        Assert.True(Count(SettlementPopulation.Sparse) < Count(SettlementPopulation.Modest));
        Assert.True(Count(SettlementPopulation.Modest) < Count(SettlementPopulation.Bustling));
        Assert.True(Count(SettlementPopulation.Bustling) < Count(SettlementPopulation.Overcrowded));
        Assert.True(Count(SettlementPopulation.Overcrowded) <= Events.Settlements.MaxResidents);
    }

    [Fact]
    public void A_large_village_is_no_longer_guarded_by_three_mobs()
    {
        var villages = Settlements(SettlementPopulation.Modest).Where(e => e.TemplateId == "village" && e.Footprint.Area >= 100).ToList();

        Assert.NotEmpty(villages);
        Assert.All(villages, v => Assert.True(Total(v.Mobs) >= 6, $"{v.Footprint.Width}x{v.Footprint.Height} village has {Total(v.Mobs)} mobs."));
    }

    [Fact]
    public void Every_settlement_gets_a_population_and_other_events_do_not()
    {
        var realms = Enumerable.Range(1, 60).Select(i => RealmGenerator.Generate(new GenerationRequest
        {
            Seed = (ulong)i * 31,
            Kind = RealmKind.FieldWithDungeon,
            Biome = Biome.Forest,
            FloorCount = 2,
        }));
        var events = realms.SelectMany(r => r.Areas.SelectMany(a => a.Events)).ToList();

        Assert.All(events.Where(e => e.Category == EventCategory.Settlement), e => Assert.NotNull(e.Population));
        Assert.All(events.Where(e => e.Category == EventCategory.Settlement), e => Assert.NotNull(e.Wealth));
        Assert.All(events.Where(e => e.Category != EventCategory.Settlement), e => Assert.Null(e.Population));
        Assert.All(events.Where(e => e.Category != EventCategory.Settlement), e => Assert.Null(e.Wealth));
        Assert.Contains(events, e => e.TemplateId == "underground-settlement");
    }

    [Fact]
    public void A_requested_population_is_used_for_every_settlement()
    {
        var settlements = Settlements(SettlementPopulation.Bustling);

        Assert.NotEmpty(settlements);
        Assert.All(settlements, e => Assert.Equal(SettlementPopulation.Bustling, e.Population));
    }

    [Fact]
    public void Settlements_vary_in_wealth_and_abandoned_ones_are_at_best_poor()
    {
        var settlements = Settlements();
        var abandoned = Settlements(SettlementPopulation.Abandoned);

        Assert.True(settlements.Select(e => e.Wealth).Distinct().Count() >= 4);
        Assert.All(abandoned, e => Assert.True(e.Wealth <= SettlementWealth.Poor));
    }

    [Fact]
    public void The_digest_lists_every_settlement_with_its_population_and_wealth()
    {
        var realm = Enumerable.Range(1, 120)
            .Select(i => RealmGenerator.Generate(Goblins((ulong)i * 104729)))
            .First(r => r.Areas.SelectMany(a => a.Events).Count(e => e.Category == EventCategory.Settlement) > 1);
        var expected = realm.Areas.SelectMany(a => a.Events).Where(e => e.Category == EventCategory.Settlement).ToList();

        var digest = RealmDigest.From(realm);

        Assert.Equal(expected.Select(e => (e.Id, e.Population, e.Wealth)), digest.Settlements.Select(s => (s.Id, s.Population, s.Wealth)));
    }

    [Fact]
    public void Abandoned_settlements_hold_only_squatters_among_ruins()
    {
        var settlements = Settlements(SettlementPopulation.Abandoned);

        Assert.All(settlements, e => Assert.True(Total(e.Mobs) <= 2));
        Assert.All(settlements.SelectMany(e => e.Structures), b => Assert.True(b.Ruined));
        Assert.Contains(settlements, e => e.Structures.Count > 0);
    }

    [Fact]
    public void Structures_sit_inside_their_settlement_with_a_path_between_them()
    {
        var settlements = Settlements();

        Assert.Contains(settlements, e => e.Structures.Count >= 3);
        foreach (var e in settlements)
        {
            foreach (var b in e.Structures)
            {
                Assert.True(b.Footprint.X >= e.Footprint.X && b.Footprint.Right <= e.Footprint.Right, $"{b.Id} leaves {e.Id}.");
                Assert.True(b.Footprint.Y >= e.Footprint.Y && b.Footprint.Bottom <= e.Footprint.Bottom, $"{b.Id} leaves {e.Id}.");
                Assert.DoesNotContain(e.Structures, other => other != b && other.Footprint.Intersects(b.Footprint.Expand(1)));
            }
        }
    }

    [Fact]
    public void Goblin_settlements_get_goblin_structures()
    {
        var goblinOnly = StaticContentCatalog.Default.StructureTemplates.Where(t => t.Tags.Contains("goblinoid")).Select(t => t.Id).ToHashSet();
        var structures = Settlements().SelectMany(e => e.Structures).ToList();

        Assert.Contains(structures, b => goblinOnly.Contains(b.TemplateId));
        Assert.DoesNotContain(structures, b => b.TemplateId is "cottage" or "barrow");
    }

    [Fact]
    public void Structure_ids_are_unique_within_an_area()
    {
        var realms = Enumerable.Range(1, 80).Select(i => RealmGenerator.Generate(Goblins((ulong)i * 7)));
        foreach (var area in realms.SelectMany(r => r.Areas))
        {
            var ids = area.Events.SelectMany(e => e.Structures).Select(b => b.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }
    }

    [Fact]
    public void Turning_structures_off_removes_them_without_changing_the_mobs()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var with = RealmGenerator.Generate(Goblins(seed * 13)).Areas.Single().Events;
            var without = RealmGenerator.Generate(Goblins(seed * 13, structures: false)).Areas.Single().Events;

            Assert.All(without, e => Assert.Empty(e.Structures));
            Assert.Equal(with.Select(e => e.Mobs.Sum(m => m.Count)), without.Select(e => e.Mobs.Sum(m => m.Count)));
        }
    }

    [Fact]
    public void Suggestions_name_structure_ideas_without_placing_them_or_changing_anything_else()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var plain = RealmGenerator.Generate(Goblins(seed * 13, structures: false));
            var suggested = RealmGenerator.Generate(Goblins(seed * 13, structures: false) with { SuggestStructures = true });
            var events = suggested.Areas.Single().Events;

            Assert.All(events, e => Assert.Empty(e.Structures));
            Assert.All(events.Where(e => e.Category != EventCategory.Settlement), e => Assert.Empty(e.SuggestedStructures));
            Assert.All(events.Where(e => e.Category == EventCategory.Settlement), e => Assert.NotEmpty(e.SuggestedStructures));
            Assert.Equal(plain.Areas.Single().Events.Select(e => (e.Id, e.Location, Mobs: e.Mobs.Sum(m => m.Count))), events.Select(e => (e.Id, e.Location, Mobs: e.Mobs.Sum(m => m.Count))));
            Assert.Equal(PromptExporter.Build(plain), PromptExporter.Build(suggested));
        }

        // Placed structures win: suggestions only apply when structures are off.
        Assert.All(RealmGenerator.Generate(Goblins(7) with { SuggestStructures = true }).Areas.Single().Events, e => Assert.Empty(e.SuggestedStructures));

        var withIdeas = Enumerable.Range(1, 60)
            .Select(i => RealmGenerator.Generate(Goblins((ulong)i * 104729, structures: false) with { SuggestStructures = true }))
            .First(r => r.Areas.Single().Events.Any(e => e.SuggestedStructures.Count > 0));
        var settlement = withIdeas.Areas.Single().Events.First(e => e.SuggestedStructures.Count > 0);
        Assert.Contains($"Structure ideas for {settlement.Id} {settlement.Name} (not placed): {settlement.SuggestedStructures[0].Name}", MarkdownExporter.Build(withIdeas), StringComparison.Ordinal);
    }

    [Fact]
    public void Settlements_are_deterministic()
    {
        var first = RealmJson.Serialize(RealmGenerator.Generate(Goblins(99)));
        var second = RealmJson.Serialize(RealmGenerator.Generate(Goblins(99)));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Exports_list_population_and_structures()
    {
        var realm = Enumerable.Range(1, 120)
            .Select(i => RealmGenerator.Generate(Goblins((ulong)i * 104729, SettlementPopulation.Modest)))
            .First(r => r.Areas.Single().Events.Any(e => e.Structures.Count > 0));
        var structure = realm.Areas.Single().Events.SelectMany(e => e.Structures).First();

        var markdown = MarkdownExporter.Build(realm);
        var svg = SvgExporter.Render(realm, realm.Areas.Single().Id);
        var prompt = PromptExporter.Build(realm);

        Assert.Contains("| Modest · ", markdown, StringComparison.Ordinal);
        Assert.Contains($"| {structure.Id} | {structure.Name} |", markdown, StringComparison.Ordinal);
        Assert.Contains($"data-structure-id=\"{structure.Id}\"", svg, StringComparison.Ordinal);
        Assert.Contains($"{structure.Name.ToLowerInvariant()} at {structure.Location}", prompt, StringComparison.Ordinal);
    }
}
