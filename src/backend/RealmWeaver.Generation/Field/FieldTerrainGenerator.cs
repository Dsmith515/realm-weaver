using RealmWeaver.Generation.Grid;
using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Randomness;

namespace RealmWeaver.Generation.Field;

/// <summary>Builds field terrain from noise, then guarantees every passable tile is reachable.</summary>
public static class FieldTerrainGenerator
{
    public static TerrainMap Generate(SeededRandom rng, Biome biome, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(rng);

        var cellSize = Math.Max(6.0, Math.Min(width, height) / 4.0);
        var elevation = ValueNoise.Sample(rng.Fork("elevation").NextUInt64(), width, height, cellSize, 3);
        var moisture = ValueNoise.Sample(rng.Fork("moisture").NextUInt64(), width, height, cellSize * 0.8, 3);
        var detailSeed = rng.Fork("detail").NextUInt64();

        var map = new TerrainMap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width) + x;
                map[x, y] = BiomePalette.Pick(biome, elevation[i], moisture[i], StableHash.UnitAt(detailSeed, x, y));
            }
        }

        Connectivity.Ensure(map);
        return map;
    }
}
