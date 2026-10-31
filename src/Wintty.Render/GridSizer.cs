namespace Wintty.Render;

/// <summary>Computes how many terminal cells fit in a drawing surface.</summary>
public static class GridSizer
{
    /// <summary>Calculates the grid for a surface.</summary>
    /// <param name="surfaceWidth">Surface width in the same unit as the cell size.</param>
    /// <param name="surfaceHeight">Surface height in the same unit as the cell size.</param>
    /// <param name="cellWidth">Width of one cell; must be positive.</param>
    /// <param name="cellHeight">Height of one cell; must be positive.</param>
    /// <param name="padding">Padding removed from every edge of the surface.</param>
    /// <returns>Columns and rows, never less than 1 each.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A cell dimension is not positive or padding is negative.</exception>
    public static (int Columns, int Rows) Compute(
        double surfaceWidth,
        double surfaceHeight,
        double cellWidth,
        double cellHeight,
        double padding = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cellWidth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cellHeight, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(padding);

        var usableWidth = Math.Max(0, surfaceWidth - (2 * padding));
        var usableHeight = Math.Max(0, surfaceHeight - (2 * padding));
        var columns = (int)Math.Floor(usableWidth / cellWidth);
        var rows = (int)Math.Floor(usableHeight / cellHeight);
        return (Math.Max(1, columns), Math.Max(1, rows));
    }
}
