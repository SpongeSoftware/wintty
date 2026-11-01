using System.Text;
using Wintty.Core.Abstractions;

namespace Wintty.Core.Selection;

/// <summary>Turns a selection into the text to copy.</summary>
public static class SelectionText
{
    /// <summary>Reads the text under a selection.</summary>
    /// <param name="selection">The selected region.</param>
    /// <param name="pageAt">
    /// Returns a snapshot whose first visible row is at or before the requested row. It is asked again for each
    /// stretch of rows that is not in the page already held, so a selection can span more than one screen.
    /// </param>
    /// <returns>
    /// The text. Trailing blanks on a line are dropped, rows that were only soft-wrapped are joined without a
    /// line break, and wide characters are copied once.
    /// </returns>
    public static string Extract(TerminalSelection selection, Func<long, TerminalSnapshot> pageAt)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(pageAt);

        var builder = new StringBuilder();
        TerminalSnapshot? page = null;
        for (var row = selection.FirstRow; row <= selection.LastRow; row++)
        {
            if (page is null || row < page.Scrollbar.Offset || row >= page.Scrollbar.Offset + page.Rows)
            {
                page = pageAt(row);
            }

            var viewRow = (int)(row - page.Scrollbar.Offset);
            if (viewRow < 0 || viewRow >= page.Rows)
            {
                break;
            }

            if (selection.SpanOnRow(row, page.Columns) is not var (start, end))
            {
                continue;
            }

            var line = ReadCells(page, viewRow, start, end);
            var continues = false;
            if (row < selection.LastRow && selection.Kind != SelectionKind.Block)
            {
                // A wrap flag lives on the next row, which may be on the next page; keep that page for the next turn.
                if (viewRow + 1 >= page.Rows)
                {
                    page = pageAt(row + 1);
                    viewRow = (int)(row + 1 - page.Scrollbar.Offset) - 1;
                }

                var nextView = viewRow + 1;
                continues = nextView >= 0 && nextView < page.Rows && page.IsWrapped(nextView);
            }

            builder.Append(continues ? line : line.TrimEnd(' '));
            if (row < selection.LastRow && !continues)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private static string ReadCells(TerminalSnapshot page, int row, int start, int end)
    {
        var builder = new StringBuilder();
        for (var column = start; column < end; column++)
        {
            var cell = page[column, row];
            if (cell.Width == 0)
            {
                continue;
            }

            builder.Append(string.IsNullOrEmpty(cell.Text) ? " " : cell.Text);
        }

        return builder.ToString();
    }
}
