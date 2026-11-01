namespace Wintty.Core.Selection;

/// <summary>
/// A position in the terminal's history. Rows are counted from the oldest row kept, so a point stays on the
/// same text while the view scrolls.
/// </summary>
/// <param name="Row">Zero-based row from the oldest retained row.</param>
/// <param name="Column">Zero-based column.</param>
public readonly record struct SelectionPoint(long Row, int Column) : IComparable<SelectionPoint>
{
    /// <inheritdoc />
    public int CompareTo(SelectionPoint other) =>
        Row != other.Row ? Row.CompareTo(other.Row) : Column.CompareTo(other.Column);

    /// <summary>Tests whether one point comes before another.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is earlier in reading order.</returns>
    public static bool operator <(SelectionPoint left, SelectionPoint right) => left.CompareTo(right) < 0;

    /// <summary>Tests whether one point comes after another.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is later in reading order.</returns>
    public static bool operator >(SelectionPoint left, SelectionPoint right) => left.CompareTo(right) > 0;

    /// <summary>Tests whether one point comes before or equals another.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not later than <paramref name="right"/>.</returns>
    public static bool operator <=(SelectionPoint left, SelectionPoint right) => left.CompareTo(right) <= 0;

    /// <summary>Tests whether one point comes after or equals another.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not earlier than <paramref name="right"/>.</returns>
    public static bool operator >=(SelectionPoint left, SelectionPoint right) => left.CompareTo(right) >= 0;
}
