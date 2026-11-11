using System.Globalization;
using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Grid;
using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Randomness;

namespace RealmWeaver.Generation.Events;

/// <summary>Picks a spawn point and places non-overlapping events on a field.</summary>
public sealed class FieldEventPlacer
{
    private const int AttemptsPerEvent = 80;
    private const int ShrinkEvery = 20;
    private const double MinPassableForClearing = 0.6;

    private readonly TerrainMap _map;
    private readonly IContentCatalog _catalog;
    private readonly GenerationRequest _request;
    private readonly bool[] _blocked;

    public FieldEventPlacer(TerrainMap map, IContentCatalog catalog, GenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
        _catalog = catalog;
        _request = request;
        _blocked = new bool[map.Area];
    }

    /// <param name="Host">The dungeon host event, when one was requested.</param>
    public sealed record Result(GridPoint Spawn, IReadOnlyList<PlacedEvent> Events, PlacedEvent? Host);

    /// <summary>
    /// Places <paramref name="count"/> events; when <paramref name="host"/> is set it is placed first and counts as one of them.
    /// The spawn point and host come from <paramref name="layoutRng"/> so they stay put when encounters are rerolled.
    /// </summary>
    public Result Place(int count, SeededRandom rng, SeededRandom layoutRng, EventTemplate? host = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(layoutRng);

        var spawn = ChooseSpawn(layoutRng.Fork("spawn"));
        BlockAround(spawn, GeneratorLimits.SpawnClearance - 1);

        var eligible = _catalog.EventTemplates
            .Where(t => t.Environments.HasFlag(Environments.Field))
            .Where(t => t.Biomes.Count == 0 || t.Biomes.Contains(_request.Biome))
            .Where(t => !t.IsDungeonHost && !t.IsBoss)
            .ToList();
        var fallbacks = eligible.Where(t => t.Footprint.MaxSide <= 2 && t.MaxPerArea == int.MaxValue).ToList();

        var planRng = rng.Fork("plan");
        var placeRng = rng.Fork("place");
        var mobs = new MobResolver(_catalog, _request);
        var mobRng = rng.Fork("mobs");

        var placed = new List<(EventTemplate Template, GridRect Footprint)>();
        GridRect? hostFootprint = null;
        if (host is not null)
        {
            hostFootprint = TryPlace(host, layoutRng.Fork("host"))
                ?? throw new RealmGenerationException($"The {host.Name} does not fit on a {_map.Width}x{_map.Height} map.");
            Commit(host, hostFootprint.Value);
            placed.Add((host, hostFootprint.Value));
        }

        var plan = ChooseTemplates(eligible, count - placed.Count, planRng)
            .Select((template, index) => (template, index))
            .OrderByDescending(p => p.template.Footprint.MaxSide)
            .ThenBy(p => p.index)
            .Select(p => p.template)
            .ToList();

        foreach (var template in plan)
        {
            var footprint = TryPlace(template, placeRng);
            var used = template;
            if (footprint is null)
            {
                foreach (var fallback in fallbacks)
                {
                    footprint = TryPlace(fallback, placeRng);
                    if (footprint is not null)
                    {
                        used = fallback;
                        break;
                    }
                }
            }

            if (footprint is null)
            {
                throw new RealmGenerationException(
                    $"Only {placed.Count} of {count} events fit on a {_map.Width}x{_map.Height} map. Try fewer events or a larger map.");
            }

            Commit(used, footprint.Value);
            placed.Add((used, footprint.Value));
        }

        // Number events in reading order (top-left to bottom-right) so the list is easy to follow on the map.
        var events = placed
            .OrderBy(p => p.Footprint.Y)
            .ThenBy(p => p.Footprint.X)
            .Select((p, i) => BuildEvent(i + 1, p.Template, p.Footprint, mobs, mobRng.Fork(i.ToString(CultureInfo.InvariantCulture))))
            .ToList();

        var hostEvent = hostFootprint is { } hf ? events.Single(e => e.Footprint == hf) : null;
        return new Result(spawn, events, hostEvent);
    }

    private static PlacedEvent BuildEvent(int number, EventTemplate template, GridRect footprint, MobResolver mobs, SeededRandom rng)
    {
        var groups = mobs.Resolve(template, rng);
        return new PlacedEvent
        {
            Id = "E" + number.ToString(CultureInfo.InvariantCulture),
            TemplateId = template.Id,
            Name = template.Name,
            Category = template.Category,
            Footprint = footprint,
            Mobs = groups,
            DifficultyTemplateIds = groups.Count > 0 ? mobs.DifficultyTemplates(rng) : [],
            Notes = template.Description,
        };
    }

    private List<EventTemplate> ChooseTemplates(List<EventTemplate> eligible, int count, SeededRandom rng)
    {
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        var chosen = new List<EventTemplate>(count);
        while (chosen.Count < count)
        {
            var available = eligible.Where(t => used.GetValueOrDefault(t.Id) < t.MaxPerArea && FitsMapAtAll(t)).ToList();
            var pick = rng.PickWeighted(available, t => t.Weight);
            used[pick.Id] = used.GetValueOrDefault(pick.Id) + 1;
            chosen.Add(pick);
        }

        return chosen;
    }

    private bool FitsMapAtAll(EventTemplate t) => t.Footprint.MinSide <= Math.Min(_map.Width, _map.Height) - 2;

    private int MaxSide(EventTemplate t)
    {
        var limit = Math.Min(_map.Width, _map.Height) - 2;
        var max = t.Footprint.ScalesWithMap
            ? Math.Max(t.Footprint.MaxSide, (int)Math.Sqrt(_map.Area / 2.0))
            : t.Footprint.MaxSide;
        return Math.Max(t.Footprint.MinSide, Math.Min(max, limit));
    }

    private GridRect? TryPlace(EventTemplate template, SeededRandom rng)
    {
        var rule = template.Footprint;
        var maxSide = MaxSide(template);

        for (var attempt = 0; attempt < AttemptsPerEvent; attempt++)
        {
            var currentMax = Math.Max(rule.MinSide, maxSide - (attempt / ShrinkEvery));
            int w;
            int h;
            if (rule.Shape == FootprintShape.Line)
            {
                var length = rng.NextInclusive(rule.MinSide, currentMax);
                (w, h) = rng.Chance(0.5) ? (length, 1) : (1, length);
            }
            else
            {
                w = rng.NextInclusive(rule.MinSide, currentMax);
                h = rng.NextInclusive(rule.MinSide, currentMax);
            }

            // Settlements may never cover more than half the map.
            if (w * h > _map.Area / 2 || w > _map.Width || h > _map.Height)
            {
                continue;
            }

            var rect = new GridRect(rng.NextInt(_map.Width - w + 1), rng.NextInt(_map.Height - h + 1), w, h);
            if (Fits(rect, template))
            {
                return rect;
            }
        }

        return null;
    }

    private bool Fits(GridRect rect, EventTemplate template)
    {
        var passable = 0;
        foreach (var p in rect.Tiles())
        {
            if (_blocked[Index(p)])
            {
                return false;
            }

            if (_map.IsPassable(p))
            {
                passable++;
            }
            else if (!template.ClearsTerrain)
            {
                return false;
            }
        }

        return passable >= rect.Area * MinPassableForClearing;
    }

    private void Commit(EventTemplate template, GridRect rect)
    {
        // Block the footprint plus a one-tile gap so events never touch.
        var padded = new GridRect(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2);
        foreach (var p in padded.Tiles())
        {
            if (_map.InBounds(p))
            {
                _blocked[Index(p)] = true;
            }
        }

        if (template.ClearsTerrain)
        {
            foreach (var p in rect.Tiles())
            {
                if (!_map.IsPassable(p))
                {
                    _map[p] = Terrain.Ground;
                }
            }
        }
    }

    private GridPoint ChooseSpawn(SeededRandom rng)
    {
        const int edgeBand = 2;
        var passable = _map.AllTiles().Where(_map.IsPassable).ToList();
        var nearEdge = passable
            .Where(p => p.X < edgeBand || p.Y < edgeBand || p.X >= _map.Width - edgeBand || p.Y >= _map.Height - edgeBand)
            .ToList();
        return rng.Pick(nearEdge.Count > 0 ? nearEdge : passable);
    }

    private void BlockAround(GridPoint center, int radius)
    {
        for (var y = center.Y - radius; y <= center.Y + radius; y++)
        {
            for (var x = center.X - radius; x <= center.X + radius; x++)
            {
                if (_map.InBounds(x, y))
                {
                    _blocked[(y * _map.Width) + x] = true;
                }
            }
        }
    }

    private int Index(GridPoint p) => (p.Y * _map.Width) + p.X;
}
