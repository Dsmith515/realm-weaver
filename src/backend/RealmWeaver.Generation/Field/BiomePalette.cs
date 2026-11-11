using RealmWeaver.Generation.Model;

namespace RealmWeaver.Generation.Field;

/// <summary>
/// Maps normalized elevation and moisture (both 0-1) to a terrain type for each biome.
/// Thresholds keep impassable terrain to a minority so fields stay playable.
/// </summary>
public static class BiomePalette
{
    public static Terrain Pick(Biome biome, double elevation, double moisture, double detail) => biome switch
    {
        Biome.Forest => elevation switch
        {
            < 0.14 => Terrain.DeepWater,
            < 0.20 => Terrain.ShallowWater,
            > 0.90 => Terrain.Rock,
            _ when moisture > 0.78 => Terrain.DenseForest,
            _ when moisture > 0.45 => Terrain.Trees,
            _ => detail < 0.3 ? Terrain.TallGrass : Terrain.Grass,
        },
        Biome.Plains => elevation switch
        {
            < 0.12 => Terrain.DeepWater,
            < 0.18 => Terrain.ShallowWater,
            > 0.92 => Terrain.Rock,
            _ when moisture > 0.80 => Terrain.Trees,
            _ when moisture > 0.55 => Terrain.TallGrass,
            _ => Terrain.Grass,
        },
        Biome.Desert => elevation switch
        {
            < 0.06 => Terrain.ShallowWater,
            > 0.86 => Terrain.Rock,
            > 0.74 => Terrain.Rubble,
            _ when moisture > 0.90 => Terrain.Grass,
            _ => Terrain.Sand,
        },
        Biome.Tundra => elevation switch
        {
            < 0.12 => Terrain.DeepWater,
            < 0.22 => Terrain.Ice,
            > 0.88 => Terrain.Rock,
            _ when moisture > 0.80 => Terrain.Trees,
            _ when moisture < 0.30 => Terrain.Grass,
            _ => Terrain.Snow,
        },
        Biome.Swamp => elevation switch
        {
            < 0.16 => Terrain.DeepWater,
            < 0.34 => Terrain.ShallowWater,
            < 0.50 => Terrain.Mud,
            _ when moisture > 0.72 => Terrain.Trees,
            _ => detail < 0.5 ? Terrain.TallGrass : Terrain.Grass,
        },
        Biome.Volcanic => elevation switch
        {
            > 0.88 => Terrain.Lava,
            > 0.78 => Terrain.Rock,
            < 0.18 => Terrain.Rubble,
            _ when moisture > 0.70 => Terrain.Rubble,
            _ => Terrain.Ash,
        },
        Biome.Mountains => elevation switch
        {
            > 0.86 => Terrain.Snow,
            > 0.70 => Terrain.Rock,
            > 0.55 => Terrain.Rubble,
            < 0.10 => Terrain.ShallowWater,
            _ when moisture > 0.70 => Terrain.Trees,
            _ => Terrain.Grass,
        },
        Biome.Coast => elevation switch
        {
            < 0.22 => Terrain.DeepWater,
            < 0.30 => Terrain.ShallowWater,
            < 0.40 => Terrain.Sand,
            > 0.92 => Terrain.Rock,
            _ when moisture > 0.70 => Terrain.Trees,
            _ => detail < 0.3 ? Terrain.TallGrass : Terrain.Grass,
        },
        _ => Terrain.Grass,
    };

    /// <summary>What an impassable tile becomes when a path is carved through it.</summary>
    public static Terrain Carve(Terrain terrain) => terrain switch
    {
        Terrain.DeepWater or Terrain.Lava => Terrain.Bridge,
        _ => Terrain.Trail,
    };
}
