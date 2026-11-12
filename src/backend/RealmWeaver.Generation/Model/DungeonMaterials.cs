using System.Collections.Frozen;

namespace RealmWeaver.Generation.Model;

/// <summary>Display and generation facts for each <see cref="DungeonMaterial"/>.</summary>
public sealed record DungeonMaterials(DungeonMaterial Material, string Name, RoomStyle Style, Terrain Floor, string Color)
{
    public static readonly IReadOnlyList<DungeonMaterials> All =
    [
        new(DungeonMaterial.DressedStone, "Dressed stone", RoomStyle.Masonry, Terrain.StoneFloor, "#9aa3ad"),
        new(DungeonMaterial.CrumblingBrick, "Crumbling brick", RoomStyle.Masonry, Terrain.StoneFloor, "#b5715a"),
        new(DungeonMaterial.Sandstone, "Sandstone", RoomStyle.Masonry, Terrain.Sand, "#d9b46a"),
        new(DungeonMaterial.TimberBracedRock, "Timber-braced rock", RoomStyle.Mine, Terrain.TimberFloor, "#a8834f"),
        new(DungeonMaterial.NaturalCave, "Natural cave", RoomStyle.Cave, Terrain.CaveFloor, "#7d6b55"),
        new(DungeonMaterial.FloodedGrotto, "Flooded grotto", RoomStyle.Cave, Terrain.ShallowWater, "#4f93b8"),
        new(DungeonMaterial.CrystalVein, "Crystal vein", RoomStyle.Cave, Terrain.CaveFloor, "#9b7fd1"),
        new(DungeonMaterial.RootEarth, "Root-choked earth", RoomStyle.Cave, Terrain.Mud, "#6b7f3a"),
        new(DungeonMaterial.LavaTube, "Lava tube", RoomStyle.Cave, Terrain.Ash, "#d0582a"),
        new(DungeonMaterial.IceCave, "Ice cave", RoomStyle.Cave, Terrain.Ice, "#8fd0e6"),
    ];

    private static readonly FrozenDictionary<DungeonMaterial, DungeonMaterials> ByMaterial = All.ToFrozenDictionary(m => m.Material);

    public static DungeonMaterials Of(DungeonMaterial material) => ByMaterial[material];

    /// <summary>Materials a dungeon in this biome can branch into, away from its host structure.</summary>
    public static IReadOnlyList<DungeonMaterial> BranchesFor(Biome biome) => biome switch
    {
        Biome.Forest => [DungeonMaterial.NaturalCave, DungeonMaterial.RootEarth],
        Biome.Plains => [DungeonMaterial.NaturalCave, DungeonMaterial.RootEarth],
        Biome.Desert => [DungeonMaterial.NaturalCave, DungeonMaterial.CrystalVein],
        Biome.Tundra => [DungeonMaterial.IceCave, DungeonMaterial.NaturalCave],
        Biome.Swamp => [DungeonMaterial.FloodedGrotto, DungeonMaterial.RootEarth],
        Biome.Volcanic => [DungeonMaterial.LavaTube, DungeonMaterial.NaturalCave],
        Biome.Mountains => [DungeonMaterial.NaturalCave, DungeonMaterial.CrystalVein],
        Biome.Coast => [DungeonMaterial.FloodedGrotto, DungeonMaterial.NaturalCave],
        _ => [DungeonMaterial.NaturalCave],
    };
}
