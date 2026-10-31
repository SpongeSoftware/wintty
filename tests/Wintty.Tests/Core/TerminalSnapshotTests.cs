using Wintty.Core.Abstractions;
using Xunit;

namespace Wintty.Tests.Core;

public class TerminalSnapshotTests
{
    private static readonly TerminalColor White = new(255, 255, 255);
    private static readonly TerminalColor Black = new(0, 0, 0);
    private static readonly TerminalColor Red = new(255, 0, 0);

    private static TerminalCell Cell(string text, CellAttributes attributes = CellAttributes.None, int width = 1) =>
        new(text, White, Black, attributes, UnderlineStyle.None, width);

    private static TerminalSnapshot Snapshot(int columns, int rows, params TerminalCell[] cells)
    {
        var all = new TerminalCell[columns * rows];
        Array.Fill(all, Cell(string.Empty));
        cells.CopyTo(all, 0);
        return new TerminalSnapshot(columns, rows, all, new CursorState(1, 0, true, CursorStyle.Bar), White, Black);
    }

    [Fact]
    public void Constructor_ExposesValues()
    {
        var snapshot = Snapshot(3, 2);

        Assert.Equal(3, snapshot.Columns);
        Assert.Equal(2, snapshot.Rows);
        Assert.Equal(new CursorState(1, 0, true, CursorStyle.Bar), snapshot.Cursor);
        Assert.Equal(White, snapshot.DefaultForeground);
        Assert.Equal(Black, snapshot.DefaultBackground);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void Constructor_RejectsNonPositiveSize(int columns, int rows) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TerminalSnapshot(columns, rows, [], default, White, Black));

    [Fact]
    public void Constructor_RejectsNullCells() =>
        Assert.Throws<ArgumentNullException>(() => new TerminalSnapshot(1, 1, null!, default, White, Black));

    [Fact]
    public void Constructor_RejectsWrongCellCount() =>
        Assert.Throws<ArgumentException>(() => new TerminalSnapshot(2, 2, new TerminalCell[3], default, White, Black));

    [Fact]
    public void Indexer_ReturnsRowMajorCells()
    {
        var snapshot = Snapshot(3, 2, Cell("a"), Cell("b"), Cell("c"), Cell("d"));

        Assert.Equal("a", snapshot[0, 0].Text);
        Assert.Equal("c", snapshot[2, 0].Text);
        Assert.Equal("d", snapshot[0, 1].Text);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 2)]
    public void Indexer_RejectsOutOfRange(int column, int row)
    {
        var snapshot = Snapshot(3, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot[column, row]);
    }

    [Fact]
    public void GetRowText_JoinsCellsTrimsTrailingBlanksAndSkipsWideTails()
    {
        var snapshot = Snapshot(5, 1, Cell("h"), Cell(string.Empty), Cell("中", width: 2), Cell(string.Empty, width: 0));

        Assert.Equal("h 中", snapshot.GetRowText(0));
    }

    [Fact]
    public void GetRowText_BlankRowIsEmpty() => Assert.Equal(string.Empty, Snapshot(4, 1).GetRowText(0));

    [Fact]
    public void EffectiveColours_SwapWhenInverse()
    {
        var normal = new TerminalCell("x", Red, Black);
        var inverse = new TerminalCell("x", Red, Black, CellAttributes.Inverse);

        Assert.Equal(Red, normal.EffectiveForeground);
        Assert.Equal(Black, normal.EffectiveBackground);
        Assert.Equal(Black, inverse.EffectiveForeground);
        Assert.Equal(Red, inverse.EffectiveBackground);
    }
}
