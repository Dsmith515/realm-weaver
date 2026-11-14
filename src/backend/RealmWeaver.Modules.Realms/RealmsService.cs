using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using RealmWeaver.Generation.Export;
using RealmWeaver.Generation.Model;
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

    /// <summary>Newest first. Summaries only; the documents can be large.</summary>
    public async Task<IReadOnlyList<RealmSummaryDto>> ListAsync(Guid? playerId, Guid? mapComponentId, int limit, CancellationToken cancellationToken)
    {
        var realms = db.Realms.AsNoTracking();
        if (playerId is { } player)
        {
            realms = realms.Where(r => r.PlayerId == player);
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
        ComponentsOf(entity));

    private static RealmComponentsDto? ComponentsOf(Realm entity) =>
        entity is { MapName: { } mapName, NeedleId: { } needleId, ThreadId: { } threadId, CoatingId: { } coatingId }
            ? new RealmComponentsDto(
                mapName,
                new ComponentRefDto(needleId, entity.NeedleName ?? string.Empty),
                new ComponentRefDto(threadId, entity.ThreadName ?? string.Empty),
                new ComponentRefDto(coatingId, entity.CoatingName ?? string.Empty))
            : null;
}
