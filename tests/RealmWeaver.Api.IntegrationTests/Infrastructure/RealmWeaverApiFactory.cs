using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using Testcontainers.PostgreSql;

namespace RealmWeaver.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real API against a throwaway Postgres 17 container (the same image as docker-compose).
/// Startup applies every module's migrations and seed data, exactly as it does locally.
/// </summary>
public sealed class RealmWeaverApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>Matches the API's JSON settings: camelCase properties and camelCase enum strings.</summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string ConnectionString => database.GetConnectionString();

    public async Task InitializeAsync() => await database.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:RealmWeaver", database.GetConnectionString());
}

[CollectionDefinition(Name)]
public sealed class PostgresApiTests : ICollectionFixture<RealmWeaverApiFactory>
{
    // Tests share one database and some change global settings, so they run one at a time.
    public const string Name = "API with Postgres";
}

internal static class HttpJsonExtensions
{
    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(new Uri(url, UriKind.Relative), body, RealmWeaverApiFactory.Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(new Uri(url, UriKind.Relative), body, RealmWeaverApiFactory.Json);

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url) =>
        await client.GetFromJsonAsync<T>(new Uri(url, UriKind.Relative), RealmWeaverApiFactory.Json)
        ?? throw new InvalidOperationException($"GET {url} returned null.");

    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(RealmWeaverApiFactory.Json)
        ?? throw new InvalidOperationException("Response body was null.");
}

/// <summary>The parts of a ProblemDetails / ValidationProblemDetails body the tests check.</summary>
internal sealed record Problem(int Status, string? Title, string? Detail, Dictionary<string, string[]>? Errors);
