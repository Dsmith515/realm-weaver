using System.Numerics;

namespace RealmWeaver.Generation.Randomness;

/// <summary>
/// Hashing that is stable across processes, platforms and .NET versions
/// (unlike <see cref="string.GetHashCode()"/>). Used to derive seeds and noise values.
/// </summary>
public static class StableHash
{
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>FNV-1a over the UTF-16 code units of <paramref name="text"/>.</summary>
    public static ulong Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var hash = FnvOffset;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= FnvPrime;
        }

        return hash;
    }

    /// <summary>SplitMix64 finalizer: a fast, well-distributed 64-bit mix.</summary>
    public static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    public static ulong Combine(ulong a, ulong b) => Mix(a ^ BitOperations.RotateLeft(Mix(b), 23));

    public static ulong Combine(ulong seed, int x, int y) =>
        Combine(Combine(seed, unchecked((ulong)x)), unchecked((ulong)y));

    /// <summary>Deterministic value in [0, 1) for a lattice point.</summary>
    public static double UnitAt(ulong seed, int x, int y) => (Combine(seed, x, y) >> 11) * (1.0 / (1UL << 53));
}
