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
        return ToSummary(entity, await CompletionCountAsync(entity.MapComponentId, cancellationToken));
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

        return new RealmDto(ToSummary(entity, await CompletionCountAsync(entity.MapComponentId, cancellationToken)), RealmJson.Deserialize(entity.Document));
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

    public async Task<RealmBriefingDto?> GetBriefingAsync(Guid realmId, bool holdsMap, CancellationToken cancellationToken = default)
    {
        if (await GetAsync(realmId, cancellationToken) is not { Summary: var summary, Document: var document })
        {
            return null;
        }

        var pin = summary.PinnedObjective;
        var objectives = document.Objectives
            .Select(o => new BriefingObjectiveDto(o.Id, o.Title, o.Description, o.Key == pin?.Key))
            .ToList();
        if (pin is not null && objectives.All(o => !o.IsPinned))
        {
            objectives.Insert(0, new BriefingObjectiveDto("P", pin.Title, pin.Description, IsPinned: true));
        }

        var components = summary.Components;
        return new RealmBriefingDto(
            summary.Id,
            components?.MapName,
            summary.Kind,
            summary.Biome,
            document.Request.Element,
            summary.Threat,
            summary.DangerModifier,
            summary.FloorCount,
            summary.IsMirror,
            components is null ? [] : [components.Needle.Name, components.Thread.Name, components.Coating.Name],
            summary.CompletionCount,
            objectives,
            holdsMap,
            holdsMap ? FindsOf(RealmDigest.From(document)) : null,
            holdsMap ? summary.Areas : null);
    }

    /// <summary>Names only, each once, in the digest's order: a player learns what kinds of things are there, not how many or where.</summary>
    private static BriefingFindsDto FindsOf(RealmDigest digest) => new(
        [.. digest.Mobs.Select(m => m.Name).Distinct(StringComparer.Ordinal)],
        [.. digest.Loot.Select(e => e.Name).Distinct(StringComparer.Ordinal)],
        [.. digest.ResourceNodes.Select(e => e.Name).Distinct(StringComparer.Ordinal)]);

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
        return new PinOutcome.Pinned(ToSummary(entity, await CompletionCountAsync(entity.MapComponentId, cancellationToken)));
    }

    public async Task<RealmCompletionResult> CompleteAsync(
        Guid realmId, string objectiveKey, IReadOnlyList<CompletionPlayerDto> players, CancellationToken cancellationToken = default)
    {
        var entity = await db.Realms.AsNoTracking()
            .Where(r => r.Id == realmId)
            .Select(r => new { r.MapComponentId, r.PinnedObjective, r.Document })
            .FirstOrDefaultAsync(cancellationToken);
        if (entity is null)
        {
            return new RealmCompletionResult(CompletionResult.NotFound);
        }

        // Any rolled objective counts, and so does the GM's pin.
        var pinned = entity.PinnedObjective is null ? null : JsonSerializer.Deserialize<RealmObjective>(entity.PinnedObjective, RealmJson.Options);
        var objective = RealmJson.Deserialize(entity.Document).Objectives.Append(pinned).FirstOrDefault(o => o?.Key == objectiveKey);
        if (objective is null)
        {
            return new RealmCompletionResult(CompletionResult.UnknownObjective);
        }

        var completion = new Completion
        {
            Id = Guid.CreateVersion7(),
            RealmId = realmId,
            MapComponentId = entity.MapComponentId,
            Objective = JsonSerializer.Serialize(objective, RealmJson.Options),
            Players = JsonSerializer.Serialize(players, RealmJson.Options),
            CompletedAt = clock.GetDatabaseUtcNow(),
        };
        db.Completions.Add(completion);
        await db.SaveChangesAsync(cancellationToken);

        return new RealmCompletionResult(CompletionResult.Completed, ToDto(completion, await IsFirstAsync(completion, cancellationToken)));
    }

    /// <summary>The completion history of the realm's map, newest first, with everyone who completed something. Null when the realm doesn't exist.</summary>
    public async Task<RealmCompletionsDto?> GetCompletionsAsync(Guid realmId, CancellationToken cancellationToken = default)
    {
        var mapId = await db.Realms.Where(r => r.Id == realmId).Select(r => (Guid?)r.MapComponentId).FirstOrDefaultAsync(cancellationToken);
        if (mapId is null)
        {
            return null;
        }

        var entities = await db.Completions.AsNoTracking()
            .Where(c => c.MapComponentId == mapId)
            .OrderBy(c => c.CompletedAt)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);
        var completions = entities.Select((c, i) => ToDto(c, isFirst: i == 0)).ToList();

        var players = completions
            .SelectMany(c => c.Players.Select(p => (Player: p, Completion: c)))
            .GroupBy(x => x.Player.Id)
            .Select(g => new CompleterDto(
                g.Key,
                g.Last().Player.Name, // the newest name
                g.Count(),
                g.First().Completion.CompletedAt,
                g.Any(x => x.Completion.IsFirst)))
            .OrderBy(p => p.FirstCompletedAt)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        completions.Reverse();
        return new RealmCompletionsDto(completions, players);
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
    public async Task<RealmSummaryPage> ListAsync(
        Guid? playerId, Guid? mapComponentId, int page, int pageSize, CancellationToken cancellationToken, bool gmOnly = false)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxListSize);
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

        var total = await realms.CountAsync(cancellationToken);
        var rows = await realms
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                Realm = new Realm
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
                },
                Completions = db.Completions.Count(c => c.MapComponentId == r.MapComponentId),
            })
            .ToListAsync(cancellationToken);

        return new RealmSummaryPage([.. rows.Select(r => ToSummary(r.Realm, r.Completions))], page, pageSize, total);
    }

    private Task<int> CompletionCountAsync(Guid mapComponentId, CancellationToken cancellationToken) =>
        db.Completions.CountAsync(c => c.MapComponentId == mapComponentId, cancellationToken);

    /// <summary>The earliest completion of the map is the first. Two in the same instant: the lower (time-ordered v7) id wins.</summary>
    private async Task<bool> IsFirstAsync(Completion completion, CancellationToken cancellationToken)
    {
        var first = await db.Completions.AsNoTracking()
            .Where(c => c.MapComponentId == completion.MapComponentId)
            .OrderBy(c => c.CompletedAt)
            .ThenBy(c => c.Id)
            .Select(c => c.Id)
            .FirstAsync(cancellationToken);
        return first == completion.Id;
    }

    private static RealmCompletionDto ToDto(Completion c, bool isFirst) => new(
        c.Id,
        c.RealmId,
        JsonSerializer.Deserialize<RealmObjective>(c.Objective, RealmJson.Options)!,
        c.CompletedAt,
        JsonSerializer.Deserialize<List<CompletionPlayerDto>>(c.Players, RealmJson.Options) ?? [],
        isFirst);

    private static List<RealmAreaSummaryDto> AreasOf(RealmDocument document) =>
        [.. document.Areas.Select(a => new RealmAreaSummaryDto(a.Id, a.Kind, a.Name, a.FloorNumber))];

    private static RealmSummaryDto ToSummary(Realm entity, int completionCount) => new(
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
        entity.PinnedObjective is null ? null : JsonSerializer.Deserialize<RealmObjective>(entity.PinnedObjective, RealmJson.Options),
        completionCount);

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
