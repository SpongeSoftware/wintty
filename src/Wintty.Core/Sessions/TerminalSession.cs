using Wintty.Core.Abstractions;

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
    private int _disposed;

    private TerminalSession(ITerminalEngine engine, IPtyConnection pty)
    {
        Engine = engine;
        _pty = pty;
        _pty.Exited += OnPtyExited;
        _pump = Task.Run(() => PumpAsync(_cts.Token));
    }

    /// <summary>Raised after output has been processed into the engine.</summary>
    public event EventHandler? Updated;

    /// <summary>Raised when the child process exits, with its exit code.</summary>
    public event EventHandler<int>? Exited;

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

        var engine = engines.Create(new TerminalEngineOptions(options.Columns, options.Rows, scrollback));
        try
        {
            var pty = await ptys.StartAsync(options, cancellationToken).ConfigureAwait(false);
            return new TerminalSession(engine, pty);
        }
        catch
        {
            engine.Dispose();
            throw;
        }
    }

    /// <summary>Sends input bytes (already encoded) to the process.</summary>
    /// <param name="data">The bytes to send.</param>
    public void SendInput(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        _pty.Input.Write(data);
        _pty.Input.Flush();
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

        _cts.Dispose();
    }

    private async Task PumpAsync(CancellationToken token)
    {
        var buffer = new byte[16 * 1024];
        try
        {
            while (!token.IsCancellationRequested)
            {
                var read = await _pty.Output.ReadAsync(buffer, token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                lock (_engineLock)
                {
                    if (_disposed != 0)
                    {
                        break;
                    }

                    Engine.Write(buffer.AsSpan(0, read));
                }

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
