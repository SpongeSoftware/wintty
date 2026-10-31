using Wintty.Render;
using Xunit;

namespace Wintty.Tests.Render;

public class GridSizerTests
{
    [Fact]
    public void Compute_FloorsToWholeCells()
    {
        Assert.Equal((10, 4), GridSizer.Compute(85, 70, 8, 16));
    }

    [Fact]
    public void Compute_SubtractsPaddingFromEveryEdge()
    {
        Assert.Equal((9, 3), GridSizer.Compute(85, 70, 8, 16, padding: 6));
    }

    [Fact]
    public void Compute_NeverReturnsLessThanOneCell()
    {
        Assert.Equal((1, 1), GridSizer.Compute(0, 0, 8, 16));
        Assert.Equal((1, 1), GridSizer.Compute(5, 5, 8, 16, padding: 20));
    }

    [Theory]
    [InlineData(0, 16, 0)]
    [InlineData(8, 0, 0)]
    [InlineData(-1, 16, 0)]
    [InlineData(8, 16, -1)]
    public void Compute_RejectsInvalidInputs(double cellWidth, double cellHeight, double padding) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => GridSizer.Compute(100, 100, cellWidth, cellHeight, padding));
}
