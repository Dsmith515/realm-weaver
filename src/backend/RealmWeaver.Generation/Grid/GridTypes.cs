using System.Globalization;
using System.Text;

namespace RealmWeaver.Generation.Grid;

/// <summary>A tile position. X is the column (0 = A), Y is the row (0 = row 1), origin top-left.</summary>
public readonly record struct GridPoint(int X, int Y)
{
    public int ChebyshevDistanceTo(GridPoint other) => Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

    public override string ToString() => TileLabels.Label(X, Y);
}

/// <summary>An axis-aligned rectangle of tiles.</summary>
public readonly record struct GridRect(int X, int Y, int Width, int Height)
{
    public int Area => Width * Height;

    public int Right => X + Width - 1;

    public int Bottom => Y + Height - 1;

    public bool Contains(GridPoint p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    public bool Intersects(GridRect other) =>
        X <= other.Right && other.X <= Right && Y <= other.Bottom && other.Y <= Bottom;

    public IEnumerable<GridPoint> Tiles()
    {
        for (var y = Y; y <= Bottom; y++)
        {
            for (var x = X; x <= Right; x++)
            {
                yield return new GridPoint(x, y);
            }
        }
    }

    /// <summary>Smallest Chebyshev distance from <paramref name="p"/> to any tile of this rectangle (0 if inside).</summary>
    public int ChebyshevDistanceTo(GridPoint p)
    {
        var dx = Math.Max(0, Math.Max(X - p.X, p.X - Right));
        var dy = Math.Max(0, Math.Max(Y - p.Y, p.Y - Bottom));
        return Math.Max(dx, dy);
    }

    public override string ToString() => TileLabels.Range(this);
}

/// <summary>
/// Alphanumeric map coordinates: columns A..Z, AA..AZ, BA.. (A = first column), rows 1..N (1 = top row).
/// </summary>
public static class TileLabels
{
    public static string Column(int x)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        var sb = new StringBuilder(3);
        var n = x + 1;
        while (n > 0)
        {
            n--;
            sb.Insert(0, (char)('A' + (n % 26)));
            n /= 26;
        }

        return sb.ToString();
    }

    public static string Row(int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        return (y + 1).ToString(CultureInfo.InvariantCulture);
    }

    public static string Label(int x, int y) => Column(x) + Row(y);

    public static string Label(GridPoint p) => Label(p.X, p.Y);

    /// <summary>"C3" for a single tile, otherwise "C3-F6" (top-left to bottom-right).</summary>
    public static string Range(GridRect rect) =>
        rect.Area == 1 ? Label(rect.X, rect.Y) : $"{Label(rect.X, rect.Y)}-{Label(rect.Right, rect.Bottom)}";
}
