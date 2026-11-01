using Wintty.Core.Abstractions;
using Wintty.Core.Selection;
using Wintty.Render;
using Xunit;

namespace Wintty.Tests.Render;

public class SelectionRunTests
{
    private static readonly TerminalColor Color = new(1, 1, 1);

    private static TerminalSnapshot Screen(long offset, int columns = 10, int rows = 3, int wideAt = -1)
    {
        var cells = new TerminalCell[columns * rows];
        Array.Fill(cells, new TerminalCell("x", Color, Color));
        if (wideAt >= 0)
        {
            cells[wideAt] = new TerminalCell("世", Color, Color, Width: 2);
            cells[wideAt + 1] = new TerminalCell(string.Empty, Color, Color, Width: 0);
        }

        return new TerminalSnapshot(columns, rows, cells, default, Color, Color, new ScrollbarState(offset + rows + 5, offset, rows));
    }

    [Fact]
    public void WithoutASelection_ThereAreNoRuns()
    {
        Assert.Empty(RenderPlanner.Build(Screen(0)).Selection!);
        Assert.Empty(RenderPlanner.Build(Screen(0), true, new TerminalSelection(default, default)).Selection!);
    }

    [Fact]
    public void Runs_AreShiftedByTheScrollOffset()
    {
        var selection = new TerminalSelection(new SelectionPoint(101, 2), new SelectionPoint(102, 4));

        var runs = RenderPlanner.Build(Screen(100), true, selection).Selection!;

        Assert.Equal([new SelectionRun(1, 2, 8), new SelectionRun(2, 0, 5)], runs);
    }

    [Fact]
    public void SelectionOutsideTheViewport_ProducesNoRuns()
    {
        var selection = new TerminalSelection(new SelectionPoint(5, 0), new SelectionPoint(6, 3));

        Assert.Empty(RenderPlanner.Build(Screen(100), true, selection).Selection!);
    }

    [Fact]
    public void SelectionEndingOnAWideCharacter_TakesItsTrailingHalf()
    {
        var selection = new TerminalSelection(new SelectionPoint(0, 0), new SelectionPoint(0, 3));

        var runs = RenderPlanner.Build(Screen(0, wideAt: 3), true, selection).Selection!;

        Assert.Equal([new SelectionRun(0, 0, 5)], runs);
    }
}
