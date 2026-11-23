using System.Net;

using RealmWeaver.Api.IntegrationTests.Infrastructure;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;

using static RealmWeaver.Api.IntegrationTests.Infrastructure.RitualTestKit;

namespace RealmWeaver.Api.IntegrationTests;

[Collection(PostgresApiTests.Name)]
public class MapFlagsEndpointTests(RealmWeaverApiFactory factory)
{
    [Fact]
    public async Task New_maps_are_open_and_allow_mirrors_and_sharing()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));

        var map = await client.GetJsonAsync<ComponentDto>($"/api/components/{ritual.MapId}");

        Assert.Equal(new MapFlags(MapRequired: false, ComponentsMustMatch: false, Unique: false), map.Map!.Flags);
    }

    [Fact]
    public async Task Components_must_match_forbids_a_mirror()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, WheatgoldThread, BeastwardWax, BeastwardWax);
        var ritual = await PerformAndRecordAsync(client, SmallField(player.Id));
        await SetMapFlagsAsync(client, ritual.MapId, new MapFlags(ComponentsMustMatch: true));

        var preview = await PreviewAsync(client, new RitualPreviewRequest(player.Id, ThreadId: WheatgoldThread, MapId: ritual.MapId));
        var perform = await PerformAsync(client, new RitualSelection(player.Id, ThreadId: WheatgoldThread, MapId: ritual.MapId));
        var matching = await PerformAsync(client, new RitualSelection(player.Id, MapId: ritual.MapId));

        Assert.Contains(preview.Issues, i => i.Code == "mirror-not-allowed" && i.Slot == RitualSlot.Map);
        Assert.Equal(HttpStatusCode.BadRequest, perform.StatusCode);
        Assert.Equal(HttpStatusCode.Created, matching.StatusCode);
    }

    [Fact]
    public async Task A_sealed_map_only_opens_for_a_holder()
    {
        using var client = factory.CreateClient();
        var owner = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformAndRecordAsync(client, SmallField(owner.Id));
        var visitor = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);

        var open = await PreviewAsync(client, new RitualPreviewRequest(visitor.Id, MapId: ritual.MapId));
        Assert.True(open.CanPerform, string.Join("; ", open.Issues.Select(i => i.Message)));

        await SetMapFlagsAsync(client, ritual.MapId, new MapFlags(MapRequired: true));
        var sealedPreview = await PreviewAsync(client, new RitualPreviewRequest(visitor.Id, MapId: ritual.MapId));
        Assert.Contains(sealedPreview.Issues, i => i.Code == "not-owned" && i.Slot == RitualSlot.Map);

        var visit = await PerformOkAsync(client, new RitualSelection(owner.Id, MapId: ritual.MapId));
        Assert.False(visit.MapCreated);
        Assert.Equal(ritual.MapId, visit.MapId);
    }

    [Fact]
    public async Task The_same_ingredients_reopen_the_realm_they_wove_before_without_the_map()
    {
        using var client = factory.CreateClient();
        var needle = await CreateUniqueNeedleAsync(client);
        var first = await CreatePlayerWithAsync(client, needle, MossgreenThread, BeastwardWax);
        var woven = await PerformOkAsync(client, new RitualSelection(first.Id, needle, MossgreenThread, BeastwardWax));
        var second = await CreatePlayerWithAsync(client, needle, MossgreenThread, BeastwardWax);

        var preview = await PreviewAsync(client, new RitualPreviewRequest(second.Id, needle, MossgreenThread, BeastwardWax));
        Assert.Equal(SlotSource.KnownRecipe, preview.Map?.Source);
        Assert.Equal(woven.MapId, preview.Map?.Component.Id);
        Assert.False(preview.CreatesNewMap);

        var again = await PerformOkAsync(client, new RitualSelection(second.Id, needle, MossgreenThread, BeastwardWax));
        Assert.False(again.MapCreated);
        Assert.Equal(woven.MapId, again.MapId);
        Assert.Equal((await GetRealmAsync(client, woven.RealmId)).Summary.Seed, (await GetRealmAsync(client, again.RealmId)).Summary.Seed);
        Assert.False((await QuantitiesAsync(client, second.Id)).ContainsKey(woven.MapId));

        // Advanced size options weave a custom realm instead.
        var custom = await PerformOkAsync(client, new RitualSelection(second.Id, needle, MossgreenThread, BeastwardWax, Width: 22, Height: 22));
        Assert.True(custom.MapCreated);
    }

    [Fact]
    public async Task Ingredients_that_open_a_sealed_map_need_the_map()
    {
        using var client = factory.CreateClient();
        var needle = await CreateUniqueNeedleAsync(client);
        var owner = await CreatePlayerWithAsync(client, needle, MossgreenThread, MossgreenThread, BeastwardWax, BeastwardWax);
        var woven = await PerformAndRecordAsync(client, new RitualSelection(owner.Id, needle, MossgreenThread, BeastwardWax));
        await SetMapFlagsAsync(client, woven.MapId, new MapFlags(MapRequired: true));
        var outsider = await CreatePlayerWithAsync(client, needle, MossgreenThread, BeastwardWax);

        var preview = await PreviewAsync(client, new RitualPreviewRequest(outsider.Id, needle, MossgreenThread, BeastwardWax));
        Assert.Contains(preview.Issues, i => i.Code == "map-required" && i.Slot == RitualSlot.Map);
        Assert.False(preview.CanPerform);
        var refused = await PerformAsync(client, new RitualSelection(outsider.Id, needle, MossgreenThread, BeastwardWax));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        // The holder walks in.
        var holder = await PerformOkAsync(client, new RitualSelection(owner.Id, needle, MossgreenThread, BeastwardWax));
        Assert.Equal(woven.MapId, holder.MapId);
    }

    [Fact]
    public async Task A_unique_map_has_one_holder()
    {
        using var client = factory.CreateClient();
        var owner = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformAndRecordAsync(client, SmallField(owner.Id));
        await SetMapFlagsAsync(client, ritual.MapId, new MapFlags(Unique: true));
        var other = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);

        var assign = await client.PostJsonAsync($"/api/components/{ritual.MapId}/assign", new AssignComponentInput(other.Id));
        Assert.Equal(HttpStatusCode.Conflict, assign.StatusCode);

        // The other player can still visit (the map isn't sealed), but completing it can't give them a second copy.
        var visit = await PerformOkAsync(client, new RitualSelection(other.Id, MapId: ritual.MapId));
        var done = await RecordMapAsync(client, visit);
        Assert.Equal([new MapHandOutSkip(other.Id, "unique-held")], done.Skipped);
        Assert.False((await QuantitiesAsync(client, other.Id)).ContainsKey(ritual.MapId));
    }

    [Fact]
    public async Task A_map_two_players_hold_cant_become_unique()
    {
        using var client = factory.CreateClient();
        var owner = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformAndRecordAsync(client, SmallField(owner.Id));
        var other = await CreatePlayerWithAsync(client);
        var assign = await client.PostJsonAsync($"/api/components/{ritual.MapId}/assign", new AssignComponentInput(other.Id));
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var map = await client.GetJsonAsync<ComponentDto>($"/api/components/{ritual.MapId}");
        var input = new ComponentInput(map.Type, map.Name, map.Rarity, map.Rank, map.Description, map.IsPlayerAssignable, Map: map.Map! with { Flags = new MapFlags(Unique: true) });
        var response = await client.PutJsonAsync($"/api/components/{map.Id}", input);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False((await client.GetJsonAsync<ComponentDto>($"/api/components/{map.Id}")).Map!.Flags!.Unique);
    }

    [Fact]
    public async Task A_map_one_player_holds_can_become_unique()
    {
        using var client = factory.CreateClient();
        var owner = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformAndRecordAsync(client, SmallField(owner.Id));

        await SetMapFlagsAsync(client, ritual.MapId, new MapFlags(Unique: true));

        Assert.True((await client.GetJsonAsync<ComponentDto>($"/api/components/{ritual.MapId}")).Map!.Flags!.Unique);
    }

    [Fact]
    public async Task Editing_a_map_without_flags_keeps_them()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));
        await SetMapFlagsAsync(client, ritual.MapId, new MapFlags(MapRequired: true, ComponentsMustMatch: true, Unique: true));

        var map = await client.GetJsonAsync<ComponentDto>($"/api/components/{ritual.MapId}");
        var input = new ComponentInput(map.Type, map.Name, map.Rarity, map.Rank, "Renamed", map.IsPlayerAssignable, Map: map.Map! with { Flags = null });
        var updated = await (await client.PutJsonAsync($"/api/components/{map.Id}", input)).ReadJsonAsync<ComponentDto>();

        Assert.Equal(new MapFlags(true, true, true), updated.Map!.Flags);
    }

    /// <summary>A needle no other test uses, so the catalog has no known recipes for it yet.</summary>
    private static async Task<Guid> CreateUniqueNeedleAsync(HttpClient client)
    {
        var name = $"Recipe {Guid.NewGuid().ToString("N")[..6]} Needle";
        var input = new ComponentInput(ComponentType.Needle, name, Rarity.Common, 1, null, false, Needle: new NeedleDetails(1, "Test", ["Test Ability"]));
        var response = await client.PostJsonAsync("/api/components", input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadJsonAsync<ComponentDto>()).Id;
    }
}
