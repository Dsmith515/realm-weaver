using RealmWeaver.Generation.Model;

namespace RealmWeaver.Generation.Grid;

/// <summary>Mutable terrain grid used during generation. Stored row-major.</summary>
public sealed class TerrainMap
{
    private readonly Terrain[] _cells;

    public TerrainMap(int width, int height, Terrain fill = Terrain.Grass)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        _cells = new Terrain[width * height];
        Array.Fill(_cells, fill);
    }

    public int Width { get; }

    public int Height { get; }

    public int Area => Width * Height;

    public Terrain this[int x, int y]
    {
        get => _cells[(y * Width) + x];
        set => _cells[(y * Width) + x] = value;
    }

    public Terrain this[GridPoint p]
    {
        get => this[p.X, p.Y];
        set => this[p.X, p.Y] = value;
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public bool InBounds(GridPoint p) => InBounds(p.X, p.Y);

    public bool IsPassable(GridPoint p) => TerrainInfo.IsPassable(this[p]);

    public IEnumerable<GridPoint> AllTiles()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                yield return new GridPoint(x, y);
            }
        }
    }

    public IEnumerable<GridPoint> Neighbors4(GridPoint p)
    {
        if (p.X > 0) yield return p with { X = p.X - 1 };
        if (p.X < Width - 1) yield return p with { X = p.X + 1 };
        if (p.Y > 0) yield return p with { Y = p.Y - 1 };
        if (p.Y < Height - 1) yield return p with { Y = p.Y + 1 };
    }

    /// <summary>One string per row, one <see cref="TerrainInfo.Code"/> character per tile.</summary>
    public IReadOnlyList<string> ToRows()
    {
        var rows = new string[Height];
        var buffer = new char[Width];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                buffer[x] = TerrainInfo.Of(this[x, y]).Code;
            }

            rows[y] = new string(buffer);
        }

        return rows;
    }

    public static TerrainMap FromRows(IReadOnlyList<string> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var map = new TerrainMap(rows[0].Length, rows.Count);
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                map[x, y] = TerrainInfo.FromCode(rows[y][x]).Terrain;
            }
        }

        return map;
    }
}
