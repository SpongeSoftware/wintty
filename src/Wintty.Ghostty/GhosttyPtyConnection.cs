using System.ComponentModel;
using LibGhostty;
using Wintty.Core.Abstractions;

namespace Wintty.Ghostty;

/// <summary>Adapts a <c>LibGhostty.Net</c> PTY connection to <see cref="IPtyConnection"/>.</summary>
internal sealed class GhosttyPtyConnection : IPtyConnection
{
    private readonly IGhosttyPtyConnection _inner;

    internal GhosttyPtyConnection(IGhosttyPtyConnection inner)
    {
        _inner = inner;
        _inner.ProcessExited += OnProcessExited;
    }

    /// <inheritdoc />
    public event EventHandler<int>? Exited;

    /// <inheritdoc />
    public Stream Output => _inner.ReaderStream;

    /// <inheritdoc />
    public Stream Input => _inner.WriterStream;

    /// <inheritdoc />
    public int ProcessId => _inner.Pid;

    /// <inheritdoc />
    public void Resize(int columns, int rows) => _inner.Resize(columns, rows);

    /// <inheritdoc />
    public void Kill()
    {
        try
        {
            _inner.Kill();
        }
        catch (Win32Exception) when (_inner.WaitForExit(2000))
        {
            // The process exited on its own (possibly just now, racing the kill); that is not an error.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _inner.ProcessExited -= OnProcessExited;
        (_inner as IDisposable)?.Dispose();
    }

    private void OnProcessExited(object? sender, GhosttyPtyExitedEventArgs args) => Exited?.Invoke(this, args.ExitCode);
}
