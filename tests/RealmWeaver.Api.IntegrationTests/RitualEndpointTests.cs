using System.Net;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using RealmWeaver.Api.IntegrationTests.Infrastructure;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Realms.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;

using static RealmWeaver.Api.IntegrationTests.Infrastructure.RitualTestKit;

namespace RealmWeaver.Api.IntegrationTests;

[Collection(PostgresApiTests.Name)]
public class RitualEndpointTests(RealmWeaverApiFactory factory)
{
    [Fact]
    public async Task Ritual_spends_consumables_creates_a_map_and_stores_the_realm()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var before = await QuantitiesAsync(client, player.Id);

        var response = await PerformAsync(client, SmallField(player.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.ReadJsonAsync<RitualResultDto>();
        Assert.Equal($"/api/rituals/{result.RitualId}", response.Headers.Location?.OriginalString);
        Assert.True(result.MapCreated);
        Assert.False(result.IsMismatch);

        var after = await QuantitiesAsync(client, player.Id);
        Assert.Equal(before[MossgreenThread] - 1, after[MossgreenThread]);
        Assert.Equal(before[BeastwardWax] - 1, after[BeastwardWax]);
        Assert.Equal(1, after[BeginnerNeedle]);
        Assert.Equal(1, after[result.MapId]);

        var map = await client.GetJsonAsync<ComponentDto>($"/api/components/{result.MapId}");
        Assert.Equal(ComponentType.Map, map.Type);
        Assert.Equal((BeginnerNeedle, MossgreenThread, BeastwardWax), (map.Map!.NeedleId, map.Map.ThreadId, map.Map.CoatingId));
        Assert.Equal((24, 20), (map.Map.Width, map.Map.Height));
        Assert.Equal(player.Id, map.Map.CreatedByPlayerId);

        var realm = await GetRealmAsync(client, result.RealmId);
        Assert.Equal(result.MapId, realm.Summary.MapComponentId);
        Assert.Equal(map.Map.Seed, realm.Summary.Seed);
        Assert.Equal(Generation.Model.Biome.Forest, realm.Summary.Biome);
        Assert.Equal(Generation.Model.RealmKind.Field, realm.Summary.Kind);

        var ritual = await client.GetJsonAsync<RitualResultDto>($"/api/rituals/{result.RitualId}");
        Assert.Equal(result, ritual);
    }

    [Fact]
    public async Task Repeating_a_request_with_the_same_key_replays_the_first_result()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var before = await QuantitiesAsync(client, player.Id);
        var key = $"replay-{Guid.NewGuid()}";

        var first = await PerformOkAsync(client, SmallField(player.Id), key);
        var again = await PerformAsync(client, SmallField(player.Id), key);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal("true", again.Headers.GetValues("Idempotent-Replayed").Single());
        Assert.Equal(first, await again.ReadJsonAsync<RitualResultDto>());
        Assert.Equal(before[MossgreenThread] - 1, (await QuantitiesAsync(client, player.Id))[MossgreenThread]);
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_key_perform_the_ritual_once()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var before = await QuantitiesAsync(client, player.Id);
        var key = $"burst-{Guid.NewGuid()}";

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => PerformAsync(client, SmallField(player.Id), key)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, $"Got {(int)r.StatusCode}"));
        var results = await Task.WhenAll(responses.Select(r => r.ReadJsonAsync<RitualResultDto>()));
        Assert.Single(results.Select(r => r.RitualId).Distinct());
        Assert.Equal(before[MossgreenThread] - 1, (await QuantitiesAsync(client, player.Id))[MossgreenThread]);
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_request_is_rejected()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, WheatgoldThread, BeastwardWax);
        var key = $"reuse-{Guid.NewGuid()}";
        await PerformOkAsync(client, SmallField(player.Id), key);

        var different = await PerformAsync(client, SmallField(player.Id, thread: WheatgoldThread), key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, different.StatusCode);
    }

    [Fact]
    public async Task A_ritual_needs_an_idempotency_key()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client);

        var response = await client.PostJsonAsync("/api/rituals", SmallField(player.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Idempotency-Key", (await response.ReadJsonAsync<Problem>()).Errors!.Keys);
    }

    [Fact]
    public async Task Two_rituals_racing_for_the_last_thread_give_one_success_and_one_conflict()
    {
        using var client = factory.CreateClient();
        var lastThread = await (await client.PostJsonAsync("/api/components", new ComponentInput(
            ComponentType.Thread, "Test Last Thread", Rarity.Rare, 1, null, false,
            Thread: new ThreadDetails(Generation.Model.Biome.Desert, Generation.Model.Element.Fire)))).ReadJsonAsync<ComponentDto>();

        for (var round = 0; round < 3; round++)
        {
            var player = await CreatePlayerWithAsync(client, BeastwardWax);
            await client.PostJsonAsync($"/api/components/{lastThread.Id}/assign", new Modules.Inventory.Contracts.AssignComponentInput(player.Id, 1));

            var responses = await Task.WhenAll(
                PerformAsync(client, SmallField(player.Id, thread: lastThread.Id)),
                PerformAsync(client, SmallField(player.Id, thread: lastThread.Id)));

            var statuses = responses.Select(r => r.StatusCode).Order().ToArray();
            Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], statuses);
            Assert.Equal("application/problem+json", responses.Single(r => r.StatusCode == HttpStatusCode.Conflict).Content.Headers.ContentType?.MediaType);
            Assert.False((await QuantitiesAsync(client, player.Id)).ContainsKey(lastThread.Id));
            Assert.Single(await client.GetJsonAsync<List<RealmSummaryDto>>($"/api/realms?playerId={player.Id}"));
        }
    }

    [Fact]
    public async Task Components_the_player_does_not_own_are_a_conflict_and_change_nothing()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var before = await QuantitiesAsync(client, player.Id);

        var response = await PerformAsync(client, SmallField(player.Id) with { NeedleId = StarforgedNeedle });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Starforged Needle", (await response.ReadJsonAsync<Problem>()).Detail, StringComparison.Ordinal);
        Assert.Equal(before, await QuantitiesAsync(client, player.Id));
    }

    [Fact]
    public async Task Invalid_selections_are_rejected_with_field_errors()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread);

        var wrongType = await PerformAsync(client, new RitualSelection(player.Id, BeginnerNeedle, ThreadId: BeastwardWax));
        var badSize = await PerformAsync(client, SmallField(player.Id) with { Width = 5 });
        var dungeonEvents = await PerformAsync(client, SmallField(player.Id, coating: GravedustLacquer) with { EventCount = 10 });

        Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);
        var errors = (await wrongType.ReadJsonAsync<Problem>()).Errors!;
        Assert.Contains("threadId", errors.Keys);
        Assert.Contains("coatingId", errors.Keys);
        Assert.Contains("width", (await badSize.ReadJsonAsync<Problem>()).Errors!.Keys);
        Assert.Contains("eventCount", (await dungeonEvents.ReadJsonAsync<Problem>()).Errors!.Keys);
    }

    [Fact]
    public async Task Unknown_player_returns_not_found()
    {
        using var client = factory.CreateClient();

        var perform = await PerformAsync(client, SmallField(Guid.NewGuid()));
        var preview = await client.PostJsonAsync("/api/rituals/preview", new RitualPreviewRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, perform.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, preview.StatusCode);
    }

    [Fact]
    public async Task Reusing_a_map_rebuilds_the_same_realm_without_a_new_map()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BrigandsPitch);
        var first = await PerformOkAsync(client, SmallField(player.Id, coating: BrigandsPitch));

        // Only the map: the other slots fill in from it.
        var preview = await PreviewAsync(client, new RitualPreviewRequest(player.Id, MapId: first.MapId));
        Assert.Equal(SlotSource.FromMap, preview.Thread?.Source);
        Assert.False(preview.CreatesNewMap);
        var again = await PerformOkAsync(client, new RitualSelection(player.Id, MapId: first.MapId));

        Assert.False(again.MapCreated);
        Assert.Equal(first.MapId, again.MapId);
        Assert.NotEqual(first.RealmId, again.RealmId);
        var original = await GetRealmAsync(client, first.RealmId);
        var rebuilt = await GetRealmAsync(client, again.RealmId);
        Assert.Equal(original.Document.GetProperty("areas").GetRawText(), rebuilt.Document.GetProperty("areas").GetRawText());
        Assert.Equal(1, (await QuantitiesAsync(client, player.Id))[first.MapId]);
    }

    [Fact]
    public async Task Choosing_the_components_of_an_owned_map_fills_in_the_map()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var first = await PerformOkAsync(client, SmallField(player.Id));

        var preview = await PreviewAsync(client, new RitualPreviewRequest(player.Id, BeginnerNeedle, MossgreenThread, BeastwardWax));
        var again = await PerformOkAsync(client, new RitualSelection(player.Id, BeginnerNeedle, MossgreenThread, BeastwardWax));

        Assert.Equal(first.MapId, preview.Map?.Component.Id);
        Assert.Equal(SlotSource.MatchingMap, preview.Map?.Source);
        Assert.False(preview.IsMismatch);
        Assert.Equal(first.MapId, again.MapId);
        Assert.False(again.MapCreated);
    }

    [Fact]
    public async Task A_mismatched_map_keeps_its_layout_raises_danger_and_rerolls_encounters()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, WheatgoldThread, BeastwardWax);
        var first = await PerformOkAsync(client, SmallField(player.Id));

        var preview = await PreviewAsync(client, new RitualPreviewRequest(player.Id, ThreadId: WheatgoldThread, MapId: first.MapId));
        var mismatched = await PerformOkAsync(client, new RitualSelection(player.Id, ThreadId: WheatgoldThread, MapId: first.MapId));

        Assert.True(preview.IsMismatch);
        Assert.Equal([RitualSlot.Thread], preview.MismatchedSlots);
        Assert.Equal(1, preview.DangerModifier);
        Assert.True(mismatched.IsMismatch);
        Assert.Equal(1, mismatched.DangerModifier);
        Assert.NotEqual(0, mismatched.EncounterVariant);
        Assert.Equal(first.MapId, mismatched.MapId);

        var original = await GetRealmAsync(client, first.RealmId);
        var rerolled = await GetRealmAsync(client, mismatched.RealmId);
        Assert.Equal(original.TerrainOutsideEvents(rerolled), rerolled.TerrainOutsideEvents(original));
        Assert.Equal(
            original.Document.GetProperty("areas")[0].GetProperty("spawn").GetRawText(),
            rerolled.Document.GetProperty("areas")[0].GetProperty("spawn").GetRawText());
        Assert.NotEqual(original.AreasJson("events"), rerolled.AreasJson("events"));
        Assert.Equal(original.Summary.Threat + 1, rerolled.Summary.Threat);
        Assert.Equal(original.Summary.Biome, rerolled.Summary.Biome);

        // The same mismatch again gives the same encounters: rerolls are deterministic.
        var repeat = await PerformOkAsync(client, new RitualSelection(player.Id, ThreadId: WheatgoldThread, MapId: first.MapId));
        Assert.Equal(rerolled.AreasJson("events"), (await GetRealmAsync(client, repeat.RealmId)).AreasJson("events"));
    }

    [Fact]
    public async Task Two_mismatched_slots_raise_danger_by_two()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, WheatgoldThread, BeastwardWax, BrigandsPitch);
        var first = await PerformOkAsync(client, SmallField(player.Id));

        var preview = await PreviewAsync(client, new RitualPreviewRequest(player.Id, ThreadId: WheatgoldThread, CoatingId: BrigandsPitch, MapId: first.MapId));

        Assert.Equal(2, preview.DangerModifier);
        Assert.Equal([RitualSlot.Thread, RitualSlot.Coating], preview.MismatchedSlots);
    }

    [Fact]
    public async Task Randomize_fills_empty_slots_from_the_players_own_items()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client);
        var owned = await QuantitiesAsync(client, player.Id);

        var preview = await PreviewAsync(client, new RitualPreviewRequest(player.Id, Randomize: true));

        Assert.True(preview.CanPerform, string.Join("; ", preview.Issues.Select(i => i.Message)));
        foreach (var slot in new[] { preview.Needle, preview.Thread, preview.Coating })
        {
            Assert.NotNull(slot);
            Assert.Equal(SlotSource.Random, slot.Source);
            Assert.True(owned.GetValueOrDefault(slot.Component.Id) >= 1);
        }
    }

    [Fact]
    public async Task Preview_lists_missing_and_unowned_components()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client);

        var preview = await PreviewAsync(client, new RitualPreviewRequest(player.Id, NeedleId: StarforgedNeedle));

        Assert.False(preview.CanPerform);
        Assert.Contains(preview.Issues, i => i.Slot == RitualSlot.Needle && i.Code == "not-owned");
        Assert.Contains(preview.Issues, i => i.Slot == RitualSlot.Thread && i.Code == "missing");
        Assert.Contains(preview.Issues, i => i.Slot == RitualSlot.Coating && i.Code == "missing");
        Assert.True(preview.CreatesNewMap);
    }

    [Fact]
    public async Task A_failure_mid_transaction_rolls_back_every_step()
    {
        using var client = factory.CreateClient();
        var player = await CreatePlayerWithAsync(client, MossgreenThread, BeastwardWax);
        var before = await QuantitiesAsync(client, player.Id);
        var mapsBefore = (await client.GetJsonAsync<PagedResult<ComponentDto>>("/api/components?type=map&pageSize=1")).TotalCount;
        var key = $"rollback-{Guid.NewGuid()}";

        // Same database, but storing the realm (the last step) fails after the key, spend, map and grant succeeded.
        using (var failing = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IRealmsModule, FailingRealms>())))
        using (var failingClient = failing.CreateClient())
        {
            var response = await PerformAsync(failingClient, SmallField(player.Id), key);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        Assert.Equal(before, await QuantitiesAsync(client, player.Id));
        Assert.Equal(mapsBefore, (await client.GetJsonAsync<PagedResult<ComponentDto>>("/api/components?type=map&pageSize=1")).TotalCount);
        Assert.Empty(await client.GetJsonAsync<List<RealmSummaryDto>>($"/api/realms?playerId={player.Id}"));

        // The key wasn't kept either: retrying it performs the ritual for real.
        var retry = await PerformAsync(client, SmallField(player.Id), key);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    private sealed class FailingRealms : IRealmsModule
    {
        public Task<RealmSummaryDto> SaveAsync(NewRealm realm, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Injected failure while storing the realm.");

        public Task<RealmSummaryDto?> GetSummaryAsync(Guid realmId, CancellationToken cancellationToken = default) =>
            Task.FromResult<RealmSummaryDto?>(null);
    }
}
