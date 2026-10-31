using System.Runtime.InteropServices;
using System.Text;
using LibGhostty;
using Wintty.Core.Abstractions;
using Wintty.Core.Input;

namespace Wintty.Ghostty;

/// <summary>Adapts a native libghostty-vt terminal handle to <see cref="ITerminalEngine"/>.</summary>
internal sealed class GhosttyTerminalEngine : ITerminalEngine
{
    private const int WideCell = 1;
    private const int SpacerTailCell = 2;
    private const int MaxEncodedKeyLength = 128;

    private readonly GhosttyNativeLibrary _native;
    private IntPtr _handle;
    private IntPtr _renderState;
    private IntPtr _rowIterator;
    private IntPtr _rowCells;
    private IntPtr _keyEncoder;
    private IntPtr _keyEvent;
    private readonly GhosttyNativeLibrary.WritePtyCallback _writePtyCallback;
    private readonly GhosttyNativeLibrary.SizeCallback _sizeCallback;
    private int _cellWidthPixels;
    private int _cellHeightPixels;

    // Terminal options and render-state fields the wrapper package does not name yet.
    private const int TerminalOptionColorCursor = 13;
    private const int TerminalOptionColorPalette = 14;
    private const int RenderStateDataColorCursor = 7;

    private readonly TerminalEngineOptions _defaults;

    internal GhosttyTerminalEngine(GhosttyNativeLibrary native, TerminalEngineOptions options)
    {
        _native = native;
        _defaults = options;
        var status = native.TerminalNew(
            new GhosttyNativeLibrary.TerminalOptions
            {
                Columns = checked((ushort)options.Columns),
                Rows = checked((ushort)options.Rows),
                MaxScrollback = (nuint)options.MaxScrollback,
            },
            out _handle);
        ThrowIfFailed(status, "create terminal");

        // Keep a reference for the engine's lifetime: native code holds the function pointer.
        _writePtyCallback = OnWritePty;
        ThrowIfFailed(
            native.TerminalSet(_handle, GhosttyNativeLibrary.TerminalOptionWritePty, Marshal.GetFunctionPointerForDelegate(_writePtyCallback)),
            "register the reply callback");

        _sizeCallback = OnSizeQuery;
        ThrowIfFailed(
            native.TerminalSet(_handle, GhosttyNativeLibrary.TerminalOptionSize, Marshal.GetFunctionPointerForDelegate(_sizeCallback)),
            "register the size callback");
        SetDefaultColor(GhosttyNativeLibrary.TerminalOptionColorForeground, options.EffectiveForeground);
        SetDefaultColor(GhosttyNativeLibrary.TerminalOptionColorBackground, options.EffectiveBackground);
        Columns = options.Columns;
        Rows = options.Rows;

        try
        {
            ThrowIfFailed(native.RenderStateNew(out _renderState), "create render state");
            ThrowIfFailed(native.RowIteratorNew(out _rowIterator), "create row iterator");
            ThrowIfFailed(native.RowCellsNew(out _rowCells), "create row cells");
            ThrowIfFailed(native.KeyEncoderNew(out _keyEncoder), "create key encoder");
            ThrowIfFailed(native.KeyEventNew(out _keyEvent), "create key event");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public TerminalResponseHandler? ResponseHandler { get; set; }

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
    public void SetCellSize(int widthPixels, int heightPixels)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(widthPixels, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(heightPixels, 0);
        _cellWidthPixels = widthPixels;
        _cellHeightPixels = heightPixels;

        // Re-send the grid size so the terminal learns the new pixel dimensions.
        Resize(Columns, Rows);
    }

    /// <inheritdoc />
    public void Resize(int columns, int rows)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(columns, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rows, 0);

        // A pixel size of 0 means "not provided" until the renderer has measured a cell.
        var status = _native.TerminalResize(
            _handle, checked((ushort)columns), checked((ushort)rows), (uint)_cellWidthPixels, (uint)_cellHeightPixels);
        ThrowIfFailed(status, "resize terminal");
        Columns = columns;
        Rows = rows;
    }

    /// <inheritdoc />
    public unsafe byte[] EncodeKey(KeyInput input)
    {
        ThrowIfDisposed();

        // Sync cursor-key mode, kitty flags and other modes from the terminal before every encode.
        _native.KeyEncoderSetFromTerminal(_keyEncoder, _handle);
        _native.KeyEventSetAction(_keyEvent, GhosttyKeyMap.ToNative(input.Action));
        _native.KeyEventSetKey(_keyEvent, GhosttyKeyMap.ToNative(input.Key));
        _native.KeyEventSetMods(_keyEvent, GhosttyKeyMap.ToNative(input.Modifiers));
        _native.KeyEventSetConsumedMods(_keyEvent, GhosttyKeyMap.ToNative(input.ConsumedModifiers));
        _native.KeyEventSetUnshiftedCodepoint(_keyEvent, (uint)Math.Max(0, input.UnshiftedCodepoint));

        var text = string.IsNullOrEmpty(input.Text) ? [] : Encoding.UTF8.GetBytes(input.Text);
        Span<byte> buffer = stackalloc byte[MaxEncodedKeyLength];
        nuint written;
        int status;
        fixed (byte* textPointer = text)
        fixed (byte* bufferPointer = buffer)
        {
            // The text pointer must stay valid until the encode call has finished.
            _native.KeyEventSetUtf8(_keyEvent, text.Length == 0 ? IntPtr.Zero : (IntPtr)textPointer, (nuint)text.Length);
            status = _native.KeyEncoderEncode(_keyEncoder, _keyEvent, (IntPtr)bufferPointer, (nuint)buffer.Length, out written);
        }

        ThrowIfFailed(status, "encode key");
        return buffer[..(int)written].ToArray();
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
        var palette = new GhosttyNativeLibrary.NativeColor[GhosttyNativeLibrary.RenderStatePaletteLength];
        var paletteOrNull = _native.RenderStateColorsGet(_renderState, palette) == GhosttyNativeLibrary.Success ? palette : null;
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
                cells[(row * columns) + column] = ReadCell(rowCells, column, defaultForeground, defaultBackground, paletteOrNull);
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

        FreeAndClear(ref _keyEvent, _native.KeyEventFree);
        FreeAndClear(ref _keyEncoder, _native.KeyEncoderFree);
        FreeAndClear(ref _rowCells, _native.RowCellsFree);
        FreeAndClear(ref _rowIterator, _native.RowIteratorFree);
        FreeAndClear(ref _renderState, _native.RenderStateFree);
        _native.TerminalFree(handle);
    }

    /// <inheritdoc />
    public unsafe void SetColors(TerminalColorScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        scheme.Validate();
        ThrowIfDisposed();

        SetDefaultColor(GhosttyNativeLibrary.TerminalOptionColorForeground, scheme.Foreground ?? _defaults.EffectiveForeground);
        SetDefaultColor(GhosttyNativeLibrary.TerminalOptionColorBackground, scheme.Background ?? _defaults.EffectiveBackground);

        // A null pointer returns an option to libghostty's built-in value.
        if (scheme.CursorColor is { } cursor)
        {
            SetDefaultColor(TerminalOptionColorCursor, cursor);
        }
        else
        {
            ThrowIfFailed(_native.TerminalSet(_handle, TerminalOptionColorCursor, IntPtr.Zero), "reset the cursor colour");
        }

        ThrowIfFailed(_native.TerminalSet(_handle, TerminalOptionColorPalette, IntPtr.Zero), "reset the palette");
        if (scheme.Entries.Count == 0)
        {
            return;
        }

        // The palette is replaced as a whole, so start from the built-in entries and overlay the overrides.
        ThrowIfFailed(_native.RenderStateUpdate(_renderState, _handle), "update render state");
        var palette = new GhosttyNativeLibrary.NativeColor[GhosttyNativeLibrary.RenderStatePaletteLength];
        ThrowIfFailed(_native.RenderStateColorsGet(_renderState, palette), "read the palette");
        foreach (var (index, color) in scheme.Entries)
        {
            palette[index] = new GhosttyNativeLibrary.NativeColor { Red = color.Red, Green = color.Green, Blue = color.Blue };
        }

        fixed (GhosttyNativeLibrary.NativeColor* pointer = palette)
        {
            ThrowIfFailed(_native.TerminalSet(_handle, TerminalOptionColorPalette, (IntPtr)pointer), "set the palette");
        }
    }

    private unsafe void SetDefaultColor(int option, TerminalColor color)
    {
        var native = new GhosttyNativeLibrary.NativeColor { Red = color.Red, Green = color.Green, Blue = color.Blue };
        ThrowIfFailed(_native.TerminalSet(_handle, option, (IntPtr)(&native)), "set a default colour");
    }

    private unsafe byte OnSizeQuery(IntPtr terminal, IntPtr userdata, IntPtr output)
    {
        // Exceptions must never cross into native code. Returning 0 means "no answer available".
        try
        {
            if (output == IntPtr.Zero)
            {
                return 0;
            }

            // Pixel sizes of 0 mean "unknown" until the renderer has measured a cell; the character grid is
            // always known, so queries that only need it are still answered.

            *(GhosttyNativeLibrary.NativeSizeReport*)output = new GhosttyNativeLibrary.NativeSizeReport
            {
                Rows = checked((ushort)Rows),
                Columns = checked((ushort)Columns),
                CellWidth = (uint)Math.Max(0, _cellWidthPixels),
                CellHeight = (uint)Math.Max(0, _cellHeightPixels),
            };
            return 1;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private unsafe void OnWritePty(IntPtr terminal, IntPtr userdata, IntPtr data, nuint length)
    {
        // Exceptions must never cross into native code.
        try
        {
            var handler = ResponseHandler;
            if (handler is not null && data != IntPtr.Zero && length > 0)
            {
                handler(new ReadOnlySpan<byte>((void*)data, checked((int)length)));
            }
        }
        catch (Exception)
        {
            // A failing consumer must not take the process down; the reply is dropped.
        }
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
        TerminalColor? color = _native.RenderStateGetColor(_renderState, RenderStateDataColorCursor, out var native) == GhosttyNativeLibrary.Success
            ? ToColor(native)
            : null;
        return new CursorState(x, y, visible != 0 && inViewport != 0, shape, color);
    }

    private unsafe TerminalCell ReadCell(
        IntPtr rowCells,
        int column,
        TerminalColor defaultForeground,
        TerminalColor defaultBackground,
        GhosttyNativeLibrary.NativeColor[]? palette)
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
        TerminalColor? underlineColor = null;
        if (_native.RowCellsGetStyle(rowCells, out var style) == GhosttyNativeLibrary.Success)
        {
            attributes = ToAttributes(style);
            underline = Enum.IsDefined((UnderlineStyle)style.Underline) ? (UnderlineStyle)style.Underline : UnderlineStyle.Single;
            underlineColor = ReadUnderlineColor(style, palette);
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

        return new TerminalCell(text, foreground, background, attributes, underline, width, underline == UnderlineStyle.None ? null : underlineColor);
    }

    private static TerminalColor? ReadUnderlineColor(GhosttyNativeLibrary.NativeStyle style, GhosttyNativeLibrary.NativeColor[]? palette)
    {
        var color = style.UnderlineColor;
        if (color.Tag == GhosttyNativeLibrary.StyleColorTagRgb)
        {
            return ToColor(color.Value.Rgb);
        }

        if (color.Tag == GhosttyNativeLibrary.StyleColorTagPalette && palette is not null && color.Value.Palette < palette.Length)
        {
            return ToColor(palette[color.Value.Palette]);
        }

        // Tag "none": the underline follows the text colour.
        return null;
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
