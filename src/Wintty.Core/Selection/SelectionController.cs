using Wintty.Core.Abstractions;

namespace Wintty.Core.Selection;

/// <summary>
/// Builds a selection from mouse gestures: a click starts one, a drag extends it, a double or triple click
/// snaps to words or lines. It holds no UI types so it can be tested directly.
/// </summary>
public sealed class SelectionController
{
    private SelectionKind _kind;
    private SelectionPoint _origin;
    private (int Start, int End) _originWord;
    private (long First, long Last) _originLines;

    /// <summary>The current selection, or <see langword="null"/> when there is none.</summary>
    public TerminalSelection? Current { get; private set; }

    /// <summary>Gets a value indicating whether a gesture is in progress.</summary>
    public bool IsDragging { get; private set; }

    /// <summary>Starts a selection at a cell.</summary>
    /// <param name="snapshot">The screen the click landed on.</param>
    /// <param name="column">Column within the snapshot.</param>
    /// <param name="row">Row within the snapshot.</param>
    /// <param name="clickCount">1 selects by character, 2 by word, 3 or more by line.</param>
    /// <param name="block">Select a rectangle instead of following the text; only for a single click.</param>
    public void Begin(TerminalSnapshot snapshot, int column, int row, int clickCount, bool block)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfLessThan(clickCount, 1);
        column = Math.Clamp(column, 0, snapshot.Columns - 1);
        row = Math.Clamp(row, 0, snapshot.Rows - 1);
        column = ResolveWide(snapshot, column, row);

        _origin = new SelectionPoint(snapshot.Scrollbar.Offset + row, column);
        IsDragging = true;
        if (clickCount >= 3)
        {
            _kind = SelectionKind.Line;
            _originLines = LinesAround(snapshot, row);
            Current = LineSelection(_originLines.First, _originLines.Last);
        }
        else if (clickCount == 2)
        {
            _kind = SelectionKind.Word;
            _originWord = WordBoundary.Find(snapshot, row, column);
            Current = new TerminalSelection(
                new SelectionPoint(_origin.Row, _originWord.Start),
                new SelectionPoint(_origin.Row, _originWord.End),
                SelectionKind.Word);
        }
        else
        {
            _kind = block ? SelectionKind.Block : SelectionKind.Character;
            Current = new TerminalSelection(_origin, _origin, _kind);
        }
    }

    /// <summary>Extends the selection while the button is held.</summary>
    /// <param name="snapshot">The screen under the pointer.</param>
    /// <param name="column">Column within the snapshot.</param>
    /// <param name="row">Row within the snapshot.</param>
    public void Drag(TerminalSnapshot snapshot, int column, int row)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!IsDragging || Current is null)
        {
            return;
        }

        column = Math.Clamp(column, 0, snapshot.Columns - 1);
        row = Math.Clamp(row, 0, snapshot.Rows - 1);
        column = ResolveWide(snapshot, column, row);
        var point = new SelectionPoint(snapshot.Scrollbar.Offset + row, column);

        switch (_kind)
        {
            case SelectionKind.Line:
                var lines = LinesAround(snapshot, row);
                Current = LineSelection(Math.Min(_originLines.First, lines.First), Math.Max(_originLines.Last, lines.Last));
                break;
            case SelectionKind.Word:
                var word = WordBoundary.Find(snapshot, row, column);
                var originStart = new SelectionPoint(_origin.Row, _originWord.Start);
                var originEnd = new SelectionPoint(_origin.Row, _originWord.End);
                var wordStart = new SelectionPoint(point.Row, word.Start);
                var wordEnd = new SelectionPoint(point.Row, word.End);
                Current = new TerminalSelection(
                    originStart < wordStart ? originStart : wordStart,
                    originEnd > wordEnd ? originEnd : wordEnd,
                    SelectionKind.Word);
                break;
            default:
                Current = new TerminalSelection(_origin, point, _kind);
                break;
        }
    }

    /// <summary>Ends the gesture, dropping a selection that covers nothing (a plain click).</summary>
    public void End()
    {
        IsDragging = false;
        if (Current is { IsEmpty: true })
        {
            Current = null;
        }
    }

    /// <summary>Removes the selection.</summary>
    public void Clear()
    {
        IsDragging = false;
        Current = null;
    }

    private static TerminalSelection LineSelection(long first, long last) =>
        new(new SelectionPoint(first, 0), new SelectionPoint(last, 0), SelectionKind.Line);

    // A line that wrapped over several rows is selected as one.
    private static (long First, long Last) LinesAround(TerminalSnapshot snapshot, int row)
    {
        var first = row;
        while (first > 0 && snapshot.IsWrapped(first))
        {
            first--;
        }

        var last = row;
        while (last + 1 < snapshot.Rows && snapshot.IsWrapped(last + 1))
        {
            last++;
        }

        return (snapshot.Scrollbar.Offset + first, snapshot.Scrollbar.Offset + last);
    }

    private static int ResolveWide(TerminalSnapshot snapshot, int column, int row)
    {
        while (column > 0 && snapshot[column, row].Width == 0)
        {
            column--;
        }

        return column;
    }
}
