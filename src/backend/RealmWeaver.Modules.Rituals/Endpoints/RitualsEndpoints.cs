using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using RealmWeaver.Infrastructure.Http;
using RealmWeaver.Modules.Rituals.Contracts;
using RealmWeaver.Modules.Rituals.Persistence;

namespace RealmWeaver.Modules.Rituals.Endpoints;

internal static class RitualsEndpoints
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";

    public static void Map(IEndpointRouteBuilder app)
    {
        var rituals = app.MapGroup("/api/rituals").WithTags("Rituals");

        rituals.MapPost("/preview", PreviewAsync).WithName("PreviewRitual")
            .WithDescription("Resolves the slots without changing anything: map auto-fill, random fill, mismatch and danger, and anything that would stop the ritual.");

        rituals.MapPost("/", PerformAsync).WithName("PerformRitual")
            .WithDescription(
                "Performs the ritual in one transaction. Requires an Idempotency-Key header: repeating a request with the same key "
                + "returns the first result (200, Idempotent-Replayed: true) instead of performing it again. "
                + "400: invalid selection. 409: components not owned, or spent by a concurrent ritual (reload and retry). "
                + "422: the key was already used for a different request, or the realm can't be generated.");

        rituals.MapGet("/{id:guid}", GetAsync).WithName("GetRitual");
    }

    private static async Task<Results<Ok<RitualPreviewDto>, ProblemHttpResult>> PreviewAsync(
        [FromBody] RitualPreviewRequest request, RitualResolver resolver, CancellationToken cancellationToken) =>
        await resolver.ResolveAsync(request.ToSelection(), request.Randomize, cancellationToken) is { } resolution
            ? TypedResults.Ok(resolution.Preview)
            : Problems.NotFound("Player", request.PlayerId);

    private static async Task<IResult> PerformAsync(
        [FromBody] RitualSelection selection,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        RitualService rituals,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > Ritual.MaxIdempotencyKeyLength)
        {
            return Problems.Validation(new Dictionary<string, string[]>
            {
                [IdempotencyKeyHeader] = [$"Send a unique {IdempotencyKeyHeader} header (1 to {Ritual.MaxIdempotencyKeyLength} characters) with each ritual, and reuse it when retrying."],
            });
        }

        var outcome = await rituals.PerformAsync(selection, idempotencyKey.Trim(), cancellationToken);
        switch (outcome)
        {
            case PerformOutcome.Performed performed:
                return TypedResults.Created($"/api/rituals/{performed.Result.RitualId}", performed.Result);

            case PerformOutcome.Replayed replayed:
                http.Response.Headers[ReplayedHeader] = "true";
                return TypedResults.Ok(replayed.Result);

            case PerformOutcome.PlayerNotFound:
                return Problems.NotFound("Player", selection.PlayerId);

            case PerformOutcome.Invalid invalid:
                return Problems.Validation(invalid.Issues
                    .GroupBy(i => i.Field)
                    .ToDictionary(g => g.Key, g => g.Select(i => i.Message).ToArray()));

            case PerformOutcome.NotOwned notOwned:
                return Problem(
                    StatusCodes.Status409Conflict,
                    "You don't have everything this ritual needs.",
                    string.Join(" ", notOwned.Issues.Select(i => i.Message)));

            case PerformOutcome.Conflict conflict:
                return Problem(StatusCodes.Status409Conflict, "The ritual lost a race for your components.", conflict.Detail);

            case PerformOutcome.KeyReused:
                return Problem(
                    StatusCodes.Status422UnprocessableEntity,
                    "This Idempotency-Key was already used for a different ritual.",
                    "Use a new key for a new ritual. Reuse a key only to retry the exact same request.");

            case PerformOutcome.GenerationFailed failed:
                return Problem(StatusCodes.Status422UnprocessableEntity, "The realm couldn't be generated.", failed.Detail);

            default:
                throw new InvalidOperationException($"Unhandled ritual outcome {outcome.GetType().Name}.");
        }
    }

    private static async Task<Results<Ok<RitualResultDto>, ProblemHttpResult>> GetAsync(Guid id, RitualService rituals, CancellationToken cancellationToken) =>
        await rituals.GetAsync(id, cancellationToken) is { } ritual
            ? TypedResults.Ok(ritual)
            : Problems.NotFound("Ritual", id);

    private static ProblemHttpResult Problem(int status, string title, string detail) =>
        TypedResults.Problem(statusCode: status, title: title, detail: detail);
}
