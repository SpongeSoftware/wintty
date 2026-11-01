namespace Wintty.Core.Selection;

/// <summary>How a selection covers text.</summary>
public enum SelectionKind
{
    /// <summary>From one cell to another in reading order, as with a text editor.</summary>
    Character = 0,

    /// <summary>Whole words; the ends snap outwards to word boundaries.</summary>
    Word = 1,

    /// <summary>Whole lines, including the rows a long line wraps over.</summary>
    Line = 2,

    /// <summary>A rectangle of cells, ignoring where lines start and end.</summary>
    Block = 3,
}

/// <summary>A selected region of the terminal's history.</summary>
/// <param name="Anchor">Where the selection started.</param>
/// <param name="Active">Where it currently ends; may be before <paramref name="Anchor"/>.</param>
/// <param name="Kind">How the region covers text.</param>
public sealed record TerminalSelection(SelectionPoint Anchor, SelectionPoint Active, SelectionKind Kind = SelectionKind.Character)
{
    /// <summary>The earlier end in reading order.</summary>
    public SelectionPoint Start => Anchor <= Active ? Anchor : Active;

    /// <summary>The later end in reading order.</summary>
    public SelectionPoint End => Anchor <= Active ? Active : Anchor;

    /// <summary><see langword="true"/> when nothing is selected, such as a click with no drag.</summary>
    public bool IsEmpty => Kind is SelectionKind.Character or SelectionKind.Block && Anchor == Active;

    /// <summary>The first row the selection touches.</summary>
    public long FirstRow => Kind == SelectionKind.Block ? Math.Min(Anchor.Row, Active.Row) : Start.Row;

    /// <summary>The last row the selection touches.</summary>
    public long LastRow => Kind == SelectionKind.Block ? Math.Max(Anchor.Row, Active.Row) : End.Row;

    /// <summary>Finds the selected columns on one row.</summary>
    /// <param name="row">The row, counted from the oldest retained row.</param>
    /// <param name="columns">Width of the grid.</param>
    /// <returns>
    /// The first selected column and the column after the last, or <see langword="null"/> when the row is not
    /// part of the selection.
    /// </returns>
    public (int Start, int End)? SpanOnRow(long row, int columns)
    {
        if (row < FirstRow || row > LastRow)
        {
            return null;
        }

        if (Kind == SelectionKind.Line)
        {
            return (0, columns);
        }

        if (Kind == SelectionKind.Block)
        {
            int left = Math.Min(Anchor.Column, Active.Column);
            int right = Math.Max(Anchor.Column, Active.Column);
            return (left, Math.Min(columns, right + 1));
        }

        SelectionPoint start = Start;
        SelectionPoint end = End;
        int from = row == start.Row ? start.Column : 0;
        int to = row == end.Row ? Math.Min(columns, end.Column + 1) : columns;
        return from < to ? (from, to) : null;
    }
}
