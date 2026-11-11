using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RealmWeaver.Api;

namespace RealmWeaver.Api.IntegrationTests;

public class ApiSmokeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
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
}
