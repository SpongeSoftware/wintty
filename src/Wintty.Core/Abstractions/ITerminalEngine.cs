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

    /// <summary>
    /// Encodes a keyboard event into the bytes the running program expects, honouring the terminal's current
    /// modes (application cursor keys, kitty keyboard protocol and so on).
    /// </summary>
    /// <param name="input">The key event.</param>
    /// <returns>The bytes to send to the program; empty when the event produces no output.</returns>
    byte[] EncodeKey(Input.KeyInput input);

    /// <summary>Captures the current screen contents for drawing.</summary>
    /// <returns>An immutable snapshot; safe to use after further writes.</returns>
    TerminalSnapshot CreateSnapshot();

    /// <summary>Resizes the grid.</summary>
    /// <param name="columns">New width in cells; must be positive.</param>
    /// <param name="rows">New height in cells; must be positive.</param>
    void Resize(int columns, int rows);
}
