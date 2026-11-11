using RealmWeaver.Generation.Events;
using RealmWeaver.Generation.Grid;
using RealmWeaver.Generation.Model;
using RealmWeaver.Generation.Randomness;

namespace RealmWeaver.Generation.Tests;

public class TileLabelTests
{
    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(51, "AZ")]
    [InlineData(52, "BA")]
    [InlineData(99, "CV")]
    public void Column_labels_follow_spreadsheet_style(int x, string expected) =>
        Assert.Equal(expected, TileLabels.Column(x));

    [Fact]
    public void Tile_label_uses_one_based_rows() => Assert.Equal("C12", TileLabels.Label(2, 11));

    [Fact]
    public void Range_label_spans_top_left_to_bottom_right()
    {
        Assert.Equal("B3-E6", TileLabels.Range(new GridRect(1, 2, 4, 4)));
        Assert.Equal("A1", TileLabels.Range(new GridRect(0, 0, 1, 1)));
    }

    [Fact]
    public void Rect_distance_is_zero_inside_and_chebyshev_outside()
    {
        var rect = new GridRect(5, 5, 3, 3);
        Assert.Equal(0, rect.ChebyshevDistanceTo(new GridPoint(6, 6)));
        Assert.Equal(5, rect.ChebyshevDistanceTo(new GridPoint(0, 6)));
        Assert.Equal(3, rect.ChebyshevDistanceTo(new GridPoint(10, 10)));
    }
}

public class EventBudgetTests
{
    [Fact]
    public void Smallest_map_has_a_small_range_starting_at_five()
    {
        var range = EventBudget.DefaultRange(15 * 15);
        Assert.Equal(5, range.Min);
        Assert.InRange(range.Max, 5, 8);
    }

    [Fact]
    public void Largest_map_defaults_to_at_most_fifty_and_allows_sixty()
    {
        var range = EventBudget.DefaultRange(100 * 100);
        Assert.Equal(50, range.Max);
        Assert.True(range.Min > 5, "larger maps should raise the minimum");
        Assert.Equal(60, EventBudget.OverrideCap(100 * 100));
    }

    [Fact]
    public void Ranges_grow_with_area()
    {
        var previous = EventBudget.DefaultRange(225);
        foreach (var side in new[] { 30, 50, 75, 100 })
        {
            var current = EventBudget.DefaultRange(side * side);
            Assert.True(current.Min >= previous.Min && current.Max >= previous.Max);
            previous = current;
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(61)]
    public void Requested_counts_outside_limits_are_rejected(int count) =>
        Assert.Throws<RealmGenerationException>(() => EventBudget.Resolve(100 * 100, count, new SeededRandom(1)));

    [Fact]
    public void Requested_count_above_the_size_cap_is_rejected_on_small_maps() =>
        Assert.Throws<RealmGenerationException>(() => EventBudget.Resolve(15 * 15, 30, new SeededRandom(1)));
}
