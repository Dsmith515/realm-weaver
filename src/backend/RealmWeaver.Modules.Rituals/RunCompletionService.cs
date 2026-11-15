using RealmWeaver.Infrastructure.Persistence;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Realms.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;

namespace RealmWeaver.Modules.Rituals;

/// <summary>
/// Completes a run and, when the weaver is a Cartographer, records its map into their inventory. This replaces the
/// automatic map grant a ritual used to make (Milestone 8). Both writes run in one transaction (ADR 0005), so a run is
/// never marked complete with its map lost, or the other way round.
/// </summary>
internal sealed class RunCompletionService(
    UnitOfWork unitOfWork,
    ICatalogModule catalog,
    IInventoryModule inventory,
    IRealmsModule realms)
{
    public const string GmRealm = "gm-realm";
    public const string NotCartographer = "not-cartographer";
    public const string AlreadyHeld = "already-held";
    public const string MapMissing = "map-missing";
    public const string UniqueHeld = "unique-held";

    public async Task<CompletionOutcome> CompleteAsync(Guid realmId, string objectiveKey, CancellationToken cancellationToken)
    {
        var summary = await realms.GetSummaryAsync(realmId, cancellationToken);
        if (summary is null)
        {
            return new CompletionOutcome.NotFound();
        }

        if (summary.Completion is not null)
        {
            return new CompletionOutcome.AlreadyCompleted();
        }

        // Decide up front whether the map gets recorded; everything below is reads only.
        var (map, reason) = await MapToRecordAsync(summary, cancellationToken);

        var result = await unitOfWork.ExecuteAsync(
            async ct =>
            {
                var completion = await realms.CompleteAsync(realmId, objectiveKey, mapRecorded: map is not null, ct);
                if (completion.Result == CompletionResult.Completed && map is not null)
                {
                    await inventory.GrantAsync(summary.PlayerId!.Value, map, 1, ct);
                }

                return completion;
            },
            cancellationToken);

        return result.Result switch
        {
            CompletionResult.Completed => new CompletionOutcome.Completed(new RunCompletionDto(
                realmId,
                result.Summary!.Completion!.Objective,
                result.Summary.Completion.CompletedAt,
                MapRecorded: map is not null,
                summary.MapComponentId,
                reason)),
            CompletionResult.UnknownObjective => new CompletionOutcome.UnknownObjective(),
            CompletionResult.AlreadyCompleted => new CompletionOutcome.AlreadyCompleted(),
            _ => new CompletionOutcome.NotFound(),
        };
    }

    /// <summary>The map to grant, or null and why not.</summary>
    private async Task<(ComponentDto? Map, string? Reason)> MapToRecordAsync(RealmSummaryDto summary, CancellationToken cancellationToken)
    {
        if (summary.PlayerId is not { } playerId)
        {
            return (null, GmRealm);
        }

        var player = await inventory.GetPlayerAsync(playerId, cancellationToken);
        if (player is not { IsCartographer: true })
        {
            return (null, NotCartographer);
        }

        var owned = await inventory.GetInventoryAsync(playerId, cancellationToken) ?? [];
        if (owned.Any(i => i.Component.Id == summary.MapComponentId))
        {
            return (null, AlreadyHeld);
        }

        var map = await catalog.GetComponentAsync(summary.MapComponentId, cancellationToken);
        if (map is null)
        {
            return (null, MapMissing);
        }

        return map.Map?.Flags is { Unique: true } && await inventory.IsHeldByOthersAsync(map.Id, playerId, cancellationToken)
            ? (null, UniqueHeld)
            : (map, null);
    }
}

internal abstract record CompletionOutcome
{
    public sealed record Completed(RunCompletionDto Result) : CompletionOutcome;

    public sealed record NotFound : CompletionOutcome;

    public sealed record UnknownObjective : CompletionOutcome;

    public sealed record AlreadyCompleted : CompletionOutcome;
}
