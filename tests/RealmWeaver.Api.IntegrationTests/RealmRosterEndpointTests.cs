using System.Net;

using RealmWeaver.Api.IntegrationTests.Infrastructure;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;

using static RealmWeaver.Api.IntegrationTests.Infrastructure.RitualTestKit;

namespace RealmWeaver.Api.IntegrationTests;

[Collection(PostgresApiTests.Name)]
public class RealmRosterEndpointTests(RealmWeaverApiFactory factory)
{
    [Fact]
    public async Task The_roster_shows_completions_and_map_holders_and_hides_players_with_nothing_left_to_get()
    {
        using var client = factory.CreateClient();
        var weaver = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var holder = await CreateNamedAsync(client, $"Roster {tag} A");
        var noMap = await CreateNamedAsync(client, $"Roster {tag} B");
        var outsider = await CreateNamedAsync(client, $"Roster {tag} C");
        var ritual = await PerformOkAsync(client, SmallField(weaver.Id));
        var objective = (await ObjectivesAsync(client, ritual.RealmId))[0];
        await CompleteAsync(client, ritual.RealmId, new CompleteRunInput(objective.Key, [holder.Id]));
        await CompleteAsync(client, ritual.RealmId, new CompleteRunInput(objective.Key, [noMap.Id], MapRecipientIds: []));

        var roster = await client.GetJsonAsync<RealmRosterDto>($"/api/realms/{ritual.RealmId}/roster?search=Roster%20{tag}");
        var everyone = await client.GetJsonAsync<RealmRosterDto>($"/api/realms/{ritual.RealmId}/roster?search=Roster%20{tag}&all=true");

        // The holder completed and has the map, so there's nothing left to give them unless the GM asks for everyone.
        Assert.Equal([noMap.Id, outsider.Id], roster.Items.Select(p => p.Id));
        Assert.Equal(2, roster.TotalCount);
        Assert.Equal(new RosterPlayerDto(noMap.Id, noMap.Name, 1, HoldsMap: false, IsWeaver: false), roster.Items[0]);
        Assert.Equal(new RosterPlayerDto(outsider.Id, outsider.Name, 0, HoldsMap: false, IsWeaver: false), roster.Items[1]);
        Assert.Equal([holder.Id, noMap.Id, outsider.Id], everyone.Items.Select(p => p.Id));
        Assert.True(everyone.Items[0].HoldsMap);
    }

    [Fact]
    public async Task The_roster_is_paged_and_marks_the_weaver()
    {
        using var client = factory.CreateClient();
        var weaver = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(weaver.Id));

        var mine = await client.GetJsonAsync<RealmRosterDto>($"/api/realms/{ritual.RealmId}/roster?search={Uri.EscapeDataString(weaver.Name)}");
        var page = await client.GetJsonAsync<RealmRosterDto>($"/api/realms/{ritual.RealmId}/roster?pageSize=1&page=2");

        Assert.True(Assert.Single(mine.Items).IsWeaver);
        Assert.Equal(weaver.Id, mine.Weaver?.Id);
        Assert.Equal(weaver.Id, page.Weaver?.Id); // whatever the page shows
        Assert.Single(page.Items);
        Assert.Equal(2, page.Page);
        Assert.True(page.TotalCount >= 1);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(new Uri($"/api/realms/{Guid.NewGuid()}/roster", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(new Uri($"/api/realms/{ritual.RealmId}/roster?pageSize=0", UriKind.Relative))).StatusCode);
    }

    private static async Task<PlayerDto> CreateNamedAsync(HttpClient client, string name)
    {
        var response = await client.PostJsonAsync("/api/players", new CreatePlayerInput(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadJsonAsync<PlayerDto>();
    }
}
