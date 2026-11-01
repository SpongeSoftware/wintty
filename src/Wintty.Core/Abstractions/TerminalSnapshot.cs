using System.Text;

namespace Wintty.Core.Abstractions;

/// <summary>An opaque 24-bit RGB colour.</summary>
/// <param name="Red">Red channel.</param>
/// <param name="Green">Green channel.</param>
/// <param name="Blue">Blue channel.</param>
public readonly record struct TerminalColor(byte Red, byte Green, byte Blue);

/// <summary>Text attributes of a cell.</summary>
[Flags]
public enum CellAttributes : byte
{
    /// <summary>No attributes.</summary>
    None = 0,

    /// <summary>Bold weight.</summary>
    Bold = 1,

    /// <summary>Italic slant.</summary>
    Italic = 2,

    /// <summary>Reduced intensity.</summary>
    Faint = 4,

    /// <summary>Blinking text.</summary>
    Blink = 8,

    /// <summary>Foreground and background swapped.</summary>
    Inverse = 16,

    /// <summary>Text is hidden.</summary>
    Invisible = 32,

    /// <summary>Line through the text.</summary>
    Strikethrough = 64,

    /// <summary>Line above the text.</summary>
    Overline = 128,
}

/// <summary>Underline style of a cell.</summary>
public enum UnderlineStyle
{
    /// <summary>No underline.</summary>
    None = 0,

    /// <summary>A single straight line.</summary>
    Single = 1,

    /// <summary>Two straight lines.</summary>
    Double = 2,

    /// <summary>A wavy line.</summary>
    Curly = 3,

    /// <summary>A dotted line.</summary>
    Dotted = 4,

    /// <summary>A dashed line.</summary>
    Dashed = 5,
}

/// <summary>Visual shape of the cursor.</summary>
public enum CursorStyle
{
    /// <summary>A vertical bar.</summary>
    Bar = 0,

    /// <summary>A filled block.</summary>
    Block = 1,

    /// <summary>A horizontal underline.</summary>
    Underline = 2,

    /// <summary>An outlined block.</summary>
    BlockHollow = 3,
}

/// <summary>Cursor position and appearance.</summary>
/// <param name="Column">Zero-based column.</param>
/// <param name="Row">Zero-based row.</param>
/// <param name="Visible">Whether the cursor should be drawn.</param>
/// <param name="Style">The cursor shape.</param>
/// <param name="Color">The cursor colour set by the colour scheme or the program, or <see langword="null"/> to use the text colour.</param>
public readonly record struct CursorState(int Column, int Row, bool Visible, CursorStyle Style, TerminalColor? Color = null);

/// <summary>One character cell with its colours already resolved to RGB.</summary>
/// <param name="Text">The grapheme cluster shown in the cell; empty for a blank cell.</param>
/// <param name="Foreground">Foreground colour before inverse is applied.</param>
/// <param name="Background">Background colour before inverse is applied.</param>
/// <param name="Attributes">Text attributes.</param>
/// <param name="Underline">Underline style.</param>
/// <param name="Width">1 for a normal cell, 2 for the left half of a wide character, 0 for its right half.</param>
/// <param name="UnderlineColor">Colour of the underline, or <see langword="null"/> to use the text colour.</param>
public readonly record struct TerminalCell(
    string Text,
    TerminalColor Foreground,
    TerminalColor Background,
    CellAttributes Attributes = CellAttributes.None,
    UnderlineStyle Underline = UnderlineStyle.None,
    int Width = 1,
    TerminalColor? UnderlineColor = null)
{
    /// <summary>The colour to draw text with, accounting for <see cref="CellAttributes.Inverse"/>.</summary>
    public TerminalColor EffectiveForeground => Attributes.HasFlag(CellAttributes.Inverse) ? Background : Foreground;

    /// <summary>The colour to fill the cell with, accounting for <see cref="CellAttributes.Inverse"/>.</summary>
    public TerminalColor EffectiveBackground => Attributes.HasFlag(CellAttributes.Inverse) ? Foreground : Background;
}

/// <summary>An immutable copy of everything needed to draw one frame of a terminal.</summary>
public sealed class TerminalSnapshot
{
    private readonly TerminalCell[] _cells;
    private readonly bool[]? _wrappedRows;

    /// <summary>Creates a snapshot.</summary>
    /// <param name="columns">Grid width.</param>
    /// <param name="rows">Grid height.</param>
    /// <param name="cells">Row-major cells; length must be <paramref name="columns"/> × <paramref name="rows"/>.</param>
    /// <param name="cursor">Cursor state.</param>
    /// <param name="defaultForeground">Default text colour.</param>
    /// <param name="defaultBackground">Default background colour.</param>
    /// <param name="scrollbar">Where the visible rows sit within the history.</param>
    /// <param name="wrappedRows">
    /// For each row, whether it continues the line above because that line was too long for the grid;
    /// <see langword="null"/> when no row does.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive.</exception>
    /// <exception cref="ArgumentException">The cell count does not match the dimensions.</exception>
    public TerminalSnapshot(
        int columns,
        int rows,
        TerminalCell[] cells,
        CursorState cursor,
        TerminalColor defaultForeground,
        TerminalColor defaultBackground,
        ScrollbarState scrollbar = default,
        bool[]? wrappedRows = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(columns, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rows, 0);
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Length != columns * rows)
        {
            throw new ArgumentException($"Expected {columns * rows} cells but got {cells.Length}.", nameof(cells));
        }

        Columns = columns;
        Rows = rows;
        _cells = cells;
        Cursor = cursor;
        DefaultForeground = defaultForeground;
        DefaultBackground = defaultBackground;
        Scrollbar = scrollbar;
        _wrappedRows = wrappedRows is { Length: > 0 } && wrappedRows.Length == rows ? wrappedRows : null;
    }

    /// <summary>Tests whether a row continues the line above it (a soft wrap rather than a real line break).</summary>
    /// <param name="row">Row to test.</param>
    /// <returns><see langword="true"/> when the row is the continuation of a wrapped line.</returns>
    public bool IsWrapped(int row)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Rows);
        return _wrappedRows is not null && _wrappedRows[row];
    }

    /// <summary>Grid width in cells.</summary>
    public int Columns { get; }

    /// <summary>Grid height in cells.</summary>
    public int Rows { get; }

    /// <summary>Cursor state.</summary>
    public CursorState Cursor { get; }

    /// <summary>Where the visible rows sit within the history, for drawing a scroll indicator.</summary>
    public ScrollbarState Scrollbar { get; }

    /// <summary>Default text colour.</summary>
    public TerminalColor DefaultForeground { get; }

    /// <summary>Default background colour.</summary>
    public TerminalColor DefaultBackground { get; }

    /// <summary>Gets the cell at a position.</summary>
    /// <param name="column">Zero-based column.</param>
    /// <param name="row">Zero-based row.</param>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the grid.</exception>
    public TerminalCell this[int column, int row]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(column);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Columns);
            ArgumentOutOfRangeException.ThrowIfNegative(row);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Rows);
            return _cells[(row * Columns) + column];
        }
    }

    /// <summary>Returns the text of a row with trailing blanks removed.</summary>
    /// <param name="row">Zero-based row.</param>
    public string GetRowText(int row)
    {
        var builder = new StringBuilder(Columns);
        for (var column = 0; column < Columns; column++)
        {
            var cell = this[column, row];
            if (cell.Width == 0)
            {
                continue;
            }

            builder.Append(cell.Text.Length == 0 ? " " : cell.Text);
        }

        return builder.ToString().TrimEnd();
    }
}
