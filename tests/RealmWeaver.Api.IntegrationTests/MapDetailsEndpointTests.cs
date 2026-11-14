using System.Net;

using RealmWeaver.Api.IntegrationTests.Infrastructure;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;

using static RealmWeaver.Api.IntegrationTests.Infrastructure.RitualTestKit;

namespace RealmWeaver.Api.IntegrationTests;

[Collection(PostgresApiTests.Name)]
public class MapDetailsEndpointTests(RealmWeaverApiFactory factory)
{
    [Fact]
    public async Task Map_details_compose_the_map_its_components_and_its_original_realm()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, WheatgoldThread, BrigandsPitch);
        var first = await PerformOkAsync(client, SmallField(player.Id, coating: BrigandsPitch));
        await PerformOkAsync(client, new RitualSelection(player.Id, ThreadId: WheatgoldThread, MapId: first.MapId)); // a mirror

        var details = await client.GetJsonAsync<MapDetails>($"/api/maps/{first.MapId}/details");

        Assert.Equal(first.MapId, details.Map.Id);
        Assert.Equal(("Beginner's Needle", "Mossgreen Thread", "Brigand's Pitch"), (details.Needle?.Name, details.Thread?.Name, details.Coating?.Name));
        Assert.Equal(first.RealmId, details.OriginalRealm?.RealmId);
        var realm = await GetRealmAsync(client, first.RealmId);
        Assert.Equal(realm.Summary.Threat, details.OriginalRealm?.Threat);
        var mobs = realm.Document.GetProperty("areas").EnumerateArray()
            .SelectMany(a => a.GetProperty("events").EnumerateArray())
            .SelectMany(e => e.GetProperty("mobs").EnumerateArray())
            .Sum(m => m.GetProperty("count").GetInt32());
        Assert.Equal(mobs, details.OriginalRealm!.Digest.Mobs.Sum(m => m.Count));
    }

    [Fact]
    public async Task Details_of_something_that_is_not_a_map_are_not_found()
    {
        using var client = factory.CreateClient();

        var notAMap = await client.GetAsync(new Uri($"/api/maps/{BeginnerNeedle}/details", UriKind.Relative));
        var unknown = await client.GetAsync(new Uri($"/api/maps/{Guid.NewGuid()}/details", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, notAMap.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task A_player_can_keep_notes_on_an_item_they_own()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));

        var saved = await client.PutJsonAsync($"/api/players/{player.Id}/inventory/{ritual.MapId}/notes", new InventoryNotesInput("  Wolves by the river.  "));
        var tooLong = await client.PutJsonAsync($"/api/players/{player.Id}/inventory/{ritual.MapId}/notes", new InventoryNotesInput(new string('x', 1001)));
        var notOwned = await client.PutJsonAsync($"/api/players/{player.Id}/inventory/{StarforgedNeedle}/notes", new InventoryNotesInput("Mine?"));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notOwned.StatusCode);
        var inventory = await client.GetJsonAsync<List<InventoryItemDto>>($"/api/players/{player.Id}/inventory");
        Assert.Equal("Wolves by the river.", inventory.Single(i => i.Component.Id == ritual.MapId).Notes);

        // Spending and regranting keeps the notes: they belong to the stack, not to one copy.
        await PerformOkAsync(client, new RitualSelection(player.Id, MapId: ritual.MapId));
        inventory = await client.GetJsonAsync<List<InventoryItemDto>>($"/api/players/{player.Id}/inventory");
        Assert.Equal("Wolves by the river.", inventory.Single(i => i.Component.Id == ritual.MapId).Notes);
    }

    private sealed record MapDetails(ComponentDto Map, ComponentDto? Needle, ComponentDto? Thread, ComponentDto? Coating, OriginalRealm? OriginalRealm);

    private sealed record OriginalRealm(Guid RealmId, int Threat, Digest Digest);

    private sealed record Digest(IReadOnlyList<Mob> Mobs, IReadOnlyList<Entry> Loot, IReadOnlyList<Entry> ResourceNodes);

    private sealed record Mob(string ArchetypeId, string Name, int Count);

    private sealed record Entry(string AreaName, string Id, string Name, string Location);
}
