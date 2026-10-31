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
using Windows.System;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;
using Windows.UI;
using Wintty.Core.Abstractions;
using Wintty.Core.Configuration;
using Wintty.Core.Input;
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
    private ConfigWatcher? _configWatcher;
    private string? _configProblems;

    private readonly GhosttyGateway _gateway = new();
    private readonly Dictionary<(FontRole Role, bool Bold, bool Italic), CanvasTextFormat> _formats = [];
    private string? _symbolFont;
    private readonly DispatcherQueueTimer _blinkTimer;
    private TerminalSession? _session;
    private float _cellWidth;
    private float _cellHeight;
    private (int Columns, int Rows) _grid = (80, 24);
    private bool _cursorOn = true;
    private int _invalidatePending;
    private char _pendingHighSurrogate;
    private string? _startupError;

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
        Loaded += async (_, _) => await StartAsync();
        Unloaded += (_, _) => Shutdown();
    }

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
        var loaded = ConfigLoader.Load(ConfigLoader.DefaultPath);
        _config = loaded.Config;
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
        var dispatcher = DispatcherQueue;
        _configWatcher = new ConfigWatcher(
            ConfigLoader.DefaultPath,
            TimeSpan.FromMilliseconds(250),
            result => dispatcher.TryEnqueue(() => ApplyConfig(result)));
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
        ReportConfigProblems(result);
        _formats.Clear();
        MeasureCell();
        var grid = ComputeGrid();
        if (grid != _grid)
        {
            _grid = grid;
            _session?.Resize(grid.Columns, grid.Rows);
        }

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

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var grid = ComputeGrid();
        if (grid == _grid)
        {
            return;
        }

        _grid = grid;
        _session?.Resize(grid.Columns, grid.Rows);
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
        lock (session.SyncRoot)
        {
            snapshot = session.Engine.CreateSnapshot();
        }

        var plan = RenderPlanner.Build(snapshot, _cursorOn || FocusState == FocusState.Unfocused);
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
            DrawDecoration(drawing, run, origin);
        }

        if (plan.Cursor is { } cursor)
        {
            DrawCursor(drawing, cursor, origin);
        }

        if (_configProblems is not null)
        {
            DrawConfigProblems(drawing);
        }
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
            case DecorationKind.DoubleUnderline:
                drawing.DrawLine(x1, top + _cellHeight - 3.5f, x2, top + _cellHeight - 3.5f, color);
                drawing.DrawLine(x1, top + _cellHeight - 1.5f, x2, top + _cellHeight - 1.5f, color);
                break;
            default:
                drawing.DrawLine(x1, top + _cellHeight - 1.5f, x2, top + _cellHeight - 1.5f, color, 1f, style);
                break;
        }
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

        _session.SendKey(new KeyInput(key, modifiers, action, text, codepoint, consumed));
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

    private void Send(string text)
    {
        _session?.SendInput(Encoding.UTF8.GetBytes(text));
        _cursorOn = true;
    }
}
