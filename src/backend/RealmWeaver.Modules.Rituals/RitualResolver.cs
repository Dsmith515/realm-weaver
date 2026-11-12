using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Randomness;
using RealmWeaver.Modules.Catalog.Contracts;
using RealmWeaver.Modules.Inventory.Contracts;
using RealmWeaver.Modules.Rituals.Contracts;

namespace RealmWeaver.Modules.Rituals;

/// <summary>
/// Applies the slot rules from the roadmap to a selection. Used by preview and, unchanged, by perform,
/// so what the player saw is exactly what runs.
/// <list type="number">
/// <item>A chosen Map fills empty Needle/Thread/Coating slots. A different component in a slot is a mismatch.</item>
/// <item>With a Needle, Thread and Coating and no Map, an owned Map made with exactly those three fills the Map slot.</item>
/// <item>Randomize fills empty non-map slots from the player's items (consumables need quantity 1+), then rule 2.</item>
/// </list>
/// </summary>
internal sealed class RitualResolver(ICatalogModule catalog, IInventoryModule inventory, Random random)
{
    public const string Missing = "missing";
    public const string Unknown = "unknown";
    public const string WrongType = "wrong-type";
    public const string NotOwned = "not-owned";
    public const string InvalidOption = "invalid-option";

    /// <summary>Returns null when the player doesn't exist.</summary>
    public async Task<RitualResolution?> ResolveAsync(RitualSelection selection, bool randomize, CancellationToken cancellationToken)
    {
        var owned = await inventory.GetInventoryAsync(selection.PlayerId, cancellationToken);
        if (owned is null)
        {
            return null;
        }

        var ownedById = owned.ToDictionary(i => i.Component.Id);
        var issues = new List<RitualIssue>();

        var requested = new[] { selection.NeedleId, selection.ThreadId, selection.CoatingId, selection.MapId }.OfType<Guid>().ToList();
        var known = await catalog.GetComponentsAsync(requested, cancellationToken);

        var needle = Selected(RitualSlot.Needle, ComponentType.Needle, selection.NeedleId);
        var thread = Selected(RitualSlot.Thread, ComponentType.Thread, selection.ThreadId);
        var coating = Selected(RitualSlot.Coating, ComponentType.Coating, selection.CoatingId);
        var map = Selected(RitualSlot.Map, ComponentType.Map, selection.MapId);

        // Rule 1: the chosen map fills empty slots and flags different ones.
        var mismatched = new List<RitualSlot>();
        ComponentDto? mapThread = null;
        ComponentDto? mapCoating = null;
        if (map?.Component.Map is { } mapDetails)
        {
            var recorded = await catalog.GetComponentsAsync([mapDetails.NeedleId, mapDetails.ThreadId, mapDetails.CoatingId], cancellationToken);
            mapThread = recorded.GetValueOrDefault(mapDetails.ThreadId);
            mapCoating = recorded.GetValueOrDefault(mapDetails.CoatingId);
            needle = FillFromMap(RitualSlot.Needle, needle, recorded.GetValueOrDefault(mapDetails.NeedleId));
            thread = FillFromMap(RitualSlot.Thread, thread, mapThread);
            coating = FillFromMap(RitualSlot.Coating, coating, mapCoating);
        }

        // Rule 3: random fills from the player's own items.
        if (randomize)
        {
            needle ??= RandomOwned(ComponentType.Needle);
            thread ??= RandomOwned(ComponentType.Thread);
            coating ??= RandomOwned(ComponentType.Coating);
        }

        // Rule 2: an owned map made with exactly these three fills the map slot.
        if (map is null && selection.MapId is null && needle is not null && thread is not null && coating is not null)
        {
            var matching = owned
                .Where(i => i.Component.Map is { } m
                            && m.NeedleId == needle.Component.Id && m.ThreadId == thread.Component.Id && m.CoatingId == coating.Component.Id)
                .OrderByDescending(i => i.AcquiredAt)
                .FirstOrDefault();
            if (matching is not null)
            {
                map = new SlotPreview(matching.Component, SlotSource.MatchingMap, matching.Quantity);
                mapThread = thread.Component;
                mapCoating = coating.Component;
            }
        }

        RequireFilled(RitualSlot.Needle, needle);
        RequireFilled(RitualSlot.Thread, thread);
        RequireFilled(RitualSlot.Coating, coating);
        foreach (var (slot, filled) in new[] { (RitualSlot.Needle, needle), (RitualSlot.Thread, thread), (RitualSlot.Coating, coating), (RitualSlot.Map, map) })
        {
            if (filled is { OwnedQuantity: < 1 })
            {
                var what = filled.Component.IsConsumable ? "any" : "this";
                issues.Add(new RitualIssue(slot, FieldOf(slot), NotOwned, $"You don't have {what} {filled.Component.Name}."));
            }
        }

        // The realm's layout (shape, size, biome) comes from the map when there is one.
        var layoutThread = map is null ? thread?.Component : mapThread;
        var layoutCoating = map is null ? coating?.Component : mapCoating;
        var advancedUsed = selection.Width is not null || selection.Height is not null || selection.EventCount is not null;
        if (map is null)
        {
            CheckAdvanced(selection, layoutCoating);
        }

        var isMismatch = mismatched.Count > 0;
        var result = new RitualPreviewDto(
            selection.PlayerId,
            needle,
            thread,
            coating,
            map,
            isMismatch,
            mismatched,
            DangerFor(mismatched.Count),
            CreatesNewMap: map is null,
            AdvancedOptionsIgnored: map is not null && advancedUsed,
            issues);

        return new RitualResolution(result, layoutThread, layoutCoating);

        SlotPreview? Selected(RitualSlot slot, ComponentType type, Guid? id)
        {
            if (id is not { } componentId)
            {
                return null;
            }

            if (!known.TryGetValue(componentId, out var component))
            {
                issues.Add(new RitualIssue(slot, FieldOf(slot), Unknown, $"No component has id '{componentId}'."));
                return null;
            }

            if (component.Type != type)
            {
                issues.Add(new RitualIssue(slot, FieldOf(slot), WrongType, $"{component.Name} is a {component.Type}, not a {type}."));
                return null;
            }

            return new SlotPreview(component, SlotSource.Selected, QuantityOf(componentId));
        }

        SlotPreview? FillFromMap(RitualSlot slot, SlotPreview? current, ComponentDto? recorded)
        {
            if (recorded is null)
            {
                return current;
            }

            if (current is null)
            {
                return new SlotPreview(recorded, SlotSource.FromMap, QuantityOf(recorded.Id));
            }

            if (current.Component.Id != recorded.Id)
            {
                mismatched.Add(slot);
            }

            return current;
        }

        SlotPreview? RandomOwned(ComponentType type)
        {
            var candidates = owned.Where(i => i.Component.Type == type && i.Quantity >= 1).ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            var pick = candidates[random.Next(candidates.Count)];
            return new SlotPreview(pick.Component, SlotSource.Random, pick.Quantity);
        }

        int QuantityOf(Guid componentId) => ownedById.TryGetValue(componentId, out var item) ? item.Quantity : 0;

        void RequireFilled(RitualSlot slot, SlotPreview? preview)
        {
            if (preview is null && !issues.Any(i => i.Slot == slot))
            {
                issues.Add(new RitualIssue(slot, FieldOf(slot), Missing, $"Choose a {slot}."));
            }
        }

        void CheckAdvanced(RitualSelection s, ComponentDto? layout)
        {
            if (s.Width is { } w && w is < GeneratorLimits.MinSize or > GeneratorLimits.MaxSize)
            {
                issues.Add(new RitualIssue(null, "width", InvalidOption, $"Width must be between {GeneratorLimits.MinSize} and {GeneratorLimits.MaxSize}."));
            }

            if (s.Height is { } h && h is < GeneratorLimits.MinSize or > GeneratorLimits.MaxSize)
            {
                issues.Add(new RitualIssue(null, "height", InvalidOption, $"Height must be between {GeneratorLimits.MinSize} and {GeneratorLimits.MaxSize}."));
            }

            if (s.EventCount is { } e)
            {
                if (e is < GeneratorLimits.MinEvents or > GeneratorLimits.AbsoluteMaxEvents)
                {
                    issues.Add(new RitualIssue(null, "eventCount", InvalidOption, $"Event count must be between {GeneratorLimits.MinEvents} and {GeneratorLimits.AbsoluteMaxEvents}."));
                }
                else if (layout?.Coating?.RealmKind == RealmKind.Dungeon)
                {
                    issues.Add(new RitualIssue(null, "eventCount", InvalidOption, "Event count sets the field's events; this coating makes a dungeon-only realm."));
                }
            }
        }
    }

    /// <summary>A mismatched map raises danger by 1 for one different slot, and by 2 for two or more.</summary>
    public static int DangerFor(int mismatchedSlots) => Math.Min(mismatchedSlots, 2);

    /// <summary>
    /// Mismatches reroll encounters, but deterministically: the same map with the same components
    /// always gives the same realm. Matching rituals use variant 0, the map's original encounters.
    /// </summary>
    public static int EncounterVariantFor(bool isMismatch, Guid needleId, Guid threadId, Guid coatingId) =>
        isMismatch ? 1 + (int)(StableHash.Of($"{needleId:N}/{threadId:N}/{coatingId:N}") % 1_000_000UL) : 0;

    private static string FieldOf(RitualSlot slot) => slot switch
    {
        RitualSlot.Needle => "needleId",
        RitualSlot.Thread => "threadId",
        RitualSlot.Coating => "coatingId",
        _ => "mapId",
    };
}

/// <summary>The preview plus the components the realm's layout comes from (the map's own, when there is a map).</summary>
internal sealed record RitualResolution(RitualPreviewDto Preview, ComponentDto? LayoutThread, ComponentDto? LayoutCoating);
