using System.Net;

using RealmWeaver.Api.IntegrationTests.Infrastructure;
using RealmWeaver.Modules.Realms.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;

using static RealmWeaver.Api.IntegrationTests.Infrastructure.RitualTestKit;

namespace RealmWeaver.Api.IntegrationTests;

[Collection(PostgresApiTests.Name)]
public class RealmBriefingEndpointTests(RealmWeaverApiFactory factory)
{
    [Fact]
    public async Task A_weaver_without_the_map_gets_the_realm_and_its_objectives_but_no_locations_or_finds()
    {
        using var client = factory.CreateClient();
        var weaver = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(weaver.Id));
        var realm = await GetRealmAsync(client, ritual.RealmId);
        var rolled = await ObjectivesAsync(client, ritual.RealmId);

        var json = await client.GetStringAsync(new Uri($"/api/realms/{ritual.RealmId}/briefing?playerId={weaver.Id}", UriKind.Relative));
        var briefing = await client.GetJsonAsync<RealmBriefingDto>($"/api/realms/{ritual.RealmId}/briefing?playerId={weaver.Id}");

        Assert.False(briefing.HoldsMap);
        Assert.Null(briefing.Finds);
        Assert.Equal(realm.Summary.Kind, briefing.Kind);
        Assert.Equal(realm.Summary.Threat, briefing.Threat);
        Assert.Equal(realm.Summary.Components!.MapName, briefing.MapName);
        Assert.Equal([realm.Summary.Components.Needle.Name, realm.Summary.Components.Thread.Name, realm.Summary.Components.Coating.Name], briefing.WovenFrom);
        Assert.Equal(rolled.Select(o => (o.Id, o.Title, o.Description)), briefing.Objectives.Select(o => (o.Id, o.Title, o.Hint)));

        // Nothing that places anything on the map: no locations, event or area ids, or the keys that embed them.
        foreach (var hidden in new[] { "location", "eventId", "areaId", "key", "seed", "areas" })
        {
            Assert.DoesNotContain($"\"{hidden}\"", json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Holding_the_map_adds_the_kinds_of_things_to_find()
    {
        using var client = factory.CreateClient();
        var weaver = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformAndRecordAsync(client, SmallField(weaver.Id));

        var briefing = await client.GetJsonAsync<RealmBriefingDto>($"/api/realms/{ritual.RealmId}/briefing?playerId={weaver.Id}");
        var anonymous = await client.GetJsonAsync<RealmBriefingDto>($"/api/realms/{ritual.RealmId}/briefing");

        Assert.True(briefing.HoldsMap);
        Assert.NotNull(briefing.Finds);
        Assert.NotEmpty(briefing.Finds.Creatures);
        Assert.Equal(briefing.Finds.Creatures.Distinct(), briefing.Finds.Creatures);
        Assert.True(briefing.CompletionCount >= 1); // completions belong to the map, which other runs of this recipe share
        Assert.False(anonymous.HoldsMap);
        Assert.Null(anonymous.Finds);
    }

    [Fact]
    public async Task A_gm_pin_is_marked_and_one_that_was_not_rolled_is_added_first()
    {
        using var client = factory.CreateClient();
        // A dungeon floor means two realm-wide objectives, and only one is ever rolled, so there's always one left to pin.
        var weaver = await CreatePlayerWithAsync(client, MossgreenThread, BrigandsPitch);
        var ritual = await PerformOkAsync(client, SmallField(weaver.Id, coating: BrigandsPitch));
        var rolled = await ObjectivesAsync(client, ritual.RealmId);
        var candidates = await client.GetJsonAsync<List<Generation.Model.RealmObjective>>($"/api/realms/{ritual.RealmId}/objectives/candidates");
        var extra = candidates.First(c => rolled.All(o => o.Key != c.Key));

        var pinRolled = await client.PutJsonAsync($"/api/realms/{ritual.RealmId}/pinned-objective", new PinObjectiveInput(rolled[0].Key));
        Assert.Equal(HttpStatusCode.OK, pinRolled.StatusCode);
        var onRolled = await client.GetJsonAsync<RealmBriefingDto>($"/api/realms/{ritual.RealmId}/briefing");

        var pinExtra = await client.PutJsonAsync($"/api/realms/{ritual.RealmId}/pinned-objective", new PinObjectiveInput(extra.Key));
        Assert.Equal(HttpStatusCode.OK, pinExtra.StatusCode);
        var onExtra = await client.GetJsonAsync<RealmBriefingDto>($"/api/realms/{ritual.RealmId}/briefing");

        Assert.Equal(rolled.Count, onRolled.Objectives.Count);
        Assert.Equal([rolled[0].Id], onRolled.Objectives.Where(o => o.IsPinned).Select(o => o.Id));
        Assert.Equal(rolled.Count + 1, onExtra.Objectives.Count);
        Assert.Equal(new BriefingObjectiveDto("P", extra.Title, extra.Description, IsPinned: true), onExtra.Objectives[0]);
    }

    [Fact]
    public async Task An_unknown_realm_is_not_found()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync(new Uri($"/api/realms/{Guid.NewGuid()}/briefing", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
