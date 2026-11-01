using Wintty.Core.Abstractions;
using Wintty.Core.Events;

namespace Wintty.Core.Sessions;

/// <summary>
/// One terminal pane: a process in a pseudo-terminal whose output is fed into a terminal engine.
/// Depends only on abstractions, so it is fully testable with fakes.
/// </summary>
public sealed class TerminalSession : IDisposable
{
    private readonly IPtyConnection _pty;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pump;
    private readonly object _engineLock = new();
    private readonly object _inputLock = new();
    private const int SynchronizedUpdateTimeoutMilliseconds = 1000;

    private readonly VtEventScanner _scanner;
    private readonly Timer _syncTimer;
    private int _disposed;

    private TerminalSession(ITerminalEngine engine, IPtyConnection pty)
    {
        Engine = engine;
        _pty = pty;
        _syncTimer = new Timer(_ => Updated?.Invoke(this, EventArgs.Empty), null, Timeout.Infinite, Timeout.Infinite);
        _scanner = new VtEventScanner(OnOsc, () => BellRang?.Invoke(this, EventArgs.Empty), OnApc);
        engine.ResponseHandler = OnEngineResponse;
        _pty.Exited += OnPtyExited;
        _pump = Task.Run(() => PumpAsync(_cts.Token));
    }

    /// <summary>Raised after output has been processed into the engine.</summary>
    public event EventHandler? Updated;

    /// <summary>Raised when the child process exits, with its exit code.</summary>
    public event EventHandler<int>? Exited;

    /// <summary>Raised on the output thread when the program rings the bell.</summary>
    public event EventHandler? BellRang;

    /// <summary>
    /// Raised on the output thread when the program asks for something on the desktop: a notification, a clipboard
    /// read or write, progress for the taskbar, or a working-directory report.
    /// </summary>
    public event EventHandler<TerminalEvent>? TerminalEventRaised;

    /// <summary>
    /// The shell's current directory as a Windows path, from its latest OSC 7 report, or <see langword="null"/>
    /// when it has not reported one or reported a path that exists only inside a Linux distribution.
    /// </summary>
    public string? WorkingDirectory { get; private set; }

    /// <summary>The terminal state for this session. Hold <see cref="SyncRoot"/> while reading it.</summary>
    public ITerminalEngine Engine { get; }

    /// <summary>Lock that guards <see cref="Engine"/> against concurrent writes from the output pump.</summary>
    public object SyncRoot => _engineLock;

    /// <summary>Starts a process and a terminal engine and connects them.</summary>
    /// <param name="engines">Engine factory.</param>
    /// <param name="ptys">Pseudo-terminal factory.</param>
    /// <param name="options">The process to run and the initial size.</param>
    /// <param name="scrollback">Maximum scrollback lines.</param>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <returns>A running session.</returns>
    public static async Task<TerminalSession> StartAsync(
        ITerminalEngineFactory engines,
        IPtyConnectionFactory ptys,
        PtyStartOptions options,
        int scrollback = 10_000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(ptys);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        ITerminalEngine engine = engines.Create(new TerminalEngineOptions(options.Columns, options.Rows, scrollback));
        try
        {
            IPtyConnection pty = await ptys.StartAsync(options, cancellationToken).ConfigureAwait(false);
            return new TerminalSession(engine, pty);
        }
        catch
        {
            engine.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Sends input bytes (already encoded) to the process. Typing returns the view to the live screen, so the
    /// user sees what they are typing even after scrolling back.
    /// </summary>
    /// <param name="data">The bytes to send.</param>
    public void SendInput(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        lock (_engineLock)
        {
            Engine.ScrollViewportToBottom();
        }

        WriteToProcess(data);
    }

    /// <summary>Scrolls the view through the history.</summary>
    /// <param name="rows">Rows to move; negative scrolls up towards older output.</param>
    public void ScrollBy(int rows)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        lock (_engineLock)
        {
            Engine.ScrollViewport(rows);
        }
    }

    /// <summary>Returns the view to the live screen without sending anything to the program.</summary>
    public void ScrollToBottom()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        lock (_engineLock)
        {
            Engine.ScrollViewportToBottom();
        }
    }

    /// <summary>Encodes a mouse event and sends it to the process, if the program asked for mouse reports.</summary>
    /// <param name="input">The mouse event.</param>
    /// <returns><see langword="true"/> if a report was sent; <see langword="false"/> if reporting is off.</returns>
    public bool SendMouse(Input.MouseInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        byte[] bytes;
        lock (_engineLock)
        {
            bytes = Engine.EncodeMouse(input);
        }

        if (bytes.Length == 0)
        {
            return false;
        }

        WriteToProcess(bytes);
        return true;
    }

    /// <summary>
    /// Tells a program that asked for focus reports (mode 1004) that the window gained or lost focus.
    /// </summary>
    /// <param name="focused"><see langword="true"/> when the window gained focus.</param>
    /// <returns><see langword="true"/> if a report was sent.</returns>
    public bool SetFocus(bool focused)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        lock (_engineLock)
        {
            if (!Engine.FocusEvents)
            {
                return false;
            }
        }

        WriteToProcess(focused ? "\u001b[I"u8 : "\u001b[O"u8);
        return true;
    }

    /// <summary>
    /// Tells a program that asked for colour scheme notifications (mode 2031) that the system theme changed.
    /// </summary>
    /// <param name="dark"><see langword="true"/> for a dark scheme, <see langword="false"/> for a light one.</param>
    /// <returns><see langword="true"/> if a notification was sent.</returns>
    public bool NotifyColorScheme(bool dark)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        lock (_engineLock)
        {
            if (!Engine.ColorSchemeNotifications)
            {
                return false;
            }
        }

        WriteToProcess(dark ? "\u001b[?997;1n"u8 : "\u001b[?997;2n"u8);
        return true;
    }

    /// <summary>Gets a value indicating whether the program enabled bracketed paste.</summary>
    public bool BracketedPaste
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            lock (_engineLock)
            {
                return Engine.BracketedPaste;
            }
        }
    }

    /// <summary>
    /// Pastes text into the program: control characters are filtered, line breaks become carriage returns and
    /// the text is wrapped in bracketed-paste markers when the program enabled them.
    /// </summary>
    /// <param name="text">The text to paste.</param>
    public void Paste(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        if (text.Length == 0)
        {
            return;
        }

        byte[] bytes;
        lock (_engineLock)
        {
            bytes = Engine.EncodePaste(text);
        }

        SendInput(bytes);
    }

    /// <summary>Reads the text under a selection, including rows scrolled out of view.</summary>
    /// <param name="selection">The selected region.</param>
    /// <returns>The text to copy.</returns>
    public string GetSelectedText(Selection.TerminalSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        lock (_engineLock)
        {
            // Reading history means moving the view, so put it back afterwards.
            long original = Engine.CreateSnapshot().Scrollbar.Offset;
            try
            {
                return Selection.SelectionText.Extract(selection, row =>
                {
                    long current = Engine.CreateSnapshot().Scrollbar.Offset;
                    Engine.ScrollViewport((int)(row - current));
                    return Engine.CreateSnapshot();
                });
            }
            finally
            {
                Engine.ScrollViewport((int)(original - Engine.CreateSnapshot().Scrollbar.Offset));
            }
        }
    }

    /// <summary>Gets a value indicating whether the program has asked for mouse reports.</summary>
    public bool MouseTracking
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            lock (_engineLock)
            {
                return Engine.MouseTracking;
            }
        }
    }

    private void OnApc(string content)
    {
        if (KittyGraphicsCommand.Parse(content) is { } command)
        {
            TerminalEventRaised?.Invoke(this, new GraphicsCommandReceived(command));
        }
    }

    private void OnOsc(OscSequence sequence)
    {
        if (TerminalEvent.From(sequence) is not { } terminalEvent)
        {
            return;
        }

        if (terminalEvent is WorkingDirectoryReported { WindowsPath: { } path })
        {
            WorkingDirectory = path;
        }

        TerminalEventRaised?.Invoke(this, terminalEvent);
    }

    private void OnEngineResponse(ReadOnlySpan<byte> data)
    {
        if (_disposed != 0)
        {
            return;
        }

        try
        {
            WriteToProcess(data);
        }
        catch (IOException)
        {
            // The pseudo-terminal closed while a reply was being written; nobody is left to receive it.
        }
        catch (ObjectDisposedException)
        {
            // Same, when the stream was already disposed.
        }
    }

    // Typing and terminal replies share one stream, so writes are serialised to keep each message contiguous.
    private void WriteToProcess(ReadOnlySpan<byte> data)
    {
        lock (_inputLock)
        {
            _pty.Input.Write(data);
            _pty.Input.Flush();
        }
    }

    /// <summary>Encodes a key event with the terminal's current modes and sends the result to the process.</summary>
    /// <param name="input">The key event.</param>
    /// <returns><see langword="true"/> if the event produced output that was sent.</returns>
    public bool SendKey(Input.KeyInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        byte[] bytes;
        lock (_engineLock)
        {
            bytes = Engine.EncodeKey(input);
        }

        if (bytes.Length == 0)
        {
            return false;
        }

        SendInput(bytes);
        return true;
    }

    /// <summary>Tells the terminal how large one cell is in physical pixels (used for size query replies).</summary>
    /// <param name="widthPixels">Cell width in pixels; must be positive.</param>
    /// <param name="heightPixels">Cell height in pixels; must be positive.</param>
    public void SetCellSize(int widthPixels, int heightPixels)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        lock (_engineLock)
        {
            Engine.SetCellSize(widthPixels, heightPixels);
        }
    }

    /// <summary>Applies a colour scheme to the running terminal.</summary>
    /// <param name="scheme">The colours to use.</param>
    public void SetColors(TerminalColorScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        lock (_engineLock)
        {
            Engine.SetColors(scheme);
        }
    }

    /// <summary>Resizes both the pseudo-terminal and the terminal engine.</summary>
    /// <param name="columns">New width in cells.</param>
    /// <param name="rows">New height in cells.</param>
    public void Resize(int columns, int rows)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(columns, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rows, 0);
        lock (_engineLock)
        {
            Engine.Resize(columns, rows);
        }

        _pty.Resize(columns, rows);
    }

    /// <summary>Stops the pump, terminates the process and releases the engine.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _pty.Exited -= OnPtyExited;
        Engine.ResponseHandler = null;
        _cts.Cancel();
        _pty.Kill();
        _pty.Dispose();
        try
        {
            _pump.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The pump ends with a cancellation or I/O error once the PTY is closed; nothing to report.
        }

        lock (_engineLock)
        {
            Engine.Dispose();
        }

        _syncTimer.Dispose();
        _cts.Dispose();
    }

    private async Task PumpAsync(CancellationToken token)
    {
        byte[] buffer = new byte[16 * 1024];
        bool holdFrame = false;
        try
        {
            while (!token.IsCancellationRequested)
            {
                int read = await _pty.Output.ReadAsync(buffer, token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                _scanner.Feed(buffer.AsSpan(0, read));
                lock (_engineLock)
                {
                    if (_disposed != 0)
                    {
                        break;
                    }

                    Engine.Write(buffer.AsSpan(0, read));
                    holdFrame = Engine.SynchronizedOutput;
                }

                // A program in a synchronised update (mode 2026) wants the frame shown only once it is complete.
                // Hold the redraw, but never for long, in case the program forgets to end the update.
                if (holdFrame)
                {
                    _syncTimer.Change(SynchronizedUpdateTimeoutMilliseconds, Timeout.Infinite);
                    continue;
                }

                _syncTimer.Change(Timeout.Infinite, Timeout.Infinite);

                Updated?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (IOException)
        {
            // The pseudo-terminal was closed underneath the read.
        }
        catch (ObjectDisposedException)
        {
            // Same as above, when the stream was disposed.
        }
    }

    private void OnPtyExited(object? sender, int exitCode) => Exited?.Invoke(this, exitCode);
}
