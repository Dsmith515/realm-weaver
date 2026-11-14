using RealmWeaver.Generation;
using RealmWeaver.Generation.Model;
using RealmWeaver.Infrastructure.Persistence;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Realms.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;

namespace RealmWeaver.Modules.Rituals;

/// <summary>
/// GM Quick Weave: generate a realm from anything in the catalog, for a one-shot. Nothing is owned or spent, so
/// there's no inventory step; the map (when new) and the realm are written in one unit of work, both unowned.
/// The admin can hand the map to a player afterwards with the normal Assign action.
/// </summary>
internal sealed class QuickWeaveService(ICatalogModule catalog, IRealmsModule realms, UnitOfWork unitOfWork, Random random)
{
    public const string NoCandidates = "no-candidates";

    public async Task<QuickWeaveResolution> ResolveAsync(QuickWeaveRequest request, CancellationToken cancellationToken)
    {
        var limits = request.Limits ?? new QuickWeaveLimits();
        var issues = new List<RitualIssue>();
        CheckLimits(limits);

        var requested = new[] { request.NeedleId, request.ThreadId, request.CoatingId, request.MapId }.OfType<Guid>().ToList();
        var known = await catalog.GetComponentsAsync(requested, cancellationToken);

        var needle = Selected(RitualSlot.Needle, ComponentType.Needle, request.NeedleId);
        var thread = Selected(RitualSlot.Thread, ComponentType.Thread, request.ThreadId);
        var coating = Selected(RitualSlot.Coating, ComponentType.Coating, request.CoatingId);
        var map = Selected(RitualSlot.Map, ComponentType.Map, request.MapId);

        // A chosen map fills empty slots with what it was woven from; a different component in a slot makes a mirror.
        var mismatched = new List<RitualSlot>();
        MapRecipe? recipe = null;
        if (map?.Component.Map is { } details)
        {
            var recorded = await catalog.GetComponentsAsync([details.NeedleId, details.ThreadId, details.CoatingId], cancellationToken);
            needle = FromMap(RitualSlot.Needle, needle, recorded.GetValueOrDefault(details.NeedleId));
            thread = FromMap(RitualSlot.Thread, thread, recorded.GetValueOrDefault(details.ThreadId));
            coating = FromMap(RitualSlot.Coating, coating, recorded.GetValueOrDefault(details.CoatingId));
            recipe = details.Recipe;
            if (recipe is null)
            {
                issues.Add(new RitualIssue(RitualSlot.Map, "mapId", RitualResolver.InvalidOption, "This map has no recorded recipe yet; restart the API to migrate it."));
            }
        }

        // Anything still empty is picked at random within the limits. Issues from earlier checks win.
        if (!issues.Any(i => i.Slot is null))
        {
            needle ??= await RandomAsync(RitualSlot.Needle, ComponentType.Needle, c =>
                (limits.MinTier is not { } min || c.Needle!.DifficultyTier >= min) && (limits.MaxTier is not { } max || c.Needle!.DifficultyTier <= max));
            thread ??= await RandomAsync(RitualSlot.Thread, ComponentType.Thread, c => limits.Biomes is not { Count: > 0 } biomes || biomes.Contains(c.Thread!.Biome));
            coating ??= await RandomAsync(RitualSlot.Coating, ComponentType.Coating, c => limits.Kinds is not { Count: > 0 } kinds || kinds.Contains(c.Coating!.RealmKind));
        }

        if (map is null && coating is not null)
        {
            CheckAdvanced(coating.Component);
        }

        var preview = new QuickWeavePreviewDto(
            needle,
            thread,
            coating,
            map,
            mismatched.Count > 0,
            mismatched,
            RitualResolver.DangerFor(mismatched.Count),
            CreatesNewMap: map is null,
            issues);
        return new QuickWeaveResolution(preview, recipe);

        SlotPreview? Selected(RitualSlot slot, ComponentType type, Guid? id)
        {
            if (id is not { } componentId)
            {
                return null;
            }

            if (!known.TryGetValue(componentId, out var component))
            {
                issues.Add(new RitualIssue(slot, FieldOf(slot), RitualResolver.Unknown, $"No component has id '{componentId}'."));
                return null;
            }

            if (component.Type != type)
            {
                issues.Add(new RitualIssue(slot, FieldOf(slot), RitualResolver.WrongType, $"{component.Name} is a {component.Type}, not a {type}."));
                return null;
            }

            return new SlotPreview(component, SlotSource.Selected, 0);
        }

        SlotPreview? FromMap(RitualSlot slot, SlotPreview? current, ComponentDto? recorded)
        {
            if (recorded is null)
            {
                return current;
            }

            if (current is null)
            {
                return new SlotPreview(recorded, SlotSource.FromMap, 0);
            }

            if (current.Component.Id != recorded.Id)
            {
                mismatched.Add(slot);
            }

            return current;
        }

        async Task<SlotPreview?> RandomAsync(RitualSlot slot, ComponentType type, Func<ComponentDto, bool> fits)
        {
            var candidates = (await catalog.ListByTypeAsync(type, limits.IncludeArchived, cancellationToken))
                .Where(c => limits.Rarities is not { Count: > 0 } rarities || rarities.Contains(c.Rarity))
                .Where(fits)
                .ToList();
            if (candidates.Count == 0)
            {
                issues.Add(new RitualIssue(slot, FieldOf(slot), NoCandidates, $"No {type} in the catalog fits the limits."));
                return null;
            }

            return new SlotPreview(candidates[random.Next(candidates.Count)], SlotSource.Random, 0);
        }

        void CheckLimits(QuickWeaveLimits l)
        {
            if (l.MinTier is { } min && min is < GeneratorLimits.MinDifficulty or > GeneratorLimits.MaxDifficulty)
            {
                issues.Add(new RitualIssue(null, "limits.minTier", RitualResolver.InvalidOption, $"Tiers run from {GeneratorLimits.MinDifficulty} to {GeneratorLimits.MaxDifficulty}."));
            }

            if (l.MaxTier is { } max && max is < GeneratorLimits.MinDifficulty or > GeneratorLimits.MaxDifficulty)
            {
                issues.Add(new RitualIssue(null, "limits.maxTier", RitualResolver.InvalidOption, $"Tiers run from {GeneratorLimits.MinDifficulty} to {GeneratorLimits.MaxDifficulty}."));
            }

            if (l is { MinTier: { } lo, MaxTier: { } hi } && lo > hi)
            {
                issues.Add(new RitualIssue(null, "limits.maxTier", RitualResolver.InvalidOption, "The highest tier can't be below the lowest."));
            }
        }

        void CheckAdvanced(ComponentDto layoutCoating)
        {
            if (request.Width is { } w && w is < GeneratorLimits.MinSize or > GeneratorLimits.MaxSize)
            {
                issues.Add(new RitualIssue(null, "width", RitualResolver.InvalidOption, $"Width must be between {GeneratorLimits.MinSize} and {GeneratorLimits.MaxSize}."));
            }

            if (request.Height is { } h && h is < GeneratorLimits.MinSize or > GeneratorLimits.MaxSize)
            {
                issues.Add(new RitualIssue(null, "height", RitualResolver.InvalidOption, $"Height must be between {GeneratorLimits.MinSize} and {GeneratorLimits.MaxSize}."));
            }

            if (request.EventCount is { } e)
            {
                if (e is < GeneratorLimits.MinEvents or > GeneratorLimits.AbsoluteMaxEvents)
                {
                    issues.Add(new RitualIssue(null, "eventCount", RitualResolver.InvalidOption, $"Event count must be between {GeneratorLimits.MinEvents} and {GeneratorLimits.AbsoluteMaxEvents}."));
                }
                else if (layoutCoating.Coating?.RealmKind == RealmKind.Dungeon)
                {
                    issues.Add(new RitualIssue(null, "eventCount", RitualResolver.InvalidOption, "Event count sets the field's events; this coating makes a dungeon-only realm."));
                }
            }
        }
    }

    public async Task<QuickWeaveOutcome> WeaveAsync(QuickWeaveRequest request, CancellationToken cancellationToken)
    {
        var (preview, recipe) = await ResolveAsync(request, cancellationToken);
        if (!preview.CanWeave)
        {
            return new QuickWeaveOutcome.Invalid(preview.Issues);
        }

        var needle = preview.Needle!.Component;
        var thread = preview.Thread!.Component;
        var coating = preview.Coating!.Component;
        var existingMap = preview.Map?.Component;
        var variant = RitualResolver.EncounterVariantFor(preview.IsMismatch, needle.Id, thread.Id, coating.Id);
        var generation = RitualService.BuildRequest(
            existingMap?.Map, recipe, preview.IsMismatch, needle, thread, coating, preview.DangerModifier, variant, request.Width, request.Height, request.EventCount);

        RealmDocument document;
        try
        {
            document = RealmGenerator.Generate(generation);
        }
        catch (RealmGenerationException ex)
        {
            return new QuickWeaveOutcome.GenerationFailed(ex.Message);
        }

        var mapId = existingMap?.Id ?? Guid.CreateVersion7();
        var realmId = Guid.CreateVersion7();
        await unitOfWork.ExecuteAsync(
            async ct =>
            {
                var map = existingMap ?? await catalog.CreateMapAsync(RitualService.NewMapFor(mapId, document, needle, thread, coating, playerId: null), ct);
                await realms.SaveAsync(
                    new NewRealm(
                        realmId,
                        PlayerId: null,
                        map.Id,
                        preview.DangerModifier,
                        variant,
                        document,
                        new RealmComponentsDto(map.Name, new(needle.Id, needle.Name), new(thread.Id, thread.Name), new(coating.Id, coating.Name))),
                    ct);
                return map;
            },
            cancellationToken);

        return new QuickWeaveOutcome.Woven(new QuickWeaveResultDto(
            realmId, mapId, existingMap is null, needle.Id, thread.Id, coating.Id, preview.IsMismatch, preview.DangerModifier));
    }

    private static string FieldOf(RitualSlot slot) => slot switch
    {
        RitualSlot.Needle => "needleId",
        RitualSlot.Thread => "threadId",
        RitualSlot.Coating => "coatingId",
        _ => "mapId",
    };
}

internal sealed record QuickWeaveResolution(QuickWeavePreviewDto Preview, MapRecipe? Recipe);

internal abstract record QuickWeaveOutcome
{
    public sealed record Woven(QuickWeaveResultDto Result) : QuickWeaveOutcome;

    public sealed record Invalid(IReadOnlyList<RitualIssue> Issues) : QuickWeaveOutcome;

    public sealed record GenerationFailed(string Detail) : QuickWeaveOutcome;
}
