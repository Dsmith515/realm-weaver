using RealmWeaver.Generation.Randomness;

namespace RealmWeaver.Generation.Field;

/// <summary>
/// Fractal value noise. Lattice values come from <see cref="StableHash"/>, so no arrays of random
/// numbers are needed and results depend only on the seed and coordinates.
/// </summary>
public static class ValueNoise
{
    /// <summary>Fractal noise sampled over a whole grid and normalized to the full [0, 1] range.</summary>
    public static double[] Sample(ulong seed, int width, int height, double baseCellSize, int octaves)
    {
        var values = new double[width * height];
        var min = double.MaxValue;
        var max = double.MinValue;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var v = Fractal(seed, x, y, baseCellSize, octaves);
                values[(y * width) + x] = v;
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
        }

        var range = max - min;
        if (range <= double.Epsilon)
        {
            Array.Fill(values, 0.5);
            return values;
        }

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (values[i] - min) / range;
        }

        return values;
    }

    public static double Fractal(ulong seed, double x, double y, double baseCellSize, int octaves)
    {
        var total = 0.0;
        var amplitude = 1.0;
        var norm = 0.0;
        var cell = baseCellSize;

        for (var o = 0; o < octaves; o++)
        {
            total += amplitude * Single(StableHash.Combine(seed, (ulong)o), x / cell, y / cell);
            norm += amplitude;
            amplitude *= 0.5;
            cell = Math.Max(1.0, cell / 2);
        }

        return total / norm;
    }

    private static double Single(ulong seed, double x, double y)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var tx = Smooth(x - x0);
        var ty = Smooth(y - y0);

        var a = StableHash.UnitAt(seed, x0, y0);
        var b = StableHash.UnitAt(seed, x0 + 1, y0);
        var c = StableHash.UnitAt(seed, x0, y0 + 1);
        var d = StableHash.UnitAt(seed, x0 + 1, y0 + 1);

        return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), ty);
    }

    private static double Smooth(double t) => t * t * (3 - (2 * t));

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);
}
