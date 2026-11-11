using System.Numerics;

namespace RealmWeaver.Generation.Randomness;

/// <summary>
/// xoshiro256** PRNG seeded through SplitMix64. Unlike <see cref="System.Random"/>, its sequence is
/// fixed forever, so a stored seed always regenerates the same realm.
/// </summary>
/// <remarks>
/// Use <see cref="Fork"/> to derive independent sub-streams per purpose ("terrain", "events/0", ...),
/// so changing how many numbers one step consumes never shifts the results of another step.
/// </remarks>
public sealed class SeededRandom
{
    private readonly ulong _seed;
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public SeededRandom(ulong seed)
    {
        _seed = seed;
        var x = seed;
        _s0 = Next(ref x);
        _s1 = Next(ref x);
        _s2 = Next(ref x);
        _s3 = Next(ref x);

        static ulong Next(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            var z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    public ulong Seed => _seed;

    /// <summary>An independent stream derived only from this stream's seed and <paramref name="purpose"/>.</summary>
    public SeededRandom Fork(string purpose) => new(StableHash.Combine(_seed, StableHash.Of(purpose)));

    public ulong NextUInt64()
    {
        var result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);
        return result;
    }

    /// <summary>Uniform integer in [0, <paramref name="maxExclusive"/>) without modulo bias.</summary>
    public int NextInt(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);
        var bound = (ulong)maxExclusive;
        var threshold = (0UL - bound) % bound;
        while (true)
        {
            var r = NextUInt64();
            if (r >= threshold)
            {
                return (int)(r % bound);
            }
        }
    }

    /// <summary>Uniform integer in [<paramref name="min"/>, <paramref name="max"/>] (both inclusive).</summary>
    public int NextInclusive(int min, int max)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(min, max);
        return min + NextInt(max - min + 1);
    }

    /// <summary>Uniform double in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    public bool Chance(double probability) => NextDouble() < probability;

    public T Pick<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
        }

        return items[NextInt(items.Count)];
    }

    public T PickWeighted<T>(IReadOnlyList<T> items, Func<T, double> weight)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(weight);

        var total = 0.0;
        foreach (var item in items)
        {
            total += Math.Max(0, weight(item));
        }

        if (total <= 0)
        {
            throw new ArgumentException("At least one item needs a positive weight.", nameof(items));
        }

        var roll = NextDouble() * total;
        foreach (var item in items)
        {
            roll -= Math.Max(0, weight(item));
            if (roll < 0)
            {
                return item;
            }
        }

        return items[^1];
    }

    public void Shuffle<T>(IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
