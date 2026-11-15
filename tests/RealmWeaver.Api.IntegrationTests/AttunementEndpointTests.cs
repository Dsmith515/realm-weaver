using System.Net;

using RealmWeaver.Api.IntegrationTests.Infrastructure;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;

using static RealmWeaver.Api.IntegrationTests.Infrastructure.RitualTestKit;

namespace RealmWeaver.Api.IntegrationTests;

[Collection(PostgresApiTests.Name)]
public class AttunementEndpointTests(RealmWeaverApiFactory factory)
{
    [Fact]
    public async Task A_new_player_is_attuned_to_the_beginner_needle()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client);

        Assert.Equal(BeginnerNeedle, player.AttunedNeedleId);
    }

    [Fact]
    public async Task Players_attune_only_needles_they_own()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client);
        var needle = await CreateNeedleAsync(client, "Attuning", tier: 3);

        var notOwned = await client.PutJsonAsync($"/api/players/{player.Id}/attunement", new AttuneNeedleInput(needle.Id));
        var aThread = await client.PutJsonAsync($"/api/players/{player.Id}/attunement", new AttuneNeedleInput(MossgreenThread));
        Assert.Contains("needleId", (await notOwned.ReadJsonAsync<Problem>()).Errors!.Keys);
        Assert.Contains("needleId", (await aThread.ReadJsonAsync<Problem>()).Errors!.Keys);

        await AssignAsync(client, needle.Id, player.Id);
        var attuned = await (await client.PutJsonAsync($"/api/players/{player.Id}/attunement", new AttuneNeedleInput(needle.Id))).ReadJsonAsync<PlayerDto>();
        Assert.Equal(needle.Id, attuned.AttunedNeedleId);

        var cleared = await (await client.DeleteAsync(new Uri($"/api/players/{player.Id}/attunement", UriKind.Relative))).ReadJsonAsync<PlayerDto>();
        Assert.Null(cleared.AttunedNeedleId);
    }

    [Fact]
    public async Task A_needle_of_the_same_base_type_matches_the_map_and_another_type_makes_a_mirror()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, MossgreenThread, BeastwardWax, BeastwardWax);
        var ritual = await PerformAndRecordAsync(client, SmallField(player.Id));

        var variant = await CreateNeedleAsync(client, "Etched Beginner's", tier: 1, baseType: "Beginner's Needle");
        var other = await CreateNeedleAsync(client, "Unrelated", tier: 1);
        await AssignAsync(client, variant.Id, player.Id);
        await AssignAsync(client, other.Id, player.Id);

        var same = await PreviewAsync(client, new RitualPreviewRequest(player.Id, NeedleId: variant.Id, MapId: ritual.MapId));
        Assert.False(same.IsMismatch);
        Assert.Equal(variant.Id, same.Needle!.Component.Id);

        // Rule 2: picking the variant with the same thread and coating finds the owned map.
        var autoFilled = await PreviewAsync(client, new RitualPreviewRequest(player.Id, variant.Id, MossgreenThread, BeastwardWax));
        Assert.Equal(ritual.MapId, autoFilled.Map?.Component.Id);
        Assert.Equal(SlotSource.MatchingMap, autoFilled.Map?.Source);

        var mirror = await PreviewAsync(client, new RitualPreviewRequest(player.Id, NeedleId: other.Id, MapId: ritual.MapId));
        Assert.True(mirror.IsMismatch);
        Assert.Equal([RitualSlot.Needle], mirror.MismatchedSlots);
    }

    [Fact]
    public async Task New_maps_record_the_needles_base_type_and_used_needles_keep_it()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));

        var map = await client.GetJsonAsync<ComponentDto>($"/api/components/{ritual.MapId}");
        Assert.Equal("Beginner's Needle", map.Map!.Recipe!.NeedleBaseType);

        var beginner = await client.GetJsonAsync<ComponentDto>($"/api/components/{BeginnerNeedle}");
        var input = new ComponentInput(beginner.Type, beginner.Name, beginner.Rarity, beginner.Rank, beginner.Description, beginner.IsPlayerAssignable,
            Needle: beginner.Needle! with { BaseType = "Some Other Needle" });
        var changed = await client.PutJsonAsync($"/api/components/{BeginnerNeedle}", input);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);

        var blank = await client.PutJsonAsync($"/api/components/{BeginnerNeedle}", input with { Needle = beginner.Needle with { BaseType = null } });
        Assert.Equal("Beginner's Needle", (await blank.ReadJsonAsync<ComponentDto>()).Needle!.BaseType);
    }

    [Fact]
    public async Task The_realm_sheet_records_the_weavers_attuned_needle()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));

        var attuned = (await GetRealmAsync(client, ritual.RealmId)).Summary.Components!.AttunedNeedle;

        Assert.Equal(BeginnerNeedle, attuned?.Id);
        Assert.Equal(["Steady Point"], attuned!.Abilities);
    }

    private static async Task<ComponentDto> CreateNeedleAsync(HttpClient client, string prefix, int tier, string? baseType = null)
    {
        var name = $"{prefix} {Guid.NewGuid().ToString("N")[..6]} Needle";
        var input = new ComponentInput(ComponentType.Needle, name, Rarity.Uncommon, 1, null, false,
            Needle: new NeedleDetails(tier, "Test", ["Test Ability"], baseType));
        var response = await client.PostJsonAsync("/api/components", input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadJsonAsync<ComponentDto>();
    }

    private static async Task AssignAsync(HttpClient client, Guid componentId, Guid playerId) =>
        Assert.Equal(HttpStatusCode.OK, (await client.PostJsonAsync($"/api/components/{componentId}/assign", new AssignComponentInput(playerId))).StatusCode);
}
