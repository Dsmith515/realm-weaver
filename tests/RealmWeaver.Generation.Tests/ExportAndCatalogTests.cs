using System.Xml.Linq;
using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Export;
using RealmWeaver.Generation.Model;

namespace RealmWeaver.Generation.Tests;

public class ExportTests
{
    private static readonly RealmDocument Realm = RealmGenerator.Generate(new GenerationRequest
    {
        Seed = 42,
        Width = 30,
        Height = 24,
        EventCount = 8,
        Biome = Biome.Forest,
        Element = Element.Shadow,
        MobTags = ["beast"],
        Difficulty = 4,
    });

    [Fact]
    public void Svg_is_valid_xml_with_labels_spawn_and_every_event()
    {
        var svg = XDocument.Parse(SvgExporter.Render(Realm));
        XNamespace ns = "http://www.w3.org/2000/svg";
        var area = Realm.Areas[0];

        Assert.Equal(ns + "svg", svg.Root!.Name);
        var labels = svg.Descendants(ns + "g").Single(g => (string?)g.Attribute("id") == "labels").Elements(ns + "text").Select(t => t.Value).ToList();
        Assert.Contains("A", labels);
        Assert.Contains("AD", labels); // 30th column
        Assert.Contains("24", labels);
        Assert.Single(svg.Descendants(ns + "g"), g => (string?)g.Attribute("id") == "spawn");

        var eventIds = svg.Descendants(ns + "g").Select(g => (string?)g.Attribute("data-event-id")).Where(id => id is not null).ToList();
        Assert.Equal(area.Events.Select(e => e.Id), eventIds);
    }

    [Fact]
    public void Prompt_describes_size_biome_and_every_location()
    {
        var prompt = PromptExporter.Build(Realm);
        var area = Realm.Areas[0];

        Assert.Contains("30 columns by 24 rows", prompt, StringComparison.Ordinal);
        Assert.Contains("forest", prompt, StringComparison.Ordinal);
        Assert.Contains("shadow", prompt, StringComparison.Ordinal);
        Assert.All(area.Events, e => Assert.Contains(e.Location, prompt, StringComparison.Ordinal));
        Assert.Contains(area.Spawn!.Value.ToString(), prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Markdown_lists_every_event_with_location()
    {
        var markdown = MarkdownExporter.Build(Realm);
        Assert.All(Realm.Areas[0].Events, e => Assert.Contains($"| {e.Id} | {e.Name} |", markdown, StringComparison.Ordinal));
    }

    [Fact]
    public void Json_round_trips_and_keeps_large_seeds_exact()
    {
        var big = RealmGenerator.Generate(new GenerationRequest { Seed = ulong.MaxValue - 1, Width = 20, Height = 20 });
        var json = RealmJson.Serialize(big);

        Assert.Contains($"\"seed\": \"{ulong.MaxValue - 1}\"", json, StringComparison.Ordinal);
        Assert.Equal(json, RealmJson.Serialize(RealmJson.Deserialize(json)));
    }
}

public class CatalogTests
{
    private readonly StaticContentCatalog _catalog = StaticContentCatalog.Default;

    [Fact]
    public void Ids_are_unique()
    {
        Assert.Equal(_catalog.EventTemplates.Count, _catalog.EventTemplates.Select(t => t.Id).Distinct().Count());
        Assert.Equal(_catalog.MobArchetypes.Count, _catalog.MobArchetypes.Select(a => a.Id).Distinct().Count());
        Assert.Equal(_catalog.DifficultyTemplates.Count, _catalog.DifficultyTemplates.Select(d => d.Id).Distinct().Count());
    }

    [Fact]
    public void Footprints_and_mob_slots_are_sane()
    {
        Assert.All(_catalog.EventTemplates, t =>
        {
            Assert.InRange(t.Footprint.MinSide, 1, t.Footprint.MaxSide);
            Assert.True(t.Weight > 0);
            Assert.All(t.MobSlots, s => Assert.InRange(s.MinCount, 0, s.MaxCount));
        });
    }

    [Fact]
    public void Every_role_used_by_a_template_has_an_archetype()
    {
        var roles = _catalog.EventTemplates.SelectMany(t => t.MobSlots).Select(s => s.Role).Distinct();
        Assert.All(roles, role => Assert.Contains(_catalog.MobArchetypes, a => a.Roles.Contains(role)));
    }

    [Fact]
    public void Every_biome_has_small_fallback_events_for_fields()
    {
        foreach (var biome in Enum.GetValues<Biome>())
        {
            Assert.Contains(_catalog.EventTemplates, t =>
                t.Environments.HasFlag(Environments.Field) && t.Footprint.MaxSide <= 2 &&
                t.MaxPerArea == int.MaxValue && (t.Biomes.Count == 0 || t.Biomes.Contains(biome)));
        }
    }
}
