using System.Net;

using RealmWeaver.Api.IntegrationTests.Infrastructure;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;

using static RealmWeaver.Api.IntegrationTests.CatalogEndpointTests;

namespace RealmWeaver.Api.IntegrationTests;

[Collection(PostgresApiTests.Name)]
public class InventoryEndpointTests(RealmWeaverApiFactory factory)
{
    [Fact]
    public async Task Startup_seeds_a_demo_player()
    {
        using var client = factory.CreateClient();

        var players = await client.GetJsonAsync<List<PlayerDto>>("/api/players");

        Assert.Contains(players, p => p.Name == "Wanderer");
    }

    [Fact]
    public async Task New_player_gets_the_starter_kit()
    {
        using var client = factory.CreateClient();

        var player = await CreatePlayerAsync(client);
        var inventory = await GetInventoryAsync(client, player.Id);

        var needle = Assert.Single(inventory, i => i.Component.Type == ComponentType.Needle);
        Assert.Equal(BeginnerNeedleId, needle.Component.Id);
        Assert.Equal(1, needle.Quantity);

        var threads = inventory.Where(i => i.Component.Type == ComponentType.Thread).ToList();
        Assert.Equal(2, threads.Select(t => t.Component.Id).Distinct().Count());
        Assert.All(threads, t => Assert.Equal(3, t.Quantity));
        Assert.All(threads, t => Assert.Equal(Rarity.Common, t.Component.Rarity));

        var coatings = inventory.Where(i => i.Component.Type == ComponentType.Coating).ToList();
        Assert.Equal(2, coatings.Select(c => c.Component.Id).Distinct().Count());
        Assert.All(coatings, c => Assert.Equal(3, c.Quantity));

        Assert.Equal(5, inventory.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("This name is far too long to fit in the sixty characters a player name may use")]
    public async Task Player_name_must_be_1_to_60_characters(string name)
    {
        using var client = factory.CreateClient();

        var response = await client.PostJsonAsync("/api/players", new CreatePlayerInput(name));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("name", (await response.ReadJsonAsync<Problem>()).Errors!.Keys);
    }

    [Fact]
    public async Task Unknown_player_returns_not_found()
    {
        using var client = factory.CreateClient();

        var player = await client.GetAsync(new Uri($"/api/players/{Guid.NewGuid()}", UriKind.Relative));
        var inventory = await client.GetAsync(new Uri($"/api/players/{Guid.NewGuid()}/inventory", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, player.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inventory.StatusCode);
    }

    [Fact]
    public async Task Self_add_stacks_consumables_when_allowed()
    {
        using var client = factory.CreateClient();
        await SetSelfAssignAsync(client, true);
        var player = await CreatePlayerAsync(client);
        var before = QuantityOf(await GetInventoryAsync(client, player.Id), MossgreenThreadId);

        var response = await client.PostJsonAsync($"/api/players/{player.Id}/inventory", new AddInventoryItemInput(MossgreenThreadId, 2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before + 2, (await response.ReadJsonAsync<InventoryItemDto>()).Quantity);
        Assert.Equal(before + 2, QuantityOf(await GetInventoryAsync(client, player.Id), MossgreenThreadId));
    }

    [Fact]
    public async Task Self_add_is_forbidden_when_the_setting_is_off()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerAsync(client);
        await SetSelfAssignAsync(client, false);
        try
        {
            var response = await client.PostJsonAsync($"/api/players/{player.Id}/inventory", new AddInventoryItemInput(MossgreenThreadId));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await SetSelfAssignAsync(client, true);
        }
    }

    [Fact]
    public async Task Self_add_is_forbidden_for_components_that_are_not_player_assignable()
    {
        using var client = factory.CreateClient();
        await SetSelfAssignAsync(client, true);
        var player = await CreatePlayerAsync(client);

        var response = await client.PostJsonAsync($"/api/players/{player.Id}/inventory", new AddInventoryItemInput(EmberwireThreadId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, QuantityOf(await GetInventoryAsync(client, player.Id), EmberwireThreadId));
    }

    [Fact]
    public async Task Self_add_validates_component_and_quantity()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerAsync(client);

        var unknown = await client.PostJsonAsync($"/api/players/{player.Id}/inventory", new AddInventoryItemInput(Guid.NewGuid()));
        var tooMany = await client.PostJsonAsync($"/api/players/{player.Id}/inventory", new AddInventoryItemInput(MossgreenThreadId, 50));

        Assert.Contains("componentId", (await unknown.ReadJsonAsync<Problem>()).Errors!.Keys);
        Assert.Contains("quantity", (await tooMany.ReadJsonAsync<Problem>()).Errors!.Keys);
    }

    [Fact]
    public async Task Admin_assign_ignores_the_self_assign_rules()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerAsync(client);
        await SetSelfAssignAsync(client, false);
        try
        {
            var response = await client.PostJsonAsync($"/api/components/{EmberwireThreadId}/assign", new AssignComponentInput(player.Id, 4));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(4, QuantityOf(await GetInventoryAsync(client, player.Id), EmberwireThreadId));
        }
        finally
        {
            await SetSelfAssignAsync(client, true);
        }
    }

    [Fact]
    public async Task Admin_assign_validates_player_and_component()
    {
        using var client = factory.CreateClient();

        var unknownComponent = await client.PostJsonAsync($"/api/components/{Guid.NewGuid()}/assign", new AssignComponentInput(Guid.NewGuid()));
        var unknownPlayer = await client.PostJsonAsync($"/api/components/{MossgreenThreadId}/assign", new AssignComponentInput(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, unknownComponent.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownPlayer.StatusCode);
        Assert.Contains("playerId", (await unknownPlayer.ReadJsonAsync<Problem>()).Errors!.Keys);
    }

    [Fact]
    public async Task Needles_are_never_stacked()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerAsync(client);
        var boneNeedleId = new Guid("10000000-0000-0000-0000-000000000002");

        await client.PostJsonAsync($"/api/components/{boneNeedleId}/assign", new AssignComponentInput(player.Id));
        var again = await client.PostJsonAsync($"/api/components/{boneNeedleId}/assign", new AssignComponentInput(player.Id));
        var tooMany = await client.PostJsonAsync($"/api/components/{boneNeedleId}/assign", new AssignComponentInput(player.Id, 2));

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(1, (await again.ReadJsonAsync<InventoryItemDto>()).Quantity);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal(1, QuantityOf(await GetInventoryAsync(client, player.Id), boneNeedleId));
    }

    [Fact]
    public async Task Concurrent_grants_of_a_consumable_all_count()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerAsync(client);
        const int grants = 20;

        // Every request upserts the same new row at once. None may be lost or fail on the primary key.
        var responses = await Task.WhenAll(Enumerable.Range(0, grants).Select(_ =>
            client.PostJsonAsync($"/api/components/{EmberwireThreadId}/assign", new AssignComponentInput(player.Id))));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(grants, QuantityOf(await GetInventoryAsync(client, player.Id), EmberwireThreadId));
    }

    [Fact]
    public async Task Archived_components_cannot_be_assigned_but_stay_in_inventories()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerAsync(client);
        var created = await (await client.PostJsonAsync("/api/components", new ComponentInput(
            ComponentType.Needle, "Test Retiring Needle", Rarity.Common, 1, null, true, Needle: new NeedleDetails(1, "Tin", []))))
            .ReadJsonAsync<ComponentDto>();
        await client.PostJsonAsync($"/api/components/{created.Id}/assign", new AssignComponentInput(player.Id));

        await client.DeleteAsync(new Uri($"/api/components/{created.Id}", UriKind.Relative));
        var assignAfterArchive = await client.PostJsonAsync($"/api/components/{created.Id}/assign", new AssignComponentInput(player.Id));

        Assert.Equal(HttpStatusCode.BadRequest, assignAfterArchive.StatusCode);
        var owned = Assert.Single(await GetInventoryAsync(client, player.Id), i => i.Component.Id == created.Id);
        Assert.True(owned.Component.IsArchived);
    }

    [Fact]
    public async Task Settings_round_trip()
    {
        using var client = factory.CreateClient();

        await SetSelfAssignAsync(client, false);
        var off = await client.GetJsonAsync<CatalogSettingsDto>("/api/settings");
        await SetSelfAssignAsync(client, true);
        var on = await client.GetJsonAsync<CatalogSettingsDto>("/api/settings");

        Assert.False(off.PlayersCanSelfAssign);
        Assert.True(on.PlayersCanSelfAssign);
    }

    private static async Task<PlayerDto> CreatePlayerAsync(HttpClient client)
    {
        var response = await client.PostJsonAsync("/api/players", new CreatePlayerInput($"Tester {Guid.NewGuid():N}"[..20]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadJsonAsync<PlayerDto>();
    }

    private static Task<List<InventoryItemDto>> GetInventoryAsync(HttpClient client, Guid playerId) =>
        client.GetJsonAsync<List<InventoryItemDto>>($"/api/players/{playerId}/inventory");

    private static int QuantityOf(IEnumerable<InventoryItemDto> inventory, Guid componentId) =>
        inventory.FirstOrDefault(i => i.Component.Id == componentId)?.Quantity ?? 0;

    private static async Task SetSelfAssignAsync(HttpClient client, bool enabled)
    {
        var response = await client.PutJsonAsync("/api/settings", new CatalogSettingsDto(enabled));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
