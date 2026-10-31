namespace Wintty.Core.Abstractions;

/// <summary>
/// A virtual terminal: consumes the byte stream produced by a program and maintains screen state.
/// Implemented by the single libghostty gateway; everything else depends on this abstraction.
/// </summary>
public interface ITerminalEngine : IDisposable
{
    /// <summary>Current width of the grid in cells.</summary>
    int Columns { get; }

    /// <summary>Current height of the grid in cells.</summary>
    int Rows { get; }

    /// <summary>The window title most recently set by the program, or <see langword="null"/> if none.</summary>
    string? Title { get; }

    /// <summary>Feeds program output (VT/ANSI bytes) into the terminal.</summary>
    /// <param name="data">The bytes to process.</param>
    void Write(ReadOnlySpan<byte> data);

    /// <summary>Resizes the grid.</summary>
    /// <param name="columns">New width in cells; must be positive.</param>
    /// <param name="rows">New height in cells; must be positive.</param>
    void Resize(int columns, int rows);
}
