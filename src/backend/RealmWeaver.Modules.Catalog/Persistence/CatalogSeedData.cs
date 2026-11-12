using RealmWeaver.Generation.Model;
using RealmWeaver.Modules.Catalog.Contracts;

namespace RealmWeaver.Modules.Catalog.Persistence;

/// <summary>
/// Starter content with fixed ids, so seeding is idempotent and tests can refer to items by id.
/// All names and descriptions are original to Realm Weaver.
/// </summary>
internal static class CatalogSeedData
{
    public static readonly Guid BeginnerNeedleId = new("10000000-0000-0000-0000-000000000001");

    public static IReadOnlyList<SeedComponent> Components { get; } =
    [
        // Needles: never consumed. The beginner needle is in every starter kit, so it isn't self-assignable.
        Needle(BeginnerNeedleId, "Beginner's Needle", Rarity.Common, 1, "A plain iron needle. Every weaver's first.", 1, "Iron", ["Steady Point"]),
        Needle(Id(1, 2), "Bone Needle", Rarity.Uncommon, 2, "Carved from an old antler; it hums near danger.", 3, "Bone", ["Marrow Sense"]),
        Needle(Id(1, 3), "Silver Needle", Rarity.Rare, 3, "Bright enough to stitch by moonlight.", 5, "Silver", ["Moonlit Path", "Ward Stitch"]),
        Needle(Id(1, 4), "Glass Needle", Rarity.Epic, 4, "Clear as water and twice as unforgiving.", 7, "Glass", ["Clear Sight", "Shatterpoint"]),
        Needle(Id(1, 5), "Starforged Needle", Rarity.Legendary, 5, "Hammered from a fallen star. It always points somewhere worse.", 10, "Starmetal", ["True North", "Fate Stitch", "Comet Eye"]),

        // Threads: consumed by a ritual. The common ones make up starter kits.
        Thread(Id(2, 1), "Mossgreen Thread", Rarity.Common, 1, "Smells of wet bark.", Biome.Forest, Element.Earth, assignable: true),
        Thread(Id(2, 2), "Wheatgold Thread", Rarity.Common, 1, "Warm, dry and a little dusty.", Biome.Plains, Element.Light, assignable: true),
        Thread(Id(2, 3), "Sandspun Thread", Rarity.Common, 1, "Gritty between the fingers.", Biome.Desert, Element.Fire, assignable: true),
        Thread(Id(2, 4), "Reedwoven Thread", Rarity.Common, 1, "Never quite dries.", Biome.Swamp, Element.Water, assignable: true),
        Thread(Id(2, 5), "Frostline Thread", Rarity.Uncommon, 2, "Cold enough to sting.", Biome.Tundra, Element.Frost, assignable: false),
        Thread(Id(2, 6), "Saltbraid Thread", Rarity.Uncommon, 2, "Leaves white crystals on the spool.", Biome.Coast, Element.Water, assignable: false),
        Thread(Id(2, 7), "Emberwire Thread", Rarity.Rare, 3, "Glows faintly and singes the cloth.", Biome.Volcanic, Element.Fire, assignable: false),
        Thread(Id(2, 8), "Stormcord Thread", Rarity.Rare, 3, "Crackles when pulled tight.", Biome.Mountains, Element.Lightning, assignable: false),

        // Coatings: consumed by a ritual. They choose the mobs and the realm's shape.
        Coating(Id(3, 1), "Beastward Wax", Rarity.Common, 1, "Draws out whatever hunts in the open.", ["beast"], RealmKind.Field, 0, assignable: true),
        Coating(Id(3, 2), "Brigand's Pitch", Rarity.Common, 1, "Sticky with stolen coin.", ["bandit", "humanoid"], RealmKind.FieldWithDungeon, 1, assignable: true),
        Coating(Id(3, 3), "Gravedust Lacquer", Rarity.Common, 1, "Dry, grey and very quiet.", ["undead"], RealmKind.Dungeon, 1, assignable: true),
        Coating(Id(3, 4), "Goblin Tallow", Rarity.Uncommon, 2, "Rancid, and somehow proud of it.", ["goblinoid"], RealmKind.FieldWithDungeon, 2, assignable: false),
        Coating(Id(3, 5), "Cinder Resin", Rarity.Rare, 3, "Still warm a week after it sets.", ["elemental"], RealmKind.Dungeon, 2, assignable: false),
        Coating(Id(3, 6), "Blightsap Varnish", Rarity.Epic, 4, "It keeps growing after you apply it.", ["plant", "beast"], RealmKind.FieldWithDungeon, 3, assignable: false),
    ];

    /// <summary>Self-assign starts on, so a fresh install is playable without visiting the admin page.</summary>
    public const bool DefaultPlayersCanSelfAssign = true;

    private static Guid Id(int type, int index) => new($"{type}0000000-0000-0000-0000-{index:D12}");

    private static SeedComponent Needle(Guid id, string name, Rarity rarity, int rank, string description, int tier, string category, string[] abilities) =>
        new(id, new ComponentInput(ComponentType.Needle, name, rarity, rank, description, IsPlayerAssignable: false, Needle: new NeedleDetails(tier, category, abilities)));

    private static SeedComponent Thread(Guid id, string name, Rarity rarity, int rank, string description, Biome biome, Element element, bool assignable) =>
        new(id, new ComponentInput(ComponentType.Thread, name, rarity, rank, description, assignable, Thread: new ThreadDetails(biome, element)));

    private static SeedComponent Coating(Guid id, string name, Rarity rarity, int rank, string description, string[] mobTypes, RealmKind kind, int floors, bool assignable) =>
        new(id, new ComponentInput(ComponentType.Coating, name, rarity, rank, description, assignable, Coating: new CoatingDetails(mobTypes, kind, floors)));
}

internal sealed record SeedComponent(Guid Id, ComponentInput Input);
