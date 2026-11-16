using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Export;
using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Objectives;
using RealmWeaver.Infrastructure.Persistence;
using RealmWeaver.Modules.Realms.Contracts;
using RealmWeaver.Modules.Realms.Persistence;

namespace RealmWeaver.Modules.Realms;

internal sealed class RealmsService(RealmsDbContext db, TimeProvider clock) : IRealmsModule
{
    public const int MaxListSize = 100;

    public async Task<RealmSummaryDto> SaveAsync(NewRealm realm, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(realm);
        var document = realm.Document;
        var entity = new Realm
        {
            Id = realm.Id,
            PlayerId = realm.PlayerId,
            MapComponentId = realm.MapComponentId,
            Kind = document.Request.Kind,
            Biome = document.Request.Biome,
            Width = document.Areas[0].Width,
            Height = document.Areas[0].Height,
            FloorCount = document.Areas.Count(a => a.Kind == AreaKind.DungeonFloor),
            Threat = document.Threat,
            DangerModifier = realm.DangerModifier,
            EncounterVariant = realm.EncounterVariant,
            Seed = document.Seed,
            GeneratorVersion = document.GeneratorVersion,
            Document = RealmJson.Serialize(document),
            Areas = JsonSerializer.Serialize(AreasOf(document), RealmJson.Options),
            CreatedAt = clock.GetDatabaseUtcNow(),
            MapName = realm.Components?.MapName,
            NeedleId = realm.Components?.Needle.Id,
            NeedleName = realm.Components?.Needle.Name,
            ThreadId = realm.Components?.Thread.Id,
            ThreadName = realm.Components?.Thread.Name,
            CoatingId = realm.Components?.Coating.Id,
            CoatingName = realm.Components?.Coating.Name,
            AttunedNeedle = realm.Components?.AttunedNeedle is { } attuned ? JsonSerializer.Serialize(attuned, RealmJson.Options) : null,
        };

        db.Realms.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(entity);
    }

    public async Task<RealmSummaryDto?> GetSummaryAsync(Guid realmId, CancellationToken cancellationToken = default) =>
        (await GetAsync(realmId, cancellationToken))?.Summary;

    public async Task<RealmDto?> GetAsync(Guid realmId, CancellationToken cancellationToken)
    {
        var entity = await db.Realms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == realmId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return new RealmDto(ToSummary(entity), RealmJson.Deserialize(entity.Document));
    }

    public async Task<RealmDigestDto?> GetOriginalDigestAsync(Guid mapComponentId, CancellationToken cancellationToken = default)
    {
        var original = await db.Realms.AsNoTracking()
            .Where(r => r.MapComponentId == mapComponentId && r.EncounterVariant == 0)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Select(r => new { r.Id, r.Threat, r.Document })
            .FirstOrDefaultAsync(cancellationToken);

        return original is null
            ? null
            : new RealmDigestDto(original.Id, original.Threat, RealmDigest.From(RealmJson.Deserialize(original.Document)));
    }

    /// <summary>
    /// Every objective the realm could offer: the rolled ones first, then the other hooks of its placed events and the
    /// realm-wide objectives. Null when the realm doesn't exist.
    /// </summary>
    public async Task<IReadOnlyList<RealmObjective>?> GetObjectiveCandidatesAsync(Guid realmId, CancellationToken cancellationToken)
    {
        var json = await db.Realms.AsNoTracking().Where(r => r.Id == realmId).Select(r => r.Document).FirstOrDefaultAsync(cancellationToken);
        return json is null ? null : CandidatesOf(RealmJson.Deserialize(json));
    }

    /// <summary>Pins one of the realm's candidate objectives, or clears the pin when <paramref name="key"/> is null.</summary>
    public async Task<PinOutcome> PinObjectiveAsync(Guid realmId, string? key, CancellationToken cancellationToken)
    {
        var entity = await db.Realms.FirstOrDefaultAsync(r => r.Id == realmId, cancellationToken);
        if (entity is null)
        {
            return new PinOutcome.NotFound();
        }

        if (key is null)
        {
            entity.PinnedObjective = null;
        }
        else
        {
            var candidate = CandidatesOf(RealmJson.Deserialize(entity.Document)).FirstOrDefault(o => o.Key == key);
            if (candidate is null)
            {
                return new PinOutcome.UnknownObjective();
            }

            entity.PinnedObjective = JsonSerializer.Serialize(candidate, RealmJson.Options);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new PinOutcome.Pinned(ToSummary(entity));
    }

    /// <summary>Rolled objectives keep their ids (O1..O3); the rest are marked "P" because they only appear when pinned.</summary>
    private static List<RealmObjective> CandidatesOf(RealmDocument document)
    {
        var rolled = document.Objectives;
        var others = ObjectivePicker.Candidates(document.Areas, StaticContentCatalog.Default)
            .Where(c => rolled.All(o => o.Key != c.Key))
            .Select(c => c with { Id = "P" });
        return [.. rolled, .. others];
    }

    /// <summary>Newest first. Summaries only; the documents can be large.</summary>
    public async Task<IReadOnlyList<RealmSummaryDto>> ListAsync(Guid? playerId, Guid? mapComponentId, int limit, CancellationToken cancellationToken, bool gmOnly = false)
    {
        var realms = db.Realms.AsNoTracking();
        if (playerId is { } player)
        {
            realms = realms.Where(r => r.PlayerId == player);
        }

        if (gmOnly)
        {
            realms = realms.Where(r => r.PlayerId == null);
        }

        if (mapComponentId is { } map)
        {
            realms = realms.Where(r => r.MapComponentId == map);
        }

        var entities = await realms
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Take(Math.Clamp(limit, 1, MaxListSize))
            .Select(r => new Realm
            {
                Id = r.Id,
                PlayerId = r.PlayerId,
                MapComponentId = r.MapComponentId,
                Kind = r.Kind,
                Biome = r.Biome,
                Width = r.Width,
                Height = r.Height,
                FloorCount = r.FloorCount,
                Threat = r.Threat,
                DangerModifier = r.DangerModifier,
                EncounterVariant = r.EncounterVariant,
                Seed = r.Seed,
                GeneratorVersion = r.GeneratorVersion,
                Document = string.Empty, // not loaded
                Areas = r.Areas,
                CreatedAt = r.CreatedAt,
                MapName = r.MapName,
                NeedleId = r.NeedleId,
                NeedleName = r.NeedleName,
                ThreadId = r.ThreadId,
                ThreadName = r.ThreadName,
                CoatingId = r.CoatingId,
                CoatingName = r.CoatingName,
                PinnedObjective = r.PinnedObjective,
                AttunedNeedle = r.AttunedNeedle,
            })
            .ToListAsync(cancellationToken);

        return [.. entities.Select(ToSummary)];
    }

    private static List<RealmAreaSummaryDto> AreasOf(RealmDocument document) =>
        [.. document.Areas.Select(a => new RealmAreaSummaryDto(a.Id, a.Kind, a.Name, a.FloorNumber))];

    private static RealmSummaryDto ToSummary(Realm entity) => new(
        entity.Id,
        entity.PlayerId,
        entity.MapComponentId,
        entity.Kind,
        entity.Biome,
        entity.Width,
        entity.Height,
        entity.FloorCount,
        entity.Threat,
        entity.DangerModifier,
        entity.EncounterVariant,
        entity.Seed,
        entity.GeneratorVersion,
        entity.CreatedAt,
        JsonSerializer.Deserialize<List<RealmAreaSummaryDto>>(entity.Areas, RealmJson.Options) ?? [],
        ComponentsOf(entity),
        entity.PinnedObjective is null ? null : JsonSerializer.Deserialize<RealmObjective>(entity.PinnedObjective, RealmJson.Options));

    private static RealmComponentsDto? ComponentsOf(Realm entity) =>
        entity is { MapName: { } mapName, NeedleId: { } needleId, ThreadId: { } threadId, CoatingId: { } coatingId }
            ? new RealmComponentsDto(
                mapName,
                new ComponentRefDto(needleId, entity.NeedleName ?? string.Empty),
                new ComponentRefDto(threadId, entity.ThreadName ?? string.Empty),
                new ComponentRefDto(coatingId, entity.CoatingName ?? string.Empty),
                entity.AttunedNeedle is null ? null : JsonSerializer.Deserialize<AttunedNeedleDto>(entity.AttunedNeedle, RealmJson.Options))
            : null;
}

internal abstract record PinOutcome
{
    public sealed record Pinned(RealmSummaryDto Summary) : PinOutcome;

    public sealed record NotFound : PinOutcome;

    public sealed record UnknownObjective : PinOutcome;
}
