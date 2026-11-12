using RealmWeaver.Modules.Catalog.Contracts;

namespace RealmWeaver.Modules.Inventory;

/// <summary>
/// A new player gets the beginner needle, 2 different threads ×3 and 2 different coatings ×3.
/// Which threads and coatings is random; this isn't generation, so it doesn't need a seeded RNG.
/// </summary>
internal sealed class StarterKitPicker(Random random)
{
    public const int DistinctThreads = 2;
    public const int DistinctCoatings = 2;
    public const int ConsumableQuantity = 3;

    public IReadOnlyList<(Guid ComponentId, int Quantity)> Pick(StarterKitCandidates candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        List<(Guid, int)> kit = [(candidates.BeginnerNeedleId, 1)];
        kit.AddRange(Choose(candidates.ThreadIds, DistinctThreads).Select(id => (id, ConsumableQuantity)));
        kit.AddRange(Choose(candidates.CoatingIds, DistinctCoatings).Select(id => (id, ConsumableQuantity)));
        return kit;
    }

    private IEnumerable<Guid> Choose(IReadOnlyList<Guid> ids, int count)
    {
        var shuffled = ids.ToArray();
        random.Shuffle(shuffled);
        return shuffled.Take(count);
    }
}
