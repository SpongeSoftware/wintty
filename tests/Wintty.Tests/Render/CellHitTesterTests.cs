using Wintty.Core.Abstractions;
using Wintty.Render;
using Xunit;

namespace Wintty.Tests.Render;

public class CellHitTesterTests
{
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(9.9, 19.9, 0, 0)]
    [InlineData(10, 20, 1, 1)]
    [InlineData(55, 45, 5, 2)]
    public void CellAt_WithoutPadding_DividesByCellSize(double x, double y, int column, int row)
    {
        Assert.Equal((column, row), CellHitTester.CellAt(x, y, 10, 20, 0, 80, 24));
    }

    [Theory]
    [InlineData(5.9, 5.9, 0, 0)]
    [InlineData(6, 6, 0, 0)]
    [InlineData(16, 26, 1, 1)]
    public void CellAt_RemovesThePaddingFirst(double x, double y, int column, int row)
    {
        Assert.Equal((column, row), CellHitTester.CellAt(x, y, 10, 20, 6, 80, 24));
    }

    [Fact]
    public void CellAt_ClampsToTheGrid()
    {
        Assert.Equal((0, 0), CellHitTester.CellAt(-50, -50, 10, 20, 6, 80, 24));
        Assert.Equal((79, 23), CellHitTester.CellAt(5000, 5000, 10, 20, 6, 80, 24));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void CellAt_RejectsNonPositiveCellSizes(double cellWidth, double cellHeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CellHitTester.CellAt(0, 0, cellWidth, cellHeight, 0, 1, 1));
    }

    [Fact]
    public void CellAt_RejectsAnEmptyGrid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CellHitTester.CellAt(0, 0, 1, 1, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CellHitTester.CellAt(0, 0, 1, 1, 0, 1, 0));
    }

    [Fact]
    public void ResolveWideColumn_StepsBackOverTheTrailingHalf()
    {
        var white = new TerminalColor(255, 255, 255);
        var black = new TerminalColor(0, 0, 0);
        var cells = new[]
        {
            new TerminalCell("a", white, black),
            new TerminalCell("世", white, black, Width: 2),
            new TerminalCell(string.Empty, white, black, Width: 0),
            new TerminalCell("b", white, black),
        };
        var snapshot = new TerminalSnapshot(4, 1, cells, default, white, black);

        Assert.Equal(1, CellHitTester.ResolveWideColumn(snapshot, 2, 0));
        Assert.Equal(1, CellHitTester.ResolveWideColumn(snapshot, 1, 0));
        Assert.Equal(3, CellHitTester.ResolveWideColumn(snapshot, 3, 0));
        Assert.Equal(0, CellHitTester.ResolveWideColumn(snapshot, 0, 0));
    }

    [Fact]
    public void ResolveWideColumn_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => CellHitTester.ResolveWideColumn(null!, 0, 0));
}
