using RealmWeaver.Generation.Content;
using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Randomness;

namespace RealmWeaver.Generation.Events;

/// <summary>Fills an event's mob slots with archetypes that fit the biome, the Coating's mob tags and the threat.</summary>
public sealed class MobResolver(IContentCatalog catalog, GenerationRequest request)
{
    private readonly int _threat = request.Threat;

    public IReadOnlyList<MobGroup> Resolve(EventTemplate template, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(rng);

        var groups = new List<MobGroup>();
        foreach (var slot in template.MobSlots)
        {
            var count = rng.NextInclusive(slot.MinCount, slot.MaxCount);
            if (count == 0)
            {
                continue;
            }

            // Higher threat adds extra minions.
            if (slot.Role == MobRole.Minion)
            {
                count += _threat / 4;
            }

            var archetype = Choose(slot.Role, rng);
            if (archetype is not null)
            {
                groups.Add(new MobGroup(archetype.Id, archetype.Name, slot.Role, count));
            }
        }

        return groups;
    }

    public IReadOnlyList<string> DifficultyTemplates(SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var eligible = catalog.DifficultyTemplates.Where(t => t.MinThreat <= _threat).ToList();
        return eligible.Where(_ => rng.Chance(0.35)).Select(t => t.Id).ToList();
    }

    /// <summary>Tries strict matches first, then relaxes tags, biome and tier, in that order.</summary>
    private MobArchetype? Choose(MobRole role, SeededRandom rng)
    {
        var withRole = catalog.MobArchetypes.Where(a => a.Roles.Contains(role)).ToList();
        IEnumerable<Func<MobArchetype, bool>> passes =
        [
            a => MatchesTags(a) && MatchesBiome(a) && MatchesTier(a),
            a => MatchesTags(a) && MatchesTier(a),
            a => MatchesTags(a),
            a => MatchesBiome(a) && MatchesTier(a),
            _ => true,
        ];

        foreach (var pass in passes)
        {
            var candidates = withRole.Where(pass).ToList();
            if (candidates.Count > 0)
            {
                return rng.Pick(candidates);
            }
        }

        return null;
    }

    private bool MatchesTags(MobArchetype a) =>
        request.MobTags.Count == 0 || a.Tags.Any(t => request.MobTags.Contains(t, StringComparer.OrdinalIgnoreCase));

    private bool MatchesBiome(MobArchetype a) => a.Biomes.Count == 0 || a.Biomes.Contains(request.Biome);

    private bool MatchesTier(MobArchetype a) => _threat >= a.MinTier && _threat <= a.MaxTier + 2;
}
