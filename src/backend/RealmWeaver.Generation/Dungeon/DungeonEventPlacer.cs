using System.Globalization;
using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Events;
using RealmWeaver.Generation.Grid;
using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Randomness;

namespace RealmWeaver.Generation.Dungeon;

/// <summary>
/// Puts events into dungeon rooms. Each room holds at most one event; settlements may spread over several
/// connected rooms of the same material. The deepest floor's farthest room always holds the boss.
/// </summary>
internal static class DungeonEventPlacer
{
    public static IReadOnlyList<PlacedEvent> Place(FloorContext ctx, List<RoomNode> rooms, int count, bool isDeepest, SeededRandom rng)
    {
        var slots = rooms.Where(r => !r.IsEntrance && !r.IsSecret).ToList();
        if (slots.Count < count)
        {
            throw new RealmGenerationException(
                $"Floor {ctx.FloorNumber} has {slots.Count} rooms for events but {count} are needed. Try fewer events or a larger map.");
        }

        var templates = ctx.Catalog.EventTemplates
            .Where(t => t.Environments.HasFlag(Environments.Dungeon) && !t.IsDungeonHost)
            .ToList();
        var mobs = new MobResolver(ctx.Catalog, ctx.Request);
        var claimed = new HashSet<int>();
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        var placed = new List<(EventTemplate Template, List<RoomNode> Rooms)>();

        if (isDeepest)
        {
            var boss = templates.First(t => t.IsBoss);
            var bossRoom = rooms.Single(r => r.IsBossRoom);
            claimed.Add(bossRoom.Index);
            placed.Add((boss, [bossRoom]));
            used[boss.Id] = 1;
        }

        var regular = templates.Where(t => !t.IsBoss).ToList();
        var order = slots.Where(r => !claimed.Contains(r.Index)).ToList();
        rng.Shuffle(order);

        foreach (var room in order)
        {
            if (placed.Count >= count)
            {
                break;
            }

            if (claimed.Contains(room.Index))
            {
                continue;
            }

            // Rooms left over beyond what the remaining events need; multi-room events may only use these.
            var unclaimed = slots.Count(r => !claimed.Contains(r.Index));
            var spare = unclaimed - (count - placed.Count);

            var options = regular
                .Where(t => used.GetValueOrDefault(t.Id) < t.MaxPerArea)
                .Where(t => t.AllowedStyles.Count == 0 || t.AllowedStyles.Contains(room.Style))
                .Where(t => t.Biomes.Count == 0 || t.Biomes.Contains(ctx.Request.Biome))
                .ToList();
            var template = rng.PickWeighted(options, t => t.Weight * (t.PreferredStyles.Contains(room.Style) ? 3 : 1));

            var group = new List<RoomNode> { room };
            claimed.Add(room.Index);
            foreach (var n in room.Neighbors.Order())
            {
                if (group.Count >= template.MaxRooms || spare <= 0)
                {
                    break;
                }

                var neighbor = rooms[n];
                if (!claimed.Contains(n) && !neighbor.IsEntrance && !neighbor.IsSecret && neighbor.Material == room.Material)
                {
                    group.Add(neighbor);
                    claimed.Add(n);
                    spare--;
                }
            }

            used[template.Id] = used.GetValueOrDefault(template.Id) + 1;
            placed.Add((template, group));
        }

        var eventRng = rng.Fork("mobs");
        return placed
            .Select(p => (p.Template, p.Rooms, Footprint: GridRect.Union(p.Rooms.Select(r => r.Box))))
            .OrderBy(p => p.Footprint.Y)
            .ThenBy(p => p.Footprint.X)
            .Select((p, i) =>
            {
                var mobRng = eventRng.Fork(i.ToString(CultureInfo.InvariantCulture));
                var groups = mobs.Resolve(p.Template, mobRng);
                var materials = string.Join(" / ", p.Rooms.Select(r => DungeonMaterials.Of(r.Material).Name).Distinct());
                return new PlacedEvent
                {
                    Id = "E" + (i + 1).ToString(CultureInfo.InvariantCulture),
                    TemplateId = p.Template.Id,
                    Name = p.Template.Name,
                    Category = p.Template.Category,
                    Footprint = p.Footprint,
                    RoomIds = p.Rooms.Select(r => r.Id).ToList(),
                    Mobs = groups,
                    DifficultyTemplateIds = groups.Count > 0 ? mobs.DifficultyTemplates(mobRng) : [],
                    Notes = $"{p.Template.Description} ({materials})",
                };
            })
            .ToList();
    }
}
