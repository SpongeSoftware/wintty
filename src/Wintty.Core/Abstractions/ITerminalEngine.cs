namespace Wintty.Core.Abstractions;

/// <summary>Handles bytes the terminal wants to send back to the running program.</summary>
/// <param name="data">The reply bytes; valid only for the duration of the call.</param>
public delegate void TerminalResponseHandler(ReadOnlySpan<byte> data);

/// <summary>
/// A virtual terminal: consumes the byte stream produced by a program and maintains screen state.
/// Implemented by the single libghostty gateway; everything else depends on this abstraction.
/// </summary>
public interface ITerminalEngine : IDisposable
{
    /// <summary>
    /// Receives bytes the terminal itself wants to send to the program: replies to queries such as device
    /// attributes, cursor position reports, mode queries and the Kitty keyboard flags query. Invoked on the
    /// thread that called <see cref="Write"/>, while that call is still running.
    /// </summary>
    TerminalResponseHandler? ResponseHandler { get; set; }

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

    /// <summary>
    /// Tells the terminal how large one cell is in physical pixels, so it can answer size queries
    /// (for example <c>CSI 14 t</c> and <c>CSI 16 t</c>) and so image protocols can scale correctly.
    /// </summary>
    /// <param name="widthPixels">Cell width in pixels; must be positive.</param>
    /// <param name="heightPixels">Cell height in pixels; must be positive.</param>
    void SetCellSize(int widthPixels, int heightPixels);

    /// <summary>Resizes the grid.</summary>
    /// <param name="columns">New width in cells; must be positive.</param>
    /// <param name="rows">New height in cells; must be positive.</param>
    void Resize(int columns, int rows);
}
