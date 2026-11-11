using RealmWeaver.Generation.Grid;
using RealmWeaver.Generation.Model;

namespace RealmWeaver.Generation.Dungeon;

/// <summary>Carves a corridor between two rooms that stays out of every other room's bounding box.</summary>
internal static class CorridorCarver
{
    public static void Connect(TerrainMap map, List<RoomNode> rooms, RoomNode a, RoomNode b, HashSet<GridPoint> corridorTiles)
    {
        var path = FindPath(map, rooms, a, b, avoidOthers: true) ?? FindPath(map, rooms, a, b, avoidOthers: false)
            ?? throw new RealmGenerationException($"Internal error: no corridor between {a.Id} and {b.Id}.");

        // Corridors between materials are built like the room that is not the transition.
        var material = a.Material == b.Material ? a.Material : (a.IsTransition ? b.Material : a.Material);
        var floor = DungeonMaterials.Of(material).Floor;

        foreach (var p in path)
        {
            if (!a.Floor.Contains(p) && !b.Floor.Contains(p))
            {
                if (map[p] == Terrain.Wall)
                {
                    map[p] = floor;
                }

                corridorTiles.Add(p);
            }
        }

        var doorA = path.First(p => !a.Floor.Contains(p));
        var doorB = path.Last(p => !b.Floor.Contains(p));
        a.Doors[b.Index] = doorA;
        b.Doors[a.Index] = doorB;
        if (a.Style == RoomStyle.Masonry)
        {
            map[doorA] = Terrain.Door;
        }

        if (b.Style == RoomStyle.Masonry)
        {
            map[doorB] = Terrain.Door;
        }
    }

    /// <summary>Breadth-first search from anchor to anchor, staying one tile inside the map edge.</summary>
    private static List<GridPoint>? FindPath(TerrainMap map, List<RoomNode> rooms, RoomNode a, RoomNode b, bool avoidOthers)
    {
        bool Allowed(GridPoint p)
        {
            if (p.X < 1 || p.Y < 1 || p.X > map.Width - 2 || p.Y > map.Height - 2)
            {
                return false;
            }

            if (!avoidOthers || a.Box.Contains(p) || b.Box.Contains(p))
            {
                return true;
            }

            return !rooms.Exists(r => r.Box.Contains(p));
        }

        var previous = new Dictionary<GridPoint, GridPoint> { [a.Anchor] = a.Anchor };
        var queue = new Queue<GridPoint>([a.Anchor]);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            if (p == b.Anchor)
            {
                var path = new List<GridPoint>();
                for (var step = p; step != a.Anchor; step = previous[step])
                {
                    path.Add(step);
                }

                path.Add(a.Anchor);
                path.Reverse();
                return path;
            }

            foreach (var n in map.Neighbors4(p))
            {
                if (!previous.ContainsKey(n) && Allowed(n))
                {
                    previous[n] = p;
                    queue.Enqueue(n);
                }
            }
        }

        return null;
    }
}
