using Wintty.Core.Abstractions;
using Wintty.Core.Selection;

namespace Wintty.Render;

/// <summary>Converts a <see cref="TerminalSnapshot"/> into a <see cref="RenderPlan"/>.</summary>
public static class RenderPlanner
{
    /// <summary>Builds the draw plan for a snapshot.</summary>
    /// <param name="snapshot">The screen contents.</param>
    /// <param name="cursorVisible">
    /// Whether the cursor is in the "on" phase of its blink; the cursor is only drawn when this and the
    /// snapshot's own visibility are both true.
    /// </param>
    /// <param name="selection">The selected region to highlight, or <see langword="null"/> for none.</param>
    /// <returns>A plan the platform renderer can draw directly.</returns>
    public static RenderPlan Build(TerminalSnapshot snapshot, bool cursorVisible = true, TerminalSelection? selection = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var backgrounds = new List<BackgroundRun>();
        var texts = new List<TextRun>();
        var decorations = new List<DecorationRun>();

        for (int row = 0; row < snapshot.Rows; row++)
        {
            AddBackgrounds(snapshot, row, backgrounds);
            AddTexts(snapshot, row, texts);
            AddDecorations(snapshot, row, decorations);
        }

        return new RenderPlan(
            snapshot.Columns,
            snapshot.Rows,
            snapshot.DefaultBackground,
            backgrounds,
            texts,
            decorations,
            BuildCursor(snapshot, cursorVisible),
            BuildScrollThumb(snapshot.Scrollbar),
            BuildSelection(snapshot, selection));
    }

    // Selection rows are counted from the oldest history row, so shift them into the visible grid.
    private static List<SelectionRun> BuildSelection(TerminalSnapshot snapshot, TerminalSelection? selection)
    {
        var runs = new List<SelectionRun>();
        if (selection is null || selection.IsEmpty)
        {
            return runs;
        }

        long offset = snapshot.Scrollbar.Offset;
        for (int row = 0; row < snapshot.Rows; row++)
        {
            if (selection.SpanOnRow(offset + row, snapshot.Columns) is not var (start, end))
            {
                continue;
            }

            // A selection that ends on the left half of a wide character takes its right half too.
            if (end > 0 && end < snapshot.Columns && snapshot[end, row].Width == 0)
            {
                end++;
            }

            runs.Add(new SelectionRun(row, start, end - start));
        }

        return runs;
    }

    // The thumb has a minimum height so it stays visible and grabbable in very long histories.
    private const double MinimumThumbHeight = 0.03;

    private static ScrollThumb? BuildScrollThumb(ScrollbarState scrollbar)
    {
        if (!scrollbar.IsScrolledBack)
        {
            return null;
        }

        double height = Math.Max(MinimumThumbHeight, (double)scrollbar.Length / scrollbar.Total);
        double top = Math.Min((double)scrollbar.Offset / scrollbar.Total, 1 - height);
        return new ScrollThumb(top, height);
    }

    private static void AddBackgrounds(TerminalSnapshot snapshot, int row, List<BackgroundRun> output)
    {
        int start = -1;
        var color = default(TerminalColor);
        for (int column = 0; column <= snapshot.Columns; column++)
        {
            bool hasFill = false;
            var next = default(TerminalColor);
            if (column < snapshot.Columns)
            {
                next = snapshot[column, row].EffectiveBackground;
                hasFill = next != snapshot.DefaultBackground;
            }

            if (start >= 0 && (!hasFill || next != color))
            {
                output.Add(new BackgroundRun(row, start, column - start, color));
                start = -1;
            }

            if (hasFill && start < 0)
            {
                start = column;
                color = next;
            }
        }
    }

    private static void AddTexts(TerminalSnapshot snapshot, int row, List<TextRun> output)
    {
        var builder = new System.Text.StringBuilder();
        int runStart = -1;
        int runCells = 0;
        TerminalCell? runStyle = null;

        void Flush()
        {
            if (runStyle is { } style && builder.Length > 0)
            {
                output.Add(new TextRun(
                    row,
                    runStart,
                    runCells,
                    builder.ToString(),
                    style.EffectiveForeground,
                    style.Attributes.HasFlag(CellAttributes.Bold),
                    style.Attributes.HasFlag(CellAttributes.Italic),
                    style.Attributes.HasFlag(CellAttributes.Faint),
                    GlyphClassifier.RoleOf(style.Text)));
            }

            builder.Clear();
            runStart = -1;
            runCells = 0;
            runStyle = null;
        }

        for (int column = 0; column < snapshot.Columns; column++)
        {
            TerminalCell cell = snapshot[column, row];
            if (cell.Width == 0)
            {
                continue;
            }

            bool drawable = cell.Text.Length > 0 && !cell.Attributes.HasFlag(CellAttributes.Invisible);
            if (!drawable)
            {
                Flush();
                continue;
            }

            // Wide characters and symbol glyphs are drawn on their own so a font whose advance differs from the
            // cell width cannot shift neighbours off the grid.
            if (cell.Width != 1 || GlyphClassifier.RoleOf(cell.Text) == FontRole.Symbols)
            {
                Flush();
                runStart = column;
                runStyle = cell;
                runCells = cell.Width;
                builder.Append(cell.Text);
                Flush();
                continue;
            }

            if (runStyle is { } current && !SameTextStyle(current, cell))
            {
                Flush();
            }

            if (runStyle is null)
            {
                runStart = column;
                runStyle = cell;
            }

            builder.Append(cell.Text);
            runCells++;
        }

        Flush();
    }

    private static bool SameTextStyle(TerminalCell a, TerminalCell b) =>
        a.EffectiveForeground == b.EffectiveForeground
        && GlyphClassifier.RoleOf(a.Text) == GlyphClassifier.RoleOf(b.Text)
        && (a.Attributes & (CellAttributes.Bold | CellAttributes.Italic | CellAttributes.Faint))
            == (b.Attributes & (CellAttributes.Bold | CellAttributes.Italic | CellAttributes.Faint));

    private static void AddDecorations(TerminalSnapshot snapshot, int row, List<DecorationRun> output)
    {
        for (int column = 0; column < snapshot.Columns; column++)
        {
            TerminalCell cell = snapshot[column, row];
            if (cell.Width == 0)
            {
                continue;
            }

            TerminalColor color = cell.EffectiveForeground;
            if (cell.Underline != UnderlineStyle.None)
            {
                Add(output, row, column, cell.Width, ToKind(cell.Underline), cell.UnderlineColor ?? color);
            }

            if (cell.Attributes.HasFlag(CellAttributes.Strikethrough))
            {
                Add(output, row, column, cell.Width, DecorationKind.Strikethrough, color);
            }

            if (cell.Attributes.HasFlag(CellAttributes.Overline))
            {
                Add(output, row, column, cell.Width, DecorationKind.Overline, color);
            }
        }
    }

    private static void Add(List<DecorationRun> output, int row, int column, int length, DecorationKind kind, TerminalColor color)
    {
        // Extend the previous run when it ends exactly here with the same look.
        if (output.Count > 0)
        {
            DecorationRun last = output[^1];
            if (last.Row == row && last.Kind == kind && last.Color == color && last.Column + last.Length == column)
            {
                output[^1] = last with { Length = last.Length + length };
                return;
            }
        }

        output.Add(new DecorationRun(row, column, length, kind, color));
    }

    private static DecorationKind ToKind(UnderlineStyle style) => style switch
    {
        UnderlineStyle.Double => DecorationKind.DoubleUnderline,
        UnderlineStyle.Curly => DecorationKind.CurlyUnderline,
        UnderlineStyle.Dotted => DecorationKind.DottedUnderline,
        UnderlineStyle.Dashed => DecorationKind.DashedUnderline,
        _ => DecorationKind.Underline,
    };

    private static CursorShape? BuildCursor(TerminalSnapshot snapshot, bool cursorVisible)
    {
        CursorState cursor = snapshot.Cursor;
        if (!cursorVisible || !cursor.Visible
            || cursor.Column < 0 || cursor.Column >= snapshot.Columns
            || cursor.Row < 0 || cursor.Row >= snapshot.Rows)
        {
            return null;
        }

        TerminalCell cell = snapshot[cursor.Column, cursor.Row];
        return new CursorShape(cursor.Row, cursor.Column, cell.Width == 2 ? 2 : 1, cursor.Style, cursor.Color ?? snapshot.DefaultForeground);
    }
}
