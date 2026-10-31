using Wintty.Core.Abstractions;

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
    /// <returns>A plan the platform renderer can draw directly.</returns>
    public static RenderPlan Build(TerminalSnapshot snapshot, bool cursorVisible = true)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var backgrounds = new List<BackgroundRun>();
        var texts = new List<TextRun>();
        var decorations = new List<DecorationRun>();

        for (var row = 0; row < snapshot.Rows; row++)
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
            BuildCursor(snapshot, cursorVisible));
    }

    private static void AddBackgrounds(TerminalSnapshot snapshot, int row, List<BackgroundRun> output)
    {
        var start = -1;
        var color = default(TerminalColor);
        for (var column = 0; column <= snapshot.Columns; column++)
        {
            var hasFill = false;
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
        var runStart = -1;
        var runCells = 0;
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
                    style.Attributes.HasFlag(CellAttributes.Faint)));
            }

            builder.Clear();
            runStart = -1;
            runCells = 0;
            runStyle = null;
        }

        for (var column = 0; column < snapshot.Columns; column++)
        {
            var cell = snapshot[column, row];
            if (cell.Width == 0)
            {
                continue;
            }

            var drawable = cell.Text.Length > 0 && !cell.Attributes.HasFlag(CellAttributes.Invisible);
            if (!drawable)
            {
                Flush();
                continue;
            }

            // Wide characters are drawn on their own so they cannot shift neighbours off the grid.
            if (cell.Width != 1)
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
        && (a.Attributes & (CellAttributes.Bold | CellAttributes.Italic | CellAttributes.Faint))
            == (b.Attributes & (CellAttributes.Bold | CellAttributes.Italic | CellAttributes.Faint));

    private static void AddDecorations(TerminalSnapshot snapshot, int row, List<DecorationRun> output)
    {
        for (var column = 0; column < snapshot.Columns; column++)
        {
            var cell = snapshot[column, row];
            if (cell.Width == 0)
            {
                continue;
            }

            var color = cell.EffectiveForeground;
            if (cell.Underline != UnderlineStyle.None)
            {
                Add(output, row, column, cell.Width, ToKind(cell.Underline), color);
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
            var last = output[^1];
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
        var cursor = snapshot.Cursor;
        if (!cursorVisible || !cursor.Visible
            || cursor.Column < 0 || cursor.Column >= snapshot.Columns
            || cursor.Row < 0 || cursor.Row >= snapshot.Rows)
        {
            return null;
        }

        var cell = snapshot[cursor.Column, cursor.Row];
        return new CursorShape(cursor.Row, cursor.Column, cell.Width == 2 ? 2 : 1, cursor.Style, snapshot.DefaultForeground);
    }
}
