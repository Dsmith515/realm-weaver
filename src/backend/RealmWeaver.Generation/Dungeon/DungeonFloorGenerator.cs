using System.Globalization;
using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Events;
using RealmWeaver.Generation.Grid;
using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Randomness;

namespace RealmWeaver.Generation.Dungeon;

/// <summary>Inputs for one dungeon floor.</summary>
/// <param name="EntrancePoint">Where the party arrives (host structure or stairs from the floor above). Random when null.</param>
/// <param name="Focus">Area the core rooms cluster around, e.g. under the host structure's footprint.</param>
public sealed record FloorContext(
    GenerationRequest Request,
    IContentCatalog Catalog,
    DungeonTheme Theme,
    int Width,
    int Height,
    int FloorNumber,
    int FloorCount,
    GridPoint? EntrancePoint,
    GridRect? Focus,
    string EntranceDescription);

/// <summary>
/// Builds one dungeon floor: room boxes, a connected room graph with loops, material zones, room shapes,
/// corridors with doors, stairs or boss room, secret rooms, events, chests and traps.
/// </summary>
public static class DungeonFloorGenerator
{
    private const int MaxRooms = 80;

    public static RealmArea Generate(FloorContext ctx, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(rng);

        var request = ctx.Request;
        var effectiveArea = Math.Max(1, ctx.Width * ctx.Height * 6 / 10);
        var eventRng = rng.Fork($"events/{request.EncounterVariant}");
        var eventCount = EventBudget.Resolve(ctx.Width * ctx.Height, request.FloorEventCount, eventRng.Fork("count"), defaultArea: effectiveArea);

        // Layout depends only on the seed and size, so a mismatched map keeps its rooms.
        var layoutRng = rng.Fork("layout");
        var target = Math.Min(MaxRooms, Math.Max(EventBudget.OverrideCap(ctx.Width * ctx.Height) + 2, ctx.Width * ctx.Height / 90));
        // The minimum uses the largest event count this floor could roll, so the layout never depends on the roll.
        var mostEvents = request.FloorEventCount ?? EventBudget.DefaultRange(effectiveArea).Max;
        var boxes = LayoutRooms(layoutRng.Fork("boxes"), ctx, target, minimum: mostEvents + 1);
        var rooms = boxes.Select((b, i) => new RoomNode(i, b)).ToList();
        rooms[0].IsEntrance = true;

        var edges = RoomGraph.SpanningTree(rooms);
        var floorIndex = ctx.FloorNumber - 1;
        double[] primaryShares = [0.75, 0.55, 0.4];
        RoomGraph.AssignMaterials(
            rooms, ctx.Theme.PrimaryMaterial, ctx.Theme.BranchMaterials,
            primaryShares[Math.Min(floorIndex, primaryShares.Length - 1)],
            coreDepth: ctx.FloorNumber == 1 ? 2 : 1,
            layoutRng.Fork("materials"));
        var maxSide = boxes.Max(b => Math.Max(b.Width, b.Height));
        edges.AddRange(RoomGraph.AddLoops(rooms, maxSide * 4, layoutRng.Fork("loops")));

        var map = new TerrainMap(ctx.Width, ctx.Height, Terrain.Wall);
        var shapeRng = layoutRng.Fork("shapes");
        foreach (var room in rooms)
        {
            room.Floor = RoomShapes.Carve(room.Box, room.Style, shapeRng);
            foreach (var p in room.Floor)
            {
                map[p] = FloorTerrain(room.Material, shapeRng);
            }

            room.Anchor = room.Floor.OrderBy(p => p.ManhattanDistanceTo(room.Box.Center)).ThenBy(p => p.Y).ThenBy(p => p.X).First();
        }

        var spawn = ConnectEntrance(map, rooms[0], ctx.EntrancePoint);
        var corridorTiles = new HashSet<GridPoint>();
        foreach (var (a, b) in edges)
        {
            CorridorCarver.Connect(map, rooms, rooms[a], rooms[b], corridorTiles);
        }

        // Stairs down or the boss room go in the room farthest from the entrance.
        var distances = RoomGraph.DistancesFrom(rooms, 0);
        var farthest = rooms.OrderByDescending(r => distances[r.Index]).ThenBy(r => r.Index).First();
        var isDeepest = ctx.FloorNumber == ctx.FloorCount;
        if (isDeepest)
        {
            farthest.IsBossRoom = true;
        }
        else
        {
            farthest.HasStairsDown = true;
        }

        var featureRng = eventRng.Fork("features");
        ChooseSecretRooms(map, rooms, eventCount, request.Difficulty, featureRng.Fork("secrets"));

        var events = DungeonEventPlacer.Place(ctx, rooms, eventCount, isDeepest, eventRng.Fork("place"));
        var features = PlaceFeatures(ctx, map, rooms, events, corridorTiles, spawn, featureRng);

        VerifyReachable(map, spawn, rooms);

        return new RealmArea
        {
            Id = $"floor-{ctx.FloorNumber}",
            Kind = AreaKind.DungeonFloor,
            Name = $"{ctx.Theme.HostName} depths - floor {ctx.FloorNumber}",
            FloorNumber = ctx.FloorNumber,
            Width = ctx.Width,
            Height = ctx.Height,
            TerrainRows = map.ToRows(),
            Spawn = spawn,
            Events = events,
            Features = features,
            Rooms = rooms.Select(r => new DungeonRoom
            {
                Id = r.Id,
                Bounds = r.Box,
                Material = r.Material,
                IsTransition = r.IsTransition,
                TransitionFrom = r.TransitionFrom,
                Connections = r.Neighbors.Order().Select(n => rooms[n].Id).ToList(),
                IsEntrance = r.IsEntrance,
                IsSecret = r.IsSecret,
                HasStairsDown = r.HasStairsDown,
                IsBossRoom = r.IsBossRoom,
                Anchor = r.Anchor,
            }).ToList(),
        };
    }

    private static List<GridRect> LayoutRooms(SeededRandom rng, FloorContext ctx, int target, int minimum)
    {
        // Prefer a two-tile wall between rooms; tight floors fall back to one tile and smaller rooms.
        var baseMax = Math.Clamp(Math.Min(ctx.Width, ctx.Height) / 5, 4, 10);
        var best = new List<GridRect>();
        foreach (var gap in new[] { 2, 1 })
        {
            for (var maxSide = baseMax; maxSide >= 3; maxSide--)
            {
                var rooms = TryLayout(rng.Fork($"{gap}/{maxSide}"), ctx, target, maxSide, gap);
                if (rooms.Count >= minimum)
                {
                    return rooms;
                }

                if (rooms.Count > best.Count)
                {
                    best = rooms;
                }
            }
        }

        throw new RealmGenerationException(
            $"Only {best.Count} rooms fit on a {ctx.Width}x{ctx.Height} floor but {minimum} are needed. Try fewer events or a larger map.");
    }

    private static List<GridRect> TryLayout(SeededRandom rng, FloorContext ctx, int target, int maxSide, int gap)
    {
        int w = ctx.Width, h = ctx.Height;
        var rooms = new List<GridRect>();

        if (ctx.EntrancePoint is { } entrance)
        {
            var size = Math.Clamp(maxSide, 3, 5);
            var x = Math.Clamp(entrance.X - (size / 2), 1, w - 1 - size);
            var y = Math.Clamp(entrance.Y - (size / 2), 1, h - 1 - size);
            rooms.Add(new GridRect(x, y, size, size));
        }

        var attempts = target * 150;
        for (var i = 0; i < attempts && rooms.Count < target; i++)
        {
            var bw = rng.NextInclusive(3, maxSide);
            var bh = rng.NextInclusive(3, maxSide);
            int x, y;

            // Early on, rooms cluster around the focus (for example under the castle) to form the core.
            if (ctx.Focus is { } focus && rooms.Count < Math.Max(3, target / 4) && i < attempts / 5)
            {
                var region = focus.Expand(4);
                x = Math.Clamp(rng.NextInclusive(region.X, region.Right), 1, w - 1 - bw);
                y = Math.Clamp(rng.NextInclusive(region.Y, region.Bottom), 1, h - 1 - bh);
            }
            else
            {
                x = rng.NextInclusive(1, w - 1 - bw);
                y = rng.NextInclusive(1, h - 1 - bh);
            }

            var candidate = new GridRect(x, y, bw, bh);
            var padded = candidate.Expand(gap);
            if (rooms.TrueForAll(r => !padded.Intersects(r)))
            {
                rooms.Add(candidate);
            }
        }

        return rooms;
    }

    /// <summary>Makes sure the arrival point is floor and joined to the entrance room.</summary>
    private static GridPoint ConnectEntrance(TerrainMap map, RoomNode entrance, GridPoint? point)
    {
        if (point is not { } p || entrance.Floor.Contains(p))
        {
            return point ?? entrance.Anchor;
        }

        var floor = DungeonMaterials.Of(entrance.Material).Floor;
        var step = p;
        while (step != entrance.Anchor)
        {
            map[step] = floor;
            entrance.Floor.Add(step);
            step = step.X != entrance.Anchor.X
                ? step with { X = step.X + Math.Sign(entrance.Anchor.X - step.X) }
                : step with { Y = step.Y + Math.Sign(entrance.Anchor.Y - step.Y) };
        }

        return p;
    }

    /// <summary>Some dead-end rooms become secret rooms behind a hidden door. Only rooms not needed for events are used.</summary>
    private static void ChooseSecretRooms(TerrainMap map, List<RoomNode> rooms, int eventCount, int difficulty, SeededRandom rng)
    {
        var spare = rooms.Count - 1 - eventCount;
        var candidates = rooms
            .Where(r => r.Neighbors.Count == 1 && !r.IsEntrance && !r.HasStairsDown && !r.IsBossRoom)
            .ToList();
        rng.Shuffle(candidates);

        var count = Math.Min(Math.Min(spare, candidates.Count), rng.NextInclusive(0, 1 + (difficulty / 4)));
        foreach (var room in candidates.Take(Math.Max(0, count)))
        {
            room.IsSecret = true;
            map[room.Doors[room.Neighbors.Single()]] = Terrain.Door;
        }
    }

    private static List<PlacedFeature> PlaceFeatures(
        FloorContext ctx, TerrainMap map, List<RoomNode> rooms, IReadOnlyList<PlacedEvent> events,
        HashSet<GridPoint> corridorTiles, GridPoint spawn, SeededRandom rng)
    {
        var features = new List<PlacedFeature>();
        var used = new HashSet<GridPoint> { spawn };
        var counters = new Dictionary<FeatureKind, int>();

        void Add(FeatureKind kind, GridPoint position, string description, string? roomId = null, string? templateId = null)
        {
            var n = counters.GetValueOrDefault(kind) + 1;
            counters[kind] = n;
            features.Add(new PlacedFeature(FeaturePrefix(kind) + n.ToString(CultureInfo.InvariantCulture), kind, position, description)
            {
                RoomId = roomId,
                TemplateId = templateId,
            });
            used.Add(position);
        }

        var entranceKind = ctx.FloorNumber == 1 ? FeatureKind.DungeonEntrance : FeatureKind.StairsUp;
        Add(entranceKind, spawn, ctx.EntranceDescription, rooms[0].Id);

        foreach (var room in rooms)
        {
            if (room.HasStairsDown)
            {
                Add(FeatureKind.StairsDown, room.Anchor, $"Stairs down to floor {ctx.FloorNumber + 1}", room.Id);
            }

            if (room.IsSecret)
            {
                var door = room.Doors[room.Neighbors.Single()];
                Add(FeatureKind.SecretDoor, door, $"Hidden door into {room.Id}", room.Id);
            }
        }

        // Event rooms are off limits for chests and traps so placements never overlap.
        var eventRects = events.Select(e => e.Footprint).ToList();
        bool InEvent(GridPoint p) => eventRects.Exists(r => r.Contains(p));
        var claimed = events.SelectMany(e => e.RoomIds).ToHashSet(StringComparer.Ordinal);
        var freeRooms = rooms.Where(r => !r.IsEntrance && !claimed.Contains(r.Id)).ToList();

        GridPoint? FreeTileIn(RoomNode room)
        {
            var tiles = room.Floor.Where(p => !used.Contains(p) && !InEvent(p) && p != room.Anchor && map.IsPassable(p)).OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
            return tiles.Count == 0 ? null : rng.Pick(tiles);
        }

        // Chests: one in every secret room, plus a few in other free rooms (dead ends first).
        var chestRooms = freeRooms.Where(r => r.IsSecret).ToList();
        var others = freeRooms.Where(r => !r.IsSecret).OrderBy(r => r.Neighbors.Count).ThenBy(r => r.Index).ToList();
        chestRooms.AddRange(others.Take(Math.Min(others.Count, 1 + (rooms.Count / 8) + rng.NextInclusive(0, 1))));
        foreach (var room in chestRooms)
        {
            if (FreeTileIn(room) is { } tile)
            {
                var tier = room.IsSecret ? "Hidden chest (rare)" : "Chest";
                Add(FeatureKind.Chest, tile, tier, room.Id);
            }
        }

        // Traps: in corridors and free rooms, never next to the arrival point.
        var trapTiles = corridorTiles
            .Concat(freeRooms.SelectMany(r => r.Floor))
            .Where(p => !used.Contains(p) && !InEvent(p) && map[p] != Terrain.Door && map.IsPassable(p) && p.ChebyshevDistanceTo(spawn) > 1)
            .Distinct()
            .OrderBy(p => p.Y).ThenBy(p => p.X)
            .ToList();
        rng.Shuffle(trapTiles);
        var trapCount = Math.Min(trapTiles.Count, Math.Clamp(1 + (rooms.Count / 5) + (ctx.Request.Threat / 4), 1, 25));
        var roomOf = rooms.SelectMany(r => r.Floor.Select(p => (p, r))).GroupBy(x => x.p).ToDictionary(g => g.Key, g => g.First().r);
        foreach (var tile in trapTiles.Take(trapCount))
        {
            var room = roomOf.GetValueOrDefault(tile);
            var style = room?.Style ?? DungeonMaterials.Of(ctx.Theme.PrimaryMaterial).Style;
            var options = ctx.Catalog.TrapTemplates.Where(t => t.Styles.Count == 0 || t.Styles.Contains(style)).ToList();
            var trap = rng.Pick(options);
            Add(FeatureKind.Trap, tile, trap.Name, room?.Id, trap.Id);
        }

        return features;
    }

    private static string FeaturePrefix(FeatureKind kind) => kind switch
    {
        FeatureKind.Trap => "T",
        FeatureKind.Chest => "C",
        FeatureKind.SecretDoor => "H",
        FeatureKind.StairsDown => "D",
        FeatureKind.StairsUp => "U",
        FeatureKind.DungeonEntrance => "X",
        _ => "L",
    };

    private static Terrain FloorTerrain(DungeonMaterial material, SeededRandom rng)
    {
        var floor = DungeonMaterials.Of(material).Floor;
        var rubbly = material is DungeonMaterial.CrumblingBrick or DungeonMaterial.NaturalCave or DungeonMaterial.CrystalVein;
        return rubbly && rng.Chance(0.08) ? Terrain.Rubble : floor;
    }

    private static void VerifyReachable(TerrainMap map, GridPoint spawn, List<RoomNode> rooms)
    {
        var seen = new HashSet<GridPoint> { spawn };
        var queue = new Queue<GridPoint>([spawn]);
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

        var unreachable = rooms.Where(r => !seen.Contains(r.Anchor)).Select(r => r.Id).ToList();
        if (unreachable.Count > 0)
        {
            throw new RealmGenerationException($"Internal error: rooms {string.Join(", ", unreachable)} are unreachable.");
        }
    }
}
