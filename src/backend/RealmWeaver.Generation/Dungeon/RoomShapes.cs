using RealmWeaver.Generation.Grid;
using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Randomness;

namespace RealmWeaver.Generation.Dungeon;

/// <summary>Picks which tiles inside a room's bounding box are floor, according to the room's style.</summary>
public static class RoomShapes
{
    public static HashSet<GridPoint> Carve(GridRect box, RoomStyle style, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var tiles = style switch
        {
            RoomStyle.Masonry => Masonry(box, rng),
            RoomStyle.Mine => Shaft(box, rng),
            _ => Cave(box, rng),
        };

        return tiles.Count > 0 ? tiles : [box.Center];
    }

    /// <summary>A rectangle, sometimes with one corner removed to make an L-shape.</summary>
    private static HashSet<GridPoint> Masonry(GridRect box, SeededRandom rng)
    {
        var tiles = box.Tiles().ToHashSet();
        if (box.Width >= 5 && box.Height >= 5 && rng.Chance(0.45))
        {
            var cutW = rng.NextInclusive(2, box.Width / 2);
            var cutH = rng.NextInclusive(2, box.Height / 2);
            var cutX = rng.Chance(0.5) ? box.X : box.Right - cutW + 1;
            var cutY = rng.Chance(0.5) ? box.Y : box.Bottom - cutH + 1;
            tiles.ExceptWith(new GridRect(cutX, cutY, cutW, cutH).Tiles());
        }

        return tiles;
    }

    /// <summary>A long shaft through the box, often with a side gallery.</summary>
    private static HashSet<GridPoint> Shaft(GridRect box, SeededRandom rng)
    {
        var tiles = new HashSet<GridPoint>();
        var horizontal = box.Width >= box.Height;
        var thickness = Math.Min(rng.NextInclusive(1, 2), horizontal ? box.Height : box.Width);

        if (horizontal)
        {
            var y = box.Y + rng.NextInt(box.Height - thickness + 1);
            tiles.UnionWith(new GridRect(box.X, y, box.Width, thickness).Tiles());
            if (box.Height >= 3 && rng.Chance(0.7))
            {
                var x = box.X + rng.NextInt(box.Width);
                tiles.UnionWith(new GridRect(x, box.Y, 1, box.Height).Tiles());
            }
        }
        else
        {
            var x = box.X + rng.NextInt(box.Width - thickness + 1);
            tiles.UnionWith(new GridRect(x, box.Y, thickness, box.Height).Tiles());
            if (box.Width >= 3 && rng.Chance(0.7))
            {
                var y = box.Y + rng.NextInt(box.Height);
                tiles.UnionWith(new GridRect(box.X, y, box.Width, 1).Tiles());
            }
        }

        return tiles;
    }

    /// <summary>Cellular-automata blob; falls back to an ellipse if the result is too small.</summary>
    private static HashSet<GridPoint> Cave(GridRect box, SeededRandom rng)
    {
        int w = box.Width, h = box.Height;
        var floor = new bool[w, h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                floor[x, y] = rng.Chance(0.62);
            }
        }

        for (var step = 0; step < 4; step++)
        {
            var next = new bool[w, h];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var walls = 0;
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h || !floor[nx, ny])
                            {
                                walls++;
                            }
                        }
                    }

                    next[x, y] = walls < 5;
                }
            }

            floor = next;
        }

        var largest = LargestRegion(floor, w, h).Select(p => new GridPoint(box.X + p.X, box.Y + p.Y)).ToHashSet();
        return largest.Count >= Math.Max(4, box.Area * 0.3) ? largest : Ellipse(box);
    }

    private static HashSet<GridPoint> Ellipse(GridRect box)
    {
        var rx = box.Width / 2.0;
        var ry = box.Height / 2.0;
        var cx = box.X + rx - 0.5;
        var cy = box.Y + ry - 0.5;
        return box.Tiles()
            .Where(p => Math.Pow((p.X - cx) / rx, 2) + Math.Pow((p.Y - cy) / ry, 2) <= 1.0)
            .ToHashSet();
    }

    private static List<GridPoint> LargestRegion(bool[,] floor, int w, int h)
    {
        var seen = new bool[w, h];
        var best = new List<GridPoint>();
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (!floor[x, y] || seen[x, y])
                {
                    continue;
                }

                var region = new List<GridPoint>();
                var queue = new Queue<GridPoint>([new GridPoint(x, y)]);
                seen[x, y] = true;
                while (queue.Count > 0)
                {
                    var p = queue.Dequeue();
                    region.Add(p);
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = p.X + dx, ny = p.Y + dy;
                        if (nx >= 0 && ny >= 0 && nx < w && ny < h && floor[nx, ny] && !seen[nx, ny])
                        {
                            seen[nx, ny] = true;
                            queue.Enqueue(new GridPoint(nx, ny));
                        }
                    }
                }

                if (region.Count > best.Count)
                {
                    best = region;
                }
            }
        }

        return best;
    }
}
