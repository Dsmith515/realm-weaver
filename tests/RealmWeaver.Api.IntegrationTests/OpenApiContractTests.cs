using System.Text.Json;
using System.Text.Json.Nodes;

using RealmWeaver.Api.IntegrationTests.Infrastructure;

namespace RealmWeaver.Api.IntegrationTests;

/// <summary>
/// The frontend's TypeScript types are generated from a committed copy of the OpenAPI document
/// (<c>src/frontend/openapi/realm-weaver.json</c>). This test fails when the API and that copy drift apart,
/// so a backend change can't silently break the frontend contract.
/// </summary>
[Collection(PostgresApiTests.Name)]
public class OpenApiContractTests(RealmWeaverApiFactory factory)
{
    private const string UpdateVariable = "UPDATE_OPENAPI";

    [Fact]
    public async Task OpenApi_document_matches_the_committed_frontend_contract()
    {
        using var client = factory.CreateClient();
        var live = Normalize(await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative)));
        var path = Path.Combine(RepositoryRoot(), "src", "frontend", "openapi", "realm-weaver.json");

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, live);
        }

        var committed = File.Exists(path) ? (await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n") : string.Empty;
        Assert.True(
            committed == live,
            $"The API's OpenAPI document differs from {path}. Run `{UpdateVariable}=1 dotnet test --filter OpenApiContractTests`, "
            + "then `npm run api:types` in src/frontend, and commit both files.");
    }

    /// <summary>Indented, LF line endings, and without the host-specific <c>servers</c> list.</summary>
    private static string Normalize(string json)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        node.Remove("servers");
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n";
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RealmWeaver.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Couldn't find the repository root (RealmWeaver.slnx).");
    }
}
