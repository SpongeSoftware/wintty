using System.Runtime.InteropServices;
using LibGhostty;
using Wintty.Core.Abstractions;

namespace Wintty.Ghostty;

/// <summary>Adapts a native libghostty-vt terminal handle to <see cref="ITerminalEngine"/>.</summary>
internal sealed class GhosttyTerminalEngine : ITerminalEngine
{
    private readonly GhosttyNativeLibrary _native;
    private IntPtr _handle;

    internal GhosttyTerminalEngine(GhosttyNativeLibrary native, TerminalEngineOptions options)
    {
        _native = native;
        var status = native.TerminalNew(
            new GhosttyNativeLibrary.TerminalOptions
            {
                Columns = checked((ushort)options.Columns),
                Rows = checked((ushort)options.Rows),
                MaxScrollback = (nuint)options.MaxScrollback,
            },
            out _handle);
        ThrowIfFailed(status, "create terminal");
        Columns = options.Columns;
        Rows = options.Rows;
    }

    /// <inheritdoc />
    public int Columns { get; private set; }

    /// <inheritdoc />
    public int Rows { get; private set; }

    /// <inheritdoc />
    public string? Title
    {
        get
        {
            ThrowIfDisposed();
            var status = _native.TerminalGetTitle(_handle, out var title);
            if (status != GhosttyNativeLibrary.Success || title.Pointer == IntPtr.Zero || title.Length == 0)
            {
                return null;
            }

            return Marshal.PtrToStringUTF8(title.Pointer, checked((int)title.Length));
        }
    }

    /// <inheritdoc />
    public unsafe void Write(ReadOnlySpan<byte> data)
    {
        ThrowIfDisposed();
        if (data.IsEmpty)
        {
            return;
        }

        fixed (byte* pointer = data)
        {
            _native.TerminalWrite(_handle, (IntPtr)pointer, (nuint)data.Length);
        }
    }

    /// <inheritdoc />
    public void Resize(int columns, int rows)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(columns, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rows, 0);

        // Cell pixel size is unknown until the renderer exists; 0 means "not provided".
        var status = _native.TerminalResize(_handle, checked((ushort)columns), checked((ushort)rows), 0, 0);
        ThrowIfFailed(status, "resize terminal");
        Columns = columns;
        Rows = rows;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero)
        {
            _native.TerminalFree(handle);
        }
    }

    private static void ThrowIfFailed(int status, string operation)
    {
        if (status != GhosttyNativeLibrary.Success)
        {
            throw new InvalidOperationException($"libghostty failed to {operation} (status {status}).");
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);
}
