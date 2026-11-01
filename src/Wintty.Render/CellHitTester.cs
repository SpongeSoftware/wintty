using Wintty.Core.Abstractions;

namespace Wintty.Render;

/// <summary>Converts pixel positions to grid cells.</summary>
public static class CellHitTester
{
    /// <summary>Finds the cell under a pixel position.</summary>
    /// <param name="x">Horizontal position in pixels from the left edge of the surface.</param>
    /// <param name="y">Vertical position in pixels from the top edge of the surface.</param>
    /// <param name="cellWidth">Width of one cell in pixels.</param>
    /// <param name="cellHeight">Height of one cell in pixels.</param>
    /// <param name="padding">Space between the surface edge and the grid, on every side.</param>
    /// <param name="columns">Grid width in cells.</param>
    /// <param name="rows">Grid height in cells.</param>
    /// <returns>The cell, clamped to the grid so a drag past an edge selects the edge cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A cell size or grid dimension is not positive.</exception>
    public static (int Column, int Row) CellAt(
        double x,
        double y,
        double cellWidth,
        double cellHeight,
        double padding,
        int columns,
        int rows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cellWidth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cellHeight, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(columns, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rows, 0);

        var column = (int)Math.Floor((x - padding) / cellWidth);
        var row = (int)Math.Floor((y - padding) / cellHeight);
        return (Math.Clamp(column, 0, columns - 1), Math.Clamp(row, 0, rows - 1));
    }

    /// <summary>
    /// Moves a column left off the trailing half of a wide character, so a click on either half of a wide cell
    /// addresses the cell that holds the text.
    /// </summary>
    /// <param name="snapshot">The screen.</param>
    /// <param name="column">Column to resolve.</param>
    /// <param name="row">Row to resolve.</param>
    /// <returns>The column of the cell that holds the character.</returns>
    public static int ResolveWideColumn(TerminalSnapshot snapshot, int column, int row)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        while (column > 0 && snapshot[column, row].Width == 0)
        {
            column--;
        }

        return column;
    }
}
