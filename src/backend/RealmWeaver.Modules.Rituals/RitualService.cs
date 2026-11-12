using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using RealmWeaver.Generation;
using RealmWeaver.Generation.Model;
using RealmWeaver.Infrastructure.Persistence;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Realms.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;
using RealmWeaver.Modules.Rituals.Persistence;

namespace RealmWeaver.Modules.Rituals;

/// <summary>
/// Performs a ritual as one local ACID transaction (ADR 0005):
/// claim the idempotency key, spend the Thread and Coating, create the Map if new, grant it, and store the realm.
/// Any failure rolls all of it back.
/// </summary>
internal sealed class RitualService(
    RitualsDbContext db,
    UnitOfWork unitOfWork,
    RitualResolver resolver,
    ICatalogModule catalog,
    IInventoryModule inventory,
    IRealmsModule realms,
    TimeProvider clock)
{
    public async Task<RitualResultDto?> GetAsync(Guid ritualId, CancellationToken cancellationToken)
    {
        var ritual = await db.Rituals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == ritualId, cancellationToken);
        return ritual is null ? null : ToResult(ritual);
    }

    public async Task<PerformOutcome> PerformAsync(RitualSelection selection, string idempotencyKey, CancellationToken cancellationToken)
    {
        var requestHash = Hash(selection);
        if (await ReplayAsync(idempotencyKey, requestHash, cancellationToken) is { } earlier)
        {
            return earlier;
        }

        var resolution = await resolver.ResolveAsync(selection, randomize: false, cancellationToken);
        if (resolution is null)
        {
            return new PerformOutcome.PlayerNotFound();
        }

        var preview = resolution.Preview;
        if (preview.Issues.Any(i => i.Code != RitualResolver.NotOwned))
        {
            return new PerformOutcome.Invalid(preview.Issues);
        }

        if (!preview.CanPerform)
        {
            return new PerformOutcome.NotOwned(preview.Issues);
        }

        // Everything up to here only read. Generation is pure, so it also runs before the transaction opens,
        // which keeps the row locks short.
        var needle = preview.Needle!.Component;
        var thread = preview.Thread!.Component;
        var coating = preview.Coating!.Component;
        var existingMap = preview.Map?.Component;
        var variant = RitualResolver.EncounterVariantFor(preview.IsMismatch, needle.Id, thread.Id, coating.Id);
        var request = BuildRequest(selection, resolution, existingMap?.Map, needle, thread, coating, preview.DangerModifier, variant);

        RealmDocument document;
        try
        {
            document = RealmGenerator.Generate(request);
        }
        catch (RealmGenerationException ex)
        {
            return new PerformOutcome.GenerationFailed(ex.Message);
        }

        var ritual = new Ritual
        {
            Id = Guid.CreateVersion7(),
            PlayerId = selection.PlayerId,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            NeedleId = needle.Id,
            ThreadId = thread.Id,
            CoatingId = coating.Id,
            MapId = existingMap?.Id ?? Guid.CreateVersion7(),
            MapCreated = existingMap is null,
            RealmId = Guid.CreateVersion7(),
            IsMismatch = preview.IsMismatch,
            DangerModifier = preview.DangerModifier,
            EncounterVariant = variant,
            Status = RitualStatus.Completed,
            CreatedAt = clock.GetDatabaseUtcNow(),
        };

        try
        {
            var result = await unitOfWork.ExecuteAsync(
                async ct =>
                {
                    // 1. Claim the key first. A concurrent request with the same key blocks here until we commit,
                    //    then fails on the unique index and replays our result instead of running twice.
                    db.Rituals.Add(ritual);
                    await db.SaveChangesAsync(ct);

                    // 2. Spend one Thread and one Coating, guarded by xmin.
                    await inventory.SpendOneEachAsync(selection.PlayerId, [thread.Id, coating.Id], ct);

                    // 3. Reuse the map, or record a new one in the catalog.
                    var map = existingMap ?? await catalog.CreateMapAsync(NewMapFor(ritual.MapId, document, needle, thread, coating, selection.PlayerId), ct);

                    // 4. The player keeps the map (a no-op when they already own it).
                    await inventory.GrantAsync(selection.PlayerId, map, 1, ct);

                    // 5. Store the realm snapshot.
                    await realms.SaveAsync(
                        new NewRealm(ritual.RealmId, selection.PlayerId, map.Id, ritual.DangerModifier, ritual.EncounterVariant, document), ct);

                    return ToResult(ritual);
                },
                cancellationToken);

            return new PerformOutcome.Performed(result);
        }
        catch (DbUpdateException ex) when (IsIdempotencyKeyClash(ex))
        {
            return await ReplayAsync(idempotencyKey, requestHash, cancellationToken)
                ?? throw new InvalidOperationException("The idempotency key clashed but no ritual holds it.", ex);
        }
        catch (InsufficientComponentsException)
        {
            return new PerformOutcome.Conflict("Another ritual used these components first. Reload your inventory and try again.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return new PerformOutcome.Conflict("Your inventory changed while the ritual ran. Reload it and try again.");
        }
    }

    private async Task<PerformOutcome?> ReplayAsync(string idempotencyKey, string requestHash, CancellationToken cancellationToken)
    {
        var earlier = await db.Rituals.AsNoTracking().FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, cancellationToken);
        if (earlier is null)
        {
            return null;
        }

        return earlier.RequestHash == requestHash
            ? new PerformOutcome.Replayed(ToResult(earlier))
            : new PerformOutcome.KeyReused();
    }

    private static bool IsIdempotencyKeyClash(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        && pg.ConstraintName == RitualsDbContext.IdempotencyKeyIndex;

    private static GenerationRequest BuildRequest(
        RitualSelection selection,
        RitualResolution resolution,
        MapDetails? map,
        ComponentDto needle,
        ComponentDto thread,
        ComponentDto coating,
        int dangerModifier,
        int variant)
    {
        // Layout (shape, size, biome) comes from the map's own Thread and Coating; the encounters come
        // from what's in the slots. A matching ritual therefore rebuilds the map's realm exactly.
        var layoutThread = resolution.LayoutThread?.Thread ?? throw new InvalidOperationException("The realm has no layout thread.");
        var layoutCoating = resolution.LayoutCoating?.Coating ?? throw new InvalidOperationException("The realm has no layout coating.");

        return new GenerationRequest
        {
            Seed = map?.Seed ?? NewSeed(),
            Kind = layoutCoating.RealmKind,
            FloorCount = Math.Max(1, layoutCoating.FloorCount),
            Biome = layoutThread.Biome,
            Element = thread.Thread!.Element,
            MobTags = coating.Coating!.MobTypes,
            Difficulty = needle.Needle!.DifficultyTier,
            DangerModifier = dangerModifier,
            EncounterVariant = variant,
            Width = map?.Width ?? selection.Width,
            Height = map?.Height ?? selection.Height,
            EventCount = map is null ? selection.EventCount : (layoutCoating.RealmKind == RealmKind.Dungeon ? null : map.EventCount),
        };
    }

    private static NewMap NewMapFor(Guid id, RealmDocument document, ComponentDto needle, ComponentDto thread, ComponentDto coating, Guid playerId)
    {
        var first = document.Areas[0];
        var kind = document.Request.Kind switch
        {
            RealmKind.Field => "field",
            RealmKind.Dungeon => "dungeon",
            _ => "field and dungeon",
        };
        var seedTag = document.Seed.ToString("X16", CultureInfo.InvariantCulture)[..6];

        return new NewMap(
            id,
            $"{document.Request.Biome} {kind} map {seedTag}",
            $"Opened with {needle.Name}, {thread.Name} and {coating.Name}.",
            new MapDetails(
                document.Seed,
                needle.Id,
                thread.Id,
                coating.Id,
                first.Width,
                first.Height,
                Math.Clamp(first.Events.Count, GeneratorLimits.MinEvents, GeneratorLimits.AbsoluteMaxEvents),
                document.GeneratorVersion,
                playerId));
    }

    private static ulong NewSeed() => BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong)));

    private static string Hash(RitualSelection selection) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(selection)))).ToLowerInvariant();

    private static RitualResultDto ToResult(Ritual r) => new(
        r.Id, r.PlayerId, r.RealmId, r.MapId, r.MapCreated, r.NeedleId, r.ThreadId, r.CoatingId,
        r.IsMismatch, r.DangerModifier, r.EncounterVariant, r.CreatedAt);
}

internal abstract record PerformOutcome
{
    public sealed record Performed(RitualResultDto Result) : PerformOutcome;

    public sealed record Replayed(RitualResultDto Result) : PerformOutcome;

    public sealed record PlayerNotFound : PerformOutcome;

    public sealed record Invalid(IReadOnlyList<RitualIssue> Issues) : PerformOutcome;

    public sealed record NotOwned(IReadOnlyList<RitualIssue> Issues) : PerformOutcome;

    public sealed record Conflict(string Detail) : PerformOutcome;

    public sealed record KeyReused : PerformOutcome;

    public sealed record GenerationFailed(string Detail) : PerformOutcome;
}
