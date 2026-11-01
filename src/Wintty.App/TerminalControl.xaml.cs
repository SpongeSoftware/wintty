using System.Text;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;
using Windows.UI;
using Wintty.Core.Abstractions;
using Wintty.Core.Configuration;
using Wintty.Core.Input;
using Wintty.Core.Keybindings;
using Wintty.Core.Selection;
using Wintty.Core.Sessions;
using Wintty.Ghostty;
using Wintty.Input;
using Wintty.Render;

namespace Wintty.App;

/// <summary>
/// Draws a <see cref="TerminalSession"/> with Win2D. Kept thin: layout and planning live in
/// <see cref="Wintty.Render"/>, process and terminal state in Core.
/// </summary>
public sealed partial class TerminalControl : UserControl
{
    private WinttyConfig _config = new();
    private IReadOnlyDictionary<KeyTrigger, TerminalAction> _bindings = new ConfigLoadResult(new WinttyConfig(), []).Bindings;
    private ConfigWatcher? _configWatcher;
    private string? _configProblems;

    private readonly GhosttyGateway _gateway = new();
    private readonly Dictionary<(FontRole Role, bool Bold, bool Italic), CanvasTextFormat> _formats = [];
    private string? _symbolFont;
    private bool _decorationErrorLogged;
    private bool _snapshotErrorLogged;
    private (int Width, int Height) _cellPixels;
    private readonly DispatcherQueueTimer _blinkTimer;
    private TerminalSession? _session;
    private float _cellWidth;
    private float _cellHeight;
    private (int Columns, int Rows) _grid = (80, 24);
    private bool _cursorOn = true;
    private int _invalidatePending;
    private char _pendingHighSurrogate;
    private string? _startupError;
    private MouseButton _heldButton;
    private bool _started;
    private ResizeDebouncer? _resizer;
    private readonly SelectionController _selection = new();
    private int _clickCount;
    private long _lastClickTick;
    private (int Column, int Row) _lastClickCell = (-1, -1);
    private (int Column, int Row) _lastDragCell = (-1, -1);

    private const int MultiClickMilliseconds = 500;
    private double _wheelRemainder;

    private const int WheelRowsPerNotch = 3;
    private const double WheelNotch = 120.0;
    private const int ScrollJump = 1_000_000;
    private const int ResizeDelayMilliseconds = 40;

    /// <summary>Creates the control.</summary>
    public TerminalControl()
    {
        InitializeComponent();
        _blinkTimer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
        _blinkTimer.Interval = TimeSpan.FromMilliseconds(530);
        _blinkTimer.Tick += (_, _) =>
        {
            _cursorOn = !_cursorOn;
            Canvas.Invalidate();
        };

        Canvas.CreateResources += (_, _) => MeasureCell();
        Canvas.SizeChanged += OnCanvasSizeChanged;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        CharacterReceived += OnCharacterReceived;
        Canvas.PointerPressed += OnPointerPressed;
        Canvas.PointerMoved += OnPointerMoved;
        Canvas.PointerReleased += OnPointerReleased;
        Canvas.PointerCaptureLost += (_, _) => _heldButton = MouseButton.None;
        Canvas.PointerWheelChanged += OnPointerWheelChanged;
        GotFocus += (_, _) => PaneActivated?.Invoke(this, EventArgs.Empty);

        // The window moves terminals between containers (tabs, splits), which raises Loaded again, so start only once.
        Loaded += async (_, _) =>
        {
            if (!_started)
            {
                _started = true;
                await StartAsync();
            }
        };
    }

    /// <summary>Raised on the UI thread when this terminal receives keyboard focus.</summary>
    public event EventHandler? PaneActivated;

    /// <summary>Raised when a key binding asks for something the window carries out, such as opening a tab.</summary>
    public event EventHandler<TerminalAction>? ActionRequested;

    /// <summary>Gives this terminal keyboard focus.</summary>
    public void FocusTerminal() => Focus(FocusState.Programmatic);

    /// <summary>Ends the shell and releases the terminal; the control cannot be reused afterwards.</summary>
    public void Close() => Shutdown();

    /// <summary>Raised on the UI thread when the shell process exits.</summary>
    public event EventHandler? ProcessExited;

    /// <summary>Raised on the UI thread when the terminal title changes.</summary>
    public event EventHandler<string>? TitleChanged;

    private async Task StartAsync()
    {
        try
        {
            await StartSessionAsync();
        }
        catch (Exception ex)
        {
            _startupError = ex.ToString();
            LogError(_startupError);
            Canvas.Invalidate();
        }
    }

    private static void LogError(string message)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "wintty");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "wintty.log"), $"{DateTime.Now:O} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Logging is best effort.
        }
    }

    private async Task StartSessionAsync()
    {
        var loaded = ConfigLoader.Load(ConfigLoader.DefaultPath, ThemeCatalog.DefaultUserDirectory);
        _config = loaded.Config;
        _bindings = loaded.Bindings;
        ReportConfigProblems(loaded);
        MeasureCell();
        _grid = ComputeGrid();
        var command = _config.Command is { } text ? CommandLine.Split(text) : null;
        var options = new PtyStartOptions(command?.Application ?? FindShell(), _grid.Columns, _grid.Rows)
        {
            Arguments = command?.Arguments ?? [],
            Environment = new Dictionary<string, string>
            {
                ["TERM"] = "xterm-256color",
                ["COLORTERM"] = "truecolor",
            },
        };

        _session = await TerminalSession.StartAsync(_gateway, _gateway, options, _config.ScrollbackLimit);
        ApplyCellPixelSize();
        var dispatcher = DispatcherQueue;

        // Dragging a window edge produces many sizes; apply only the one the drag settles on.
        var session = _session;
        _resizer = new ResizeDebouncer(
            TimeSpan.FromMilliseconds(ResizeDelayMilliseconds),
            (columns, rows) =>
            {
                session.Resize(columns, rows);
                dispatcher.TryEnqueue(() => Canvas.Invalidate());
            },
            ex => LogError("Resize failed: " + ex));
        _configWatcher = new ConfigWatcher(
            ConfigLoader.DefaultPath,
            TimeSpan.FromMilliseconds(250),
            result => dispatcher.TryEnqueue(() => ApplyConfig(result)),
            ThemeCatalog.DefaultUserDirectory);
        _session.SetColors(loaded.Colors);
        _session.Updated += (_, _) =>
        {
            if (Interlocked.Exchange(ref _invalidatePending, 1) == 0)
            {
                dispatcher.TryEnqueue(() =>
                {
                    Volatile.Write(ref _invalidatePending, 0);
                    PublishTitle();
                    Canvas.Invalidate();
                });
            }
        };
        _session.Exited += (_, _) => dispatcher.TryEnqueue(() => ProcessExited?.Invoke(this, EventArgs.Empty));
        if (_config.CursorBlink)
        {
            _blinkTimer.Start();
        }

        Focus(FocusState.Programmatic);
    }

    // Applies a reloaded config to the running terminal: font, padding and blink change live;
    // the shell command and scrollback only affect terminals started afterwards.
    private void ApplyConfig(ConfigLoadResult result)
    {
        _config = result.Config;
        _bindings = result.Bindings;
        _session?.SetColors(result.Colors);
        ReportConfigProblems(result);
        _formats.Clear();
        MeasureCell();
        var grid = ComputeGrid();
        if (grid != _grid)
        {
            _grid = grid;
            _resizer?.ApplyNow(grid.Columns, grid.Rows);
        }

        ApplyCellPixelSize();
        _cursorOn = true;
        if (_config.CursorBlink)
        {
            _blinkTimer.Start();
        }
        else
        {
            _blinkTimer.Stop();
        }

        Canvas.Invalidate();
    }

    private void ReportConfigProblems(ConfigLoadResult result)
    {
        if (result.Diagnostics.Count == 0)
        {
            _configProblems = null;
            return;
        }

        var lines = result.Diagnostics.Select(d => d.ToString()).ToList();
        _configProblems = $"config: {string.Join("\n", lines)}";
        LogError(_configProblems);
    }

    private void Shutdown()
    {
        _configWatcher?.Dispose();
        _configWatcher = null;
        _resizer?.Dispose();
        _resizer = null;
        _blinkTimer.Stop();
        _session?.Dispose();
        _session = null;
    }

    private static string FindShell()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(directory, "pwsh.exe")))
                {
                    return "pwsh.exe";
                }
            }
            catch (ArgumentException)
            {
                // Ignore malformed PATH entries.
            }
        }

        return "powershell.exe";
    }

    private void MeasureCell()
    {
        _symbolFont ??= SymbolFontResolver.Resolve(CanvasTextFormat.GetSystemFontFamilies());
        using var layout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), "M", FormatFor(FontRole.Primary, false, false), 0, 0);
        _cellWidth = (float)Math.Ceiling(layout.LayoutBounds.Width);
        _cellHeight = (float)Math.Ceiling(layout.LayoutBounds.Height);
    }

    private (int Columns, int Rows) ComputeGrid() =>
        _cellWidth <= 0 || _cellHeight <= 0
            ? _grid
            : GridSizer.Compute(Canvas.ActualWidth, Canvas.ActualHeight, _cellWidth, _cellHeight, _config.WindowPadding);

    // Terminal size queries are answered in physical pixels, so convert from DIPs with the current display scale.
    private void ApplyCellPixelSize()
    {
        if (_session is null || _cellWidth <= 0 || _cellHeight <= 0)
        {
            return;
        }

        var scale = XamlRoot?.RasterizationScale ?? 1.0;
        var pixels = ((int)Math.Round(_cellWidth * scale), (int)Math.Round(_cellHeight * scale));
        if (pixels == _cellPixels || pixels.Item1 <= 0 || pixels.Item2 <= 0)
        {
            return;
        }

        _cellPixels = pixels;
        _session.SetCellSize(pixels.Item1, pixels.Item2);
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var grid = ComputeGrid();
        if (grid == _grid)
        {
            return;
        }

        _grid = grid;
        _resizer?.Request(grid.Columns, grid.Rows);
        ApplyCellPixelSize();
        Canvas.Invalidate();
    }

    private void PublishTitle()
    {
        if (_session is null)
        {
            return;
        }

        string? title;
        lock (_session.SyncRoot)
        {
            title = _session.Engine.Title;
        }

        if (!string.IsNullOrEmpty(title))
        {
            TitleChanged?.Invoke(this, title);
        }
    }

    private CanvasTextFormat FormatFor(FontRole role, bool bold, bool italic)
    {
        if (!_formats.TryGetValue((role, bold, italic), out var format))
        {
            format = new CanvasTextFormat
            {
                FontFamily = role == FontRole.Symbols && _symbolFont is not null ? _symbolFont : _config.FontFamily,
                FontSize = _config.FontSize,
                FontWeight = bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
                FontStyle = italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
                WordWrapping = CanvasWordWrapping.NoWrap,
            };
            _formats[(role, bold, italic)] = format;
        }

        return format;
    }

    private static Color ToColor(TerminalColor color, byte alpha = 255) => Color.FromArgb(alpha, color.Red, color.Green, color.Blue);

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (_startupError is not null)
        {
            args.DrawingSession.Clear(Colors.Black);
            args.DrawingSession.DrawText(_startupError, 8, 8, Colors.Tomato, FormatFor(FontRole.Primary, false, false));
            return;
        }

        var session = _session;
        if (session is null || _cellWidth <= 0)
        {
            return;
        }

        TerminalSnapshot snapshot;
        try
        {
            lock (session.SyncRoot)
            {
                snapshot = session.Engine.CreateSnapshot();
            }
        }
        catch (Exception ex)
        {
            if (!_snapshotErrorLogged)
            {
                _snapshotErrorLogged = true;
                LogError("Snapshot failed: " + ex);
            }

            return;
        }

        var plan = RenderPlanner.Build(snapshot, _cursorOn || FocusState == FocusState.Unfocused, _selection.Current);
        var drawing = args.DrawingSession;
        var origin = (float)_config.WindowPadding;
        drawing.Clear(ToColor(plan.Background));

        foreach (var run in plan.Backgrounds)
        {
            drawing.FillRectangle(
                origin + (run.Column * _cellWidth),
                origin + (run.Row * _cellHeight),
                run.Length * _cellWidth,
                _cellHeight,
                ToColor(run.Color));
        }

        // A translucent wash in the text colour keeps colours readable while marking the selection.
        foreach (var run in plan.Selection ?? [])
        {
            drawing.FillRectangle(
                origin + (run.Column * _cellWidth),
                origin + (run.Row * _cellHeight),
                run.Length * _cellWidth,
                _cellHeight,
                ToColor(snapshot.DefaultForeground, 90));
        }

        foreach (var run in plan.Texts)
        {
            drawing.DrawText(
                run.Text,
                origin + (run.Column * _cellWidth),
                origin + (run.Row * _cellHeight),
                ToColor(run.Color, run.Faint ? (byte)150 : (byte)255),
                FormatFor(run.Role, run.Bold, run.Italic));
        }

        foreach (var run in plan.Decorations)
        {
            try
            {
                DrawDecoration(drawing, run, origin);
            }
            catch (Exception ex)
            {
                // A failing decoration must not blank the whole frame; log it once and carry on.
                if (!_decorationErrorLogged)
                {
                    _decorationErrorLogged = true;
                    LogError("Decoration drawing failed: " + ex);
                }
            }
        }

        if (plan.Cursor is { } cursor)
        {
            DrawCursor(drawing, cursor, origin);
        }

        if (plan.ScrollThumb is { } thumb)
        {
            DrawScrollThumb(drawing, thumb);
        }

        if (_configProblems is not null)
        {
            DrawConfigProblems(drawing);
        }
    }

    // A slim translucent bar on the right edge while the view is scrolled back.
    private void DrawScrollThumb(CanvasDrawingSession drawing, ScrollThumb thumb)
    {
        const float width = 6f;
        const float margin = 2f;
        var height = (float)Canvas.ActualHeight;
        drawing.FillRoundedRectangle(
            (float)Canvas.ActualWidth - width - margin,
            (float)(thumb.Top * height),
            width,
            (float)(thumb.Height * height),
            width / 2,
            width / 2,
            Color.FromArgb(150, 160, 160, 160));
    }

    // A banner along the bottom so a mistyped config line is noticed without opening the log.
    private void DrawConfigProblems(CanvasDrawingSession drawing)
    {
        var format = FormatFor(FontRole.Primary, false, false);
        var height = (float)Canvas.ActualHeight;
        var lines = _configProblems!.Split('\n').Length;
        var bannerHeight = (lines * _cellHeight) + 8;
        drawing.FillRectangle(0, height - bannerHeight, (float)Canvas.ActualWidth, bannerHeight, Color.FromArgb(230, 60, 0, 0));
        drawing.DrawText(_configProblems, 8, height - bannerHeight + 4, Colors.Tomato, format);
    }

    private void DrawDecoration(CanvasDrawingSession drawing, DecorationRun run, float origin)
    {
        var x1 = origin + (run.Column * _cellWidth);
        var x2 = x1 + (run.Length * _cellWidth);
        var top = origin + (run.Row * _cellHeight);
        var color = ToColor(run.Color);
        var dotted = run.Kind == DecorationKind.DottedUnderline;
        var dashed = run.Kind == DecorationKind.DashedUnderline || run.Kind == DecorationKind.CurlyUnderline;
        using var style = new CanvasStrokeStyle
        {
            DashStyle = dotted ? CanvasDashStyle.Dot : dashed ? CanvasDashStyle.Dash : CanvasDashStyle.Solid,
        };

        switch (run.Kind)
        {
            case DecorationKind.Strikethrough:
                drawing.DrawLine(x1, top + (_cellHeight / 2), x2, top + (_cellHeight / 2), color);
                break;
            case DecorationKind.Overline:
                drawing.DrawLine(x1, top + 0.5f, x2, top + 0.5f, color);
                break;
            case DecorationKind.CurlyUnderline:
                DrawCurlyUnderline(drawing, x1, x2, top + _cellHeight - 2.5f, color);
                break;
            case DecorationKind.DoubleUnderline:
                drawing.DrawLine(x1, top + _cellHeight - 3.5f, x2, top + _cellHeight - 3.5f, color);
                drawing.DrawLine(x1, top + _cellHeight - 1.5f, x2, top + _cellHeight - 1.5f, color);
                break;
            default:
                drawing.DrawLine(x1, top + _cellHeight - 1.5f, x2, top + _cellHeight - 1.5f, color, 1f, style);
                break;
        }
    }

    // A zig-zag wave, one period per cell, so curly underlines read differently from straight and dashed ones.
    private void DrawCurlyUnderline(CanvasDrawingSession drawing, float x1, float x2, float y, Color color)
    {
        const float amplitude = 1.5f;
        var half = Math.Max(2f, _cellWidth / 2f);
        using var path = new CanvasPathBuilder(drawing);
        path.BeginFigure(x1, y);
        var up = false;
        for (var x = x1 + half; x < x2 + half; x += half)
        {
            path.AddLine(Math.Min(x, x2), up ? y : y - amplitude);
            up = !up;
        }

        path.EndFigure(CanvasFigureLoop.Open);
        using var geometry = CanvasGeometry.CreatePath(path);
        drawing.DrawGeometry(geometry, color, 1f);
    }

    private void DrawCursor(CanvasDrawingSession drawing, CursorShape cursor, float origin)
    {
        var x = origin + (cursor.Column * _cellWidth);
        var y = origin + (cursor.Row * _cellHeight);
        var width = cursor.CellCount * _cellWidth;
        var color = ToColor(cursor.Color, 190);
        switch (cursor.Style)
        {
            case CursorStyle.Bar:
                drawing.FillRectangle(x, y, 2, _cellHeight, color);
                break;
            case CursorStyle.Underline:
                drawing.FillRectangle(x, y + _cellHeight - 2, width, 2, color);
                break;
            case CursorStyle.BlockHollow:
                drawing.DrawRectangle(x + 0.5f, y + 0.5f, width - 1, _cellHeight - 1, color);
                break;
            default:
                drawing.FillRectangle(x, y, width, _cellHeight, color);
                break;
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e) =>
        HandleKey(e, e.KeyStatus.WasKeyDown ? KeyAction.Repeat : KeyAction.Press);

    private void OnKeyUp(object sender, KeyRoutedEventArgs e) => HandleKey(e, KeyAction.Release);

    private void HandleKey(KeyRoutedEventArgs e, KeyAction action)
    {
        var virtualKey = (int)e.Key;

        // While an IME is composing, the text arrives through CharacterReceived instead.
        if (_session is null || virtualKey == VirtualKeyMap.ProcessKey)
        {
            return;
        }

        var key = VirtualKeyMap.Map(virtualKey, e.KeyStatus.IsExtendedKey);
        if (key == TerminalKey.Unidentified)
        {
            return;
        }

        var modifiers = NativeKeyboard.CurrentModifiers();
        if (TryBoundAction(key, modifiers, action))
        {
            e.Handled = true;
            return;
        }

        var text = NativeKeyboard.TranslateText(virtualKey, e.KeyStatus.ScanCode, modifiers);
        if (text is null)
        {
            // Dead key (for example an accent): let the composed character arrive via CharacterReceived.
            return;
        }

        var consumed = KeyModifiers.None;
        if (text.Length > 0 && text[0] >= ' ' && text[0] != '')
        {
            // AltGr is reported as Ctrl+Alt; the layout already folded it into the text.
            if (modifiers.HasFlag(KeyModifiers.Control) && modifiers.HasFlag(KeyModifiers.Alt))
            {
                modifiers &= ~(KeyModifiers.Control | KeyModifiers.Alt);
            }

            if (modifiers.HasFlag(KeyModifiers.Shift))
            {
                consumed = KeyModifiers.Shift;
            }
        }
        else
        {
            // Control characters (Ctrl+C and friends) are derived by the encoder from key and modifiers.
            text = null;
        }

        var unshifted = NativeKeyboard.TranslateText(virtualKey, e.KeyStatus.ScanCode, KeyModifiers.None);
        var codepoint = string.IsNullOrEmpty(unshifted) || unshifted[0] < ' ' ? 0 : char.ConvertToUtf32(unshifted, 0);

        if (_session.SendKey(new KeyInput(key, modifiers, action, text, codepoint, consumed)) && action != KeyAction.Release)
        {
            ClearSelection();
        }

        _cursorOn = true;

        // Handled so the same key does not also arrive through CharacterReceived.
        e.Handled = true;
    }

    // Reached only for text the key handler did not take: IME composition, dead-key results and unmapped keys.
    private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        var character = e.Character;
        if (character < ' ' || character == '')
        {
            return;
        }

        if (char.IsHighSurrogate(character))
        {
            _pendingHighSurrogate = character;
            e.Handled = true;
            return;
        }

        var text = char.IsLowSurrogate(character) && _pendingHighSurrogate != ' '
            ? new string([_pendingHighSurrogate, character])
            : character.ToString();
        _pendingHighSurrogate = ' ';
        Send(text);
        e.Handled = true;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Focus(FocusState.Pointer);
        var button = ButtonOf(e.GetCurrentPoint(Canvas).Properties);
        if (button != MouseButton.None && !TryReport(MouseAction.Press, button, e))
        {
            if (button == MouseButton.Left)
            {
                BeginSelection(e);
                e.Handled = true;
            }
            else if (button == MouseButton.Middle)
            {
                _ = PasteAsync();
                e.Handled = true;
            }

            return;
        }

        if (button == MouseButton.None)
        {
            return;
        }

        _heldButton = button;
        Canvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_selection.IsDragging)
        {
            DragSelection(e);
            e.Handled = true;
            return;
        }

        if (TryReport(MouseAction.Motion, _heldButton, e))
        {
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_selection.IsDragging)
        {
            _selection.End();
            Canvas.ReleasePointerCapture(e.Pointer);
            Canvas.Invalidate();
            e.Handled = true;
            return;
        }

        var button = _heldButton;
        _heldButton = MouseButton.None;
        Canvas.ReleasePointerCapture(e.Pointer);
        if (button != MouseButton.None && TryReport(MouseAction.Release, button, e))
        {
            e.Handled = true;
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_session is not { } session)
        {
            return;
        }

        var properties = e.GetCurrentPoint(Canvas).Properties;
        var modifiers = NativeKeyboard.CurrentModifiers();
        _wheelRemainder += properties.MouseWheelDelta / WheelNotch;
        var notches = (int)_wheelRemainder;
        if (notches == 0)
        {
            return;
        }

        _wheelRemainder -= notches;
        var reporting = session.MouseTracking && !modifiers.HasFlag(KeyModifiers.Shift);
        if (reporting)
        {
            var button = properties.IsHorizontalMouseWheel
                ? (notches > 0 ? MouseButton.WheelRight : MouseButton.WheelLeft)
                : (notches > 0 ? MouseButton.WheelUp : MouseButton.WheelDown);
            for (var step = 0; step < Math.Abs(notches); step++)
            {
                TryReport(MouseAction.Press, button, e);
            }
        }
        else if (!properties.IsHorizontalMouseWheel)
        {
            // Wheel up (positive) moves towards older output, which is a negative row delta.
            session.ScrollBy(-notches * WheelRowsPerNotch);
            Canvas.Invalidate();
        }

        e.Handled = true;
    }

    private static MouseButton ButtonOf(PointerPointProperties properties) =>
        properties.IsLeftButtonPressed ? MouseButton.Left
        : properties.IsRightButtonPressed ? MouseButton.Right
        : properties.IsMiddleButtonPressed ? MouseButton.Middle
        : MouseButton.None;

    // Sends a mouse report when the program asked for them. Holding Shift bypasses reporting.
    private bool TryReport(MouseAction action, MouseButton button, PointerRoutedEventArgs e)
    {
        var modifiers = NativeKeyboard.CurrentModifiers();
        if (_session is not { } session || _cellPixels.Width <= 0 || modifiers.HasFlag(KeyModifiers.Shift))
        {
            return false;
        }

        // Positions are sent in physical pixels relative to the grid, matching the cell size the engine was given.
        var point = e.GetCurrentPoint(Canvas).Position;
        var x = (point.X - _config.WindowPadding) / _cellWidth * _cellPixels.Width;
        var y = (point.Y - _config.WindowPadding) / _cellHeight * _cellPixels.Height;
        x = Math.Clamp(x, 0, (_grid.Columns * _cellPixels.Width) - 1);
        y = Math.Clamp(y, 0, (_grid.Rows * _cellPixels.Height) - 1);
        return session.SendMouse(new MouseInput(action, button, x, y, modifiers));
    }

    private void Send(string text)
    {
        _session?.SendInput(Encoding.UTF8.GetBytes(text));
        ClearSelection();
        _cursorOn = true;
    }

    private void ClearSelection()
    {
        if (_selection.Current is not null)
        {
            _selection.Clear();
            Canvas.Invalidate();
        }
    }

    // Looks the key up in the bindings from the config; actions that are not built yet pass the key on to the program.
    private bool TryBoundAction(TerminalKey key, KeyModifiers modifiers, KeyAction action)
    {
        if (!_bindings.TryGetValue(KeyTrigger.From(key, modifiers), out var bound)
            || !(IsImplemented(bound) || (ActionRequested is not null && bound != TerminalAction.Unbind)))
        {
            return false;
        }

        if (action != KeyAction.Release)
        {
            Execute(bound);
        }

        return true;
    }

    private static bool IsImplemented(TerminalAction action) => action is
        TerminalAction.CopyToClipboard or TerminalAction.PasteFromClipboard or TerminalAction.ScrollPageUp
        or TerminalAction.ScrollPageDown or TerminalAction.ScrollToTop or TerminalAction.ScrollToBottom;

    private void Execute(TerminalAction action)
    {
        switch (action)
        {
            case TerminalAction.CopyToClipboard:
                CopySelection();
                break;
            case TerminalAction.PasteFromClipboard:
                _ = PasteAsync();
                break;
            case TerminalAction.ScrollPageUp:
                _session?.ScrollBy(-Math.Max(1, _grid.Rows - 1));
                break;
            case TerminalAction.ScrollPageDown:
                _session?.ScrollBy(Math.Max(1, _grid.Rows - 1));
                break;
            case TerminalAction.ScrollToTop:
                _session?.ScrollBy(-ScrollJump);
                break;
            case TerminalAction.ScrollToBottom:
                _session?.ScrollToBottom();
                break;
            default:
                ActionRequested?.Invoke(this, action);
                break;
        }

        Canvas.Invalidate();
    }

    private void CopySelection()
    {
        if (_session is not { } session || _selection.Current is not { IsEmpty: false } selection)
        {
            return;
        }

        var text = session.GetSelectedText(selection);
        if (text.Length == 0)
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    private async Task PasteAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (_session is not { } session || !content.Contains(StandardDataFormats.Text))
            {
                return;
            }

            var text = await content.GetTextAsync();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // Programs that did not ask for bracketed paste would run each pasted line as a command.
            if (_config.PasteProtection && !session.BracketedPaste && text.Contains('\n') && !await ConfirmPasteAsync(text))
            {
                return;
            }

            session.Paste(text);
            ClearSelection();
        }
        catch (Exception ex)
        {
            LogError("Paste failed: " + ex);
        }
    }

    private async Task<bool> ConfirmPasteAsync(string text)
    {
        var lines = text.Split('\n').Length;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Paste " + lines + " lines?",
            Content = "The text contains line breaks, so the program may run each line as soon as it is pasted.",
            PrimaryButtonText = "Paste",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void BeginSelection(PointerRoutedEventArgs e)
    {
        if (_session is not { } session || _cellWidth <= 0)
        {
            return;
        }

        var cell = CellUnder(e);
        var now = Environment.TickCount64;
        _clickCount = cell == _lastClickCell && now - _lastClickTick <= MultiClickMilliseconds ? _clickCount + 1 : 1;
        _lastClickCell = cell;
        _lastClickTick = now;
        _lastDragCell = cell;

        var block = NativeKeyboard.CurrentModifiers().HasFlag(KeyModifiers.Alt);
        lock (session.SyncRoot)
        {
            _selection.Begin(session.Engine.CreateSnapshot(), cell.Column, cell.Row, _clickCount, block);
        }

        Canvas.CapturePointer(e.Pointer);
        Canvas.Invalidate();
    }

    private void DragSelection(PointerRoutedEventArgs e)
    {
        if (_session is not { } session)
        {
            return;
        }

        // Dragging past the top or bottom edge scrolls the view so the selection can grow into history.
        var y = e.GetCurrentPoint(Canvas).Position.Y;
        if (y < _config.WindowPadding)
        {
            session.ScrollBy(-1);
        }
        else if (y > Canvas.ActualHeight - _config.WindowPadding)
        {
            session.ScrollBy(1);
        }

        var cell = CellUnder(e);
        if (cell == _lastDragCell && y >= _config.WindowPadding && y <= Canvas.ActualHeight - _config.WindowPadding)
        {
            return;
        }

        _lastDragCell = cell;
        lock (session.SyncRoot)
        {
            _selection.Drag(session.Engine.CreateSnapshot(), cell.Column, cell.Row);
        }

        Canvas.Invalidate();
    }

    private (int Column, int Row) CellUnder(PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Canvas).Position;
        return CellHitTester.CellAt(point.X, point.Y, _cellWidth, _cellHeight, _config.WindowPadding, _grid.Columns, _grid.Rows);
    }
}
