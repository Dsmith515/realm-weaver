using RealmWeaver.Generation.Grid;
using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Randomness;

namespace RealmWeaver.Generation.Dungeon;

/// <summary>Mutable room state used while a floor is being generated.</summary>
internal sealed class RoomNode(int index, GridRect box)
{
    public int Index { get; } = index;

    public string Id => "R" + (Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    public GridRect Box { get; } = box;

    public DungeonMaterial Material { get; set; }

    public bool IsTransition { get; set; }

    public DungeonMaterial? TransitionFrom { get; set; }

    public int TreeDepth { get; set; }

    public int? Parent { get; set; }

    public List<int> Children { get; } = [];

    public HashSet<int> Neighbors { get; } = [];

    public HashSet<GridPoint> Floor { get; set; } = [];

    public GridPoint Anchor { get; set; }

    /// <summary>The corridor tile where this room opens towards each neighbor.</summary>
    public Dictionary<int, GridPoint> Doors { get; } = [];

    public bool IsEntrance { get; set; }

    public bool IsSecret { get; set; }

    public bool HasStairsDown { get; set; }

    public bool IsBossRoom { get; set; }

    public RoomStyle Style => DungeonMaterials.Of(Material).Style;
}

/// <summary>
/// Connects rooms (minimum spanning tree plus a few loops) and assigns materials so that the core near
/// the entrance keeps the primary material and distant branches can change through a transition room.
/// </summary>
internal static class RoomGraph
{
    public static List<(int A, int B)> SpanningTree(List<RoomNode> rooms)
    {
        var n = rooms.Count;
        var inTree = new bool[n];
        var best = Enumerable.Repeat(int.MaxValue, n).ToArray();
        var from = new int[n];
        var edges = new List<(int, int)>(n - 1);

        inTree[0] = true;
        for (var i = 1; i < n; i++)
        {
            best[i] = Distance(rooms[0], rooms[i]);
            from[i] = 0;
        }

        for (var added = 1; added < n; added++)
        {
            var next = -1;
            for (var i = 0; i < n; i++)
            {
                if (!inTree[i] && (next < 0 || best[i] < best[next]))
                {
                    next = i;
                }
            }

            inTree[next] = true;
            edges.Add((from[next], next));
            Link(rooms, from[next], next);

            for (var i = 0; i < n; i++)
            {
                var d = Distance(rooms[next], rooms[i]);
                if (!inTree[i] && d < best[i])
                {
                    best[i] = d;
                    from[i] = next;
                }
            }
        }

        // Depths and parent/child links from the entrance (room 0), for material assignment.
        var queue = new Queue<int>([0]);
        var seen = new bool[n];
        seen[0] = true;
        while (queue.Count > 0)
        {
            var r = queue.Dequeue();
            foreach (var nb in rooms[r].Neighbors.Order())
            {
                if (!seen[nb])
                {
                    seen[nb] = true;
                    rooms[nb].Parent = r;
                    rooms[nb].TreeDepth = rooms[r].TreeDepth + 1;
                    rooms[r].Children.Add(nb);
                    queue.Enqueue(nb);
                }
            }
        }

        return edges;
    }

    /// <summary>
    /// Everything starts as <paramref name="primary"/>. Beyond <paramref name="coreDepth"/>, whole subtrees may switch
    /// to a branch material; the subtree's root becomes the transition room. The share of primary rooms never drops
    /// below <paramref name="primaryShare"/>.
    /// </summary>
    public static void AssignMaterials(
        List<RoomNode> rooms, DungeonMaterial primary, IReadOnlyList<DungeonMaterial> branches,
        double primaryShare, int coreDepth, SeededRandom rng)
    {
        foreach (var room in rooms)
        {
            room.Material = primary;
        }

        if (branches.Count == 0)
        {
            return;
        }

        var budget = (int)Math.Floor(rooms.Count * (1 - primaryShare));
        var used = 0;
        foreach (var room in BreadthFirst(rooms))
        {
            if (room.Parent is not { } parent || room.TreeDepth <= coreDepth)
            {
                continue;
            }

            var parentRoom = rooms[parent];
            if (parentRoom.Material != primary || parentRoom.IsTransition)
            {
                continue;
            }

            var size = SubtreeSize(rooms, room.Index);
            if (used + size > budget || !rng.Chance(0.55))
            {
                continue;
            }

            var material = rng.Pick(branches);
            foreach (var member in Subtree(rooms, room.Index))
            {
                rooms[member].Material = material;
            }

            room.IsTransition = true;
            room.TransitionFrom = primary;
            used += size;
        }
    }

    /// <summary>Adds a few extra connections between nearby rooms so the layout has loops, never joining two materials directly.</summary>
    public static List<(int A, int B)> AddLoops(List<RoomNode> rooms, int maxDistance, SeededRandom rng)
    {
        var loops = new List<(int, int)>();
        if (rooms.Count < 4)
        {
            return loops;
        }

        var wanted = Math.Max(1, (int)Math.Round(rooms.Count * 0.15));
        var candidates = new List<(int A, int B, int D)>();
        foreach (var a in rooms)
        {
            foreach (var b in rooms.Where(b => b.Index > a.Index && !a.Neighbors.Contains(b.Index))
                         .OrderBy(b => Distance(a, b)).Take(3))
            {
                var d = Distance(a, b);
                if (d <= maxDistance && CanJoin(a, b))
                {
                    candidates.Add((a.Index, b.Index, d));
                }
            }
        }

        // Shuffle first so ties do not always favor the same rooms, then prefer short connections.
        rng.Shuffle(candidates);
        foreach (var (a, b, _) in candidates.OrderBy(c => c.D).Take(wanted))
        {
            Link(rooms, a, b);
            loops.Add((a, b));
        }

        return loops;
    }

    /// <summary>Two rooms may be joined when they share a material or one of them is the transition between materials.</summary>
    public static bool CanJoin(RoomNode a, RoomNode b) => a.Material == b.Material || a.IsTransition || b.IsTransition;

    /// <summary>Graph distance (number of connections) from the entrance to every room.</summary>
    public static int[] DistancesFrom(List<RoomNode> rooms, int start)
    {
        var dist = Enumerable.Repeat(-1, rooms.Count).ToArray();
        dist[start] = 0;
        var queue = new Queue<int>([start]);
        while (queue.Count > 0)
        {
            var r = queue.Dequeue();
            foreach (var nb in rooms[r].Neighbors)
            {
                if (dist[nb] < 0)
                {
                    dist[nb] = dist[r] + 1;
                    queue.Enqueue(nb);
                }
            }
        }

        return dist;
    }

    private static void Link(List<RoomNode> rooms, int a, int b)
    {
        rooms[a].Neighbors.Add(b);
        rooms[b].Neighbors.Add(a);
    }

    private static int Distance(RoomNode a, RoomNode b) => a.Box.Center.ManhattanDistanceTo(b.Box.Center);

    private static IEnumerable<RoomNode> BreadthFirst(List<RoomNode> rooms)
    {
        var queue = new Queue<int>([0]);
        while (queue.Count > 0)
        {
            var r = queue.Dequeue();
            yield return rooms[r];
            foreach (var child in rooms[r].Children)
            {
                queue.Enqueue(child);
            }
        }
    }

    private static IEnumerable<int> Subtree(List<RoomNode> rooms, int root)
    {
        var stack = new Stack<int>([root]);
        while (stack.Count > 0)
        {
            var r = stack.Pop();
            yield return r;
            foreach (var child in rooms[r].Children)
            {
                stack.Push(child);
            }
        }
    }

    private static int SubtreeSize(List<RoomNode> rooms, int root) => Subtree(rooms, root).Count();
}
