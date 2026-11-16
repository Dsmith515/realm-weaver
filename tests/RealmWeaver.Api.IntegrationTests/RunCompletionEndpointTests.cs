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
    public async Task A_ritual_doesnt_grant_the_map_but_the_gm_completing_an_objective_hands_it_to_the_party()
    {
        using var client = factory.CreateClient();
        var weaver = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var partyMember = await CreatePlayerWithAsync(client);
        var ritual = await PerformOkAsync(client, SmallField(weaver.Id));
        Assert.False((await QuantitiesAsync(client, weaver.Id)).ContainsKey(ritual.MapId));

        var objective = (await ObjectivesAsync(client, ritual.RealmId))[^1];
        var done = await CompleteAsync(client, ritual.RealmId, new CompleteRunInput(objective.Key, [weaver.Id, partyMember.Id]));

        Assert.Equal(objective.Key, done.Objective.Key);
        Assert.True(done.IsFirst);
        Assert.Equal([weaver.Id, partyMember.Id], done.MapGivenTo);
        Assert.Empty(done.Skipped);
        Assert.Equal(1, (await QuantitiesAsync(client, weaver.Id))[ritual.MapId]);
        Assert.Equal(1, (await QuantitiesAsync(client, partyMember.Id))[ritual.MapId]);
        Assert.Equal(1, (await GetRealmAsync(client, ritual.RealmId)).Summary.CompletionCount);
    }

    [Fact]
    public async Task A_realm_can_be_completed_again_by_a_new_party_and_only_newcomers_get_the_map()
    {
        using var client = factory.CreateClient();
        var veteran = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var newcomer = await CreatePlayerWithAsync(client);
        var ritual = await PerformOkAsync(client, SmallField(veteran.Id));
        var objectives = await ObjectivesAsync(client, ritual.RealmId);

        var first = await CompleteAsync(client, ritual.RealmId, new CompleteRunInput(objectives[0].Key, [veteran.Id]));
        var second = await CompleteAsync(client, ritual.RealmId, new CompleteRunInput(objectives[0].Key, [veteran.Id, newcomer.Id]));

        Assert.True(first.IsFirst);
        Assert.False(second.IsFirst);
        Assert.Equal([newcomer.Id], second.MapGivenTo);
        Assert.Empty(second.Skipped); // the veteran wasn't a recipient: by default only players without the map are
        Assert.Equal(1, (await QuantitiesAsync(client, veteran.Id))[ritual.MapId]);

        var history = await client.GetJsonAsync<RealmCompletionsDto>($"/api/realms/{ritual.RealmId}/completions");
        Assert.Equal([second.CompletionId, first.CompletionId], history.Completions.Select(c => c.Id));
        Assert.Equal([false, true], history.Completions.Select(c => c.IsFirst));
        Assert.Equal([veteran.Id, newcomer.Id], history.Players.Select(p => p.Id));
        Assert.Equal([2, 1], history.Players.Select(p => p.Completions));
        Assert.Equal([true, false], history.Players.Select(p => p.InFirstCompletion));
        Assert.Equal(veteran.Name, history.Players[0].Name);
        Assert.Equal(2, (await GetRealmAsync(client, ritual.RealmId)).Summary.CompletionCount);
    }

    [Fact]
    public async Task Every_run_of_a_map_shares_one_completion_history()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, MossgreenThread, BeastwardWax, BeastwardWax);
        var first = await PerformAndRecordAsync(client, SmallField(player.Id));
        var replay = await PerformOkAsync(client, new RitualSelection(player.Id, MapId: first.MapId));
        Assert.NotEqual(first.RealmId, replay.RealmId);

        var done = await CompleteAsync(client, replay.RealmId, new CompleteRunInput((await ObjectivesAsync(client, replay.RealmId))[0].Key, [player.Id]));
        Assert.False(done.IsFirst);

        var history = await client.GetJsonAsync<RealmCompletionsDto>($"/api/realms/{first.RealmId}/completions");
        Assert.Equal([replay.RealmId, first.RealmId], history.Completions.Select(c => c.RealmId));
    }

    [Fact]
    public async Task Named_recipients_who_already_hold_the_map_are_skipped_with_a_reason()
    {
        using var client = factory.CreateClient();
        var holder = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var first = await PerformAndRecordAsync(client, SmallField(holder.Id));
        var replay = await PerformOkAsync(client, new RitualSelection(holder.Id, MapId: first.MapId));
        var key = (await ObjectivesAsync(client, replay.RealmId))[0].Key;

        var done = await CompleteAsync(client, replay.RealmId, new CompleteRunInput(key, [holder.Id], MapRecipientIds: [holder.Id]));

        Assert.Empty(done.MapGivenTo);
        Assert.Equal([new MapHandOutSkip(holder.Id, "already-held")], done.Skipped);
        Assert.Equal(1, (await QuantitiesAsync(client, holder.Id))[first.MapId]);
    }

    [Fact]
    public async Task The_gm_can_complete_without_handing_out_the_map()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));

        var done = await CompleteAsync(client, ritual.RealmId, new CompleteRunInput((await ObjectivesAsync(client, ritual.RealmId))[0].Key, [player.Id], MapRecipientIds: []));

        Assert.Equal([player.Id], done.PlayerIds);
        Assert.Empty(done.MapGivenTo);
        Assert.False((await QuantitiesAsync(client, player.Id)).ContainsKey(ritual.MapId));
        var history = await client.GetJsonAsync<RealmCompletionsDto>($"/api/realms/{ritual.RealmId}/completions");
        Assert.False(Assert.Single(Assert.Single(history.Completions).Players).MapGiven);
    }

    [Fact]
    public async Task A_completion_needs_an_objective_known_players_and_recipients_from_the_party()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var outsider = await CreatePlayerWithAsync(client);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));
        var key = (await ObjectivesAsync(client, ritual.RealmId))[0].Key;

        async Task<string[]> ErrorsFor(CompleteRunInput input) =>
            [.. (await (await client.PostJsonAsync($"/api/realms/{ritual.RealmId}/completion", input)).ReadJsonAsync<Problem>()).Errors!.Keys];

        Assert.Contains("objectiveKey", await ErrorsFor(new CompleteRunInput(" ", [player.Id])));
        Assert.Contains("playerIds", await ErrorsFor(new CompleteRunInput(key, [])));
        Assert.Contains("playerIds", await ErrorsFor(new CompleteRunInput(key, [Guid.NewGuid()])));
        Assert.Contains("mapRecipientIds", await ErrorsFor(new CompleteRunInput(key, [player.Id], MapRecipientIds: [outsider.Id])));
        Assert.Equal(0, (await GetRealmAsync(client, ritual.RealmId)).Summary.CompletionCount);
    }

    [Fact]
    public async Task A_unique_map_goes_to_one_player_only()
    {
        using var client = factory.CreateClient();
        var weaver = await CreatePlayerWithAsync(client, MossgreenThread, MossgreenThread, BeastwardWax, BeastwardWax);
        var other = await CreatePlayerWithAsync(client);
        var ritual = await PerformOkAsync(client, SmallField(weaver.Id));
        await SetMapFlagsAsync(client, ritual.MapId, new MapFlags(Unique: true));
        var key = (await ObjectivesAsync(client, ritual.RealmId))[0].Key;

        var two = await client.PostJsonAsync($"/api/realms/{ritual.RealmId}/completion", new CompleteRunInput(key, [weaver.Id, other.Id]));
        Assert.Contains("mapRecipientIds", (await two.ReadJsonAsync<Problem>()).Errors!.Keys);

        var one = await CompleteAsync(client, ritual.RealmId, new CompleteRunInput(key, [weaver.Id, other.Id], MapRecipientIds: [weaver.Id]));
        Assert.Equal([weaver.Id], one.MapGivenTo);

        // A later completion can't give a second copy to someone else.
        var refused = await CompleteAsync(client, ritual.RealmId, new CompleteRunInput(key, [other.Id]));
        Assert.Equal([new MapHandOutSkip(other.Id, "unique-held")], refused.Skipped);
    }

    [Fact]
    public async Task Any_rolled_objective_or_the_gm_pin_can_be_completed()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BrigandsPitch);
        var ritual = await PerformOkAsync(client, SmallField(player.Id, coating: BrigandsPitch));
        var rolled = await ObjectivesAsync(client, ritual.RealmId);

        var notRolled = (await client.GetJsonAsync<List<RealmObjective>>($"/api/realms/{ritual.RealmId}/objectives/candidates"))
            .First(c => rolled.All(o => o.Key != c.Key));
        var unknown = await client.PostJsonAsync($"/api/realms/{ritual.RealmId}/completion", new CompleteRunInput(notRolled.Key, [player.Id]));
        Assert.Contains("objectiveKey", (await unknown.ReadJsonAsync<Problem>()).Errors!.Keys);

        // Once the GM pins it, it can be completed.
        await client.PutJsonAsync($"/api/realms/{ritual.RealmId}/pinned-objective", new PinObjectiveInput(notRolled.Key));
        var done = await CompleteAsync(client, ritual.RealmId, new CompleteRunInput(notRolled.Key, [player.Id]));
        Assert.Equal(notRolled.Key, done.Objective.Key);

        var missing = await client.PostJsonAsync($"/api/realms/{Guid.NewGuid()}/completion", new CompleteRunInput("realm/clear-the-realm", [player.Id]));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var noHistory = await client.GetAsync(new Uri($"/api/realms/{Guid.NewGuid()}/completions", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, noHistory.StatusCode);
    }

    [Fact]
    public async Task Two_completions_racing_both_count_and_the_map_is_granted_once()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));
        var input = new CompleteRunInput((await ObjectivesAsync(client, ritual.RealmId))[0].Key, [player.Id]);

        using var other = factory.CreateClient();
        var responses = await Task.WhenAll(
            client.PostJsonAsync($"/api/realms/{ritual.RealmId}/completion", input),
            other.PostJsonAsync($"/api/realms/{ritual.RealmId}/completion", input));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, (await QuantitiesAsync(client, player.Id))[ritual.MapId]);
        var history = await client.GetJsonAsync<RealmCompletionsDto>($"/api/realms/{ritual.RealmId}/completions");
        Assert.Equal(2, history.Completions.Count);
        Assert.Single(history.Completions, c => c.IsFirst);
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
}
