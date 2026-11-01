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
    private IntPtr _mouseEncoder;
    private IntPtr _mouseEvent;
    private readonly GhosttyNativeLibrary.WritePtyCallback _writePtyCallback;
    private readonly GhosttyNativeLibrary.SizeCallback _sizeCallback;
    private int _cellWidthPixels;

    // Snapshots are immutable, so one is reused until something changes the screen (cursor blinks, for example,
    // redraw without a new snapshot).
    private TerminalSnapshot? _cachedSnapshot;
    private int _cellHeightPixels;

    // Terminal options and render-state fields the wrapper package does not name yet.
    private const int TerminalOptionColorCursor = 13;
    private const int TerminalOptionColorPalette = 14;
    private const int RenderStateDataColorCursor = 7;
    private const int MouseEncoderOptionSize = 2;
    private const int MouseEncoderOptionTrackLastCell = 4;
    private const int ScrollViewportBottom = 1;
    private const int ScrollViewportDelta = 2;
    private const int MaxEncodedMouseLength = 64;
    private const int PasteMarkerAllowance = 32;

    private readonly TerminalEngineOptions _defaults;

    internal GhosttyTerminalEngine(GhosttyNativeLibrary native, TerminalEngineOptions options)
    {
        _native = native;
        _defaults = options;
        int status = native.TerminalNew(
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
            ThrowIfFailed(native.MouseEncoderNew(out _mouseEncoder), "create mouse encoder");
            ThrowIfFailed(native.MouseEventNew(out _mouseEvent), "create mouse event");

            // Report a drag once per cell rather than once per pixel.
            native.MouseEncoderSetByteOption(_mouseEncoder, MouseEncoderOptionTrackLastCell, 1);
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
            int status = _native.TerminalGetTitle(_handle, out GhosttyNativeLibrary.NativeString title);
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
            _cachedSnapshot = null;
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

        _cachedSnapshot = null;

        // A pixel size of 0 means "not provided" until the renderer has measured a cell.
        int status = _native.TerminalResize(
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

        byte[] text = string.IsNullOrEmpty(input.Text) ? [] : Encoding.UTF8.GetBytes(input.Text);
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
    public bool MouseTracking
    {
        get
        {
            ThrowIfDisposed();
            return _native.TerminalGetMouseTracking(_handle, out byte enabled) == GhosttyNativeLibrary.Success && enabled != 0;
        }
    }

    /// <inheritdoc />
    public unsafe byte[] EncodeMouse(MouseInput input)
    {
        ThrowIfDisposed();
        if (!MouseTracking)
        {
            return [];
        }

        // Sync the tracking mode and report format (normal, UTF-8, SGR, urxvt, SGR-pixels) from the terminal.
        _native.MouseEncoderSetFromTerminal(_mouseEncoder, _handle);
        var size = new GhosttyNativeLibrary.NativeMouseEncoderSize
        {
            Size = (UIntPtr)sizeof(GhosttyNativeLibrary.NativeMouseEncoderSize),
            CellWidth = (uint)Math.Max(1, _cellWidthPixels),
            CellHeight = (uint)Math.Max(1, _cellHeightPixels),
        };
        size.ScreenWidth = size.CellWidth * (uint)Columns;
        size.ScreenHeight = size.CellHeight * (uint)Rows;
        _native.MouseEncoderSetOption(_mouseEncoder, MouseEncoderOptionSize, ref size);

        _native.MouseEventSetAction(_mouseEvent, (int)input.Action);
        if (input.Button == MouseButton.None)
        {
            _native.MouseEventClearButton(_mouseEvent);
        }
        else
        {
            _native.MouseEventSetButton(_mouseEvent, (int)input.Button);
        }

        _native.MouseEventSetMods(_mouseEvent, GhosttyKeyMap.ToNative(input.Modifiers));
        _native.MouseEventSetPosition(_mouseEvent, new GhosttyNativeLibrary.NativeMousePosition { X = (float)input.X, Y = (float)input.Y });

        Span<byte> buffer = stackalloc byte[MaxEncodedMouseLength];
        nuint written;
        int status;
        fixed (byte* bufferPointer = buffer)
        {
            status = _native.MouseEncoderEncode(_mouseEncoder, _mouseEvent, (IntPtr)bufferPointer, (nuint)buffer.Length, out written);
        }

        ThrowIfFailed(status, "encode mouse event");
        return buffer[..(int)written].ToArray();
    }

    /// <inheritdoc />
    public bool BracketedPaste
    {
        get
        {
            ThrowIfDisposed();
            return NativeModes.IsSet(_handle, NativeModes.BracketedPaste);
        }
    }

    /// <inheritdoc />
    public bool SynchronizedOutput => Mode(NativeModes.SynchronizedOutput);

    /// <inheritdoc />
    public bool FocusEvents => Mode(NativeModes.FocusEvents);

    /// <inheritdoc />
    public bool ColorSchemeNotifications => Mode(NativeModes.ColorSchemeNotifications);

    private bool Mode(ushort mode)
    {
        ThrowIfDisposed();
        return NativeModes.IsSet(_handle, mode);
    }

    /// <inheritdoc />
    public unsafe byte[] EncodePaste(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ThrowIfDisposed();

        // The encoder rewrites its input in place, and may add the bracketed-paste markers, so work on a copy.
        byte[] data = Encoding.UTF8.GetBytes(text);
        byte[] output = new byte[data.Length + PasteMarkerAllowance];
        nuint written;
        int status;
        fixed (byte* dataPointer = data)
        fixed (byte* outputPointer = output)
        {
            status = _native.PasteEncode(
                (IntPtr)dataPointer, (nuint)data.Length, BracketedPaste, (IntPtr)outputPointer, (nuint)output.Length, out written);
        }

        ThrowIfFailed(status, "encode paste");
        return output[..(int)written];
    }

    /// <inheritdoc />
    public void ScrollViewport(int rows)
    {
        ThrowIfDisposed();
        _cachedSnapshot = null;
        _native.ScrollViewport(
            _handle,
            new GhosttyNativeLibrary.TerminalScrollViewport
            {
                Tag = ScrollViewportDelta,
                Value = new GhosttyNativeLibrary.TerminalScrollViewportValue { Delta = (IntPtr)rows },
            });
    }

    /// <inheritdoc />
    public void ScrollViewportToBottom()
    {
        ThrowIfDisposed();
        _cachedSnapshot = null;
        _native.ScrollViewport(_handle, new GhosttyNativeLibrary.TerminalScrollViewport { Tag = ScrollViewportBottom });
    }

    /// <inheritdoc />
    public TerminalSnapshot CreateSnapshot()
    {
        ThrowIfDisposed();
        return _cachedSnapshot ??= BuildSnapshot();
    }

    private TerminalSnapshot BuildSnapshot()
    {
        ThrowIfFailed(_native.RenderStateUpdate(_renderState, _handle), "update render state");

        ThrowIfFailed(_native.RenderStateGetU16(_renderState, GhosttyNativeLibrary.RenderStateDataCols, out ushort columns), "read columns");
        ThrowIfFailed(_native.RenderStateGetU16(_renderState, GhosttyNativeLibrary.RenderStateDataRows, out ushort rows), "read rows");
        TerminalColor defaultForeground = ReadStateColor(GhosttyNativeLibrary.RenderStateDataColorForeground);
        TerminalColor defaultBackground = ReadStateColor(GhosttyNativeLibrary.RenderStateDataColorBackground);

        var cells = new TerminalCell[columns * rows];
        var palette = new GhosttyNativeLibrary.NativeColor[GhosttyNativeLibrary.RenderStatePaletteLength];
        GhosttyNativeLibrary.NativeColor[]? paletteOrNull = _native.RenderStateColorsGet(_renderState, palette) == GhosttyNativeLibrary.Success ? palette : null;
        nint iterator = _rowIterator;
        ThrowIfFailed(
            _native.RenderStateGetHandle(_renderState, GhosttyNativeLibrary.RenderStateDataRowIterator, ref iterator),
            "begin row iteration");

        bool[] wrapped = new bool[rows];
        int row = 0;
        while (row < rows && _native.RowIteratorNext(iterator))
        {
            wrapped[row] = IsWrapContinuation(iterator);
            nint rowCells = _rowCells;
            ThrowIfFailed(_native.RowGetCells(iterator, ref rowCells), "read row cells");
            for (int column = 0; column < columns; column++)
            {
                cells[(row * columns) + column] = ReadCell(rowCells, column, defaultForeground, defaultBackground, paletteOrNull);
            }

            row++;
        }

        // Rows the iterator did not produce stay blank rather than null-ish default structs.
        for (int index = row * columns; index < cells.Length; index++)
        {
            cells[index] = new TerminalCell(string.Empty, defaultForeground, defaultBackground);
        }

        return new TerminalSnapshot(columns, rows, cells, ReadCursor(), defaultForeground, defaultBackground, ReadScrollbar(), wrapped);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        nint handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            return;
        }

        FreeAndClear(ref _mouseEvent, _native.MouseEventFree);
        FreeAndClear(ref _mouseEncoder, _native.MouseEncoderFree);
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
        _cachedSnapshot = null;

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
        foreach ((int index, TerminalColor color) in scheme.Entries)
        {
            palette[index] = new GhosttyNativeLibrary.NativeColor { Red = color.Red, Green = color.Green, Blue = color.Blue };
        }

        fixed (GhosttyNativeLibrary.NativeColor* pointer = palette)
        {
            ThrowIfFailed(_native.TerminalSet(_handle, TerminalOptionColorPalette, (IntPtr)pointer), "set the palette");
        }
    }

    // True when the row carries on a line that was too long for the grid and so wrapped.
    private bool IsWrapContinuation(IntPtr iterator) =>
        _native.RowGetRaw(iterator, out ulong raw) == GhosttyNativeLibrary.Success
        && _native.RowGetWrapContinuation(raw, out byte value) == GhosttyNativeLibrary.Success
        && value != 0;

    private ScrollbarState ReadScrollbar() =>
        _native.TerminalGetScrollbar(_handle, out GhosttyNativeLibrary.NativeScrollbar bar) == GhosttyNativeLibrary.Success
            ? new ScrollbarState((long)bar.Total, (long)bar.Offset, (long)bar.Length)
            : ScrollbarState.None;

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
            TerminalResponseHandler? handler = ResponseHandler;
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
        ThrowIfFailed(_native.RenderStateGetColor(_renderState, data, out GhosttyNativeLibrary.NativeColor color), "read colour");
        return ToColor(color);
    }

    private CursorState ReadCursor()
    {
        _native.RenderStateGetByte(_renderState, GhosttyNativeLibrary.RenderStateDataCursorVisible, out byte visible);
        _native.RenderStateGetByte(_renderState, GhosttyNativeLibrary.RenderStateDataCursorViewportHasValue, out byte inViewport);
        _native.RenderStateGetInt(_renderState, GhosttyNativeLibrary.RenderStateDataCursorVisualStyle, out int style);
        ushort x = 0;
        ushort y = 0;
        if (inViewport != 0)
        {
            _native.RenderStateGetU16(_renderState, GhosttyNativeLibrary.RenderStateDataCursorViewportX, out x);
            _native.RenderStateGetU16(_renderState, GhosttyNativeLibrary.RenderStateDataCursorViewportY, out y);
        }

        CursorStyle shape = Enum.IsDefined((CursorStyle)style) ? (CursorStyle)style : CursorStyle.Block;
        TerminalColor? color = _native.RenderStateGetColor(_renderState, RenderStateDataColorCursor, out GhosttyNativeLibrary.NativeColor native) == GhosttyNativeLibrary.Success
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

        string text = string.Empty;
        if (_native.RowCellsGetU32(rowCells, GhosttyNativeLibrary.RenderStateRowCellsDataGraphemesLength, out uint length) == GhosttyNativeLibrary.Success
            && length > 0)
        {
            uint[] codepoints = new uint[length];
            fixed (uint* buffer = codepoints)
            {
                if (_native.RowCellsGetBuffer(rowCells, GhosttyNativeLibrary.RenderStateRowCellsDataGraphemesBuffer, (IntPtr)buffer) == GhosttyNativeLibrary.Success)
                {
                    text = ToText(codepoints);
                }
            }
        }

        TerminalColor foreground = _native.RowCellsGetColor(rowCells, GhosttyNativeLibrary.RenderStateRowCellsDataForegroundColor, out GhosttyNativeLibrary.NativeColor fg) == GhosttyNativeLibrary.Success
            ? ToColor(fg)
            : defaultForeground;
        TerminalColor background = _native.RowCellsGetColor(rowCells, GhosttyNativeLibrary.RenderStateRowCellsDataBackgroundColor, out GhosttyNativeLibrary.NativeColor bg) == GhosttyNativeLibrary.Success
            ? ToColor(bg)
            : defaultBackground;

        CellAttributes attributes = CellAttributes.None;
        UnderlineStyle underline = UnderlineStyle.None;
        TerminalColor? underlineColor = null;
        if (_native.RowCellsGetStyle(rowCells, out GhosttyNativeLibrary.NativeStyle style) == GhosttyNativeLibrary.Success)
        {
            attributes = ToAttributes(style);
            underline = Enum.IsDefined((UnderlineStyle)style.Underline) ? (UnderlineStyle)style.Underline : UnderlineStyle.Single;
            underlineColor = ReadUnderlineColor(style, palette);
        }

        int width = 1;
        if (_native.RowCellsGetRaw(rowCells, out ulong raw) == GhosttyNativeLibrary.Success
            && _native.CellGetWide(raw, out int wide) == GhosttyNativeLibrary.Success)
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
        GhosttyNativeLibrary.NativeStyleColor color = style.UnderlineColor;
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
        foreach (uint codepoint in codepoints)
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
        CellAttributes attributes = CellAttributes.None;
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
