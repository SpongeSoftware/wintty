using Wintty.Core.Abstractions;

namespace Wintty.Core.Selection;

/// <summary>Decides where a double-click selects to.</summary>
public static class WordBoundary
{
    private const string Separators = "()[]{}<>\"'`,;|";

    /// <summary>Finds the word under a cell.</summary>
    /// <param name="snapshot">The screen.</param>
    /// <param name="row">Row within the snapshot.</param>
    /// <param name="column">Column within the snapshot.</param>
    /// <returns>
    /// The first and last column of the word. A word is a run of characters that are neither whitespace nor
    /// brackets, quotes or separators, so paths and URLs select whole. On a blank or separator cell this is the
    /// run of identical kind (spaces, or the single separator).
    /// </returns>
    public static (int Start, int End) Find(TerminalSnapshot snapshot, int row, int column)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var kind = KindAt(snapshot, row, column);
        var start = column;
        while (start > 0 && KindAt(snapshot, row, start - 1) == kind)
        {
            start--;
        }

        var end = column;
        while (end < snapshot.Columns - 1 && KindAt(snapshot, row, end + 1) == kind)
        {
            end++;
        }

        return (start, end);
    }

    private static int KindAt(TerminalSnapshot snapshot, int row, int column)
    {
        var cell = snapshot[column, row];
        if (cell.Width == 0)
        {
            // The trailing half of a wide character belongs to the character before it.
            return column > 0 ? KindAt(snapshot, row, column - 1) : 0;
        }

        if (string.IsNullOrEmpty(cell.Text) || char.IsWhiteSpace(cell.Text[0]))
        {
            return 0;
        }

        return Separators.Contains(cell.Text[0], StringComparison.Ordinal) ? 2 + cell.Text[0] : 1;
    }
}
