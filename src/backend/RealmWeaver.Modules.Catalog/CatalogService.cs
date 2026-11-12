using Microsoft.EntityFrameworkCore;

using RealmWeaver.Infrastructure.Http;
using RealmWeaver.Infrastructure.Persistence;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Catalog.Persistence;

namespace RealmWeaver.Modules.Catalog;

internal sealed class CatalogService(CatalogDbContext db, TimeProvider clock) : ICatalogModule
{
    public const int MaxPageSize = 200;

    public async Task<ComponentDto?> GetComponentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Components.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return entity?.ToDto();
    }

    public async Task<IReadOnlyDictionary<Guid, ComponentDto>> GetComponentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, ComponentDto>();
        }

        var distinct = ids.Distinct().ToList();
        var entities = await db.Components.AsNoTracking().Where(c => distinct.Contains(c.Id)).ToListAsync(cancellationToken);
        return entities.ToDictionary(c => c.Id, c => c.ToDto());
    }

    public async Task<CatalogSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return new CatalogSettingsDto(settings?.PlayersCanSelfAssign ?? CatalogSeedData.DefaultPlayersCanSelfAssign);
    }

    public async Task<StarterKitCandidates> GetStarterKitCandidatesAsync(CancellationToken cancellationToken = default)
    {
        var starters = await db.Components.AsNoTracking()
            .Where(c => !c.IsArchived && c.Rarity == Rarity.Common && c.IsPlayerAssignable
                        && (c.Type == ComponentType.Thread || c.Type == ComponentType.Coating))
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Type })
            .ToListAsync(cancellationToken);

        return new StarterKitCandidates(
            CatalogSeedData.BeginnerNeedleId,
            [.. starters.Where(s => s.Type == ComponentType.Thread).Select(s => s.Id)],
            [.. starters.Where(s => s.Type == ComponentType.Coating).Select(s => s.Id)]);
    }

    public async Task<ComponentDto> CreateMapAsync(NewMap map, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        var name = map.Name.Length <= ComponentRules.MaxNameLength ? map.Name : map.Name[..ComponentRules.MaxNameLength];
        var description = map.Description.Length <= ComponentRules.MaxDescriptionLength ? map.Description : map.Description[..ComponentRules.MaxDescriptionLength];
        var input = new ComponentInput(ComponentType.Map, name, Rarity.Common, ComponentRules.MinRank, description, IsPlayerAssignable: false, Map: map.Details);

        var errors = ComponentRules.Validate(input);
        if (!errors.IsValid)
        {
            throw new ArgumentException($"Invalid map: {string.Join("; ", errors.ToDictionary().Select(e => $"{e.Key}: {string.Join(' ', e.Value)}"))}", nameof(map));
        }

        return await CreateAsync(input, map.Id, cancellationToken);
    }

    public async Task<PagedResult<ComponentDto>> SearchAsync(ComponentQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var components = db.Components.AsNoTracking();
        if (!query.IncludeArchived)
        {
            components = components.Where(c => !c.IsArchived);
        }

        if (query.Type is { } type)
        {
            components = components.Where(c => c.Type == type);
        }

        if (query.Rarity is { } rarity)
        {
            components = components.Where(c => c.Rarity == rarity);
        }

        if (query.Biome is { } biome)
        {
            components = components.Where(c => c.Thread != null && c.Thread.Biome == biome);
        }

        if (query.Assignable is { } assignable)
        {
            components = components.Where(c => c.IsPlayerAssignable == assignable);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            components = components.Where(c => EF.Functions.ILike(c.Name, pattern) || EF.Functions.ILike(c.Description, pattern));
        }

        var total = await components.CountAsync(cancellationToken);
        var items = await components
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ComponentDto>([.. items.Select(c => c.ToDto())], page, pageSize, total);
    }

    /// <summary>A Map must point at an existing Needle, Thread and Coating.</summary>
    public async Task ValidateReferencesAsync(ComponentInput input, ValidationErrors errors, CancellationToken cancellationToken)
    {
        if (input.Type != ComponentType.Map || input.Map is not { } map)
        {
            return;
        }

        var referenced = await GetComponentsAsync([map.NeedleId, map.ThreadId, map.CoatingId], cancellationToken);
        Check(map.NeedleId, ComponentType.Needle, "map.needleId");
        Check(map.ThreadId, ComponentType.Thread, "map.threadId");
        Check(map.CoatingId, ComponentType.Coating, "map.coatingId");

        void Check(Guid id, ComponentType expected, string field) =>
            errors.Require(referenced.TryGetValue(id, out var c) && c.Type == expected, field, $"No {expected} has id '{id}'.");
    }

    public async Task<ComponentDto> CreateAsync(ComponentInput input, Guid? id, CancellationToken cancellationToken)
    {
        var entity = new ComponentDefinition
        {
            Id = id ?? Guid.CreateVersion7(),
            Type = input.Type,
            Name = input.Name,
            CreatedAt = clock.GetDatabaseUtcNow(),
        };
        ComponentRules.Apply(input, entity);

        db.Components.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    /// <summary>Returns null when the component doesn't exist. The caller has already checked the type is unchanged.</summary>
    public async Task<ComponentDto?> UpdateAsync(Guid id, ComponentInput input, CancellationToken cancellationToken)
    {
        var entity = await db.Components.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        ComponentRules.Apply(input, entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    /// <summary>Archives instead of deleting: players may still own the component, and Inventory can't see our tables.</summary>
    public async Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Components.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.IsArchived = true;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CatalogSettingsDto> UpdateSettingsAsync(CatalogSettingsDto input, CancellationToken cancellationToken)
    {
        var settings = await db.Settings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new CatalogSettings();
            db.Settings.Add(settings);
        }

        settings.PlayersCanSelfAssign = input.PlayersCanSelfAssign;
        await db.SaveChangesAsync(cancellationToken);
        return new CatalogSettingsDto(settings.PlayersCanSelfAssign);
    }

    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
