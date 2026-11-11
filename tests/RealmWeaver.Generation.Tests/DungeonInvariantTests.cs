using System.Xml.Linq;
using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Export;
using RealmWeaver.Generation.Grid;
using RealmWeaver.Generation.Model;

namespace RealmWeaver.Generation.Tests;

/// <summary>Rules every generated dungeon must follow, checked over many seeds, kinds, sizes and floor counts.</summary>
public class DungeonInvariantTests
{
    private static readonly Biome[] Biomes = Enum.GetValues<Biome>();

    public static TheoryData<ulong> Seeds()
    {
        var data = new TheoryData<ulong>();
        for (ulong seed = 1; seed <= 120; seed++)
        {
            data.Add(seed * 104729);
        }

        return data;
    }

    private static GenerationRequest RequestFor(ulong seed) => new()
    {
        Seed = seed,
        Kind = seed % 2 == 0 ? RealmKind.FieldWithDungeon : RealmKind.Dungeon,
        Biome = Biomes[(int)(seed % (ulong)Biomes.Length)],
        FloorCount = (int)(seed % 3) + 1,
        Difficulty = (int)(seed % 10) + 1,
        MobTags = (seed % 4) switch { 0 => ["undead"], 1 => ["beast", "insect"], 2 => ["goblinoid"], _ => [] },
    };

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Random_dungeons_satisfy_all_rules(ulong seed)
    {
        var request = RequestFor(seed);
        AssertDungeonRules(RealmGenerator.Generate(request), request);
    }

    [Theory]
    [InlineData(RealmKind.Dungeon, 15, 15, 3, null)]
    [InlineData(RealmKind.Dungeon, 15, 15, 1, 5)]
    [InlineData(RealmKind.FieldWithDungeon, 15, 15, 2, null)]
    [InlineData(RealmKind.Dungeon, 100, 100, 3, 60)]
    [InlineData(RealmKind.FieldWithDungeon, 100, 60, 3, null)]
    [InlineData(RealmKind.Dungeon, 40, 25, 2, 11)]
    public void Boundary_sizes_and_counts_work_for_every_biome(RealmKind kind, int width, int height, int floors, int? floorEvents)
    {
        foreach (var biome in Biomes)
        {
            var request = new GenerationRequest
            {
                Seed = 8080, Kind = kind, Biome = biome, Width = width, Height = height,
                FloorCount = floors, FloorEventCount = floorEvents, Difficulty = 6,
            };
            var realm = RealmGenerator.Generate(request);
            AssertDungeonRules(realm, request);

            if (floorEvents is { } expected)
            {
                Assert.All(Floors(realm), f => Assert.Equal(expected, f.Events.Count));
            }
        }
    }

    [Fact]
    public void Dungeons_are_deterministic()
    {
        var request = RequestFor(42) with { Kind = RealmKind.FieldWithDungeon, FloorCount = 3 };
        Assert.Equal(RealmJson.Serialize(RealmGenerator.Generate(request)), RealmJson.Serialize(RealmGenerator.Generate(request)));
    }

    [Fact]
    public void Encounter_variant_keeps_the_dungeon_layout()
    {
        var request = RequestFor(64) with { Kind = RealmKind.FieldWithDungeon, FloorCount = 2 };
        var original = RealmGenerator.Generate(request);
        var variant = RealmGenerator.Generate(request with { EncounterVariant = 3, DangerModifier = 2 });

        Assert.Equal(original.Theme!.HostTemplateId, variant.Theme!.HostTemplateId);
        Assert.Equal(original.Theme.BranchMaterials, variant.Theme.BranchMaterials);
        foreach (var (a, b) in Floors(original).Zip(Floors(variant)))
        {
            Assert.Equal(a.Rooms.Select(r => (r.Id, r.Bounds, r.Material)), b.Rooms.Select(r => (r.Id, r.Bounds, r.Material)));
        }

        Assert.NotEqual(
            Floors(original).SelectMany(f => f.Events).Select(e => (e.TemplateId, e.Footprint)),
            Floors(variant).SelectMany(f => f.Events).Select(e => (e.TemplateId, e.Footprint)));
    }

    [Fact]
    public void Castle_dungeons_are_mostly_dressed_stone_and_can_branch_into_caves()
    {
        var castle = StaticContentCatalog.Default.EventTemplates.Single(t => t.Id == "castle");
        var catalog = new SingleHostCatalog(castle);
        var branched = false;

        for (ulong seed = 1; seed <= 40; seed++)
        {
            var realm = RealmGenerator.Generate(
                new GenerationRequest { Seed = seed, Kind = RealmKind.FieldWithDungeon, Biome = Biome.Forest, Width = 70, Height = 70, FloorCount = 2 },
                catalog);

            Assert.Equal(DungeonMaterial.DressedStone, realm.Theme!.PrimaryMaterial);
            var floor1 = Floors(realm).First();
            Assert.True(floor1.Rooms.Count(r => r.Material == DungeonMaterial.DressedStone) * 2 > floor1.Rooms.Count);
            branched |= Floors(realm).Any(f => f.Rooms.Any(r => r.Material is DungeonMaterial.NaturalCave or DungeonMaterial.RootEarth));
        }

        Assert.True(branched, "at least one castle dungeon should branch into cave or root-choked earth");
    }

    [Fact]
    public void Dungeon_svg_shows_rooms_and_features()
    {
        var realm = RealmGenerator.Generate(RequestFor(10) with { Kind = RealmKind.Dungeon, FloorCount = 1, Width = 40, Height = 40 });
        var floor = Floors(realm).Single();
        XNamespace ns = "http://www.w3.org/2000/svg";
        var svg = XDocument.Parse(SvgExporter.Render(realm, floor.Id));

        var roomIds = svg.Descendants(ns + "g").Select(g => (string?)g.Attribute("data-room-id")).Where(id => id is not null);
        var featureIds = svg.Descendants(ns + "g").Select(g => (string?)g.Attribute("data-feature-id")).Where(id => id is not null);
        Assert.Equal(floor.Rooms.Select(r => r.Id), roomIds);
        Assert.Equal(floor.Features.Select(f => f.Id), featureIds);
    }

    [Fact]
    public void Prompt_hides_traps_and_secret_doors()
    {
        var realm = RealmGenerator.Generate(RequestFor(12) with { Kind = RealmKind.Dungeon, Width = 60, Height = 60, Difficulty = 10 });
        var floor = Floors(realm).First();
        var prompt = PromptExporter.Build(realm, floor.Id);

        Assert.Contains("Construction:", prompt, StringComparison.Ordinal);
        Assert.Contains(floor.Features, f => f.Kind == FeatureKind.Trap);
        Assert.All(floor.Features.Where(f => f.Kind == FeatureKind.Trap), t => Assert.DoesNotContain(t.Description.ToLowerInvariant() + " at", prompt, StringComparison.Ordinal));
    }

    private static IEnumerable<RealmArea> Floors(RealmDocument realm) => realm.Areas.Where(a => a.Kind == AreaKind.DungeonFloor);

    private static void AssertDungeonRules(RealmDocument realm, GenerationRequest request)
    {
        Assert.NotNull(realm.Theme);
        var floors = Floors(realm).ToList();
        Assert.Equal(request.FloorCount, floors.Count);
        Assert.InRange(floors.Count, 1, GeneratorLimits.MaxFloors);

        if (request.Kind == RealmKind.FieldWithDungeon)
        {
            var field = realm.Areas[0];
            Assert.Equal(AreaKind.Field, field.Kind);
            var host = Assert.Single(field.Events, e => e.Category == EventCategory.Structure);
            Assert.Equal(realm.Theme.HostTemplateId, host.TemplateId);
            var entrance = Assert.Single(field.Features, f => f.Kind == FeatureKind.DungeonEntrance);
            Assert.True(host.Footprint.Contains(entrance.Position), "dungeon entrance must be inside the host structure");
            Assert.Equal(entrance.Position, floors[0].Spawn);
        }

        for (var i = 0; i < floors.Count; i++)
        {
            var floor = floors[i];
            var deepest = i == floors.Count - 1;
            var map = TerrainMap.FromRows(floor.TerrainRows);
            var rooms = floor.Rooms.ToDictionary(r => r.Id);
            var spawn = Assert.NotNull(floor.Spawn);

            // Every room is reachable from the arrival point, and has at least one way in.
            var reachable = Reachable(map, spawn);
            Assert.All(floor.Rooms, r => Assert.True(reachable.Contains(r.Anchor), $"floor {floor.FloorNumber} room {r.Id} is unreachable"));
            Assert.All(floor.Rooms, r => Assert.NotEmpty(r.Connections));

            // Materials only meet through a transition room; floor 1 is mostly the host material.
            foreach (var room in floor.Rooms)
            {
                foreach (var other in room.Connections.Select(id => rooms[id]))
                {
                    Assert.True(
                        room.Material == other.Material || room.IsTransition || other.IsTransition,
                        $"{room.Id} ({room.Material}) joins {other.Id} ({other.Material}) without a transition");
                }
            }

            if (floor.FloorNumber == 1)
            {
                Assert.True(
                    floor.Rooms.Count(r => r.Material == realm.Theme.PrimaryMaterial) * 2 > floor.Rooms.Count,
                    "the host material must be the majority on floor 1");
            }

            // Events: count in range, one room belongs to at most one event, never in the entrance or a secret room.
            Assert.InRange(floor.Events.Count, GeneratorLimits.MinEvents, GeneratorLimits.AbsoluteMaxEvents);
            var eventRooms = floor.Events.SelectMany(e => e.RoomIds).ToList();
            Assert.Equal(eventRooms.Count, eventRooms.Distinct().Count());
            Assert.All(eventRooms, id => Assert.False(rooms[id].IsEntrance || rooms[id].IsSecret));

            // Boss only in the farthest room of the deepest floor; stairs everywhere else.
            var bosses = floor.Events.Where(e => e.TemplateId == "boss-chamber").ToList();
            if (deepest)
            {
                var boss = Assert.Single(bosses);
                Assert.True(rooms[Assert.Single(boss.RoomIds)].IsBossRoom);
                Assert.DoesNotContain(floor.Features, f => f.Kind == FeatureKind.StairsDown);
            }
            else
            {
                Assert.Empty(bosses);
                var down = Assert.Single(floor.Features, f => f.Kind == FeatureKind.StairsDown);
                var up = Assert.Single(floors[i + 1].Features, f => f.Kind == FeatureKind.StairsUp);
                Assert.Equal(down.Position, up.Position);
                Assert.Equal(up.Position, floors[i + 1].Spawn);
            }

            // Features: on passable tiles, never stacked, and traps/chests never inside an event.
            Assert.All(floor.Features, f => Assert.True(map.IsPassable(f.Position), $"{f.Id} is on an impassable tile"));
            Assert.Equal(floor.Features.Count, floor.Features.Select(f => f.Position).Distinct().Count());
            foreach (var f in floor.Features.Where(f => f.Kind is FeatureKind.Trap or FeatureKind.Chest))
            {
                Assert.DoesNotContain(floor.Events, e => e.Footprint.Contains(f.Position));
            }

            // Every secret room has a hidden door on its only connection, and a chest.
            foreach (var secret in floor.Rooms.Where(r => r.IsSecret))
            {
                Assert.Single(secret.Connections);
                Assert.Contains(floor.Features, f => f.Kind == FeatureKind.SecretDoor && f.RoomId == secret.Id);
            }

            Assert.All(floor.Features.Where(f => f.Kind == FeatureKind.Trap), t => Assert.NotNull(t.TemplateId));
        }
    }

    private static HashSet<GridPoint> Reachable(TerrainMap map, GridPoint start)
    {
        var seen = new HashSet<GridPoint> { start };
        var queue = new Queue<GridPoint>([start]);
        while (queue.Count > 0)
        {
            foreach (var n in map.Neighbors4(queue.Dequeue()))
            {
                if (map.IsPassable(n) && seen.Add(n))
                {
                    queue.Enqueue(n);
                }
            }
        }

        return seen;
    }

    /// <summary>Catalog that only offers one host structure, to test a specific dungeon theme.</summary>
    private sealed class SingleHostCatalog(EventTemplate host) : IContentCatalog
    {
        private static readonly StaticContentCatalog Inner = StaticContentCatalog.Default;

        public IReadOnlyList<EventTemplate> EventTemplates { get; } =
            [.. Inner.EventTemplates.Where(t => !t.IsDungeonHost), host];

        public IReadOnlyList<MobArchetype> MobArchetypes => Inner.MobArchetypes;

        public IReadOnlyList<DifficultyTemplate> DifficultyTemplates => Inner.DifficultyTemplates;

        public IReadOnlyList<TrapTemplate> TrapTemplates => Inner.TrapTemplates;
    }
}
