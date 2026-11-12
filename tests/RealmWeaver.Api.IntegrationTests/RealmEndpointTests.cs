using System.Net;
using System.Text.Json;

using RealmWeaver.Api.IntegrationTests.Infrastructure;
using RealmWeaver.Generation.Model;
using RealmWeaver.Modules.Realms.Contracts;

using static RealmWeaver.Api.IntegrationTests.Infrastructure.RitualTestKit;

namespace RealmWeaver.Api.IntegrationTests;

[Collection(PostgresApiTests.Name)]
public class RealmEndpointTests(RealmWeaverApiFactory factory)
{
    [Fact]
    public async Task A_field_with_a_dungeon_lists_every_area_and_exports_each_one()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BrigandsPitch);
        var ritual = await PerformOkAsync(client, SmallField(player.Id, coating: BrigandsPitch));

        var realm = await GetRealmAsync(client, ritual.RealmId);

        Assert.Equal(RealmKind.FieldWithDungeon, realm.Summary.Kind);
        Assert.Equal(1, realm.Summary.FloorCount);
        Assert.Equal([AreaKind.Field, AreaKind.DungeonFloor], realm.Summary.Areas.Select(a => a.Kind));
        foreach (var area in realm.Summary.Areas)
        {
            var svg = await client.GetAsync(new Uri($"/api/realms/{ritual.RealmId}/export?format=svg&area={area.Id}", UriKind.Relative));
            Assert.Equal(HttpStatusCode.OK, svg.StatusCode);
            Assert.Equal("image/svg+xml", svg.Content.Headers.ContentType?.MediaType);
            Assert.StartsWith("<svg", (await svg.Content.ReadAsStringAsync()).TrimStart(), StringComparison.Ordinal);
            Assert.EndsWith($"-{area.Id}.svg", svg.Content.Headers.ContentDisposition?.FileName?.Trim('"'), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("prompt", "text/plain")]
    [InlineData("md", "text/markdown")]
    [InlineData("json", "application/json")]
    public async Task Text_exports_have_the_right_content_type(string format, string mediaType)
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));

        var response = await client.GetAsync(new Uri($"/api/realms/{ritual.RealmId}/export?format={format}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));
        if (format == "json")
        {
            using var json = JsonDocument.Parse(body);
            var summary = (await GetRealmAsync(client, ritual.RealmId)).Summary;
            Assert.Equal(summary.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture), json.RootElement.GetProperty("seed").GetString());
        }
    }

    [Fact]
    public async Task Export_rejects_unknown_formats_and_areas()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var ritual = await PerformOkAsync(client, SmallField(player.Id));

        var badFormat = await client.GetAsync(new Uri($"/api/realms/{ritual.RealmId}/export?format=pdf", UriKind.Relative));
        var badArea = await client.GetAsync(new Uri($"/api/realms/{ritual.RealmId}/export?format=svg&area=floor-9", UriKind.Relative));
        var unknownRealm = await client.GetAsync(new Uri($"/api/realms/{Guid.NewGuid()}/export", UriKind.Relative));

        Assert.Contains("format", (await badFormat.ReadJsonAsync<Problem>()).Errors!.Keys);
        Assert.Contains("area", (await badArea.ReadJsonAsync<Problem>()).Errors!.Keys);
        Assert.Equal(HttpStatusCode.NotFound, unknownRealm.StatusCode);
    }

    [Fact]
    public async Task Realms_are_listed_newest_first_per_player_and_per_map()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var first = await PerformOkAsync(client, SmallField(player.Id));
        var second = await PerformOkAsync(client, new Modules.Rituals.Contracts.RitualSelection(player.Id, MapId: first.MapId));

        var byPlayer = await client.GetJsonAsync<List<RealmSummaryDto>>($"/api/realms?playerId={player.Id}");
        var byMap = await client.GetJsonAsync<List<RealmSummaryDto>>($"/api/realms?mapId={first.MapId}");

        Assert.Equal([second.RealmId, first.RealmId], byPlayer.Select(r => r.Id));
        Assert.Equal([second.RealmId, first.RealmId], byMap.Select(r => r.Id));
        Assert.All(byPlayer, r => Assert.Single(r.Areas));
    }
}
