using System.Net;

using RealmWeaver.Api.IntegrationTests.Infrastructure;
using RealmWeaver.Generation.Model;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Realms.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;

using static RealmWeaver.Api.IntegrationTests.Infrastructure.RitualTestKit;

namespace RealmWeaver.Api.IntegrationTests;

[Collection(PostgresApiTests.Name)]
public class QuickWeaveEndpointTests(RealmWeaverApiFactory factory)
{
    private static QuickWeaveRequest Small(QuickWeaveLimits? limits = null) => new(Limits: limits, Width: 24, Height: 20);

    [Fact]
    public async Task Quick_weave_records_an_unowned_map_and_realm_without_touching_any_inventory()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var before = await QuantitiesAsync(client, player.Id);

        var result = await WeaveOkAsync(client, new QuickWeaveRequest(BeginnerNeedle, MossgreenThread, BeastwardWax, Width: 24, Height: 20));

        Assert.True(result.MapCreated);
        Assert.Equal(before, await QuantitiesAsync(client, player.Id));
        var map = await client.GetJsonAsync<ComponentDto>($"/api/components/{result.MapId}");
        Assert.Null(map.Map?.CreatedByPlayerId);
        Assert.Equal(Biome.Forest, map.Map?.Recipe?.Biome);
        var realm = await GetRealmAsync(client, result.RealmId);
        Assert.Null(realm.Summary.PlayerId);
        Assert.Contains((await client.GetJsonAsync<RealmSummaryPage>("/api/realms?gm=true")).Items, r => r.Id == result.RealmId);
        Assert.DoesNotContain((await client.GetJsonAsync<RealmSummaryPage>($"/api/realms?playerId={player.Id}")).Items, r => r.Id == result.RealmId);
    }

    [Fact]
    public async Task Random_picks_respect_every_limit()
    {
        using var client = factory.CreateClient();
        var limits = new QuickWeaveLimits(Rarities: [Rarity.Common, Rarity.Uncommon], MinTier: 1, MaxTier: 3, Biomes: [Biome.Forest, Biome.Plains], Kinds: [RealmKind.Field]);

        for (var i = 0; i < 15; i++)
        {
            var preview = await PreviewAsync(client, Small(limits));

            Assert.True(preview.CanWeave, string.Join("; ", preview.Issues.Select(x => x.Message)));
            Assert.All(new[] { preview.Needle!, preview.Thread!, preview.Coating! }, s =>
            {
                Assert.Equal(SlotSource.Random, s.Source);
                Assert.Contains(s.Component.Rarity, limits.Rarities!);
            });
            Assert.InRange(preview.Needle!.Component.Needle!.DifficultyTier, 1, 3);
            Assert.Contains(preview.Thread!.Component.Thread!.Biome, limits.Biomes!);
            Assert.Equal(RealmKind.Field, preview.Coating!.Component.Coating!.RealmKind);
        }
    }

    [Fact]
    public async Task Limits_nothing_fits_and_bad_tier_ranges_are_reported()
    {
        using var client = factory.CreateClient();

        var nothing = await PreviewAsync(client, Small(new QuickWeaveLimits(Rarities: [Rarity.Legendary], Kinds: [RealmKind.Field])));
        var backwards = await client.PostJsonAsync("/api/gm/weave", Small(new QuickWeaveLimits(MinTier: 6, MaxTier: 2)));

        Assert.Contains(nothing.Issues, i => i.Slot == RitualSlot.Coating && i.Code == "no-candidates");
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Contains("limits.maxTier", (await backwards.ReadJsonAsync<Problem>()).Errors!.Keys);
    }

    [Fact]
    public async Task Weaving_a_gm_map_again_rebuilds_the_same_realm_and_it_can_be_assigned()
    {
        using var client = factory.CreateClient();
        var first = await WeaveOkAsync(client, new QuickWeaveRequest(BeginnerNeedle, MossgreenThread, BrigandsPitch, Width: 24, Height: 20));

        var again = await WeaveOkAsync(client, new QuickWeaveRequest(MapId: first.MapId));

        Assert.False(again.MapCreated);
        Assert.False(again.IsMismatch);
        Assert.Equal(
            (await GetRealmAsync(client, first.RealmId)).Document.GetProperty("areas").GetRawText(),
            (await GetRealmAsync(client, again.RealmId)).Document.GetProperty("areas").GetRawText());

        var player = await CreatePlayerWithAsync(client);
        var assign = await client.PostJsonAsync($"/api/components/{first.MapId}/assign", new AssignComponentInput(player.Id));
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        Assert.Equal(1, (await QuantitiesAsync(client, player.Id))[first.MapId]);
    }

    [Fact]
    public async Task Quick_weave_leaves_structures_out_by_default_and_can_suggest_them_instead()
    {
        using var client = factory.CreateClient();
        var plain = await WeaveOkAsync(client, new QuickWeaveRequest(BeginnerNeedle, MossgreenThread, BrigandsPitch, Width: 60, Height: 40, EventCount: 20));
        var ideas = await WeaveOkAsync(
            client,
            new QuickWeaveRequest(BeginnerNeedle, MossgreenThread, BrigandsPitch, Width: 60, Height: 40, EventCount: 20, SuggestStructures: true));

        Assert.False((await client.GetJsonAsync<ComponentDto>($"/api/components/{plain.MapId}")).Map!.Recipe!.IncludeStructures);
        Assert.True((await client.GetJsonAsync<ComponentDto>($"/api/components/{ideas.MapId}")).Map!.Recipe!.SuggestStructures);

        static List<System.Text.Json.JsonElement> SettlementsOf(RealmView realm) => [.. realm.Document.GetProperty("areas").EnumerateArray().Where(a => a.GetProperty("kind").GetString() == "field")
            .SelectMany(a => a.GetProperty("events").EnumerateArray())
            .Where(e => e.GetProperty("category").GetString() == "settlement")];
        Assert.All(SettlementsOf(await GetRealmAsync(client, plain.RealmId)), e => Assert.Equal(0, e.GetProperty("structures").GetArrayLength()));
        var suggested = SettlementsOf(await GetRealmAsync(client, ideas.RealmId));
        Assert.All(suggested, e => Assert.Equal(0, e.GetProperty("structures").GetArrayLength()));
        Assert.All(suggested, e => Assert.NotEqual(0, e.GetProperty("suggestedStructures").GetArrayLength()));
    }

    [Fact]
    public async Task A_gm_map_records_its_settlement_settings_and_rebuilds_with_them()
    {
        using var client = factory.CreateClient();
        var first = await WeaveOkAsync(
            client,
            new QuickWeaveRequest(BeginnerNeedle, MossgreenThread, BrigandsPitch, Width: 60, Height: 40, EventCount: 20, Population: SettlementPopulation.Abandoned, IncludeStructures: false));

        var map = await client.GetJsonAsync<ComponentDto>($"/api/components/{first.MapId}");
        Assert.Equal(SettlementPopulation.Abandoned, map.Map!.Recipe!.SettlementPopulation);
        Assert.False(map.Map.Recipe.IncludeStructures);

        // Weaving the recorded map again uses its recipe, not the request's defaults.
        var again = await WeaveOkAsync(client, new QuickWeaveRequest(MapId: first.MapId));
        var areas = (await GetRealmAsync(client, again.RealmId)).Document.GetProperty("areas");
        Assert.Equal((await GetRealmAsync(client, first.RealmId)).Document.GetProperty("areas").GetRawText(), areas.GetRawText());

        var settlements = areas.EnumerateArray()
            .SelectMany(a => a.GetProperty("events").EnumerateArray())
            .Where(e => e.GetProperty("category").GetString() == "settlement")
            .ToList();
        Assert.All(settlements, e => Assert.Equal("abandoned", e.GetProperty("population").GetString()));
        Assert.All(settlements, e => Assert.Equal(0, e.GetProperty("structures").GetArrayLength()));
    }

    private static async Task<QuickWeavePreviewDto> PreviewAsync(HttpClient client, QuickWeaveRequest request)
    {
        var response = await client.PostJsonAsync("/api/gm/weave/preview", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync<QuickWeavePreviewDto>();
    }

    private static async Task<QuickWeaveResultDto> WeaveOkAsync(HttpClient client, QuickWeaveRequest request)
    {
        var response = await client.PostJsonAsync("/api/gm/weave", request);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected 201 but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.ReadJsonAsync<QuickWeaveResultDto>();
    }
}
