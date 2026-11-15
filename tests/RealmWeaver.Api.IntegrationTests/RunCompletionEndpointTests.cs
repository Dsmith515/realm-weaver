using System.Net;
using System.Text.Json;

using RealmWeaver.Api.IntegrationTests.Infrastructure;
using RealmWeaver.Generation.Export;
using RealmWeaver.Generation.Model;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Realms.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;

using static RealmWeaver.Api.IntegrationTests.Infrastructure.RitualTestKit;

namespace RealmWeaver.Api.IntegrationTests;

[Collection(PostgresApiTests.Name)]
public class RunCompletionEndpointTests(RealmWeaverApiFactory factory)
{
    [Fact]
    public async Task A_ritual_doesnt_grant_the_map_but_the_gm_completing_the_run_hands_it_out()
    {
        using var client = factory.CreateClient();
        var weaver = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var partyMember = await CreatePlayerWithAsync(client);
        var ritual = await PerformOkAsync(client, SmallField(weaver.Id));
        Assert.False((await QuantitiesAsync(client, weaver.Id)).ContainsKey(ritual.MapId));

        var objective = (await ObjectivesAsync(client, ritual.RealmId))[^1];
        var done = await CompleteAsync(client, ritual.RealmId, new CompleteRunInput(objective.Key, [weaver.Id, partyMember.Id]));

        Assert.Equal(objective.Key, done.Objective?.Key);
        Assert.Equal([weaver.Id, partyMember.Id], done.MapGivenTo);
        Assert.Empty(done.Skipped);
        Assert.Equal(1, (await QuantitiesAsync(client, weaver.Id))[ritual.MapId]);
        Assert.Equal(1, (await QuantitiesAsync(client, partyMember.Id))[ritual.MapId]);

        var summary = (await GetRealmAsync(client, ritual.RealmId)).Summary;
        Assert.Equal(objective.Key, summary.Completion?.Objective?.Key);
        Assert.Equal([weaver.Id, partyMember.Id], summary.Completion!.MapGivenTo);
    }

    [Fact]
    public async Task The_gm_can_close_a_run_without_an_objective_or_a_hand_out()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));

        var done = await CompleteAsync(client, ritual.RealmId, new CompleteRunInput());

        Assert.Null(done.Objective);
        Assert.Empty(done.MapGivenTo);
        Assert.False((await QuantitiesAsync(client, player.Id)).ContainsKey(ritual.MapId));
        Assert.NotNull((await GetRealmAsync(client, ritual.RealmId)).Summary.Completion);
    }

    [Fact]
    public async Task Players_who_cant_receive_the_map_are_skipped_with_a_reason()
    {
        using var client = factory.CreateClient();
        var holder = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var first = await PerformAndRecordAsync(client, SmallField(holder.Id));
        var replay = await PerformOkAsync(client, new RitualSelection(holder.Id, MapId: first.MapId));
        var nobody = Guid.NewGuid();

        var done = await CompleteAsync(client, replay.RealmId, new CompleteRunInput(RecipientPlayerIds: [holder.Id, nobody]));

        Assert.Empty(done.MapGivenTo);
        Assert.Equal([new MapHandOutSkip(holder.Id, "already-held"), new MapHandOutSkip(nobody, "unknown-player")], done.Skipped);
        Assert.Equal(1, (await QuantitiesAsync(client, holder.Id))[first.MapId]);
    }

    [Fact]
    public async Task A_unique_map_goes_to_one_player_only()
    {
        using var client = factory.CreateClient();
        var weaver = await CreatePlayerWithAsync(client, MossgreenThread, MossgreenThread, BeastwardWax, BeastwardWax);
        var other = await CreatePlayerWithAsync(client);
        var ritual = await PerformOkAsync(client, SmallField(weaver.Id));
        await SetMapFlagsAsync(client, ritual.MapId, new MapFlags(Unique: true));

        var two = await client.PostJsonAsync($"/api/realms/{ritual.RealmId}/completion", new CompleteRunInput(RecipientPlayerIds: [weaver.Id, other.Id]));
        Assert.Contains("recipientPlayerIds", (await two.ReadJsonAsync<Problem>()).Errors!.Keys);

        var one = await CompleteAsync(client, ritual.RealmId, new CompleteRunInput(RecipientPlayerIds: [weaver.Id]));
        Assert.Equal([weaver.Id], one.MapGivenTo);

        // A later run of the same map can't give a second copy to someone else.
        var again = await PerformOkAsync(client, new RitualSelection(weaver.Id, MapId: ritual.MapId));
        var refused = await CompleteAsync(client, again.RealmId, new CompleteRunInput(RecipientPlayerIds: [other.Id]));
        Assert.Equal([new MapHandOutSkip(other.Id, "unique-held")], refused.Skipped);
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
        var done = await CompleteAsync(client, ritual.RealmId, new CompleteRunInput(notRolled.Key));
        Assert.Equal(notRolled.Key, done.Objective?.Key);

        var again = await client.PostJsonAsync($"/api/realms/{ritual.RealmId}/completion", new CompleteRunInput(rolled[0].Key));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var missing = await client.PostJsonAsync($"/api/realms/{Guid.NewGuid()}/completion", new CompleteRunInput("realm/clear-the-realm"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Two_completions_racing_give_one_success_one_conflict_and_one_map()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));
        var input = new CompleteRunInput(RecipientPlayerIds: [player.Id]);

        using var other = factory.CreateClient();
        var responses = await Task.WhenAll(
            client.PostJsonAsync($"/api/realms/{ritual.RealmId}/completion", input),
            other.PostJsonAsync($"/api/realms/{ritual.RealmId}/completion", input));

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], responses.Select(r => r.StatusCode).Order());
        Assert.Equal(1, (await QuantitiesAsync(client, player.Id))[ritual.MapId]);
    }

    [Fact]
    public async Task A_gm_quick_weave_can_be_completed_and_handed_to_players()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client);
        var weave = await (await client.PostJsonAsync("/api/gm/weave", new QuickWeaveRequest(BeginnerNeedle, MossgreenThread, BeastwardWax, Width: 20, Height: 20)))
            .ReadJsonAsync<QuickWeaveResultDto>();

        var done = await CompleteAsync(client, weave.RealmId, new CompleteRunInput((await ObjectivesAsync(client, weave.RealmId))[0].Key, [player.Id]));

        Assert.Equal([player.Id], done.MapGivenTo);
        Assert.Contains(weave.MapId, (await QuantitiesAsync(client, player.Id)).Keys);
    }

    private static async Task<RunCompletionDto> CompleteAsync(HttpClient client, Guid realmId, CompleteRunInput input)
    {
        var response = await client.PostJsonAsync($"/api/realms/{realmId}/completion", input);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadJsonAsync<RunCompletionDto>();
    }

    private static async Task<List<RealmObjective>> ObjectivesAsync(HttpClient client, Guid realmId) =>
        (await GetRealmAsync(client, realmId)).Document.GetProperty("objectives").Deserialize<List<RealmObjective>>(RealmJson.Options)!;
}
