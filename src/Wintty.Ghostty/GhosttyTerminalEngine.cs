using System.Runtime.InteropServices;
using System.Text;
using LibGhostty;
using Wintty.Core.Abstractions;

namespace Wintty.Ghostty;

/// <summary>Adapts a native libghostty-vt terminal handle to <see cref="ITerminalEngine"/>.</summary>
internal sealed class GhosttyTerminalEngine : ITerminalEngine
{
    private const int WideCell = 1;
    private const int SpacerTailCell = 2;

    private readonly GhosttyNativeLibrary _native;
    private IntPtr _handle;
    private IntPtr _renderState;
    private IntPtr _rowIterator;
    private IntPtr _rowCells;

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

        try
        {
            ThrowIfFailed(native.RenderStateNew(out _renderState), "create render state");
            ThrowIfFailed(native.RowIteratorNew(out _rowIterator), "create row iterator");
            ThrowIfFailed(native.RowCellsNew(out _rowCells), "create row cells");
        }
        catch
        {
            Dispose();
            throw;
        }
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
    public TerminalSnapshot CreateSnapshot()
    {
        ThrowIfDisposed();
        ThrowIfFailed(_native.RenderStateUpdate(_renderState, _handle), "update render state");

        ThrowIfFailed(_native.RenderStateGetU16(_renderState, GhosttyNativeLibrary.RenderStateDataCols, out var columns), "read columns");
        ThrowIfFailed(_native.RenderStateGetU16(_renderState, GhosttyNativeLibrary.RenderStateDataRows, out var rows), "read rows");
        var defaultForeground = ReadStateColor(GhosttyNativeLibrary.RenderStateDataColorForeground);
        var defaultBackground = ReadStateColor(GhosttyNativeLibrary.RenderStateDataColorBackground);

        var cells = new TerminalCell[columns * rows];
        var iterator = _rowIterator;
        ThrowIfFailed(
            _native.RenderStateGetHandle(_renderState, GhosttyNativeLibrary.RenderStateDataRowIterator, ref iterator),
            "begin row iteration");

        var row = 0;
        while (row < rows && _native.RowIteratorNext(iterator))
        {
            var rowCells = _rowCells;
            ThrowIfFailed(_native.RowGetCells(iterator, ref rowCells), "read row cells");
            for (var column = 0; column < columns; column++)
            {
                cells[(row * columns) + column] = ReadCell(rowCells, column, defaultForeground, defaultBackground);
            }

            row++;
        }

        // Rows the iterator did not produce stay blank rather than null-ish default structs.
        for (var index = row * columns; index < cells.Length; index++)
        {
            cells[index] = new TerminalCell(string.Empty, defaultForeground, defaultBackground);
        }

        return new TerminalSnapshot(columns, rows, cells, ReadCursor(), defaultForeground, defaultBackground);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            return;
        }

        FreeAndClear(ref _rowCells, _native.RowCellsFree);
        FreeAndClear(ref _rowIterator, _native.RowIteratorFree);
        FreeAndClear(ref _renderState, _native.RenderStateFree);
        _native.TerminalFree(handle);
    }

    private static void FreeAndClear(ref IntPtr pointer, Action<IntPtr> free)
    {
        if (pointer != IntPtr.Zero)
        {
            free(pointer);
            pointer = IntPtr.Zero;
        }
    }

    private static void ThrowIfFailed(int status, string operation)
    {
        if (status != GhosttyNativeLibrary.Success)
        {
            throw new InvalidOperationException($"libghostty failed to {operation} (status {status}).");
        }
    }

    private static TerminalColor ToColor(GhosttyNativeLibrary.NativeColor color) => new(color.Red, color.Green, color.Blue);

    private TerminalColor ReadStateColor(int data)
    {
        ThrowIfFailed(_native.RenderStateGetColor(_renderState, data, out var color), "read colour");
        return ToColor(color);
    }

    private CursorState ReadCursor()
    {
        _native.RenderStateGetByte(_renderState, GhosttyNativeLibrary.RenderStateDataCursorVisible, out var visible);
        _native.RenderStateGetByte(_renderState, GhosttyNativeLibrary.RenderStateDataCursorViewportHasValue, out var inViewport);
        _native.RenderStateGetInt(_renderState, GhosttyNativeLibrary.RenderStateDataCursorVisualStyle, out var style);
        ushort x = 0;
        ushort y = 0;
        if (inViewport != 0)
        {
            _native.RenderStateGetU16(_renderState, GhosttyNativeLibrary.RenderStateDataCursorViewportX, out x);
            _native.RenderStateGetU16(_renderState, GhosttyNativeLibrary.RenderStateDataCursorViewportY, out y);
        }

        var shape = Enum.IsDefined((CursorStyle)style) ? (CursorStyle)style : CursorStyle.Block;
        return new CursorState(x, y, visible != 0 && inViewport != 0, shape);
    }

    private unsafe TerminalCell ReadCell(IntPtr rowCells, int column, TerminalColor defaultForeground, TerminalColor defaultBackground)
    {
        ThrowIfFailed(_native.RowCellsSelect(rowCells, checked((ushort)column)), "select cell");

        var text = string.Empty;
        if (_native.RowCellsGetU32(rowCells, GhosttyNativeLibrary.RenderStateRowCellsDataGraphemesLength, out var length) == GhosttyNativeLibrary.Success
            && length > 0)
        {
            var codepoints = new uint[length];
            fixed (uint* buffer = codepoints)
            {
                if (_native.RowCellsGetBuffer(rowCells, GhosttyNativeLibrary.RenderStateRowCellsDataGraphemesBuffer, (IntPtr)buffer) == GhosttyNativeLibrary.Success)
                {
                    text = ToText(codepoints);
                }
            }
        }

        var foreground = _native.RowCellsGetColor(rowCells, GhosttyNativeLibrary.RenderStateRowCellsDataForegroundColor, out var fg) == GhosttyNativeLibrary.Success
            ? ToColor(fg)
            : defaultForeground;
        var background = _native.RowCellsGetColor(rowCells, GhosttyNativeLibrary.RenderStateRowCellsDataBackgroundColor, out var bg) == GhosttyNativeLibrary.Success
            ? ToColor(bg)
            : defaultBackground;

        var attributes = CellAttributes.None;
        var underline = UnderlineStyle.None;
        if (_native.RowCellsGetStyle(rowCells, out var style) == GhosttyNativeLibrary.Success)
        {
            attributes = ToAttributes(style);
            underline = Enum.IsDefined((UnderlineStyle)style.Underline) ? (UnderlineStyle)style.Underline : UnderlineStyle.Single;
        }

        var width = 1;
        if (_native.RowCellsGetRaw(rowCells, out var raw) == GhosttyNativeLibrary.Success
            && _native.CellGetWide(raw, out var wide) == GhosttyNativeLibrary.Success)
        {
            width = wide switch
            {
                WideCell => 2,
                SpacerTailCell => 0,
                _ => 1,
            };
        }

        return new TerminalCell(text, foreground, background, attributes, underline, width);
    }

    private static string ToText(uint[] codepoints)
    {
        var builder = new StringBuilder(codepoints.Length);
        foreach (var codepoint in codepoints)
        {
            if (Rune.IsValid(codepoint))
            {
                builder.Append(char.ConvertFromUtf32((int)codepoint));
            }
        }

        return builder.ToString();
    }

    private static CellAttributes ToAttributes(GhosttyNativeLibrary.NativeStyle style)
    {
        var attributes = CellAttributes.None;
        if (style.Bold != 0) { attributes |= CellAttributes.Bold; }
        if (style.Italic != 0) { attributes |= CellAttributes.Italic; }
        if (style.Faint != 0) { attributes |= CellAttributes.Faint; }
        if (style.Blink != 0) { attributes |= CellAttributes.Blink; }
        if (style.Inverse != 0) { attributes |= CellAttributes.Inverse; }
        if (style.Invisible != 0) { attributes |= CellAttributes.Invisible; }
        if (style.Strikethrough != 0) { attributes |= CellAttributes.Strikethrough; }
        if (style.Overline != 0) { attributes |= CellAttributes.Overline; }
        return attributes;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);
}
