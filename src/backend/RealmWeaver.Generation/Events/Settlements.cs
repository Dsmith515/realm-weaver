using System.Globalization;
using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Grid;
using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Randomness;

namespace RealmWeaver.Generation.Events;

/// <summary>
/// Population, residents and structures for settlement events. Each piece draws from its own fork of the event's
/// stream ("population", "structures"), so turning structures off doesn't change the mobs.
/// </summary>
public static class Settlements
{
    /// <summary>Most residents one settlement gets, so the placement sheet stays readable.</summary>
    public const int MaxResidents = 40;

    private static readonly SettlementPopulation[] Levels = Enum.GetValues<SettlementPopulation>();

    /// <summary>The request's population, or a roll that leans busier as threat rises.</summary>
    public static SettlementPopulation RollPopulation(GenerationRequest request, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rng);
        if (request.SettlementPopulation is { } fixedLevel)
        {
            return fixedLevel;
        }

        var threat = Math.Max(0, request.Threat);
        return rng.PickWeighted(Levels, level => level switch
        {
            SettlementPopulation.Abandoned => 10,
            SettlementPopulation.Sparse => 20,
            SettlementPopulation.Modest => 35,
            SettlementPopulation.Bustling => 25 + threat,
            _ => 10 + threat,
        });
    }

    /// <summary>
    /// How many residents a settlement of <paramref name="area"/> tiles holds: one per 24, 14, 9 or 6 tiles from Sparse to
    /// Overcrowded, never fewer than <paramref name="minimum"/> nor more than <see cref="MaxResidents"/>. Abandoned is 0.
    /// </summary>
    public static int Residents(SettlementPopulation population, int area, int minimum)
    {
        var tilesPerResident = population switch
        {
            SettlementPopulation.Abandoned => 0,
            SettlementPopulation.Sparse => 24,
            SettlementPopulation.Modest => 14,
            SettlementPopulation.Bustling => 9,
            _ => 6,
        };

        return tilesPerResident == 0 ? 0 : Math.Clamp(area / tilesPerResident, minimum, MaxResidents);
    }

    /// <summary>
    /// Packs structures into <paramref name="footprint"/> with a one-tile path between them. Templates matching the
    /// Coating's mob tags come first, with structures any settlement could have as the fallback. Ids count up from
    /// <paramref name="firstNumber"/> ("B1", "B2"…) so they stay unique across the area.
    /// </summary>
    public static IReadOnlyList<PlacedStructure> PlaceStructures(
        GridRect footprint,
        SettlementPopulation population,
        IContentCatalog catalog,
        GenerationRequest request,
        SeededRandom rng,
        int firstNumber)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rng);

        var pool = StructurePool(footprint, catalog, request);
        if (pool.Count == 0)
        {
            return [];
        }

        var target = StructureTarget(footprint, population);

        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        var placed = new List<PlacedStructure>();
        for (var attempt = 0; attempt < target * 25 && placed.Count < target; attempt++)
        {
            var available = pool.Where(t => used.GetValueOrDefault(t.Id) < t.MaxPerSettlement).ToList();
            if (available.Count == 0)
            {
                break;
            }

            // Cultural structures are three times as likely, so a goblin village reads as one.
            var template = rng.PickWeighted(available, t => t.Weight * (t.Tags.Count > 0 ? 3 : 1));
            var w = rng.NextInclusive(template.MinSide, Math.Min(template.MaxSide, footprint.Width));
            var h = rng.NextInclusive(template.MinSide, Math.Min(template.MaxSide, footprint.Height));
            var rect = new GridRect(
                footprint.X + rng.NextInt(footprint.Width - w + 1),
                footprint.Y + rng.NextInt(footprint.Height - h + 1),
                w,
                h);

            // Keep a path between buildings.
            var padded = rect.Expand(1);
            if (placed.Any(p => p.Footprint.Intersects(padded)))
            {
                continue;
            }

            used[template.Id] = used.GetValueOrDefault(template.Id) + 1;
            placed.Add(new PlacedStructure
            {
                Id = "B" + (firstNumber + placed.Count).ToString(CultureInfo.InvariantCulture),
                TemplateId = template.Id,
                Name = template.Name,
                Footprint = rect,
                Ruined = population == SettlementPopulation.Abandoned,
            });
        }

        return placed;
    }

    /// <summary>
    /// Structure ideas for a settlement the GM lays out themselves: about as many as <see cref="PlaceStructures"/> would
    /// place, from the same pool and weights, with no positions. Abandoned settlements suggest ruins.
    /// </summary>
    public static IReadOnlyList<StructureSuggestion> SuggestStructures(
        GridRect footprint,
        SettlementPopulation population,
        IContentCatalog catalog,
        GenerationRequest request,
        SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rng);

        var pool = StructurePool(footprint, catalog, request);
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        var suggestions = new List<StructureSuggestion>();
        var target = StructureTarget(footprint, population);
        while (suggestions.Count < target)
        {
            var available = pool.Where(t => used.GetValueOrDefault(t.Id) < t.MaxPerSettlement).ToList();
            if (available.Count == 0)
            {
                break;
            }

            var template = rng.PickWeighted(available, t => t.Weight * (t.Tags.Count > 0 ? 3 : 1));
            used[template.Id] = used.GetValueOrDefault(template.Id) + 1;
            var name = population == SettlementPopulation.Abandoned ? $"Ruined {template.Name.ToLowerInvariant()}" : template.Name;
            suggestions.Add(new StructureSuggestion(template.Id, name));
        }

        return suggestions;
    }

    /// <summary>Structures that fit the footprint and biome: ones any settlement could have, plus the Coating's cultural ones.</summary>
    private static List<StructureTemplate> StructurePool(GridRect footprint, IContentCatalog catalog, GenerationRequest request)
    {
        var fits = catalog.StructureTemplates
            .Where(t => t.Biomes.Count == 0 || t.Biomes.Contains(request.Biome))
            .Where(t => t.MinSide <= Math.Min(footprint.Width, footprint.Height))
            .ToList();
        var cultural = fits.Where(t => t.Tags.Any(tag => request.MobTags.Contains(tag, StringComparer.OrdinalIgnoreCase))).ToList();
        return [.. fits.Where(t => t.Tags.Count == 0).Concat(cultural)];
    }

    private static int StructureTarget(GridRect footprint, SettlementPopulation population)
    {
        var tilesPerStructure = population switch
        {
            SettlementPopulation.Abandoned => 18,
            SettlementPopulation.Sparse => 24,
            SettlementPopulation.Modest => 16,
            SettlementPopulation.Bustling => 12,
            _ => 9,
        };
        return Math.Clamp(footprint.Area / tilesPerStructure, 1, 12);
    }
}
