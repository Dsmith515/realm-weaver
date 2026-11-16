using System.Net;
using System.Text.Json;

using RealmWeaver.Api.IntegrationTests.Infrastructure;
using RealmWeaver.Generation.Export;
using RealmWeaver.Generation.Model;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Realms.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;

using static RealmWeaver.Api.IntegrationTests.Infrastructure.RitualTestKit;

namespace RealmWeaver.Api.IntegrationTests;

[Collection(PostgresApiTests.Name)]
public class RunCompletionEndpointTests(RealmWeaverApiFactory factory)
{
    [Fact]
    public async Task A_ritual_no_longer_grants_the_map_but_completing_as_a_cartographer_records_it()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));
        Assert.False((await QuantitiesAsync(client, player.Id)).ContainsKey(ritual.MapId));

        await client.PutJsonAsync($"/api/players/{player.Id}/skills", new PlayerSkillsInput([PlayerSkill.Cartographer]));
        var objective = (await ObjectivesAsync(client, ritual.RealmId))[^1];
        var done = await CompleteAsync(client, ritual.RealmId, objective.Key);

        Assert.True(done.MapRecorded);
        Assert.Null(done.MapNotRecordedReason);
        Assert.Equal(objective.Key, done.Objective.Key);
        Assert.Equal(1, (await QuantitiesAsync(client, player.Id))[ritual.MapId]);

        var summary = (await GetRealmAsync(client, ritual.RealmId)).Summary;
        Assert.Equal(objective.Key, summary.Completion?.Objective.Key);
        Assert.True(summary.Completion!.MapRecorded);
    }

    [Fact]
    public async Task Completing_without_cartographer_status_completes_the_run_but_records_nothing()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));

        var done = await CompleteAsync(client, ritual.RealmId, (await ObjectivesAsync(client, ritual.RealmId))[0].Key);

        Assert.False(done.MapRecorded);
        Assert.Equal("not-cartographer", done.MapNotRecordedReason);
        Assert.False((await QuantitiesAsync(client, player.Id)).ContainsKey(ritual.MapId));
        Assert.False((await GetRealmAsync(client, ritual.RealmId)).Summary.Completion!.MapRecorded);
    }

    [Fact]
    public async Task An_attuned_cartographer_needle_is_enough_to_record_the_map()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var surveyor = await (await client.PostJsonAsync("/api/components", new ComponentInput(
            ComponentType.Needle, $"Surveyor {Guid.NewGuid().ToString("N")[..6]} Needle", Rarity.Rare, 1, null, false,
            Needle: new NeedleDetails(2, "Brass", ["Chart Sense"], Boon: NeedleBoon.Cartographer)))).ReadJsonAsync<ComponentDto>();
        await client.PostJsonAsync($"/api/components/{surveyor.Id}/assign", new AssignComponentInput(player.Id));
        await client.PutJsonAsync($"/api/players/{player.Id}/attunement", new AttuneNeedleInput(surveyor.Id));

        // Weave with the beginner needle: the attuned needle is separate.
        var ritual = await PerformOkAsync(client, SmallField(player.Id));
        var done = await CompleteAsync(client, ritual.RealmId, (await ObjectivesAsync(client, ritual.RealmId))[0].Key);

        Assert.True(done.MapRecorded);
        Assert.Contains(ritual.MapId, (await QuantitiesAsync(client, player.Id)).Keys);
    }

    [Fact]
    public async Task A_run_completes_once_with_one_of_its_own_objectives_or_the_gm_pin()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BrigandsPitch);
        var ritual = await PerformOkAsync(client, SmallField(player.Id, coating: BrigandsPitch));
        var rolled = await ObjectivesAsync(client, ritual.RealmId);

        var notRolled = (await client.GetJsonAsync<List<RealmObjective>>($"/api/realms/{ritual.RealmId}/objectives/candidates"))
            .First(c => rolled.All(o => o.Key != c.Key));
        var unknown = await client.PostJsonAsync($"/api/realms/{ritual.RealmId}/completion", new CompleteRunInput(notRolled.Key));
        Assert.Contains("objectiveKey", (await unknown.ReadJsonAsync<Problem>()).Errors!.Keys);

        // Once the GM pins it, it completes the run.
        await client.PutJsonAsync($"/api/realms/{ritual.RealmId}/pinned-objective", new PinObjectiveInput(notRolled.Key));
        var done = await CompleteAsync(client, ritual.RealmId, notRolled.Key);
        Assert.Equal(notRolled.Key, done.Objective.Key);

        var again = await client.PostJsonAsync($"/api/realms/{ritual.RealmId}/completion", new CompleteRunInput(rolled[0].Key));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var missing = await client.PostJsonAsync($"/api/realms/{Guid.NewGuid()}/completion", new CompleteRunInput("realm/clear-the-realm"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Two_completions_racing_give_one_success_and_one_conflict()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        await client.PutJsonAsync($"/api/players/{player.Id}/skills", new PlayerSkillsInput([PlayerSkill.Cartographer]));
        var ritual = await PerformOkAsync(client, SmallField(player.Id));
        var key = (await ObjectivesAsync(client, ritual.RealmId))[0].Key;

        using var other = factory.CreateClient();
        var responses = await Task.WhenAll(
            client.PostJsonAsync($"/api/realms/{ritual.RealmId}/completion", new CompleteRunInput(key)),
            other.PostJsonAsync($"/api/realms/{ritual.RealmId}/completion", new CompleteRunInput(key)));

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], responses.Select(r => r.StatusCode).Order());
        Assert.Equal(1, (await QuantitiesAsync(client, player.Id))[ritual.MapId]);
    }

    [Fact]
    public async Task A_gm_quick_weave_completes_without_recording_a_map()
    {
        using var client = factory.CreateClient();
        var weave = await (await client.PostJsonAsync("/api/gm/weave", new QuickWeaveRequest(BeginnerNeedle, MossgreenThread, BeastwardWax, Width: 20, Height: 20)))
            .ReadJsonAsync<QuickWeaveResultDto>();

        var done = await CompleteAsync(client, weave.RealmId, (await ObjectivesAsync(client, weave.RealmId))[0].Key);

        Assert.False(done.MapRecorded);
        Assert.Equal("gm-realm", done.MapNotRecordedReason);
    }

    private static async Task<RunCompletionDto> CompleteAsync(HttpClient client, Guid realmId, string key)
    {
        var response = await client.PostJsonAsync($"/api/realms/{realmId}/completion", new CompleteRunInput(key));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync<RunCompletionDto>();
    }

    private static async Task<List<RealmObjective>> ObjectivesAsync(HttpClient client, Guid realmId) =>
        (await GetRealmAsync(client, realmId)).Document.GetProperty("objectives").Deserialize<List<RealmObjective>>(RealmJson.Options)!;
}
