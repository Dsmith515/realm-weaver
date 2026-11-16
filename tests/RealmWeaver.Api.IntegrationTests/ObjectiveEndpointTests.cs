using System.Net;
using System.Text.Json;

using RealmWeaver.Api.IntegrationTests.Infrastructure;
using RealmWeaver.Generation.Export;
using RealmWeaver.Generation.Model;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Realms.Contracts;

using static RealmWeaver.Api.IntegrationTests.Infrastructure.RitualTestKit;

namespace RealmWeaver.Api.IntegrationTests;

[Collection(PostgresApiTests.Name)]
public class ObjectiveEndpointTests(RealmWeaverApiFactory factory)
{
    [Fact]
    public async Task A_ritual_records_the_realms_objectives_on_its_new_map()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BrigandsPitch);
        var ritual = await PerformOkAsync(client, SmallField(player.Id, coating: BrigandsPitch));

        var objectives = await ObjectivesAsync(client, ritual.RealmId);
        var map = await client.GetJsonAsync<ComponentDto>($"/api/components/{ritual.MapId}");

        Assert.InRange(objectives.Count, GeneratorLimits.MinObjectives, GeneratorLimits.MaxObjectives);
        Assert.Equal(objectives.Select(o => o.Key), map.Map!.Objectives!.Select(o => o.Key));
    }

    [Fact]
    public async Task A_gm_can_pin_any_candidate_and_unpin_it_without_touching_the_map()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BrigandsPitch);
        var ritual = await PerformOkAsync(client, SmallField(player.Id, coating: BrigandsPitch));
        var mapBefore = await client.GetJsonAsync<ComponentDto>($"/api/components/{ritual.MapId}");

        var candidates = await client.GetJsonAsync<List<RealmObjective>>($"/api/realms/{ritual.RealmId}/objectives/candidates");
        var rolled = await ObjectivesAsync(client, ritual.RealmId);
        Assert.Equal(rolled.Select(o => o.Key), candidates.Take(rolled.Count).Select(o => o.Key));
        var choice = candidates[^1];

        var pinned = await (await client.PutJsonAsync($"/api/realms/{ritual.RealmId}/pinned-objective", new PinObjectiveInput(choice.Key)))
            .ReadJsonAsync<RealmSummaryDto>();
        Assert.Equal(choice.Key, pinned.PinnedObjective?.Key);
        Assert.Equal(choice.Key, (await GetRealmAsync(client, ritual.RealmId)).Summary.PinnedObjective?.Key);
        Assert.Contains((await client.GetJsonAsync<RealmSummaryPage>($"/api/realms?playerId={player.Id}")).Items, r => r.PinnedObjective?.Key == choice.Key);

        var mapAfter = await client.GetJsonAsync<ComponentDto>($"/api/components/{ritual.MapId}");
        Assert.Equal(mapBefore.Map!.Objectives!.Select(o => o.Key), mapAfter.Map!.Objectives!.Select(o => o.Key));

        var unpinned = await (await client.DeleteAsync(new Uri($"/api/realms/{ritual.RealmId}/pinned-objective", UriKind.Relative)))
            .ReadJsonAsync<RealmSummaryDto>();
        Assert.Null(unpinned.PinnedObjective);
    }

    [Fact]
    public async Task Pinning_rejects_unknown_keys_and_realms()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));

        var badKey = await client.PutJsonAsync($"/api/realms/{ritual.RealmId}/pinned-objective", new PinObjectiveInput("field/E99/nothing"));
        var emptyKey = await client.PutJsonAsync($"/api/realms/{ritual.RealmId}/pinned-objective", new PinObjectiveInput(" "));
        var noRealm = await client.PutJsonAsync($"/api/realms/{Guid.NewGuid()}/pinned-objective", new PinObjectiveInput("realm/clear-the-realm"));

        Assert.Contains("key", (await badKey.ReadJsonAsync<Problem>()).Errors!.Keys);
        Assert.Contains("key", (await emptyKey.ReadJsonAsync<Problem>()).Errors!.Keys);
        Assert.Equal(HttpStatusCode.NotFound, noRealm.StatusCode);
    }

    [Fact]
    public async Task Editing_a_map_without_objectives_keeps_the_recorded_ones()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));
        var map = await client.GetJsonAsync<ComponentDto>($"/api/components/{ritual.MapId}");

        var input = new ComponentInput(map.Type, map.Name, map.Rarity, map.Rank, "Renamed by the GM", map.IsPlayerAssignable, Map: map.Map! with { Objectives = null });
        var response = await client.PutJsonAsync($"/api/components/{map.Id}", input);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var updated = await response.ReadJsonAsync<ComponentDto>();

        Assert.Equal("Renamed by the GM", updated.Description);
        Assert.Equal(map.Map!.Objectives!.Select(o => o.Key), updated.Map!.Objectives!.Select(o => o.Key));
    }

    private static async Task<List<RealmObjective>> ObjectivesAsync(HttpClient client, Guid realmId)
    {
        var realm = await GetRealmAsync(client, realmId);
        return realm.Document.GetProperty("objectives").Deserialize<List<RealmObjective>>(RealmJson.Options)!;
    }
}
