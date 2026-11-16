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
    public async Task New_maps_require_the_map_and_allow_mirrors_and_sharing()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));

        var map = await client.GetJsonAsync<ComponentDto>($"/api/components/{ritual.MapId}");

        Assert.Equal(new MapFlags(MapRequired: true, ComponentsMustMatch: false, Unique: false), map.Map!.Flags);
    }

    [Fact]
    public async Task Components_must_match_forbids_a_mirror()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, WheatgoldThread, BeastwardWax, BeastwardWax);
        var ritual = await PerformAndRecordAsync(client, SmallField(player.Id));
        await SetFlagsAsync(client, ritual.MapId, new MapFlags(ComponentsMustMatch: true));

        var preview = await PreviewAsync(client, new RitualPreviewRequest(player.Id, ThreadId: WheatgoldThread, MapId: ritual.MapId));
        var perform = await PerformAsync(client, new RitualSelection(player.Id, ThreadId: WheatgoldThread, MapId: ritual.MapId));
        var matching = await PerformAsync(client, new RitualSelection(player.Id, MapId: ritual.MapId));

        Assert.Contains(preview.Issues, i => i.Code == "mirror-not-allowed" && i.Slot == RitualSlot.Map);
        Assert.Equal(HttpStatusCode.BadRequest, perform.StatusCode);
        Assert.Equal(HttpStatusCode.Created, matching.StatusCode);
    }

    [Fact]
    public async Task With_map_required_off_anyone_holding_the_components_can_weave_it()
    {
        using var client = factory.CreateClient();
        var owner = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformAndRecordAsync(client, SmallField(owner.Id));
        var visitor = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);

        var locked = await PreviewAsync(client, new RitualPreviewRequest(visitor.Id, MapId: ritual.MapId));
        Assert.Contains(locked.Issues, i => i.Code == "not-owned" && i.Slot == RitualSlot.Map);

        await SetFlagsAsync(client, ritual.MapId, new MapFlags(MapRequired: false));
        var open = await PreviewAsync(client, new RitualPreviewRequest(visitor.Id, MapId: ritual.MapId));
        Assert.True(open.CanPerform, string.Join("; ", open.Issues.Select(i => i.Message)));

        var visit = await PerformOkAsync(client, new RitualSelection(visitor.Id, MapId: ritual.MapId));
        Assert.False(visit.MapCreated);
        Assert.Equal(ritual.MapId, visit.MapId);
    }

    [Fact]
    public async Task A_unique_map_has_one_holder()
    {
        using var client = factory.CreateClient();
        var owner = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformAndRecordAsync(client, SmallField(owner.Id));
        await SetFlagsAsync(client, ritual.MapId, new MapFlags(MapRequired: false, Unique: true));
        var other = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);

        var assign = await client.PostJsonAsync($"/api/components/{ritual.MapId}/assign", new AssignComponentInput(other.Id));
        Assert.Equal(HttpStatusCode.Conflict, assign.StatusCode);

        // The other player can still visit (Map Required is off), but completing it can't record a second copy.
        var visit = await PerformOkAsync(client, new RitualSelection(other.Id, MapId: ritual.MapId));
        var done = await RecordMapAsync(client, visit);
        Assert.False(done.MapRecorded);
        Assert.Equal("unique-held", done.MapNotRecordedReason);
        Assert.False((await QuantitiesAsync(client, other.Id)).ContainsKey(ritual.MapId));
    }

    [Fact]
    public async Task Editing_a_map_without_flags_keeps_them()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));
        await SetFlagsAsync(client, ritual.MapId, new MapFlags(MapRequired: false, ComponentsMustMatch: true, Unique: true));

        var map = await client.GetJsonAsync<ComponentDto>($"/api/components/{ritual.MapId}");
        var input = new ComponentInput(map.Type, map.Name, map.Rarity, map.Rank, "Renamed", map.IsPlayerAssignable, Map: map.Map! with { Flags = null });
        var updated = await (await client.PutJsonAsync($"/api/components/{map.Id}", input)).ReadJsonAsync<ComponentDto>();

        Assert.Equal(new MapFlags(false, true, true), updated.Map!.Flags);
    }

    private static async Task SetFlagsAsync(HttpClient client, Guid mapId, MapFlags flags)
    {
        var map = await client.GetJsonAsync<ComponentDto>($"/api/components/{mapId}");
        var input = new ComponentInput(map.Type, map.Name, map.Rarity, map.Rank, map.Description, map.IsPlayerAssignable, Map: map.Map! with { Flags = flags });
        var response = await client.PutJsonAsync($"/api/components/{mapId}", input);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
}
