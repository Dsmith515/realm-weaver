using RealmWeaver.Generation.Export;
using RealmWeaver.Generation.Model;

namespace RealmWeaver.Generation.Tests;

public class RealmDigestTests
{
    private static RealmDocument Realm(ulong seed) => RealmGenerator.Generate(new GenerationRequest
    {
        Seed = seed,
        Kind = RealmKind.FieldWithDungeon,
        FloorCount = 2,
        Biome = Biome.Forest,
        MobTags = ["beast", "bandit"],
        Difficulty = 4,
        Width = 50,
        Height = 40,
    });

    [Theory]
    [InlineData(1UL)]
    [InlineData(42UL)]
    [InlineData(9001UL)]
    public void Mob_totals_add_up_to_every_mob_in_every_area(ulong seed)
    {
        var realm = Realm(seed);

        var digest = RealmDigest.From(realm);

        var expected = realm.Areas.SelectMany(a => a.Events).SelectMany(e => e.Mobs).Sum(m => m.Count);
        Assert.Equal(expected, digest.Mobs.Sum(m => m.Count));
        Assert.Equal(digest.Mobs.Count, digest.Mobs.Select(m => m.ArchetypeId).Distinct().Count());
        Assert.Equal(digest.Mobs.OrderByDescending(m => m.Count).Select(m => m.Count), digest.Mobs.Select(m => m.Count));
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(42UL)]
    public void Loot_lists_every_chest_and_vault_and_resources_list_every_node(ulong seed)
    {
        var realm = Realm(seed);

        var digest = RealmDigest.From(realm);

        var chests = realm.Areas.Sum(a => a.Features.Count(f => f.Kind == FeatureKind.Chest));
        var vaults = realm.Areas.Sum(a => a.Events.Count(e => e.TemplateId == RealmDigest.TreasureVaultTemplateId));
        var nodes = realm.Areas.Sum(a => a.Events.Count(e => e.TemplateId == RealmDigest.ResourceNodeTemplateId));
        Assert.Equal(chests + vaults, digest.Loot.Count);
        Assert.Equal(nodes, digest.ResourceNodes.Count);
        Assert.All(digest.Loot.Concat(digest.ResourceNodes), e => Assert.False(string.IsNullOrEmpty(e.Location)));
    }

    [Fact]
    public void The_same_realm_always_gives_the_same_digest()
    {
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(RealmDigest.From(Realm(7))),
            System.Text.Json.JsonSerializer.Serialize(RealmDigest.From(Realm(7))));
    }
}
