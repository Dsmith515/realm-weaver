using RealmWeaver.Generation.Grid;
using RealmWeaver.Generation.Model;

namespace RealmWeaver.Generation.Field;

/// <summary>Makes all passable tiles of a map one connected region (4-way movement).</summary>
public static class Connectivity
{
    /// <summary>Pockets smaller than this are filled in rather than connected with a path.</summary>
    private const int MinPocketSize = 6;

    public static void Ensure(TerrainMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        while (true)
        {
            var regions = PassableRegions(map);
            if (regions.Count <= 1)
            {
                return;
            }

            var main = regions.MaxBy(r => r.Count)!;
            var mainSet = main.ToHashSet();

            foreach (var region in regions)
            {
                if (ReferenceEquals(region, main))
                {
                    continue;
                }

                if (region.Count < MinPocketSize)
                {
                    Fill(map, region);
                }
                else
                {
                    CarvePath(map, region, mainSet);
                }
            }
        }
    }

    /// <summary>Connected components of passable tiles, largest first is not guaranteed.</summary>
    public static List<List<GridPoint>> PassableRegions(TerrainMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        var seen = new bool[map.Area];
        var regions = new List<List<GridPoint>>();

        foreach (var start in map.AllTiles())
        {
            if (seen[Index(map, start)] || !map.IsPassable(start))
            {
                continue;
            }

            var region = new List<GridPoint>();
            var queue = new Queue<GridPoint>();
            queue.Enqueue(start);
            seen[Index(map, start)] = true;

            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                region.Add(p);
                foreach (var n in map.Neighbors4(p))
                {
                    if (!seen[Index(map, n)] && map.IsPassable(n))
                    {
                        seen[Index(map, n)] = true;
                        queue.Enqueue(n);
                    }
                }
            }

            regions.Add(region);
        }

        return regions;
    }

    private static void Fill(TerrainMap map, List<GridPoint> region)
    {
        foreach (var p in region)
        {
            var blocker = map.Neighbors4(p).Select(n => map[n]).FirstOrDefault(t => !TerrainInfo.IsPassable(t), Terrain.Rock);
            map[p] = blocker;
        }
    }

    /// <summary>Breadth-first search from the pocket to the nearest main-region tile, then carve that path.</summary>
    private static void CarvePath(TerrainMap map, List<GridPoint> region, HashSet<GridPoint> main)
    {
        var previous = new Dictionary<GridPoint, GridPoint>();
        var queue = new Queue<GridPoint>();
        foreach (var p in region)
        {
            previous[p] = p;
            queue.Enqueue(p);
        }

        GridPoint? target = null;
        while (queue.Count > 0 && target is null)
        {
            var p = queue.Dequeue();
            foreach (var n in map.Neighbors4(p))
            {
                if (previous.ContainsKey(n))
                {
                    continue;
                }

                previous[n] = p;
                if (main.Contains(n))
                {
                    target = n;
                    break;
                }

                queue.Enqueue(n);
            }
        }

        if (target is null)
        {
            return;
        }

        var step = target.Value;
        while (previous[step] != step)
        {
            if (!map.IsPassable(step))
            {
                map[step] = BiomePalette.Carve(map[step]);
            }

            step = previous[step];
        }
    }

    private static int Index(TerrainMap map, GridPoint p) => (p.Y * map.Width) + p.X;
}
