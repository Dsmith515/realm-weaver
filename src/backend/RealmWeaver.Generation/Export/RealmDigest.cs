using RealmWeaver.Generation.Model;

namespace RealmWeaver.Generation.Export;

/// <summary>A mob archetype and how many of it the realm holds, across every area.</summary>
public sealed record MobTally(string ArchetypeId, string Name, int Count);

/// <summary>One thing worth finding: a chest, a vault, or a resource node, and where it is.</summary>
public sealed record DigestEntry(string AreaName, string Id, string Name, string Location);

/// <summary>
/// What a realm holds at a glance, for a map's details: its mobs, its potential loot (chests and treasure vaults)
/// and its resource nodes. Built from the document only, so it's as deterministic as the realm itself.
/// </summary>
public sealed record RealmDigest(IReadOnlyList<MobTally> Mobs, IReadOnlyList<DigestEntry> Loot, IReadOnlyList<DigestEntry> ResourceNodes)
{
    public const string ResourceNodeTemplateId = "resource-node";
    public const string TreasureVaultTemplateId = "treasure-vault";

    public static RealmDigest From(RealmDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var mobs = document.Areas
            .SelectMany(a => a.Events)
            .SelectMany(e => e.Mobs)
            .GroupBy(m => m.ArchetypeId, StringComparer.Ordinal)
            .Select(g => new MobTally(g.Key, g.First().Name, g.Sum(m => m.Count)))
            .OrderByDescending(m => m.Count)
            .ThenBy(m => m.Name, StringComparer.Ordinal)
            .ToList();

        var loot = document.Areas
            .SelectMany(a =>
                a.Features.Where(f => f.Kind == FeatureKind.Chest).Select(f => new DigestEntry(a.Name, f.Id, f.Description, f.Location))
                    .Concat(a.Events.Where(e => e.TemplateId == TreasureVaultTemplateId).Select(e => new DigestEntry(a.Name, e.Id, e.Name, e.Location))))
            .ToList();

        var resources = document.Areas
            .SelectMany(a => a.Events.Where(e => e.TemplateId == ResourceNodeTemplateId).Select(e => new DigestEntry(a.Name, e.Id, e.Name, e.Location)))
            .ToList();

        return new RealmDigest(mobs, loot, resources);
    }
}
