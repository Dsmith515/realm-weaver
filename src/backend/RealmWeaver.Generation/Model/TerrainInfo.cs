using System.Collections.Frozen;

namespace RealmWeaver.Generation.Model;

/// <summary>Static facts about each terrain type: single-character code (for compact storage), passability, display.</summary>
public sealed record TerrainInfo(Terrain Terrain, char Code, string Name, bool Passable, bool Difficult, string Color)
{
    public static readonly IReadOnlyList<TerrainInfo> All =
    [
        new(Terrain.Grass, '.', "Grass", true, false, "#8fbf6a"),
        new(Terrain.TallGrass, ',', "Tall grass", true, true, "#6fa653"),
        new(Terrain.Trees, 't', "Trees", true, true, "#3f7d3a"),
        new(Terrain.DenseForest, 'T', "Dense forest", false, false, "#24522a"),
        new(Terrain.ShallowWater, 'w', "Shallow water", true, true, "#7ab8d9"),
        new(Terrain.DeepWater, 'W', "Deep water", false, false, "#3b78a8"),
        new(Terrain.Sand, 's', "Sand", true, false, "#e2cf92"),
        new(Terrain.Rock, '^', "Rock / cliff", false, false, "#7d7469"),
        new(Terrain.Rubble, ':', "Rubble", true, true, "#a59a8a"),
        new(Terrain.Snow, '*', "Snow", true, true, "#eef3f6"),
        new(Terrain.Ice, 'i', "Ice", true, true, "#bfe3ef"),
        new(Terrain.Mud, 'm', "Mud", true, true, "#7a6a45"),
        new(Terrain.Lava, 'L', "Lava", false, false, "#d9481c"),
        new(Terrain.Ash, 'a', "Ash", true, false, "#5d5856"),
        new(Terrain.Ground, '_', "Cleared ground", true, false, "#c4a87a"),
        new(Terrain.Trail, '=', "Trail", true, false, "#b8935b"),
        new(Terrain.Bridge, '#', "Bridge / ford", true, false, "#9c6b3c"),
        new(Terrain.Wall, 'X', "Wall / solid rock", false, false, "#2f2b28"),
        new(Terrain.StoneFloor, 'o', "Stone floor", true, false, "#b9b2a6"),
        new(Terrain.CaveFloor, 'c', "Cave floor", true, false, "#8c7c68"),
        new(Terrain.TimberFloor, 'b', "Timber-braced floor", true, false, "#a8834f"),
        new(Terrain.Door, 'D', "Door / opening", true, false, "#6b4423"),
    ];

    private static readonly FrozenDictionary<Terrain, TerrainInfo> ByTerrain = All.ToFrozenDictionary(t => t.Terrain);
    private static readonly FrozenDictionary<char, TerrainInfo> ByCode = All.ToFrozenDictionary(t => t.Code);

    public static TerrainInfo Of(Terrain terrain) => ByTerrain[terrain];

    public static TerrainInfo FromCode(char code) =>
        ByCode.TryGetValue(code, out var info) ? info : throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown terrain code.");

    public static bool IsPassable(Terrain terrain) => ByTerrain[terrain].Passable;
}
