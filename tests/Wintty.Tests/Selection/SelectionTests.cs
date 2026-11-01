using Wintty.Core.Abstractions;
using Wintty.Core.Selection;
using Xunit;

namespace Wintty.Tests.Selection;

internal static class Screens
{
    private static readonly TerminalColor Fg = new(200, 200, 200);
    private static readonly TerminalColor Bg = new(0, 0, 0);

    /// <summary>Builds a snapshot from text rows; a "世" takes two cells (character plus a zero-width tail).</summary>
    internal static TerminalSnapshot From(int columns, string[] rows, long offset = 0, long total = 0, bool[]? wrapped = null)
    {
        var cells = new TerminalCell[columns * rows.Length];
        for (var row = 0; row < rows.Length; row++)
        {
            var column = 0;
            foreach (var rune in rows[row].EnumerateRunes())
            {
                var wide = rune.Value == '世';
                cells[(row * columns) + column] = new TerminalCell(rune.ToString(), Fg, Bg, Width: wide ? 2 : 1);
                column++;
                if (wide)
                {
                    cells[(row * columns) + column] = new TerminalCell(string.Empty, Fg, Bg, Width: 0);
                    column++;
                }
            }

            for (; column < columns; column++)
            {
                cells[(row * columns) + column] = new TerminalCell(string.Empty, Fg, Bg);
            }
        }

        var scrollbar = new ScrollbarState(Math.Max(total, offset + rows.Length), offset, rows.Length);
        return new TerminalSnapshot(columns, rows.Length, cells, default, Fg, Bg, scrollbar, wrapped);
    }
}

public class SelectionPointTests
{
    [Fact]
    public void Ordering_ComparesRowsThenColumns()
    {
        var early = new SelectionPoint(1, 9);
        var late = new SelectionPoint(2, 0);

        Assert.True(early < late);
        Assert.True(late > early);
        var same = new SelectionPoint(1, 9);
        Assert.True(early <= same);
        Assert.True(early >= same);
        Assert.True(new SelectionPoint(2, 1) > late);
        Assert.Equal(0, late.CompareTo(late));
    }
}

public class TerminalSelectionTests
{
    [Fact]
    public void StartAndEnd_AreOrderedRegardlessOfDragDirection()
    {
        var selection = new TerminalSelection(new SelectionPoint(5, 3), new SelectionPoint(2, 7));

        Assert.Equal(new SelectionPoint(2, 7), selection.Start);
        Assert.Equal(new SelectionPoint(5, 3), selection.End);
        Assert.Equal(2, selection.FirstRow);
        Assert.Equal(5, selection.LastRow);
    }

    [Theory]
    [InlineData(SelectionKind.Character, true)]
    [InlineData(SelectionKind.Block, true)]
    [InlineData(SelectionKind.Word, false)]
    [InlineData(SelectionKind.Line, false)]
    public void IsEmpty_OnlyForAnUndraggedCharacterOrBlock(SelectionKind kind, bool expected)
    {
        var point = new SelectionPoint(1, 1);

        Assert.Equal(expected, new TerminalSelection(point, point, kind).IsEmpty);
    }

    [Fact]
    public void SpanOnRow_CharacterSelectionRunsThroughTheMiddleRows()
    {
        var selection = new TerminalSelection(new SelectionPoint(1, 4), new SelectionPoint(3, 2));

        Assert.Null(selection.SpanOnRow(0, 10));
        Assert.Equal((4, 10), selection.SpanOnRow(1, 10));
        Assert.Equal((0, 10), selection.SpanOnRow(2, 10));
        Assert.Equal((0, 3), selection.SpanOnRow(3, 10));
        Assert.Null(selection.SpanOnRow(4, 10));
    }

    [Fact]
    public void SpanOnRow_SingleRowIncludesBothEndCells()
    {
        var selection = new TerminalSelection(new SelectionPoint(1, 6), new SelectionPoint(1, 2));

        Assert.Equal((2, 7), selection.SpanOnRow(1, 10));
    }

    [Fact]
    public void SpanOnRow_ClampsToTheGridWidth() =>
        Assert.Equal((2, 10), new TerminalSelection(new SelectionPoint(1, 2), new SelectionPoint(1, 20)).SpanOnRow(1, 10));

    [Fact]
    public void SpanOnRow_LineSelectionTakesWholeRows()
    {
        var selection = new TerminalSelection(new SelectionPoint(1, 4), new SelectionPoint(2, 0), SelectionKind.Line);

        Assert.Equal((0, 10), selection.SpanOnRow(1, 10));
        Assert.Equal((0, 10), selection.SpanOnRow(2, 10));
    }

    [Fact]
    public void SpanOnRow_BlockSelectionIsARectangleInEitherDragDirection()
    {
        var selection = new TerminalSelection(new SelectionPoint(3, 6), new SelectionPoint(1, 2), SelectionKind.Block);

        Assert.Equal(1, selection.FirstRow);
        Assert.Equal(3, selection.LastRow);
        Assert.Equal((2, 7), selection.SpanOnRow(2, 10));
        Assert.Equal((2, 7), selection.SpanOnRow(3, 10));
        Assert.Null(selection.SpanOnRow(4, 10));
    }
}

public class WordBoundaryTests
{
    [Theory]
    [InlineData(0, 0, 4)]
    [InlineData(3, 0, 4)]
    [InlineData(5, 5, 6)]
    [InlineData(6, 5, 6)]
    [InlineData(8, 7, 17)]
    public void Find_SelectsRunsOfTheSameKind(int column, int start, int end)
    {
        var snapshot = Screens.From(20, ["abcde  foo/bar.txt"]);

        Assert.Equal((start, end), WordBoundary.Find(snapshot, 0, column));
    }

    [Fact]
    public void Find_StopsAtBracketsAndQuotes()
    {
        var snapshot = Screens.From(20, ["(\"path/to\")"]);

        Assert.Equal((2, 8), WordBoundary.Find(snapshot, 0, 4));
        Assert.Equal((0, 0), WordBoundary.Find(snapshot, 0, 0));
    }

    [Fact]
    public void Find_BlankCellsSelectTheWholeGap()
    {
        var snapshot = Screens.From(10, ["ab   cd"]);

        Assert.Equal((2, 4), WordBoundary.Find(snapshot, 0, 3));
    }

    [Fact]
    public void Find_TreatsAWideCharacterAsOneWord()
    {
        var snapshot = Screens.From(10, ["a世b c"]);

        Assert.Equal((0, 3), WordBoundary.Find(snapshot, 0, 2));
        Assert.Equal((0, 3), WordBoundary.Find(snapshot, 0, 1));
    }

    [Fact]
    public void Find_RejectsNull() => Assert.Throws<ArgumentNullException>(() => WordBoundary.Find(null!, 0, 0));
}

public class SelectionTextTests
{
    private static SelectionPoint P(long row, int column) => new(row, column);

    private static string Extract(TerminalSelection selection, TerminalSnapshot page) => SelectionText.Extract(selection, _ => page);

    [Fact]
    public void SingleRow_ReturnsTheSelectedCells()
    {
        var page = Screens.From(20, ["hello world"]);

        Assert.Equal("lo wo", Extract(new TerminalSelection(P(0, 3), P(0, 7)), page));
    }

    [Fact]
    public void Rows_AreJoinedWithNewlinesAndTrailingBlanksDropped()
    {
        var page = Screens.From(20, ["one   ", "two", "three"]);

        Assert.Equal("one\ntwo\nth", Extract(new TerminalSelection(P(0, 0), P(2, 1)), page));
    }

    [Fact]
    public void WrappedRows_AreJoinedWithoutABreak()
    {
        var page = Screens.From(5, ["abcde", "fghij", "klm"], wrapped: [false, true, true]);

        Assert.Equal("abcdefghijklm", Extract(new TerminalSelection(P(0, 0), P(2, 4)), page));
    }

    [Fact]
    public void WideCharacters_AreCopiedOnce()
    {
        var page = Screens.From(10, ["a世b"]);

        Assert.Equal("a世b", Extract(new TerminalSelection(P(0, 0), P(0, 3)), page));
    }

    [Fact]
    public void BlankCellsInsideALine_BecomeSpaces()
    {
        var page = Screens.From(10, ["a  b"]);

        Assert.Equal("a  b", Extract(new TerminalSelection(P(0, 0), P(0, 3)), page));
    }

    [Fact]
    public void BlockSelection_CopiesARectangleAndAlwaysBreaksLines()
    {
        var page = Screens.From(6, ["abcdef", "ghijkl", "mnopqr"], wrapped: [false, true, true]);

        Assert.Equal("bc\nhi\nno", Extract(new TerminalSelection(P(0, 1), P(2, 2), SelectionKind.Block), page));
    }

    [Fact]
    public void LineSelection_CopiesWholeRowsWithoutTrailingBlanks()
    {
        var page = Screens.From(10, ["first", "second"]);

        Assert.Equal("first\nsecond", Extract(new TerminalSelection(P(0, 3), P(1, 0), SelectionKind.Line), page));
    }

    [Fact]
    public void SelectionSpanningPages_AsksForEachPage()
    {
        var requested = new List<long>();
        var pages = new Dictionary<long, TerminalSnapshot>
        {
            [0] = Screens.From(5, ["aaaaa", "bbbbb"], 0, 4),
            [2] = Screens.From(5, ["ccccc", "ddddd"], 2, 4),
        };

        var text = SelectionText.Extract(new TerminalSelection(P(1, 0), P(2, 4)), row =>
        {
            requested.Add(row);
            return pages[row < 2 ? 0 : 2];
        });

        Assert.Equal("bbbbb\nccccc", text);
        Assert.Equal([1, 2], requested);
    }

    [Fact]
    public void WrapAcrossAPageBoundary_IsStillJoined()
    {
        var pages = new Dictionary<long, TerminalSnapshot>
        {
            [0] = Screens.From(5, ["aaaaa", "bbbbb"], 0, 4, [false, false]),
            [2] = Screens.From(5, ["ccccc", "ddddd"], 2, 4, [true, false]),
        };

        var text = SelectionText.Extract(new TerminalSelection(P(1, 0), P(2, 4)), row => pages[row < 2 ? 0 : 2]);

        Assert.Equal("bbbbbccccc", text);
    }

    [Fact]
    public void ARowTheTerminalCannotProvide_EndsTheCopy()
    {
        var page = Screens.From(5, ["aaaaa"], 0, 1);

        Assert.Equal("aaaaa\n", Extract(new TerminalSelection(P(0, 0), P(5, 0)), page));
    }

    [Fact]
    public void RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => SelectionText.Extract(null!, _ => Screens.From(1, ["a"])));
        Assert.Throws<ArgumentNullException>(() => SelectionText.Extract(new TerminalSelection(P(0, 0), P(0, 0)), null!));
    }
}

public class SelectionControllerTests
{
    private static readonly TerminalSnapshot Screen = Screens.From(
        20, ["foo bar baz", "second line", "long line wraps", "onto next row"], offset: 10, wrapped: [false, false, false, true]);

    [Fact]
    public void Click_StartsAnEmptySelectionThatIsDroppedWithoutADrag()
    {
        var controller = new SelectionController();

        controller.Begin(Screen, 3, 1, 1, block: false);
        Assert.True(controller.IsDragging);
        Assert.Equal(new SelectionPoint(11, 3), controller.Current!.Anchor);

        controller.End();
        Assert.False(controller.IsDragging);
        Assert.Null(controller.Current);
    }

    [Fact]
    public void Drag_ExtendsFromTheAnchorInAnyDirection()
    {
        var controller = new SelectionController();
        controller.Begin(Screen, 4, 0, 1, block: false);

        controller.Drag(Screen, 1, 1);
        controller.End();

        Assert.Equal(new TerminalSelection(new SelectionPoint(10, 4), new SelectionPoint(11, 1)), controller.Current);
    }

    [Fact]
    public void Drag_ClampsToTheGridAndHonoursWideCharacters()
    {
        var wide = Screens.From(10, ["a世b"]);
        var controller = new SelectionController();
        controller.Begin(wide, 99, 99, 1, block: false);
        Assert.Equal(9, controller.Current!.Anchor.Column);

        controller.Drag(wide, 2, 0);

        Assert.Equal(1, controller.Current!.Active.Column);
    }

    [Fact]
    public void BlockClick_MakesABlockSelection()
    {
        var controller = new SelectionController();
        controller.Begin(Screen, 2, 0, 1, block: true);
        controller.Drag(Screen, 5, 1);

        Assert.Equal(SelectionKind.Block, controller.Current!.Kind);
    }

    [Fact]
    public void DoubleClick_SelectsTheWordAndDraggingGrowsByWords()
    {
        var controller = new SelectionController();
        controller.Begin(Screen, 5, 0, 2, block: false);
        Assert.Equal(new TerminalSelection(new SelectionPoint(10, 4), new SelectionPoint(10, 6), SelectionKind.Word), controller.Current);

        controller.Drag(Screen, 9, 0);
        Assert.Equal(new TerminalSelection(new SelectionPoint(10, 4), new SelectionPoint(10, 10), SelectionKind.Word), controller.Current);

        controller.Drag(Screen, 1, 0);
        Assert.Equal(new TerminalSelection(new SelectionPoint(10, 0), new SelectionPoint(10, 6), SelectionKind.Word), controller.Current);
        controller.End();
        Assert.NotNull(controller.Current);
    }

    [Fact]
    public void TripleClick_SelectsTheLineIncludingItsWrappedRows()
    {
        var controller = new SelectionController();

        controller.Begin(Screen, 2, 2, 3, block: false);
        Assert.Equal(new TerminalSelection(new SelectionPoint(12, 0), new SelectionPoint(13, 0), SelectionKind.Line), controller.Current);

        controller.Drag(Screen, 0, 0);
        Assert.Equal(new TerminalSelection(new SelectionPoint(10, 0), new SelectionPoint(13, 0), SelectionKind.Line), controller.Current);

        controller.Begin(Screen, 2, 3, 4, block: false);
        Assert.Equal(12, controller.Current!.FirstRow);
    }

    [Fact]
    public void Drag_WithoutAGesture_DoesNothing()
    {
        var controller = new SelectionController();

        controller.Drag(Screen, 1, 1);

        Assert.Null(controller.Current);
    }

    [Fact]
    public void Clear_RemovesTheSelection()
    {
        var controller = new SelectionController();
        controller.Begin(Screen, 1, 1, 2, block: false);

        controller.Clear();

        Assert.Null(controller.Current);
        Assert.False(controller.IsDragging);
    }

    [Fact]
    public void RejectsBadArguments()
    {
        var controller = new SelectionController();

        Assert.Throws<ArgumentNullException>(() => controller.Begin(null!, 0, 0, 1, false));
        Assert.Throws<ArgumentNullException>(() => controller.Drag(null!, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => controller.Begin(Screen, 0, 0, 0, false));
    }
}

public class SnapshotWrapTests
{
    [Fact]
    public void IsWrapped_ReadsTheFlagsAndDefaultsToFalse()
    {
        var plain = Screens.From(3, ["a", "b"]);
        var wrapped = Screens.From(3, ["a", "b"], wrapped: [false, true]);

        Assert.False(plain.IsWrapped(1));
        Assert.False(wrapped.IsWrapped(0));
        Assert.True(wrapped.IsWrapped(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => wrapped.IsWrapped(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => wrapped.IsWrapped(-1));
    }

    [Fact]
    public void FlagsOfTheWrongLength_AreIgnored() =>
        Assert.False(Screens.From(3, ["a", "b"], wrapped: [true]).IsWrapped(0));
}
