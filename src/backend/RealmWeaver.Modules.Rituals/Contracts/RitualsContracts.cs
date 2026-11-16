using RealmWeaver.Modules.Catalog.Contracts;

namespace RealmWeaver.Modules.Rituals.Contracts;

/// <summary>
/// What the player put in the four slots, plus the optional Advanced settings. Empty slots may be filled
/// from a chosen Map, by an owned Map matching the other three, or (in a preview) at random.
/// </summary>
public sealed record RitualSelection(
    Guid PlayerId,
    Guid? NeedleId = null,
    Guid? ThreadId = null,
    Guid? CoatingId = null,
    Guid? MapId = null,
    int? Width = null,
    int? Height = null,
    int? EventCount = null);

/// <summary>Body of <c>POST /api/rituals/preview</c>. <see cref="Randomize"/> fills empty non-map slots from the player's items.</summary>
public sealed record RitualPreviewRequest(
    Guid PlayerId,
    Guid? NeedleId = null,
    Guid? ThreadId = null,
    Guid? CoatingId = null,
    Guid? MapId = null,
    int? Width = null,
    int? Height = null,
    int? EventCount = null,
    bool Randomize = false)
{
    public RitualSelection ToSelection() => new(PlayerId, NeedleId, ThreadId, CoatingId, MapId, Width, Height, EventCount);
}

public enum RitualSlot
{
    Needle,
    Thread,
    Coating,
    Map,
}

/// <summary>How a slot got its component.</summary>
public enum SlotSource
{
    /// <summary>The player chose it.</summary>
    Selected,

    /// <summary>Filled in from the chosen Map's recorded Needle, Thread or Coating.</summary>
    FromMap,

    /// <summary>The player owns a Map made with exactly the chosen Needle, Thread and Coating.</summary>
    MatchingMap,

    /// <summary>
    /// A Map in the catalog was woven with these ingredients, so the ritual reopens that realm even though the player
    /// doesn't hold it. Only a map flagged Map Required (sealed) refuses.
    /// </summary>
    KnownRecipe,

    /// <summary>Picked at random from the player's own items.</summary>
    Random,
}

public sealed record SlotPreview(ComponentDto Component, SlotSource Source, int OwnedQuantity);

/// <summary>
/// Something that stops the ritual. Codes: <c>missing</c>, <c>unknown</c>, <c>wrong-type</c>, <c>invalid-option</c> and
/// <c>mirror-not-allowed</c> (the map is flagged Components Must Match) are input errors (400 on perform);
/// <c>not-owned</c> means the player lacks the component, and <c>map-required</c> that the ingredients open a sealed map
/// the player doesn't hold (both 409 on perform).
/// </summary>
public sealed record RitualIssue(RitualSlot? Slot, string Field, string Code, string Message);

public sealed record RitualPreviewDto(
    Guid PlayerId,
    SlotPreview? Needle,
    SlotPreview? Thread,
    SlotPreview? Coating,
    SlotPreview? Map,
    bool IsMismatch,
    IReadOnlyList<RitualSlot> MismatchedSlots,
    int DangerModifier,
    bool CreatesNewMap,
    bool AdvancedOptionsIgnored,
    IReadOnlyList<RitualIssue> Issues)
{
    public bool CanPerform => Issues.Count == 0;
}

/// <summary>
/// Limits for the slots a GM leaves empty in a Quick Weave. Every limit is optional: rarities apply to all three,
/// the tier range to the Needle, biomes to the Thread and realm kinds to the Coating.
/// </summary>
public sealed record QuickWeaveLimits(
    IReadOnlyList<Rarity>? Rarities = null,
    int? MinTier = null,
    int? MaxTier = null,
    IReadOnlyList<Generation.Model.Biome>? Biomes = null,
    IReadOnlyList<Generation.Model.RealmKind>? Kinds = null,
    bool IncludeArchived = false);

/// <summary>
/// A GM weaving from the whole catalog: no inventory, nothing consumed. A chosen slot is used as is; an empty one is
/// picked at random within <see cref="Limits"/>. With a Map the realm reuses its seed (a mirror if the slots differ);
/// without one a new, unowned Map is recorded. <see cref="Population"/> (null means rolled per settlement) and
/// <see cref="IncludeStructures"/> (off by default, so image generators aren't handed buildings to draw) and
/// <see cref="SuggestStructures"/> (structure ideas for the GM, not placed) apply to new maps; a recorded map keeps the
/// settings in its recipe.
/// </summary>
public sealed record QuickWeaveRequest(
    Guid? NeedleId = null,
    Guid? ThreadId = null,
    Guid? CoatingId = null,
    Guid? MapId = null,
    QuickWeaveLimits? Limits = null,
    int? Width = null,
    int? Height = null,
    int? EventCount = null,
    Generation.Model.SettlementPopulation? Population = null,
    bool IncludeStructures = false,
    bool SuggestStructures = false);

public sealed record QuickWeavePreviewDto(
    SlotPreview? Needle,
    SlotPreview? Thread,
    SlotPreview? Coating,
    SlotPreview? Map,
    bool IsMismatch,
    IReadOnlyList<RitualSlot> MismatchedSlots,
    int DangerModifier,
    bool CreatesNewMap,
    IReadOnlyList<RitualIssue> Issues)
{
    public bool CanWeave => Issues.Count == 0;
}

public sealed record QuickWeaveResultDto(
    Guid RealmId,
    Guid MapId,
    bool MapCreated,
    Guid NeedleId,
    Guid ThreadId,
    Guid CoatingId,
    bool IsMismatch,
    int DangerModifier);

public sealed record RitualResultDto(
    Guid RitualId,
    Guid PlayerId,
    Guid RealmId,
    Guid MapId,
    bool MapCreated,
    Guid NeedleId,
    Guid ThreadId,
    Guid CoatingId,
    bool IsMismatch,
    int DangerModifier,
    int EncounterVariant,
    DateTimeOffset CreatedAt);

/// <summary>
/// The GM marking an objective complete for the players who ran it (ADR 0011). Repeatable: a realm can be completed any
/// number of times, by the same party or a new one. <see cref="MapRecipientIds"/> are the players among
/// <see cref="PlayerIds"/> who receive the realm's map; when it's null, every player who doesn't hold it yet does.
/// </summary>
public sealed record CompleteRunInput(string ObjectiveKey, IReadOnlyList<Guid> PlayerIds, IReadOnlyList<Guid>? MapRecipientIds = null);

/// <summary>
/// A completion. <see cref="IsFirst"/> marks the first completion of the realm's map. <see cref="MapGivenTo"/> lists
/// the players who received the map; <see cref="Skipped"/> says why the other recipients didn't: <c>already-held</c>,
/// <c>unique-held</c> (a Unique map another player holds) or <c>map-missing</c>.
/// </summary>
public sealed record RunCompletionDto(
    Guid CompletionId,
    Guid RealmId,
    Generation.Model.RealmObjective Objective,
    DateTimeOffset CompletedAt,
    bool IsFirst,
    Guid MapId,
    IReadOnlyList<Guid> PlayerIds,
    IReadOnlyList<Guid> MapGivenTo,
    IReadOnlyList<MapHandOutSkip> Skipped);

public sealed record MapHandOutSkip(Guid PlayerId, string Reason);
