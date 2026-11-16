using RealmWeaver.Generation.Model;

namespace RealmWeaver.Generation.Content;

/// <summary>Built-in original content used until the content tables milestone.</summary>
public sealed class StaticContentCatalog : IContentCatalog
{
    public static StaticContentCatalog Default { get; } = new();

    public IReadOnlyList<EventTemplate> EventTemplates { get; } =
    [
        // Encounters
        new()
        {
            Id = "wandering-monster", Name = "Wandering monster", Category = EventCategory.Encounter,
            Environments = Environments.Any, Footprint = new(1, 2), Weight = 30,
            MobSlots = [new(MobRole.Minion, 1, 3)],
            Description = "A lone creature or small group roaming the area.",
        },
        new()
        {
            Id = "patrol", Name = "Patrol", Category = EventCategory.Encounter,
            Environments = Environments.Any, Footprint = new(3, 6, FootprintShape.Line), Weight = 22,
            MobSlots = [new(MobRole.Minion, 2, 4), new(MobRole.Leader, 0, 1)],
            Description = "A group walking this route back and forth.",
        },
        new()
        {
            Id = "monster-camp", Name = "Monster camp", Category = EventCategory.Encounter,
            Environments = Environments.Field, Footprint = new(3, 5), Weight = 12, ClearsTerrain = true,
            MobSlots = [new(MobRole.Minion, 3, 6), new(MobRole.Brute, 0, 2), new(MobRole.Leader, 1, 1)],
            Description = "Tents, a fire pit and sentries.",
            ObjectiveHooks = [new("break-the-camp", "Break the camp", "Scatter the camp's occupants and burn their supplies.")],
        },
        new()
        {
            Id = "beast-lair", Name = "Lair", Category = EventCategory.Encounter,
            Environments = Environments.Any, Footprint = new(2, 4), Weight = 12,
            MobSlots = [new(MobRole.Brute, 1, 2), new(MobRole.Minion, 0, 3)],
            Description = "A den with bones and a territorial occupant.",
            ObjectiveHooks = [new("slay-the-beast", "Slay or drive off the beast", "Deal with the lair's occupant for good.")],
        },
        new()
        {
            Id = "ambush-site", Name = "Ambush", Category = EventCategory.Encounter,
            Environments = Environments.Any, Footprint = new(2, 3), Weight = 10,
            MobSlots = [new(MobRole.Skirmisher, 2, 4)],
            Description = "Hidden attackers wait for travellers.",
        },

        // Settlements (count as one event, mobs included)
        new()
        {
            Id = "village", Name = "Village", Category = EventCategory.Settlement,
            Environments = Environments.Field, Footprint = new(6, 12) { ScalesWithMap = true },
            Weight = 6, MaxPerArea = 1, ClearsTerrain = true,
            MobSlots = [new(MobRole.Leader, 1, 1), new(MobRole.Minion, 2, 6)],
            Description = "A village held by hostile occupants.",
            ObjectiveHooks = [new("settle-the-dispute", "Settle the dispute", "End the trouble in the village, by word or by force.") { NeedsResidents = true }],
        },
        new()
        {
            Id = "outpost", Name = "Outpost", Category = EventCategory.Settlement,
            Environments = Environments.Field, Footprint = new(4, 7), Weight = 6, MaxPerArea = 2, ClearsTerrain = true,
            MobSlots = [new(MobRole.Minion, 2, 4), new(MobRole.Caster, 0, 1)],
            Description = "A walled outpost or watch camp.",
            ObjectiveHooks = [new("take-the-outpost", "Take the outpost", "Drive out the garrison and hold the walls.")],
        },

        // Social
        new()
        {
            Id = "merchant-caravan", Name = "Merchant caravan", Category = EventCategory.Social,
            Environments = Environments.Field, Footprint = new(2, 3), Weight = 6, MaxPerArea = 2,
            Description = "Traders willing to barter. Friendly unless provoked.",
            ObjectiveHooks = [new("complete-the-trade", "Complete the trade", "Strike a deal with the caravan before it moves on.")],
        },
        new()
        {
            Id = "lost-traveler", Name = "Lost traveler", Category = EventCategory.Social,
            Environments = Environments.Any, Footprint = new(1, 1), Weight = 6,
            Description = "Someone who needs help, or has information.",
            ObjectiveHooks = [new("escort-to-safety", "Escort the traveler to safety", "Bring them back to the way out alive.")],
        },

        // Landmarks
        new()
        {
            Id = "shrine", Name = "Shrine", Category = EventCategory.Landmark,
            Environments = Environments.Any, Footprint = new(1, 2), Weight = 7,
            Description = "An old shrine; may grant a blessing.",
            ObjectiveHooks = [new("uncover-the-past", "Uncover what happened", "Learn why the shrine was abandoned.", 8)],
        },
        new()
        {
            Id = "ancient-ruins", Name = "Ancient ruins", Category = EventCategory.Landmark,
            Environments = Environments.Field, Footprint = new(3, 6), Weight = 6, MaxPerArea = 3,
            MobSlots = [new(MobRole.Minion, 0, 3)],
            Description = "Collapsed walls hiding salvage and squatters.",
            ObjectiveHooks = [new("uncover-the-past", "Uncover what happened", "Piece together who lived here and why they left.", 8)],
        },
        new()
        {
            Id = "resource-node", Name = "Resource node", Category = EventCategory.Landmark,
            Environments = Environments.Any, Footprint = new(1, 2), Weight = 9,
            Description = "Harvestable herbs, ore or monster materials.",
        },

        // Hazards
        new()
        {
            Id = "unstable-ground", Name = "Unstable ground", Category = EventCategory.Hazard,
            Environments = Environments.Field, Footprint = new(2, 4), Weight = 5,
            Description = "Sinkholes and loose earth.",
        },

        // Biome specials
        new()
        {
            Id = "oasis", Name = "Oasis", Category = EventCategory.Landmark, Environments = Environments.Field,
            Footprint = new(2, 3), Weight = 8, MaxPerArea = 2, Biomes = [Biome.Desert],
            Description = "Fresh water and shade.",
        },
        new()
        {
            Id = "frozen-cairn", Name = "Frozen cairn", Category = EventCategory.Landmark, Environments = Environments.Field,
            Footprint = new(1, 2), Weight = 8, Biomes = [Biome.Tundra, Biome.Mountains],
            Description = "A stone marker encased in ice, holding an offering.",
            ObjectiveHooks = [new("claim-the-offering", "Claim the cairn's offering", "Free the offering from the ice without angering whoever left it.", 6)],
        },
        new()
        {
            Id = "bog-hut", Name = "Bog hermit's hut", Category = EventCategory.Social, Environments = Environments.Field,
            Footprint = new(2, 2), Weight = 8, MaxPerArea = 1, Biomes = [Biome.Swamp],
            Description = "A reclusive alchemist who trades in strange brews.",
            ObjectiveHooks = [new("gather-reagents", "Gather the hermit's reagents", "Bring back what the hermit asks for in exchange for a brew.", 8)],
        },
        new()
        {
            Id = "lava-vent", Name = "Lava vent", Category = EventCategory.Hazard, Environments = Environments.Field,
            Footprint = new(1, 3), Weight = 10, Biomes = [Biome.Volcanic],
            Description = "Erupts every few rounds.",
        },
        new()
        {
            Id = "fairy-ring", Name = "Fairy ring", Category = EventCategory.Landmark, Environments = Environments.Field,
            Footprint = new(2, 3), Weight = 6, MaxPerArea = 1, Biomes = [Biome.Forest, Biome.Plains],
            MobSlots = [new(MobRole.Skirmisher, 0, 2)],
            Description = "A circle of mushrooms where the veil is thin.",
            ObjectiveHooks = [new("bargain-with-the-fey", "Strike a bargain with the fey", "Step into the ring and leave with a fair deal.", 8)],
        },
        new()
        {
            Id = "shipwreck", Name = "Shipwreck", Category = EventCategory.Landmark, Environments = Environments.Field,
            Footprint = new(2, 4), Weight = 8, MaxPerArea = 1, Biomes = [Biome.Coast],
            MobSlots = [new(MobRole.Minion, 0, 3)],
            Description = "A beached hull with cargo still aboard.",
            ObjectiveHooks = [new("salvage-the-cargo", "Salvage the cargo", "Recover the hull's cargo before the tide or the scavengers do.")],
        },

        // Dungeon hosts: field structures holding the way down. Their material sets the dungeon's primary material.
        new()
        {
            Id = "castle", Name = "Castle", Category = EventCategory.Structure, Environments = Environments.Field,
            Footprint = new(6, 10), Weight = 10, MaxPerArea = 1, ClearsTerrain = true, HostMaterial = DungeonMaterial.DressedStone,
            MobSlots = [new(MobRole.Minion, 2, 4), new(MobRole.Leader, 0, 1)],
            Description = "A stone castle. Its cellars lead down into the dungeon.",
        },
        new()
        {
            Id = "ruined-keep", Name = "Ruined keep", Category = EventCategory.Structure, Environments = Environments.Field,
            Footprint = new(5, 8), Weight = 10, MaxPerArea = 1, ClearsTerrain = true, HostMaterial = DungeonMaterial.CrumblingBrick,
            MobSlots = [new(MobRole.Minion, 1, 3)],
            Description = "A collapsed brick keep with a stairwell going down.",
        },
        new()
        {
            Id = "mine-entrance", Name = "Mine entrance", Category = EventCategory.Structure, Environments = Environments.Field,
            Footprint = new(3, 4), Weight = 10, MaxPerArea = 1, ClearsTerrain = true, HostMaterial = DungeonMaterial.TimberBracedRock,
            Biomes = [Biome.Mountains, Biome.Tundra, Biome.Desert, Biome.Volcanic, Biome.Plains],
            Description = "Timber headframe over an abandoned mine shaft.",
        },
        new()
        {
            Id = "cave-mouth", Name = "Cave mouth", Category = EventCategory.Structure, Environments = Environments.Field,
            Footprint = new(2, 3), Weight = 10, MaxPerArea = 1, ClearsTerrain = true, HostMaterial = DungeonMaterial.NaturalCave,
            Description = "A dark opening in the rock.",
        },
        new()
        {
            Id = "wizard-tower", Name = "Wizard's tower", Category = EventCategory.Structure, Environments = Environments.Field,
            Footprint = new(3, 4), Weight = 6, MaxPerArea = 1, ClearsTerrain = true, HostMaterial = DungeonMaterial.DressedStone,
            MobSlots = [new(MobRole.Caster, 0, 1)],
            Description = "A lone tower whose vaults reach far underground.",
        },
        new()
        {
            Id = "sunken-temple", Name = "Sunken temple", Category = EventCategory.Structure, Environments = Environments.Field,
            Footprint = new(4, 7), Weight = 10, MaxPerArea = 1, ClearsTerrain = true, HostMaterial = DungeonMaterial.Sandstone,
            Biomes = [Biome.Desert, Biome.Swamp, Biome.Coast, Biome.Forest],
            Description = "A half-buried sandstone temple.",
        },

        // Dungeon events. Footprints are whole rooms, so the footprint rule is unused.
        new()
        {
            Id = "guard-post", Name = "Guard post", Category = EventCategory.Encounter, Environments = Environments.Dungeon,
            Footprint = new(1, 1), Weight = 18, AllowedStyles = [RoomStyle.Masonry, RoomStyle.Mine], PreferredStyles = [RoomStyle.Masonry],
            MobSlots = [new(MobRole.Minion, 2, 4), new(MobRole.Leader, 1, 1)],
            Description = "Sentries watching a junction.",
        },
        new()
        {
            Id = "crawler-nest", Name = "Crawler nest", Category = EventCategory.Encounter, Environments = Environments.Dungeon,
            Footprint = new(1, 1), Weight = 16, AllowedStyles = [RoomStyle.Cave, RoomStyle.Mine], PreferredStyles = [RoomStyle.Cave],
            MobSlots = [new(MobRole.Minion, 3, 6), new(MobRole.Brute, 0, 1)],
            Description = "Eggs, webbing and something guarding them.",
            ObjectiveHooks = [new("destroy-the-nest", "Destroy the nest", "Burn the eggs and kill what guards them.")],
        },
        new()
        {
            Id = "underground-settlement", Name = "Underground settlement", Category = EventCategory.Settlement, Environments = Environments.Dungeon,
            Footprint = new(1, 1), Weight = 8, MaxPerArea = 1, MaxRooms = 3,
            MobSlots = [new(MobRole.Minion, 3, 6), new(MobRole.Leader, 1, 1), new(MobRole.Caster, 0, 1)],
            Description = "A warren of dwellings spread over connected rooms.",
            ObjectiveHooks = [new("settle-the-dispute", "Settle the dispute", "End the trouble in the warren, by word or by force.") { NeedsResidents = true }],
        },
        new()
        {
            Id = "smugglers-den", Name = "Smugglers' den", Category = EventCategory.Settlement, Environments = Environments.Dungeon,
            Footprint = new(1, 1), Weight = 6, MaxPerArea = 1, MaxRooms = 2, AllowedStyles = [RoomStyle.Masonry, RoomStyle.Mine],
            MobSlots = [new(MobRole.Minion, 2, 4), new(MobRole.Leader, 1, 1)],
            Description = "Crates of contraband and people who would rather not be found.",
            ObjectiveHooks = [new("seize-the-contraband", "Seize the contraband", "Take the smugglers' goods and find out who they were for.")],
        },
        new()
        {
            Id = "prison", Name = "Prison cells", Category = EventCategory.Social, Environments = Environments.Dungeon,
            Footprint = new(1, 1), Weight = 6, MaxPerArea = 1, AllowedStyles = [RoomStyle.Masonry],
            MobSlots = [new(MobRole.Minion, 1, 2)],
            Description = "Captives who may help if freed.",
            ObjectiveHooks = [new("free-the-captive", "Free the captive", "Get the prisoners out of their cells and out of the dungeon.", 12)],
        },
        new()
        {
            Id = "treasure-vault", Name = "Treasure vault", Category = EventCategory.Landmark, Environments = Environments.Dungeon,
            Footprint = new(1, 1), Weight = 5, MaxPerArea = 1,
            MobSlots = [new(MobRole.Brute, 1, 1)],
            Description = "A guarded hoard behind a heavy door.",
            ObjectiveHooks = [new("recover-the-relic", "Recover the relic", "Get past the vault's guardian and carry out its relic.", 12)],
        },
        new()
        {
            Id = "collapsed-passage", Name = "Collapsed passage", Category = EventCategory.Hazard, Environments = Environments.Dungeon,
            Footprint = new(1, 1), Weight = 8, AllowedStyles = [RoomStyle.Mine, RoomStyle.Cave], PreferredStyles = [RoomStyle.Mine],
            Description = "Loose ceiling; loud noises bring it down.",
        },
        new()
        {
            Id = "fungal-grove", Name = "Fungal grove", Category = EventCategory.Landmark, Environments = Environments.Dungeon,
            Footprint = new(1, 1), Weight = 8, AllowedStyles = [RoomStyle.Cave], PreferredStyles = [RoomStyle.Cave],
            Description = "Glowing mushrooms; some are useful, some are not.",
        },
        new()
        {
            Id = "boss-chamber", Name = "Guardian's chamber", Category = EventCategory.Encounter, Environments = Environments.Dungeon,
            Footprint = new(1, 1), Weight = 1, MaxPerArea = 1, IsBoss = true,
            MobSlots = [new(MobRole.Boss, 1, 1), new(MobRole.Minion, 0, 3)],
            Description = "The deepest room and whatever rules it.",
            ObjectiveHooks = [new("defeat-the-guardian", "Defeat the guardian", "Overcome whatever rules the deepest room.", 15)],
        },
    ];

    public IReadOnlyList<MobArchetype> MobArchetypes { get; } =
    [
        // Beasts
        new() { Id = "grey-wolf", Name = "Grey wolf", Tags = ["beast"], Roles = [MobRole.Minion, MobRole.Skirmisher], Biomes = [Biome.Forest, Biome.Plains, Biome.Tundra, Biome.Mountains], MaxTier = 4 },
        new() { Id = "dire-wolf", Name = "Dire wolf", Tags = ["beast"], Roles = [MobRole.Brute, MobRole.Leader], Biomes = [Biome.Forest, Biome.Tundra, Biome.Mountains], MinTier = 3 },
        new() { Id = "tusk-boar", Name = "Tusk boar", Tags = ["beast"], Roles = [MobRole.Brute], Biomes = [Biome.Forest, Biome.Plains, Biome.Swamp] },
        new() { Id = "sand-stalker", Name = "Sand stalker", Tags = ["beast", "insect"], Roles = [MobRole.Skirmisher, MobRole.Minion], Biomes = [Biome.Desert] },
        new() { Id = "giant-beetle", Name = "Giant beetle", Tags = ["insect", "beast"], Roles = [MobRole.Minion, MobRole.Brute] },
        new() { Id = "carrion-swarm", Name = "Carrion swarm", Tags = ["beast", "insect"], Roles = [MobRole.Minion], MaxTier = 5 },
        new() { Id = "bog-lurker", Name = "Bog lurker", Tags = ["beast"], Roles = [MobRole.Brute, MobRole.Boss], Biomes = [Biome.Swamp, Biome.Coast], MinTier = 2 },
        new() { Id = "reef-crab", Name = "Reef crab", Tags = ["beast"], Roles = [MobRole.Minion, MobRole.Brute], Biomes = [Biome.Coast] },
        new() { Id = "frost-bear", Name = "Frost bear", Tags = ["beast"], Roles = [MobRole.Brute, MobRole.Leader], Biomes = [Biome.Tundra, Biome.Mountains], MinTier = 2 },

        // Undead
        new() { Id = "risen-soldier", Name = "Risen soldier", Tags = ["undead"], Roles = [MobRole.Minion] },
        new() { Id = "bone-archer", Name = "Bone archer", Tags = ["undead"], Roles = [MobRole.Skirmisher, MobRole.Minion] },
        new() { Id = "grave-hulk", Name = "Grave hulk", Tags = ["undead"], Roles = [MobRole.Brute], MinTier = 2 },
        new() { Id = "hollow-priest", Name = "Hollow priest", Tags = ["undead"], Roles = [MobRole.Caster, MobRole.Leader], MinTier = 3 },
        new() { Id = "barrow-king", Name = "Barrow king", Tags = ["undead"], Roles = [MobRole.Boss, MobRole.Leader], MinTier = 5 },

        // Bandits / humanoids
        new() { Id = "cutthroat", Name = "Cutthroat", Tags = ["bandit", "humanoid"], Roles = [MobRole.Minion, MobRole.Skirmisher] },
        new() { Id = "brigand-bruiser", Name = "Brigand bruiser", Tags = ["bandit", "humanoid"], Roles = [MobRole.Brute] },
        new() { Id = "hedge-mage", Name = "Hedge mage", Tags = ["bandit", "humanoid"], Roles = [MobRole.Caster] },
        new() { Id = "bandit-captain", Name = "Bandit captain", Tags = ["bandit", "humanoid"], Roles = [MobRole.Leader, MobRole.Boss], MinTier = 2 },

        // Goblinoids
        new() { Id = "goblin-raider", Name = "Goblin raider", Tags = ["goblinoid"], Roles = [MobRole.Minion, MobRole.Skirmisher], MaxTier = 6 },
        new() { Id = "goblin-hexer", Name = "Goblin hexer", Tags = ["goblinoid"], Roles = [MobRole.Caster] },
        new() { Id = "hob-warlord", Name = "Hob warlord", Tags = ["goblinoid"], Roles = [MobRole.Leader, MobRole.Brute, MobRole.Boss], MinTier = 3 },

        // Elementals / constructs
        new() { Id = "ember-sprite", Name = "Ember sprite", Tags = ["elemental"], Roles = [MobRole.Minion, MobRole.Skirmisher], Biomes = [Biome.Volcanic, Biome.Desert] },
        new() { Id = "magma-brute", Name = "Magma brute", Tags = ["elemental"], Roles = [MobRole.Brute, MobRole.Boss], Biomes = [Biome.Volcanic], MinTier = 3 },
        new() { Id = "rime-wisp", Name = "Rime wisp", Tags = ["elemental"], Roles = [MobRole.Minion, MobRole.Caster], Biomes = [Biome.Tundra, Biome.Mountains] },
        new() { Id = "stone-sentinel", Name = "Stone sentinel", Tags = ["construct", "elemental"], Roles = [MobRole.Brute, MobRole.Leader], MinTier = 2 },
        new() { Id = "storm-mote", Name = "Storm mote", Tags = ["elemental"], Roles = [MobRole.Skirmisher, MobRole.Caster], Biomes = [Biome.Coast, Biome.Plains, Biome.Mountains] },

        // Fey / plants
        new() { Id = "thorn-blight", Name = "Thorn blight", Tags = ["plant"], Roles = [MobRole.Minion, MobRole.Skirmisher], Biomes = [Biome.Forest, Biome.Swamp] },
        new() { Id = "rotwood-treant", Name = "Rotwood treant", Tags = ["plant"], Roles = [MobRole.Brute, MobRole.Boss], Biomes = [Biome.Forest, Biome.Swamp], MinTier = 3 },
        new() { Id = "pixie-trickster", Name = "Pixie trickster", Tags = ["fey"], Roles = [MobRole.Skirmisher, MobRole.Caster], Biomes = [Biome.Forest, Biome.Plains] },

        // Dragonkin
        new() { Id = "drake-whelp", Name = "Drake whelp", Tags = ["dragonkin"], Roles = [MobRole.Minion, MobRole.Skirmisher], MinTier = 2 },
        new() { Id = "scaled-zealot", Name = "Scaled zealot", Tags = ["dragonkin", "humanoid"], Roles = [MobRole.Caster, MobRole.Leader], MinTier = 3 },
        new() { Id = "elder-drake", Name = "Elder drake", Tags = ["dragonkin"], Roles = [MobRole.Boss, MobRole.Brute], MinTier = 6 },
    ];

    public IReadOnlyList<DifficultyTemplate> DifficultyTemplates { get; } =
    [
        new("swarm", "Swarm", 4, "Add half again as many minions."),
        new("elite", "Elite", 5, "Increase health and damage; add one extra ability."),
        new("champion", "Champion", 8, "Leaders and brutes gain a second action each round."),
        new("enraged", "Enraged", 10, "All mobs gain bonus damage when below half health."),
    ];

    public IReadOnlyList<StructureTemplate> StructureTemplates { get; } =
    [
        // Any settlement
        new() { Id = "well", Name = "Well", MinSide = 1, MaxSide = 1, Weight = 8, MaxPerSettlement = 2 },
        new() { Id = "storehouse", Name = "Storehouse", MinSide = 2, MaxSide = 3, Weight = 8 },
        new() { Id = "watchtower", Name = "Watchtower", MinSide = 2, MaxSide = 2, Weight = 6, MaxPerSettlement = 2 },
        new() { Id = "shrine", Name = "Shrine", MinSide = 2, MaxSide = 2, Weight = 5, MaxPerSettlement = 1 },
        new() { Id = "animal-pen", Name = "Animal pen", MinSide = 2, MaxSide = 4, Weight = 6 },

        // Goblinoid
        new() { Id = "hut-cluster", Name = "Hut cluster", Tags = ["goblinoid"], MinSide = 2, MaxSide = 3, Weight = 18 },
        new() { Id = "training-pit", Name = "Training pit", Tags = ["goblinoid"], MinSide = 3, MaxSide = 4, Weight = 10, MaxPerSettlement = 2 },
        new() { Id = "wolf-pen", Name = "Wolf pen", Tags = ["goblinoid"], MinSide = 2, MaxSide = 3, Weight = 8, MaxPerSettlement = 2 },
        new() { Id = "war-totem", Name = "War totem", Tags = ["goblinoid"], MinSide = 1, MaxSide = 1, Weight = 6, MaxPerSettlement = 2 },
        new() { Id = "chieftains-longhut", Name = "Chieftain's longhut", Tags = ["goblinoid"], MinSide = 3, MaxSide = 4, Weight = 8, MaxPerSettlement = 1 },

        // Humanoid and bandit
        new() { Id = "cottage", Name = "Cottage", Tags = ["humanoid", "bandit"], MinSide = 2, MaxSide = 3, Weight = 18 },
        new() { Id = "farm-plot", Name = "Farm plot", Tags = ["humanoid"], Biomes = [Biome.Plains, Biome.Forest, Biome.Swamp, Biome.Coast, Biome.Mountains], MinSide = 3, MaxSide = 5, Weight = 10 },
        new() { Id = "smithy", Name = "Smithy", Tags = ["humanoid", "bandit"], MinSide = 2, MaxSide = 3, Weight = 7, MaxPerSettlement = 1 },
        new() { Id = "market-square", Name = "Market square", Tags = ["humanoid"], MinSide = 3, MaxSide = 4, Weight = 6, MaxPerSettlement = 1 },
        new() { Id = "tavern", Name = "Tavern", Tags = ["humanoid", "bandit"], MinSide = 3, MaxSide = 3, Weight = 6, MaxPerSettlement = 1 },
        new() { Id = "loot-cache", Name = "Loot cache", Tags = ["bandit"], MinSide = 2, MaxSide = 2, Weight = 6, MaxPerSettlement = 2 },

        // Undead
        new() { Id = "barrow", Name = "Barrow", Tags = ["undead"], MinSide = 2, MaxSide = 3, Weight = 14 },
        new() { Id = "ossuary", Name = "Ossuary", Tags = ["undead"], MinSide = 2, MaxSide = 3, Weight = 8, MaxPerSettlement = 2 },
        new() { Id = "bone-spire", Name = "Bone spire", Tags = ["undead"], MinSide = 1, MaxSide = 1, Weight = 5, MaxPerSettlement = 2 },

        // Beasts, insects and plants
        new() { Id = "den-mound", Name = "Den mound", Tags = ["beast"], MinSide = 2, MaxSide = 3, Weight = 14 },
        new() { Id = "hive-mound", Name = "Hive mound", Tags = ["insect"], MinSide = 2, MaxSide = 3, Weight = 12 },
        new() { Id = "overgrown-arbor", Name = "Overgrown arbor", Tags = ["plant", "fey"], MinSide = 2, MaxSide = 3, Weight = 12 },

        // Elementals, constructs and dragonkin
        new() { Id = "rune-circle", Name = "Rune circle", Tags = ["elemental", "construct"], MinSide = 2, MaxSide = 3, Weight = 10, MaxPerSettlement = 2 },
        new() { Id = "scale-shrine", Name = "Scale shrine", Tags = ["dragonkin"], MinSide = 2, MaxSide = 3, Weight = 10, MaxPerSettlement = 2 },
        new() { Id = "hoard-cairn", Name = "Hoard cairn", Tags = ["dragonkin"], MinSide = 2, MaxSide = 2, Weight = 6, MaxPerSettlement = 1 },
    ];

    public IReadOnlyList<RealmObjectiveTemplate> RealmObjectives { get; } =
    [
        new("clear-the-realm", "Clear the realm", "Defeat or drive off every hostile group in the realm.", 6, ObjectiveRequirement.None),
        new("chart-every-floor", "Chart every floor", "Walk every room on every floor and record it. A Cartographer's favorite.", 6, ObjectiveRequirement.DungeonFloors),
    ];

    public IReadOnlyList<TrapTemplate> TrapTemplates { get; } =
    [
        new("pressure-plate-darts", "Pressure plate (darts)", [RoomStyle.Masonry]),
        new("rune-ward", "Rune ward", [RoomStyle.Masonry]),
        new("swinging-blade", "Swinging blade", [RoomStyle.Masonry, RoomStyle.Mine]),
        new("tripwire-rockfall", "Tripwire rockfall", [RoomStyle.Mine, RoomStyle.Cave]),
        new("weak-floor", "Weak floor", [RoomStyle.Mine]),
        new("spore-puff", "Spore puff", [RoomStyle.Cave]),
        new("hidden-pit", "Hidden pit", []),
        new("snare", "Snare", []),
    ];
}
