using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Randomness;

namespace RealmWeaver.Generation.Events;

/// <summary>How many events an area gets, scaled by its size and always within 5-60.</summary>
public static class EventBudget
{
    public readonly record struct Range(int Min, int Max);

    /// <summary>Range used when no count is given (max 50 on the largest maps).</summary>
    public static Range DefaultRange(int area)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area);
        var min = Math.Clamp(GeneratorLimits.MinEvents + (area / 1500), GeneratorLimits.MinEvents, 15);
        var max = Math.Clamp(GeneratorLimits.MinEvents + (area / 180), GeneratorLimits.MinEvents, GeneratorLimits.DefaultMaxEvents);
        return new Range(min, Math.Max(min, max));
    }

    /// <summary>Highest count a user may ask for on this size of area (60 on the largest maps).</summary>
    public static int OverrideCap(int area)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area);
        return Math.Clamp(GeneratorLimits.MinEvents + (area / 160), GeneratorLimits.MinEvents, GeneratorLimits.AbsoluteMaxEvents);
    }

    /// <param name="defaultArea">Area used for the default range, e.g. the walkable part of a dungeon floor. Defaults to <paramref name="area"/>.</param>
    public static int Resolve(int area, int? requested, SeededRandom rng, int? defaultArea = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (requested is { } count)
        {
            var cap = OverrideCap(area);
            if (count < GeneratorLimits.MinEvents || count > cap)
            {
                throw new RealmGenerationException(
                    $"Event count {count} is outside the allowed range {GeneratorLimits.MinEvents}-{cap} for an area of {area} tiles.");
            }

            return count;
        }

        var range = DefaultRange(defaultArea ?? area);
        return rng.NextInclusive(range.Min, range.Max);
    }
}
