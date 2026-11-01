using Wintty.Core.Abstractions;

namespace Wintty.Render;

/// <summary>A horizontal run of cells filled with one background colour.</summary>
/// <param name="Row">Zero-based row.</param>
/// <param name="Column">Zero-based first column.</param>
/// <param name="Length">Number of cells covered.</param>
/// <param name="Color">Fill colour.</param>
public readonly record struct BackgroundRun(int Row, int Column, int Length, TerminalColor Color);

/// <summary>A run of text drawn from one starting cell with a single style.</summary>
/// <param name="Row">Zero-based row.</param>
/// <param name="Column">Zero-based first column.</param>
/// <param name="CellCount">Number of grid cells the run spans.</param>
/// <param name="Text">The text to draw.</param>
/// <param name="Color">Text colour.</param>
/// <param name="Bold">Draw with a bold weight.</param>
/// <param name="Italic">Draw with an italic slant.</param>
/// <param name="Faint">Draw with reduced intensity.</param>
/// <param name="Role">Which font to draw with.</param>
public readonly record struct TextRun(
    int Row,
    int Column,
    int CellCount,
    string Text,
    TerminalColor Color,
    bool Bold,
    bool Italic,
    bool Faint,
    FontRole Role = FontRole.Primary);

/// <summary>The kind of line drawn over or under text.</summary>
public enum DecorationKind
{
    /// <summary>A single underline.</summary>
    Underline,

    /// <summary>A double underline.</summary>
    DoubleUnderline,

    /// <summary>A wavy underline.</summary>
    CurlyUnderline,

    /// <summary>A dotted underline.</summary>
    DottedUnderline,

    /// <summary>A dashed underline.</summary>
    DashedUnderline,

    /// <summary>A line through the middle of the text.</summary>
    Strikethrough,

    /// <summary>A line at the top of the cell.</summary>
    Overline,
}

/// <summary>A line decoration spanning a run of cells.</summary>
/// <param name="Row">Zero-based row.</param>
/// <param name="Column">Zero-based first column.</param>
/// <param name="Length">Number of cells covered.</param>
/// <param name="Kind">Which decoration.</param>
/// <param name="Color">Line colour.</param>
public readonly record struct DecorationRun(int Row, int Column, int Length, DecorationKind Kind, TerminalColor Color);

/// <summary>Where and how to draw the cursor.</summary>
/// <param name="Row">Zero-based row.</param>
/// <param name="Column">Zero-based column.</param>
/// <param name="CellCount">Width in cells (2 over a wide character).</param>
/// <param name="Style">Cursor shape.</param>
/// <param name="Color">Cursor colour.</param>
public readonly record struct CursorShape(int Row, int Column, int CellCount, CursorStyle Style, TerminalColor Color);

/// <summary>Everything needed to draw one frame, independent of any graphics API.</summary>
/// <param name="Columns">Grid width.</param>
/// <param name="Rows">Grid height.</param>
/// <param name="Background">Colour to clear the surface with.</param>
/// <param name="Backgrounds">Non-default background fills.</param>
/// <param name="Texts">Text runs.</param>
/// <param name="Decorations">Underlines, strikethroughs and overlines.</param>
/// <param name="Cursor">The cursor to draw, or <see langword="null"/> when hidden.</param>
/// <param name="ScrollThumb">The scroll position indicator, or <see langword="null"/> when the view is at the bottom.</param>
public sealed record RenderPlan(
    int Columns,
    int Rows,
    TerminalColor Background,
    IReadOnlyList<BackgroundRun> Backgrounds,
    IReadOnlyList<TextRun> Texts,
    IReadOnlyList<DecorationRun> Decorations,
    CursorShape? Cursor,
    ScrollThumb? ScrollThumb = null);

/// <summary>
/// A scroll position indicator, as fractions of the grid height so the drawing layer can place it at any scale.
/// </summary>
/// <param name="Top">Where the thumb starts, from 0 (top) to 1 (bottom).</param>
/// <param name="Height">How tall the thumb is, from 0 to 1.</param>
public readonly record struct ScrollThumb(double Top, double Height);
