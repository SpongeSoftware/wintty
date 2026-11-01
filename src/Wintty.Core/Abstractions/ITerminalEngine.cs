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

    /// <summary>
    /// Encodes a mouse event into the report the running program asked for. Returns nothing when the program
    /// has not enabled mouse reporting.
    /// </summary>
    /// <param name="input">The mouse event, positioned in pixels within the cell grid.</param>
    /// <returns>The bytes to send to the program; empty when mouse reporting is off or the event is not reported.</returns>
    byte[] EncodeMouse(Input.MouseInput input);

    /// <summary>Gets a value indicating whether the program has asked for mouse reports.</summary>
    bool MouseTracking { get; }

    /// <summary>Gets a value indicating whether the program asked for pasted text to be wrapped in bracketed-paste markers.</summary>
    bool BracketedPaste { get; }

    /// <summary>
    /// Prepares text for pasting: unsafe control characters are removed, line breaks become carriage returns and,
    /// when the program enabled bracketed paste, the text is wrapped in the paste markers.
    /// </summary>
    /// <param name="text">The text to paste.</param>
    /// <returns>The bytes to send to the program.</returns>
    byte[] EncodePaste(string text);

    /// <summary>
    /// Gets a value indicating whether the program is in the middle of a synchronised update (mode 2026): it has
    /// asked for the screen not to be presented until it says the frame is complete.
    /// </summary>
    bool SynchronizedOutput { get; }

    /// <summary>Gets a value indicating whether the program asked to be told when the window gains or loses focus (mode 1004).</summary>
    bool FocusEvents { get; }

    /// <summary>Gets a value indicating whether the program asked to be told when the colour scheme changes (mode 2031).</summary>
    bool ColorSchemeNotifications { get; }

    /// <summary>Scrolls the visible rows through the history.</summary>
    /// <param name="rows">Rows to move; negative scrolls up towards older output.</param>
    void ScrollViewport(int rows);

    /// <summary>Returns the view to the live bottom of the screen.</summary>
    void ScrollViewportToBottom();

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

    /// <summary>
    /// Sets the default colours, cursor colour and palette. Anything the scheme leaves unset returns to the
    /// terminal's built-in value, so each call fully replaces the previous scheme.
    /// </summary>
    /// <param name="scheme">The colours to use.</param>
    void SetColors(TerminalColorScheme scheme);

    /// <summary>Resizes the grid.</summary>
    /// <param name="columns">New width in cells; must be positive.</param>
    /// <param name="rows">New height in cells; must be positive.</param>
    void Resize(int columns, int rows);
}
