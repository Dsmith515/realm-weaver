using System.Globalization;
using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Randomness;

namespace RealmWeaver.Generation.Objectives;

/// <summary>
/// Chooses a realm's objectives from the hooks of its placed events plus the realm-wide objectives.
/// Runs after the events are placed, so every objective has a place on the map and is always achievable.
/// </summary>
public static class ObjectivePicker
{
    /// <summary>Every objective the realm could offer, in map order: event hooks first, then realm-wide ones.</summary>
    public static IReadOnlyList<RealmObjective> Candidates(IReadOnlyList<RealmArea> areas, IContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentNullException.ThrowIfNull(catalog);
        return [.. Pool(areas, catalog).Select(c => c.Objective)];
    }

    /// <summary>
    /// Picks 1-3 objectives by weight without replacement: at most one per event and at most one realm-wide.
    /// </summary>
    public static IReadOnlyList<RealmObjective> Pick(IReadOnlyList<RealmArea> areas, IContentCatalog catalog, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rng);

        var pool = Pool(areas, catalog);
        var target = rng.NextInclusive(GeneratorLimits.MinObjectives, GeneratorLimits.MaxObjectives);
        var picked = new List<RealmObjective>();

        while (picked.Count < target && pool.Count > 0)
        {
            var choice = rng.PickWeighted(pool, c => c.Weight).Objective;
            picked.Add(choice with { Id = "O" + (picked.Count + 1).ToString(CultureInfo.InvariantCulture) });

            // One objective per event, and one realm-wide objective at most.
            pool.RemoveAll(c => c.Objective.Scope == choice.Scope
                && (choice.Scope == ObjectiveScope.Realm || (c.Objective.AreaId == choice.AreaId && c.Objective.EventId == choice.EventId)));
        }

        return picked;
    }

    private sealed record Candidate(RealmObjective Objective, int Weight);

    private static List<Candidate> Pool(IReadOnlyList<RealmArea> areas, IContentCatalog catalog)
    {
        var templates = catalog.EventTemplates.ToDictionary(t => t.Id);
        var pool = new List<Candidate>();

        foreach (var area in areas)
        {
            foreach (var placed in area.Events)
            {
                if (!templates.TryGetValue(placed.TemplateId, out var template))
                {
                    continue;
                }

                foreach (var hook in template.ObjectiveHooks)
                {
                    if (hook.NeedsResidents && placed.Population == SettlementPopulation.Abandoned)
                    {
                        continue;
                    }

                    var objective = new RealmObjective
                    {
                        Id = string.Empty,
                        Key = $"{area.Id}/{placed.Id}/{hook.Id}",
                        TemplateId = hook.Id,
                        Title = hook.Title,
                        Description = hook.Description,
                        Scope = ObjectiveScope.Event,
                        AreaId = area.Id,
                        EventId = placed.Id,
                        EventName = placed.Name,
                        Location = placed.Location,
                    };
                    pool.Add(new Candidate(objective, hook.Weight));
                }
            }
        }

        var hasFloors = areas.Any(a => a.Kind == AreaKind.DungeonFloor);
        foreach (var realmWide in catalog.RealmObjectives)
        {
            if (realmWide.Requirement == ObjectiveRequirement.DungeonFloors && !hasFloors)
            {
                continue;
            }

            var objective = new RealmObjective
            {
                Id = string.Empty,
                Key = "realm/" + realmWide.Id,
                TemplateId = realmWide.Id,
                Title = realmWide.Title,
                Description = realmWide.Description,
                Scope = ObjectiveScope.Realm,
            };
            pool.Add(new Candidate(objective, realmWide.Weight));
        }

        return pool;
    }
}
