using RealmWeaver.Infrastructure.Persistence;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Realms.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;

namespace RealmWeaver.Modules.Rituals;

/// <summary>
/// The GM completes a run and hands its map to the chosen players (ADR 0010). Rituals never grant maps, so this is how
/// a map reaches an inventory besides a plain assign. The completion and every grant run in one transaction (ADR 0005),
/// so a run is never marked complete with a hand-out half done.
/// </summary>
internal sealed class RunCompletionService(
    UnitOfWork unitOfWork,
    ICatalogModule catalog,
    IInventoryModule inventory,
    IRealmsModule realms)
{
    public const string AlreadyHeld = "already-held";
    public const string UnknownPlayer = "unknown-player";
    public const string MapMissing = "map-missing";
    public const string UniqueHeld = "unique-held";

    public async Task<CompletionOutcome> CompleteAsync(Guid realmId, CompleteRunInput input, CancellationToken cancellationToken)
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

        var map = await catalog.GetComponentAsync(summary.MapComponentId, cancellationToken);
        var recipients = (input.RecipientPlayerIds ?? []).Distinct().ToList();
        if (map?.Map?.Flags is { Unique: true } && recipients.Count > 1)
        {
            return new CompletionOutcome.TooManyForUnique();
        }

        // Decide every recipient up front; everything below is reads only.
        var given = new List<Guid>();
        var skipped = new List<MapHandOutSkip>();
        foreach (var playerId in recipients)
        {
            var reason = await ReasonToSkipAsync(map, playerId, cancellationToken);
            if (reason is null)
            {
                given.Add(playerId);
            }
            else
            {
                skipped.Add(new MapHandOutSkip(playerId, reason));
            }
        }

        RealmCompletionResult result;
        try
        {
            result = await unitOfWork.ExecuteAsync(
                async ct =>
                {
                    var completion = await realms.CompleteAsync(realmId, input.ObjectiveKey, given, ct);
                    if (completion.Result == CompletionResult.Completed)
                    {
                        foreach (var playerId in given)
                        {
                            await inventory.GrantAsync(playerId, map!, 1, ct);
                        }
                    }

                    return completion;
                },
                cancellationToken);
        }
        catch (MapHeldElsewhereException)
        {
            // Someone else was handed this Unique map between the checks and the grant. Nothing was saved.
            return new CompletionOutcome.Conflict();
        }

        return result.Result switch
        {
            CompletionResult.Completed => new CompletionOutcome.Completed(new RunCompletionDto(
                realmId,
                result.Summary!.Completion!.Objective,
                result.Summary.Completion.CompletedAt,
                summary.MapComponentId,
                given,
                skipped)),
            CompletionResult.UnknownObjective => new CompletionOutcome.UnknownObjective(),
            CompletionResult.AlreadyCompleted => new CompletionOutcome.AlreadyCompleted(),
            _ => new CompletionOutcome.NotFound(),
        };
    }

    /// <summary>Null when the player can receive the map; otherwise why not.</summary>
    private async Task<string?> ReasonToSkipAsync(ComponentDto? map, Guid playerId, CancellationToken cancellationToken)
    {
        if (map is null)
        {
            return MapMissing;
        }

        var owned = await inventory.GetInventoryAsync(playerId, cancellationToken);
        if (owned is null)
        {
            return UnknownPlayer;
        }

        if (owned.Any(i => i.Component.Id == map.Id))
        {
            return AlreadyHeld;
        }

        return map.Map?.Flags is { Unique: true } && await inventory.IsHeldByOthersAsync(map.Id, playerId, cancellationToken)
            ? UniqueHeld
            : null;
    }
}

internal abstract record CompletionOutcome
{
    public sealed record Completed(RunCompletionDto Result) : CompletionOutcome;

    public sealed record NotFound : CompletionOutcome;

    public sealed record UnknownObjective : CompletionOutcome;

    public sealed record AlreadyCompleted : CompletionOutcome;

    /// <summary>A Unique map can go to one player only.</summary>
    public sealed record TooManyForUnique : CompletionOutcome;

    /// <summary>A Unique map was handed to someone else while this completion ran.</summary>
    public sealed record Conflict : CompletionOutcome;
}
