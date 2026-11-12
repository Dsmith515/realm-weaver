using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Realms.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;

namespace RealmWeaver.Api.IntegrationTests.Infrastructure;

/// <summary>Seed component ids (fixed in CatalogSeedData) and helpers for ritual tests.</summary>
internal static class RitualTestKit
{
    public static readonly Guid BeginnerNeedle = new("10000000-0000-0000-0000-000000000001");
    public static readonly Guid StarforgedNeedle = new("10000000-0000-0000-0000-000000000005");
    public static readonly Guid MossgreenThread = new("20000000-0000-0000-0000-000000000001"); // forest
    public static readonly Guid WheatgoldThread = new("20000000-0000-0000-0000-000000000002"); // plains
    public static readonly Guid BeastwardWax = new("30000000-0000-0000-0000-000000000001");   // field
    public static readonly Guid BrigandsPitch = new("30000000-0000-0000-0000-000000000002");  // field with a 1-floor dungeon
    public static readonly Guid GravedustLacquer = new("30000000-0000-0000-0000-000000000003"); // dungeon only

    /// <summary>A new player who also owns <paramref name="extra"/> (×3 for consumables), so tests don't depend on the random starter kit.</summary>
    public static async Task<PlayerDto> CreatePlayerWithAsync(HttpClient client, params Guid[] extra)
    {
        var response = await client.PostJsonAsync("/api/players", new CreatePlayerInput($"Ritualist {Guid.NewGuid():N}"[..20]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var player = await response.ReadJsonAsync<PlayerDto>();
        foreach (var componentId in extra)
        {
            var component = await client.GetJsonAsync<ComponentDto>($"/api/components/{componentId}");
            var grant = await client.PostJsonAsync($"/api/components/{componentId}/assign", new AssignComponentInput(player.Id, component.IsConsumable ? 3 : 1));
            Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
        }

        return player;
    }

    public static Task<HttpResponseMessage> PerformAsync(HttpClient client, RitualSelection selection, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/rituals", UriKind.Relative))
        {
            Content = JsonContent.Create(selection, options: RealmWeaverApiFactory.Json),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        return client.SendAsync(request);
    }

    public static async Task<RitualResultDto> PerformOkAsync(HttpClient client, RitualSelection selection, string? idempotencyKey = null)
    {
        var response = await PerformAsync(client, selection, idempotencyKey);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected 201 but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.ReadJsonAsync<RitualResultDto>();
    }

    public static async Task<RitualPreviewDto> PreviewAsync(HttpClient client, RitualPreviewRequest request)
    {
        var response = await client.PostJsonAsync("/api/rituals/preview", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync<RitualPreviewDto>();
    }

    public static async Task<Dictionary<Guid, int>> QuantitiesAsync(HttpClient client, Guid playerId) =>
        (await client.GetJsonAsync<List<InventoryItemDto>>($"/api/players/{playerId}/inventory"))
        .ToDictionary(i => i.Component.Id, i => i.Quantity);

    public static async Task<RealmView> GetRealmAsync(HttpClient client, Guid realmId) =>
        await client.GetJsonAsync<RealmView>($"/api/realms/{realmId}");

    /// <summary>A small field ritual: quick to generate.</summary>
    public static RitualSelection SmallField(Guid playerId, Guid? thread = null, Guid? coating = null) =>
        new(playerId, BeginnerNeedle, thread ?? MossgreenThread, coating ?? BeastwardWax, Width: 24, Height: 20);
}

/// <summary>A realm as the API returns it, with the document kept as raw JSON for comparisons.</summary>
internal sealed record RealmView(RealmSummaryDto Summary, JsonElement Document)
{
    public string AreasJson(string property) =>
        string.Join("\n", Document.GetProperty("areas").EnumerateArray().Select(a => a.GetProperty(property).GetRawText()));

    /// <summary>
    /// The terrain with every tile under an event footprint (from this realm or <paramref name="other"/>) masked out.
    /// Settlements clear terrain inside their own footprint (ADR 0002), so only the rest is the stable layout.
    /// </summary>
    public string TerrainOutsideEvents(RealmView other)
    {
        var areas = Document.GetProperty("areas").EnumerateArray().ToList();
        var otherAreas = other.Document.GetProperty("areas").EnumerateArray().ToList();
        return string.Join("\n\n", areas.Select((area, i) =>
        {
            var rows = area.GetProperty("terrainRows").EnumerateArray().Select(r => r.GetString()!.ToCharArray()).ToArray();
            var footprints = area.GetProperty("events").EnumerateArray()
                .Concat(i < otherAreas.Count ? otherAreas[i].GetProperty("events").EnumerateArray() : Enumerable.Empty<JsonElement>())
                .Select(e => e.GetProperty("footprint"));
            foreach (var f in footprints)
            {
                int x0 = f.GetProperty("x").GetInt32(), y0 = f.GetProperty("y").GetInt32();
                for (var y = y0; y < y0 + f.GetProperty("height").GetInt32() && y < rows.Length; y++)
                {
                    for (var x = x0; x < x0 + f.GetProperty("width").GetInt32() && x < rows[y].Length; x++)
                    {
                        rows[y][x] = '?';
                    }
                }
            }

            return string.Join("\n", rows.Select(r => new string(r)));
        }));
    }
}
