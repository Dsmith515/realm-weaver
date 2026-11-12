using System.Net;
using System.Net.Http.Json;

using RealmWeaver.Api.IntegrationTests.Infrastructure;

namespace RealmWeaver.Api.IntegrationTests;

[Collection(PostgresApiTests.Name)]
public class ApiSmokeTests(RealmWeaverApiFactory factory)
{
    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Info_endpoint_returns_name()
    {
        using var client = factory.CreateClient();
        var info = await client.GetFromJsonAsync<ApiInfo>(new Uri("/api/info", UriKind.Relative));
        Assert.Equal("Realm Weaver", info?.Name);
    }

    [Fact]
    public async Task OpenApi_document_describes_the_module_endpoints()
    {
        using var client = factory.CreateClient();

        var document = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Contains("\"/api/components\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/api/players/{id}/inventory\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/api/settings\"", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Malformed_json_body_returns_bad_request_not_server_error()
    {
        using var client = factory.CreateClient();
        using var body = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync(new Uri("/api/players", UriKind.Relative), body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
