using System.Net;

using RealmWeaver.Api.IntegrationTests.Infrastructure;
using RealmWeaver.Generation.Model;
using RealmWeaver.Modules.Catalog.Contracts;

namespace RealmWeaver.Api.IntegrationTests;

[Collection(PostgresApiTests.Name)]
public class CatalogEndpointTests(RealmWeaverApiFactory factory)
{
    public static readonly Guid BeginnerNeedleId = new("10000000-0000-0000-0000-000000000001");
    public static readonly Guid MossgreenThreadId = new("20000000-0000-0000-0000-000000000001");
    public static readonly Guid EmberwireThreadId = new("20000000-0000-0000-0000-000000000007");
    public static readonly Guid BeastwardWaxId = new("30000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Seeded_catalog_has_the_beginner_needle_and_common_starter_threads()
    {
        using var client = factory.CreateClient();

        var needle = await client.GetJsonAsync<ComponentDto>($"/api/components/{BeginnerNeedleId}");
        Assert.Equal(ComponentType.Needle, needle.Type);
        Assert.Equal(1, needle.Needle?.DifficultyTier);
        Assert.False(needle.IsConsumable);

        var threads = await client.GetJsonAsync<PagedResult<ComponentDto>>("/api/components?type=thread&rarity=common&pageSize=200");
        Assert.True(threads.Items.Count >= 4);
        Assert.All(threads.Items, t => Assert.True(t.IsConsumable));
        Assert.All(threads.Items, t => Assert.NotNull(t.Thread));
    }

    [Fact]
    public async Task Created_component_can_be_read_back()
    {
        using var client = factory.CreateClient();
        var input = new ComponentInput(
            ComponentType.Needle, "Test Copper Needle", Rarity.Uncommon, 2, "  Made for a test.  ", IsPlayerAssignable: true,
            Needle: new NeedleDetails(4, "Copper", ["Spark", "Hum"]));

        var response = await client.PostJsonAsync("/api/components", input);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadJsonAsync<ComponentDto>();
        Assert.Equal($"/api/components/{created.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Made for a test.", created.Description);
        Assert.False(created.IsConsumable);
        Assert.Equal(["Spark", "Hum"], created.Needle?.Abilities);

        var fetched = await client.GetJsonAsync<ComponentDto>($"/api/components/{created.Id}");
        Assert.Equal(created.Name, fetched.Name);
        Assert.Equal(4, fetched.Needle?.DifficultyTier);
    }

    [Fact]
    public async Task Threads_and_coatings_are_always_consumable()
    {
        using var client = factory.CreateClient();
        var input = new ComponentInput(
            ComponentType.Coating, "Test Ash Wax", Rarity.Common, 1, null, IsPlayerAssignable: false,
            Coating: new CoatingDetails(["undead", "elemental"], RealmKind.Dungeon, 2));

        var created = await (await client.PostJsonAsync("/api/components", input)).ReadJsonAsync<ComponentDto>();

        Assert.True(created.IsConsumable);
        Assert.Equal(RealmKind.Dungeon, created.Coating?.RealmKind);
        Assert.Equal(["undead", "elemental"], created.Coating?.MobTypes);
    }

    [Fact]
    public async Task Details_that_do_not_match_the_type_are_rejected()
    {
        using var client = factory.CreateClient();
        var input = new ComponentInput(
            ComponentType.Needle, "", Rarity.Common, 0, null, false,
            Thread: new ThreadDetails(Biome.Forest, Element.Fire));

        var problem = await PostExpectingValidationProblemAsync(client, input);

        Assert.Contains("name", problem.Errors!.Keys);
        Assert.Contains("rank", problem.Errors.Keys);
        Assert.Contains("needle", problem.Errors.Keys);
        Assert.Contains("thread", problem.Errors.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task Needle_tier_outside_1_to_10_is_rejected(int tier)
    {
        using var client = factory.CreateClient();
        var input = new ComponentInput(ComponentType.Needle, "Bad Needle", Rarity.Common, 1, null, false, Needle: new NeedleDetails(tier, "Iron", []));

        var problem = await PostExpectingValidationProblemAsync(client, input);

        Assert.Contains("needle.difficultyTier", problem.Errors!.Keys);
    }

    [Fact]
    public async Task Coating_floor_count_must_fit_the_realm_kind()
    {
        using var client = factory.CreateClient();
        var fieldWithFloors = new ComponentInput(ComponentType.Coating, "Bad Wax", Rarity.Common, 1, null, false,
            Coating: new CoatingDetails(["beast"], RealmKind.Field, 2));
        var dungeonTooDeep = fieldWithFloors with { Coating = new CoatingDetails(["beast"], RealmKind.Dungeon, GeneratorLimits.MaxFloors + 1) };
        var badTag = fieldWithFloors with { Coating = new CoatingDetails(["Not A Tag"], RealmKind.Field, 0) };

        Assert.Contains("coating.floorCount", (await PostExpectingValidationProblemAsync(client, fieldWithFloors)).Errors!.Keys);
        Assert.Contains("coating.floorCount", (await PostExpectingValidationProblemAsync(client, dungeonTooDeep)).Errors!.Keys);
        Assert.Contains("coating.mobTypes", (await PostExpectingValidationProblemAsync(client, badTag)).Errors!.Keys);
    }

    [Fact]
    public async Task Map_must_reference_an_existing_needle_thread_and_coating()
    {
        using var client = factory.CreateClient();
        var input = new ComponentInput(ComponentType.Map, "Lost Map", Rarity.Common, 1, null, false,
            Map: new MapDetails(42, MossgreenThreadId, BeginnerNeedleId, Guid.NewGuid(), 40, 30, 10, GeneratorLimits.Version, null));

        var problem = await PostExpectingValidationProblemAsync(client, input);

        Assert.Contains("map.needleId", problem.Errors!.Keys);
        Assert.Contains("map.threadId", problem.Errors.Keys);
        Assert.Contains("map.coatingId", problem.Errors.Keys);
    }

    [Fact]
    public async Task Map_seed_keeps_full_64_bit_precision()
    {
        using var client = factory.CreateClient();
        const ulong seed = ulong.MaxValue - 12345; // far beyond what a JavaScript number can hold exactly
        var input = new ComponentInput(ComponentType.Map, "Test Map of Big Seeds", Rarity.Common, 1, null, false,
            Map: new MapDetails(seed, BeginnerNeedleId, MossgreenThreadId, BeastwardWaxId, 40, 30, 10, GeneratorLimits.Version, null));

        var response = await client.PostJsonAsync("/api/components", input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadJsonAsync<ComponentDto>();

        var raw = await client.GetStringAsync(new Uri($"/api/components/{created.Id}", UriKind.Relative));
        Assert.Contains($"\"seed\":\"{seed}\"", raw, StringComparison.Ordinal);
        var fetched = await client.GetJsonAsync<ComponentDto>($"/api/components/{created.Id}");
        Assert.Equal(seed, fetched.Map?.Seed);
        Assert.False(fetched.IsConsumable);
    }

    [Fact]
    public async Task Components_can_be_filtered_by_type_biome_and_text()
    {
        using var client = factory.CreateClient();
        var token = $"zq{Guid.NewGuid():N}"[..12];
        await client.PostJsonAsync("/api/components", new ComponentInput(ComponentType.Thread, $"Test {token} Thread", Rarity.Rare, 3, null, false,
            Thread: new ThreadDetails(Biome.Volcanic, Element.Shadow)));
        await client.PostJsonAsync("/api/components", new ComponentInput(ComponentType.Needle, "Test Plain Needle", Rarity.Rare, 3, $"Mentions {token} only here.", false,
            Needle: new NeedleDetails(2, "Iron", [])));

        var byText = await client.GetJsonAsync<PagedResult<ComponentDto>>($"/api/components?search={token.ToUpperInvariant()}");
        Assert.Equal(2, byText.TotalCount);

        var byTypeAndText = await client.GetJsonAsync<PagedResult<ComponentDto>>($"/api/components?type=thread&search={token}");
        Assert.Equal($"Test {token} Thread", Assert.Single(byTypeAndText.Items).Name);

        var volcanic = await client.GetJsonAsync<PagedResult<ComponentDto>>("/api/components?biome=volcanic&pageSize=200");
        Assert.Contains(volcanic.Items, c => c.Id == EmberwireThreadId);
        Assert.All(volcanic.Items, c => Assert.Equal(Biome.Volcanic, c.Thread?.Biome));
    }

    [Fact]
    public async Task Like_wildcards_in_search_text_are_matched_literally()
    {
        using var client = factory.CreateClient();

        var result = await client.GetJsonAsync<PagedResult<ComponentDto>>("/api/components?search=%25");

        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Paging_returns_the_requested_slice_and_the_total()
    {
        using var client = factory.CreateClient();

        var all = await client.GetJsonAsync<PagedResult<ComponentDto>>("/api/components?pageSize=200");
        var page2 = await client.GetJsonAsync<PagedResult<ComponentDto>>("/api/components?page=2&pageSize=3");

        Assert.Equal(all.TotalCount, page2.TotalCount);
        Assert.Equal(all.Items.Skip(3).Take(3).Select(c => c.Id), page2.Items.Select(c => c.Id));
    }

    [Theory]
    [InlineData("type=sword")]
    [InlineData("rarity=7")]
    [InlineData("biome=moon")]
    [InlineData("pageSize=1000")]
    [InlineData("page=0")]
    public async Task Invalid_filters_are_rejected(string queryString)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri($"/api/components?{queryString}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_replaces_fields_but_not_the_type()
    {
        using var client = factory.CreateClient();
        var input = new ComponentInput(ComponentType.Thread, "Test Dull Thread", Rarity.Common, 1, null, false,
            Thread: new ThreadDetails(Biome.Plains, Element.None));
        var created = await (await client.PostJsonAsync("/api/components", input)).ReadJsonAsync<ComponentDto>();

        var renamed = input with { Name = "Test Bright Thread", IsPlayerAssignable = true, Thread = new ThreadDetails(Biome.Coast, Element.Light) };
        var updateResponse = await client.PutJsonAsync($"/api/components/{created.Id}", renamed);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.ReadJsonAsync<ComponentDto>();
        Assert.Equal("Test Bright Thread", updated.Name);
        Assert.Equal(Biome.Coast, updated.Thread?.Biome);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);

        var retyped = new ComponentInput(ComponentType.Needle, "Test Needle Now", Rarity.Common, 1, null, false, Needle: new NeedleDetails(1, "Iron", []));
        var retypeResponse = await client.PutJsonAsync($"/api/components/{created.Id}", retyped);
        Assert.Equal(HttpStatusCode.BadRequest, retypeResponse.StatusCode);
        Assert.Contains("type", (await retypeResponse.ReadJsonAsync<Problem>()).Errors!.Keys);
    }

    [Fact]
    public async Task Archived_components_leave_the_catalog_but_stay_readable()
    {
        using var client = factory.CreateClient();
        var token = $"arch{Guid.NewGuid():N}"[..12];
        var input = new ComponentInput(ComponentType.Needle, $"Test {token} Needle", Rarity.Common, 1, null, false, Needle: new NeedleDetails(1, "Iron", []));
        var created = await (await client.PostJsonAsync("/api/components", input)).ReadJsonAsync<ComponentDto>();

        var delete = await client.DeleteAsync(new Uri($"/api/components/{created.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(0, (await client.GetJsonAsync<PagedResult<ComponentDto>>($"/api/components?search={token}")).TotalCount);
        Assert.Equal(1, (await client.GetJsonAsync<PagedResult<ComponentDto>>($"/api/components?search={token}&includeArchived=true")).TotalCount);
        Assert.True((await client.GetJsonAsync<ComponentDto>($"/api/components/{created.Id}")).IsArchived);
    }

    [Theory]
    [InlineData(ComponentType.Needle, "Test Sliver")]
    [InlineData(ComponentType.Thread, "Thread")]
    [InlineData(ComponentType.Coating, "Test Goo")]
    public async Task Names_must_end_with_a_word_allowed_for_the_type(ComponentType type, string name)
    {
        using var client = factory.CreateClient();
        var input = type switch
        {
            ComponentType.Needle => new ComponentInput(type, name, Rarity.Common, 1, null, false, Needle: new NeedleDetails(1, "Iron", [])),
            ComponentType.Thread => new ComponentInput(type, name, Rarity.Common, 1, null, false, Thread: new ThreadDetails(Biome.Forest, Element.Earth)),
            _ => new ComponentInput(type, name, Rarity.Common, 1, null, false, Coating: new CoatingDetails(["beast"], RealmKind.Field, 0)),
        };

        var problem = await PostExpectingValidationProblemAsync(client, input);

        Assert.Contains("name", problem.Errors!.Keys);
    }

    [Fact]
    public async Task Name_suffixes_are_published_for_the_form()
    {
        using var client = factory.CreateClient();

        var suffixes = await client.GetJsonAsync<NameSuffixesDto>("/api/components/name-suffixes");

        Assert.Equal("Needle", suffixes.Needle[0]);
        Assert.Contains("Wax", suffixes.Coating);
        Assert.Contains("Lacquer", suffixes.Coating);
    }

    [Fact]
    public async Task A_map_created_without_a_recipe_copies_it_from_its_components()
    {
        using var client = factory.CreateClient();
        var input = new ComponentInput(ComponentType.Map, "Test Copied Recipe Map", Rarity.Common, 1, null, false,
            Map: new MapDetails(7, BeginnerNeedleId, MossgreenThreadId, BeastwardWaxId, 40, 30, 10, GeneratorLimits.Version, null));

        var created = await (await client.PostJsonAsync("/api/components", input)).ReadJsonAsync<ComponentDto>();

        var recipe = created.Map?.Recipe;
        Assert.NotNull(recipe);
        Assert.Equal((RealmKind.Field, 0, Biome.Forest, Element.Earth, 1), (recipe.Kind, recipe.FloorCount, recipe.Biome, recipe.Element, recipe.Difficulty));
        Assert.Equal(["beast"], recipe.MobTags);
    }

    [Fact]
    public async Task Recipe_fields_of_a_component_used_by_a_map_are_locked()
    {
        using var client = factory.CreateClient();
        var input = new ComponentInput(ComponentType.Coating, "Test Locking Wax", Rarity.Common, 1, null, false,
            Coating: new CoatingDetails(["beast"], RealmKind.Field, 0));
        var coating = await (await client.PostJsonAsync("/api/components", input)).ReadJsonAsync<ComponentDto>();
        Assert.Equal(0, coating.UsedByMapCount);
        await client.PostJsonAsync("/api/components", new ComponentInput(ComponentType.Map, "Test Locking Map", Rarity.Common, 1, null, false,
            Map: new MapDetails(9, BeginnerNeedleId, MossgreenThreadId, coating.Id, 40, 30, 10, GeneratorLimits.Version, null)));

        var reshaped = await client.PutJsonAsync($"/api/components/{coating.Id}", input with { Coating = new CoatingDetails(["beast"], RealmKind.Dungeon, 1) });
        var renamed = await client.PutJsonAsync($"/api/components/{coating.Id}", input with { Name = "Test Relabelled Wax", Description = "Same recipe." });

        Assert.Equal(HttpStatusCode.Conflict, reshaped.StatusCode);
        Assert.Contains("coating.realmKind", (await reshaped.ReadJsonAsync<Problem>()).Detail, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var fetched = await client.GetJsonAsync<ComponentDto>($"/api/components/{coating.Id}");
        Assert.Equal(("Test Relabelled Wax", RealmKind.Field, 1), (fetched.Name, fetched.Coating!.RealmKind, fetched.UsedByMapCount));
    }

    [Fact]
    public async Task Unknown_component_returns_not_found_problem()
    {
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();

        var response = await client.GetAsync(new Uri($"/api/components/{id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(id.ToString(), (await response.ReadJsonAsync<Problem>()).Detail, StringComparison.Ordinal);
    }

    private static async Task<Problem> PostExpectingValidationProblemAsync(HttpClient client, ComponentInput input)
    {
        var response = await client.PostJsonAsync("/api/components", input);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadJsonAsync<Problem>();
        Assert.NotNull(problem.Errors);
        return problem;
    }
}
