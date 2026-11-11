namespace RealmWeaver.Generation.Model;

/// <summary>Set by the Thread component.</summary>
public enum Biome
{
    Plains,
    Forest,
    Desert,
    Tundra,
    Swamp,
    Volcanic,
    Mountains,
    Coast,
}

/// <summary>Set by the Thread component.</summary>
public enum Element
{
    None,
    Fire,
    Water,
    Earth,
    Air,
    Frost,
    Lightning,
    Light,
    Shadow,
}

/// <summary>Set by the Coating component.</summary>
public enum RealmKind
{
    Field,
    Dungeon,
    FieldWithDungeon,
}

public enum AreaKind
{
    Field,
    DungeonFloor,
}

public enum Terrain
{
    Grass,
    TallGrass,
    Trees,
    DenseForest,
    ShallowWater,
    DeepWater,
    Sand,
    Rock,
    Rubble,
    Snow,
    Ice,
    Mud,
    Lava,
    Ash,
    Ground,
    Trail,
    Bridge,

    // Dungeon terrain
    Wall,
    StoneFloor,
    CaveFloor,
    TimberFloor,
    Door,
}

/// <summary>What a dungeon room is built from. Set by the host structure, with biome-specific branches.</summary>
public enum DungeonMaterial
{
    DressedStone,
    CrumblingBrick,
    Sandstone,
    TimberBracedRock,
    NaturalCave,
    FloodedGrotto,
    CrystalVein,
    RootEarth,
    LavaTube,
    IceCave,
}

/// <summary>How rooms of a material are shaped.</summary>
public enum RoomStyle
{
    /// <summary>Built rooms: rectangles and L-shapes with doors.</summary>
    Masonry,

    /// <summary>Dug rooms: long shafts with side galleries.</summary>
    Mine,

    /// <summary>Natural rooms: irregular cellular-automata blobs.</summary>
    Cave,
}
