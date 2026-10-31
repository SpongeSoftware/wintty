using Wintty.Core.Abstractions;
using Wintty.Render;
using Xunit;

namespace Wintty.Tests.Render;

public class RenderPlannerTests
{
    private static readonly TerminalColor Fg = new(200, 200, 200);
    private static readonly TerminalColor Bg = new(10, 10, 10);
    private static readonly TerminalColor Red = new(255, 0, 0);
    private static readonly TerminalColor Blue = new(0, 0, 255);

    private static TerminalCell Blank() => new(string.Empty, Fg, Bg);

    private static TerminalCell Ch(string text, TerminalColor? fg = null, TerminalColor? bg = null,
        CellAttributes attributes = CellAttributes.None, UnderlineStyle underline = UnderlineStyle.None, int width = 1) =>
        new(text, fg ?? Fg, bg ?? Bg, attributes, underline, width);

    private static TerminalSnapshot Snapshot(int columns, int rows, CursorState? cursor = null, params (int Column, int Row, TerminalCell Cell)[] cells)
    {
        var all = new TerminalCell[columns * rows];
        Array.Fill(all, Blank());
        foreach (var (column, row, cell) in cells)
        {
            all[(row * columns) + column] = cell;
        }

        return new TerminalSnapshot(columns, rows, all, cursor ?? new CursorState(0, 0, false, CursorStyle.Block), Fg, Bg);
    }

    private static TerminalSnapshot Row(params TerminalCell[] cells)
    {
        var snapshot = new TerminalCell[cells.Length];
        cells.CopyTo(snapshot, 0);
        return new TerminalSnapshot(cells.Length, 1, snapshot, new CursorState(0, 0, false, CursorStyle.Block), Fg, Bg);
    }

    [Fact]
    public void Build_RejectsNull() => Assert.Throws<ArgumentNullException>(() => RenderPlanner.Build(null!));

    [Fact]
    public void Build_CarriesDimensionsAndBackground()
    {
        var plan = RenderPlanner.Build(Snapshot(7, 3));

        Assert.Equal(7, plan.Columns);
        Assert.Equal(3, plan.Rows);
        Assert.Equal(Bg, plan.Background);
        Assert.Empty(plan.Backgrounds);
        Assert.Empty(plan.Texts);
        Assert.Empty(plan.Decorations);
        Assert.Null(plan.Cursor);
    }

    [Fact]
    public void Text_CellsWithSameStyleMergeIntoOneRun()
    {
        var plan = RenderPlanner.Build(Row(Ch("h"), Ch("i"), Ch("!")));

        var run = Assert.Single(plan.Texts);
        Assert.Equal(new TextRun(0, 0, 3, "hi!", Fg, false, false, false), run);
    }

    [Fact]
    public void Text_BlankCellSplitsRuns()
    {
        var plan = RenderPlanner.Build(Row(Ch("a"), Blank(), Ch("b")));

        Assert.Equal(2, plan.Texts.Count);
        Assert.Equal((0, 1, "a"), (plan.Texts[0].Column, plan.Texts[0].CellCount, plan.Texts[0].Text));
        Assert.Equal((2, 1, "b"), (plan.Texts[1].Column, plan.Texts[1].CellCount, plan.Texts[1].Text));
    }

    [Fact]
    public void Text_StyleChangeSplitsRuns()
    {
        var plan = RenderPlanner.Build(Row(Ch("a"), Ch("b", fg: Red), Ch("c", attributes: CellAttributes.Bold)));

        Assert.Equal(3, plan.Texts.Count);
        Assert.Equal(Red, plan.Texts[1].Color);
        Assert.True(plan.Texts[2].Bold);
    }

    [Fact]
    public void Text_ItalicAndFaintAreFlagged()
    {
        var plan = RenderPlanner.Build(Row(Ch("a", attributes: CellAttributes.Italic | CellAttributes.Faint)));

        var run = Assert.Single(plan.Texts);
        Assert.True(run.Italic);
        Assert.True(run.Faint);
        Assert.False(run.Bold);
    }

    [Fact]
    public void Text_InvisibleCellsAreNotDrawn()
    {
        var plan = RenderPlanner.Build(Row(Ch("s", attributes: CellAttributes.Invisible), Ch("v")));

        var run = Assert.Single(plan.Texts);
        Assert.Equal("v", run.Text);
    }

    [Fact]
    public void Text_InverseUsesBackgroundAsTextColour()
    {
        var plan = RenderPlanner.Build(Row(Ch("x", fg: Red, bg: Blue, attributes: CellAttributes.Inverse)));

        Assert.Equal(Blue, Assert.Single(plan.Texts).Color);
    }

    [Fact]
    public void Text_WideCharacterIsItsOwnRunSpanningTwoCellsAndTailIsSkipped()
    {
        var plan = RenderPlanner.Build(Row(Ch("a"), Ch("中", width: 2), Ch(string.Empty, width: 0), Ch("b")));

        Assert.Equal(3, plan.Texts.Count);
        Assert.Equal((1, 2, "中"), (plan.Texts[1].Column, plan.Texts[1].CellCount, plan.Texts[1].Text));
        Assert.Equal(3, plan.Texts[2].Column);
    }

    [Fact]
    public void Backgrounds_DefaultBackgroundProducesNoRuns()
    {
        Assert.Empty(RenderPlanner.Build(Row(Ch("a"), Ch("b"))).Backgrounds);
    }

    [Fact]
    public void Backgrounds_AdjacentSameColourCellsMerge()
    {
        var plan = RenderPlanner.Build(Row(Blank(), Ch("a", bg: Red), Ch("b", bg: Red), Blank()));

        Assert.Equal(new BackgroundRun(0, 1, 2, Red), Assert.Single(plan.Backgrounds));
    }

    [Fact]
    public void Backgrounds_DifferentColoursAndRunsReachingTheEdgeAreSplit()
    {
        var plan = RenderPlanner.Build(Row(Ch("a", bg: Red), Ch("b", bg: Blue), Ch("c", bg: Blue)));

        Assert.Equal(
            [new BackgroundRun(0, 0, 1, Red), new BackgroundRun(0, 1, 2, Blue)],
            plan.Backgrounds);
    }

    [Fact]
    public void Backgrounds_InverseUsesForegroundAsFill()
    {
        var plan = RenderPlanner.Build(Row(Ch("x", fg: Red, attributes: CellAttributes.Inverse)));

        Assert.Equal(Red, Assert.Single(plan.Backgrounds).Color);
    }

    [Fact]
    public void Backgrounds_AreProducedPerRow()
    {
        var snapshot = Snapshot(2, 2, null, (0, 0, Ch("a", bg: Red)), (0, 1, Ch("b", bg: Red)));

        var plan = RenderPlanner.Build(snapshot);

        Assert.Equal([new BackgroundRun(0, 0, 1, Red), new BackgroundRun(1, 0, 1, Red)], plan.Backgrounds);
    }

    [Theory]
    [InlineData(UnderlineStyle.Single, DecorationKind.Underline)]
    [InlineData(UnderlineStyle.Double, DecorationKind.DoubleUnderline)]
    [InlineData(UnderlineStyle.Curly, DecorationKind.CurlyUnderline)]
    [InlineData(UnderlineStyle.Dotted, DecorationKind.DottedUnderline)]
    [InlineData(UnderlineStyle.Dashed, DecorationKind.DashedUnderline)]
    public void Decorations_UnderlineStylesMap(UnderlineStyle style, DecorationKind expected)
    {
        var plan = RenderPlanner.Build(Row(Ch("a", underline: style)));

        Assert.Equal(expected, Assert.Single(plan.Decorations).Kind);
    }

    [Fact]
    public void Decorations_UnderlineUsesItsOwnColourWhenSet()
    {
        var cell = new TerminalCell("a", Fg, Bg, CellAttributes.None, UnderlineStyle.Curly, 1, Red);

        var plan = RenderPlanner.Build(Row(cell));

        Assert.Equal(new DecorationRun(0, 0, 1, DecorationKind.CurlyUnderline, Red), Assert.Single(plan.Decorations));
    }

    [Fact]
    public void Decorations_UnderlineColourDoesNotAffectStrikethrough()
    {
        var cell = new TerminalCell("a", Fg, Bg, CellAttributes.Strikethrough, UnderlineStyle.Single, 1, Red);

        var plan = RenderPlanner.Build(Row(cell));

        Assert.Equal(Red, plan.Decorations.Single(d => d.Kind == DecorationKind.Underline).Color);
        Assert.Equal(Fg, plan.Decorations.Single(d => d.Kind == DecorationKind.Strikethrough).Color);
    }

    [Fact]
    public void Decorations_DifferentUnderlineColoursDoNotMerge()
    {
        var plan = RenderPlanner.Build(Row(
            new TerminalCell("a", Fg, Bg, CellAttributes.None, UnderlineStyle.Single, 1, Red),
            new TerminalCell("b", Fg, Bg, CellAttributes.None, UnderlineStyle.Single, 1, Blue)));

        Assert.Equal(2, plan.Decorations.Count);
    }

    [Fact]
    public void Decorations_StrikethroughAndOverlineAreEmitted()
    {
        var plan = RenderPlanner.Build(Row(Ch("a", attributes: CellAttributes.Strikethrough | CellAttributes.Overline)));

        Assert.Contains(plan.Decorations, d => d.Kind == DecorationKind.Strikethrough);
        Assert.Contains(plan.Decorations, d => d.Kind == DecorationKind.Overline);
    }

    [Fact]
    public void Decorations_AdjacentIdenticalCellsMergeAndWideCellsCountTwo()
    {
        var plan = RenderPlanner.Build(Row(
            Ch("a", underline: UnderlineStyle.Single),
            Ch("b", underline: UnderlineStyle.Single),
            Ch("中", underline: UnderlineStyle.Single, width: 2),
            Ch(string.Empty, width: 0)));

        Assert.Equal(new DecorationRun(0, 0, 4, DecorationKind.Underline, Fg), Assert.Single(plan.Decorations));
    }

    [Fact]
    public void Decorations_DifferentColourDoesNotMerge()
    {
        var plan = RenderPlanner.Build(Row(
            Ch("a", underline: UnderlineStyle.Single),
            Ch("b", fg: Red, underline: UnderlineStyle.Single)));

        Assert.Equal(2, plan.Decorations.Count);
    }

    [Fact]
    public void Cursor_IsPlacedWithStyleAndDefaultForeground()
    {
        var snapshot = Snapshot(5, 2, new CursorState(3, 1, true, CursorStyle.Bar));

        var cursor = RenderPlanner.Build(snapshot).Cursor;

        Assert.Equal(new CursorShape(1, 3, 1, CursorStyle.Bar, Fg), cursor);
    }

    [Fact]
    public void Cursor_UsesItsOwnColourWhenTheSnapshotHasOne()
    {
        var snapshot = Snapshot(5, 1, new CursorState(0, 0, true, CursorStyle.Block, Red));

        Assert.Equal(Red, RenderPlanner.Build(snapshot).Cursor!.Value.Color);
    }

    [Fact]
    public void Cursor_OverWideCharacterSpansTwoCells()
    {
        var snapshot = Snapshot(5, 1, new CursorState(1, 0, true, CursorStyle.Block), (1, 0, Ch("中", width: 2)));

        Assert.Equal(2, RenderPlanner.Build(snapshot).Cursor!.Value.CellCount);
    }

    [Fact]
    public void Cursor_HiddenWhenBlinkedOffOrNotVisible()
    {
        var visible = Snapshot(5, 1, new CursorState(0, 0, true, CursorStyle.Block));
        var hidden = Snapshot(5, 1, new CursorState(0, 0, false, CursorStyle.Block));

        Assert.Null(RenderPlanner.Build(visible, cursorVisible: false).Cursor);
        Assert.Null(RenderPlanner.Build(hidden).Cursor);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(5, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 2)]
    public void Cursor_OutsideGridIsIgnored(int column, int row)
    {
        var snapshot = Snapshot(5, 2, new CursorState(column, row, true, CursorStyle.Block));

        Assert.Null(RenderPlanner.Build(snapshot).Cursor);
    }
}
