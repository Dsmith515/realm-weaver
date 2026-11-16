using System.Text;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

using RealmWeaver.Generation.Export;
using RealmWeaver.Generation.Model;
using RealmWeaver.Infrastructure.Http;
using RealmWeaver.Modules.Realms.Contracts;

namespace RealmWeaver.Modules.Realms.Endpoints;

internal static class RealmsEndpoints
{
    private static readonly string[] Formats = ["svg", "prompt", "json", "md"];

    public static void Map(IEndpointRouteBuilder app)
    {
        var realms = app.MapGroup("/api/realms").WithTags("Realms");
        realms.MapGet("/", ListAsync).WithName("ListRealms")
            .WithDescription("Realm summaries, newest first. Filter by player, by the map they were opened from, or gm=true for GM Quick Weaves.");
        realms.MapGet("/{id:guid}", GetAsync).WithName("GetRealm");
        realms.MapGet("/{id:guid}/objectives/candidates", CandidatesAsync).WithName("ListObjectiveCandidates")
            .WithDescription("Every objective a GM can pin: the rolled objectives first, then the other hooks of the placed events and the realm-wide objectives.");
        realms.MapPut("/{id:guid}/pinned-objective", PinAsync).WithName("PinObjective")
            .WithDescription("Pins one candidate objective, by key, for this run. Stored on the realm, never on the map.");
        realms.MapDelete("/{id:guid}/pinned-objective", UnpinAsync).WithName("UnpinObjective");
        realms.MapGet("/{id:guid}/export", ExportAsync).WithName("ExportRealm")
            .Produces<string>(StatusCodes.Status200OK, "image/svg+xml", "text/plain", "text/markdown", "application/json")
            .WithDescription("format=svg|prompt (one area; area defaults to the first) or json|md (the whole realm).");
    }

    private static async Task<Results<Ok<IReadOnlyList<RealmSummaryDto>>, ValidationProblem>> ListAsync(
        RealmsService realms, CancellationToken cancellationToken, Guid? playerId = null, Guid? mapId = null, int limit = 50, bool gm = false)
    {
        if (limit is < 1 or > RealmsService.MaxListSize)
        {
            return Problems.Validation(new Dictionary<string, string[]> { ["limit"] = [$"Limit must be between 1 and {RealmsService.MaxListSize}."] });
        }

        return TypedResults.Ok(await realms.ListAsync(playerId, mapId, limit, cancellationToken, gmOnly: gm));
    }

    private static async Task<Results<Ok<RealmDto>, ProblemHttpResult>> GetAsync(Guid id, RealmsService realms, CancellationToken cancellationToken) =>
        await realms.GetAsync(id, cancellationToken) is { } realm
            ? TypedResults.Ok(realm)
            : Problems.NotFound("Realm", id);

    private static async Task<Results<Ok<IReadOnlyList<RealmObjective>>, ProblemHttpResult>> CandidatesAsync(
        Guid id, RealmsService realms, CancellationToken cancellationToken) =>
        await realms.GetObjectiveCandidatesAsync(id, cancellationToken) is { } candidates
            ? TypedResults.Ok(candidates)
            : Problems.NotFound("Realm", id);

    private static async Task<Results<Ok<RealmSummaryDto>, ProblemHttpResult, ValidationProblem>> PinAsync(
        Guid id, PinObjectiveInput input, RealmsService realms, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Key))
        {
            return Problems.Validation(new Dictionary<string, string[]> { ["key"] = ["Give the key of the objective to pin."] });
        }

        return await realms.PinObjectiveAsync(id, input.Key, cancellationToken) switch
        {
            PinOutcome.Pinned pinned => TypedResults.Ok(pinned.Summary),
            PinOutcome.UnknownObjective => Problems.Validation(new Dictionary<string, string[]> { ["key"] = ["This realm offers no objective with that key."] }),
            _ => Problems.NotFound("Realm", id),
        };
    }

    private static async Task<Results<Ok<RealmSummaryDto>, ProblemHttpResult>> UnpinAsync(Guid id, RealmsService realms, CancellationToken cancellationToken) =>
        await realms.PinObjectiveAsync(id, key: null, cancellationToken) is PinOutcome.Pinned pinned
            ? TypedResults.Ok(pinned.Summary)
            : Problems.NotFound("Realm", id);

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult, ValidationProblem>> ExportAsync(
        Guid id, RealmsService realms, CancellationToken cancellationToken, string format = "svg", string? area = null)
    {
        var realm = await realms.GetAsync(id, cancellationToken);
        if (realm is null)
        {
            return Problems.NotFound("Realm", id);
        }

        var errors = new ValidationErrors();
        var normalizedFormat = format.ToLowerInvariant();
        errors.Require(Formats.Contains(normalizedFormat), "format", $"Use one of: {string.Join(", ", Formats)}.");
        errors.Require(
            area is null || realm.Document.Areas.Any(a => a.Id == area),
            "area",
            $"This realm's areas are: {string.Join(", ", realm.Document.Areas.Select(a => a.Id))}.");
        if (!errors.IsValid)
        {
            return Problems.Validation(errors.ToDictionary());
        }

        var document = realm.Document;
        var areaId = area ?? document.Areas[0].Id;
        var baseName = $"realm-{id.ToString("N")[..8]}";
        var (content, contentType, fileName) = normalizedFormat switch
        {
            "svg" => (SvgExporter.Render(document, areaId), "image/svg+xml", $"{baseName}-{areaId}.svg"),
            "prompt" => (PromptExporter.Build(document, areaId), "text/plain; charset=utf-8", $"{baseName}-{areaId}.prompt.txt"),
            "json" => (RealmJson.Serialize(document), "application/json", $"{baseName}.json"),
            _ => (MarkdownExporter.Build(document), "text/markdown; charset=utf-8", $"{baseName}.md"),
        };

        return TypedResults.File(Encoding.UTF8.GetBytes(content), contentType, fileName);
    }
}
