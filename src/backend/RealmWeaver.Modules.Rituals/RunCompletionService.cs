using RealmWeaver.Infrastructure.Persistence;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Realms.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;

namespace RealmWeaver.Modules.Rituals;

/// <summary>
/// The GM records that a party completed an objective and hands the realm's map to the players who should keep it
/// (ADR 0010, ADR 0011). Repeatable: every map can be visited again, so any party can complete it. Rituals never grant
/// maps, so this is how a map reaches an inventory besides a plain assign. The completion and every grant run in one
/// transaction (ADR 0005), so a completion is never recorded with a hand-out half done.
/// </summary>
internal sealed class RunCompletionService(
    UnitOfWork unitOfWork,
    ICatalogModule catalog,
    IInventoryModule inventory,
    IRealmsModule realms)
{
    public const string AlreadyHeld = "already-held";
    public const string MapMissing = "map-missing";
    public const string UniqueHeld = "unique-held";

    /// <param name="input">Validated: an objective key, at least one distinct player, and recipients among them.</param>
    public async Task<CompletionOutcome> CompleteAsync(Guid realmId, CompleteRunInput input, CancellationToken cancellationToken)
    {
        var summary = await realms.GetSummaryAsync(realmId, cancellationToken);
        if (summary is null)
        {
            return new CompletionOutcome.NotFound();
        }

        // Names are copied onto the completion (ADR 0006), so the history reads without calling Inventory.
        var party = new List<PlayerDto>();
        var unknown = new List<Guid>();
        foreach (var playerId in input.PlayerIds)
        {
            if (await inventory.GetPlayerAsync(playerId, cancellationToken) is { } player)
            {
                party.Add(player);
            }
            else
            {
                unknown.Add(playerId);
            }
        }

        if (unknown.Count > 0)
        {
            return new CompletionOutcome.UnknownPlayers(unknown);
        }

        var map = await catalog.GetComponentAsync(summary.MapComponentId, cancellationToken);
        var held = new HashSet<Guid>();
        foreach (var player in party)
        {
            if ((await inventory.GetInventoryAsync(player.Id, cancellationToken))?.Any(i => i.Component.Id == summary.MapComponentId) == true)
            {
                held.Add(player.Id);
            }
        }

        // Without an explicit list, everyone who doesn't hold the map yet receives it.
        var recipients = input.MapRecipientIds ?? [.. party.Select(p => p.Id).Where(id => !held.Contains(id))];
        var unique = map?.Map?.Flags is { Unique: true };
        if (unique && recipients.Count(id => !held.Contains(id)) > 1)
        {
            return new CompletionOutcome.TooManyForUnique();
        }

        // Decide the recipients up front from reads. A concurrent completion can still hand the map to one of them
        // first, so the grants below have the final say on who received it.
        var given = new List<Guid>();
        var skipped = new List<MapHandOutSkip>();
        foreach (var playerId in recipients)
        {
            var reason = map is null ? MapMissing
                : held.Contains(playerId) ? AlreadyHeld
                : unique && await inventory.IsHeldByOthersAsync(map.Id, playerId, cancellationToken) ? UniqueHeld
                : null;
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
        var received = new List<Guid>();
        try
        {
            result = await unitOfWork.ExecuteAsync(
                async ct =>
                {
                    // Grant first, so the completion records only the players this transaction actually gave the map
                    // to. If another completion granted it first, the upsert changes nothing and reports that.
                    foreach (var playerId in given)
                    {
                        if ((await inventory.GrantAsync(playerId, map!, 1, ct)).Added)
                        {
                            received.Add(playerId);
                        }
                    }

                    var players = party.Select(p => new CompletionPlayerDto(p.Id, p.Name, received.Contains(p.Id))).ToList();
                    var completion = await realms.CompleteAsync(realmId, input.ObjectiveKey, players, ct);

                    // Nothing was recorded (unknown objective, or the realm is gone), so undo the grants too.
                    return completion.Result == CompletionResult.Completed ? completion : throw new CompletionNotRecordedException(completion);
                },
                cancellationToken);
        }
        catch (MapHeldElsewhereException)
        {
            // Someone else was handed this Unique map between the checks and the grant. Nothing was saved.
            return new CompletionOutcome.Conflict();
        }
        catch (CompletionNotRecordedException ex)
        {
            result = ex.Result;
        }

        // A recipient who received the map from a concurrent completion is reported as already holding it.
        skipped.AddRange(given.Except(received).Select(id => new MapHandOutSkip(id, AlreadyHeld)));

        return result switch
        {
            { Result: CompletionResult.Completed, Completion: { } done } => new CompletionOutcome.Completed(new RunCompletionDto(
                done.Id,
                realmId,
                done.Objective,
                done.CompletedAt,
                done.IsFirst,
                summary.MapComponentId,
                [.. party.Select(p => p.Id)],
                received,
                skipped)),
            { Result: CompletionResult.UnknownObjective } => new CompletionOutcome.UnknownObjective(),
            _ => new CompletionOutcome.NotFound(),
        };
    }
}

/// <summary>Rolls back the grants when Realms didn't record the completion. Never leaves <see cref="RunCompletionService"/>.</summary>
internal sealed class CompletionNotRecordedException(RealmCompletionResult result) : Exception("The completion wasn't recorded.")
{
    public RealmCompletionResult Result { get; } = result;
}

internal abstract record CompletionOutcome
{
    public sealed record Completed(RunCompletionDto Result) : CompletionOutcome;

    public sealed record NotFound : CompletionOutcome;

    public sealed record UnknownObjective : CompletionOutcome;

    public sealed record UnknownPlayers(IReadOnlyList<Guid> Ids) : CompletionOutcome;

    /// <summary>A Unique map can go to one player only.</summary>
    public sealed record TooManyForUnique : CompletionOutcome;

    /// <summary>A Unique map was handed to someone else while this completion ran.</summary>
    public sealed record Conflict : CompletionOutcome;
}
