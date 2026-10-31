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
using Wintty.Core.Sessions;
using Wintty.Ghostty;
using Wintty.Render;

namespace Wintty.App;

/// <summary>
/// Draws a <see cref="TerminalSession"/> with Win2D. Kept thin: layout and planning live in
/// <see cref="Wintty.Render"/>, process and terminal state in Core.
/// </summary>
public sealed partial class TerminalControl : UserControl
{
    private const string FontFamilyName = "Cascadia Mono";
    private const float TerminalFontSize = 15f;
    private const double SurfacePadding = 6;

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
        MeasureCell();
        _grid = ComputeGrid();
        var options = new PtyStartOptions(FindShell(), _grid.Columns, _grid.Rows)
        {
            Environment = new Dictionary<string, string>
            {
                ["TERM"] = "xterm-256color",
                ["COLORTERM"] = "truecolor",
            },
        };

        _session = await TerminalSession.StartAsync(_gateway, _gateway, options);
        var dispatcher = DispatcherQueue;
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
        _blinkTimer.Start();
        Focus(FocusState.Programmatic);
    }

    private void Shutdown()
    {
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
            : GridSizer.Compute(Canvas.ActualWidth, Canvas.ActualHeight, _cellWidth, _cellHeight, SurfacePadding);

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
                FontFamily = role == FontRole.Symbols && _symbolFont is not null ? _symbolFont : FontFamilyName,
                FontSize = TerminalFontSize,
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
        var origin = (float)SurfacePadding;
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

    // Placeholder input: special keys and text only. Task 09 replaces this with libghostty's key encoder.
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var sequence = e.Key switch
        {
            VirtualKey.Enter => "\r",
            VirtualKey.Back => "\x7f",
            VirtualKey.Tab => "\t",
            VirtualKey.Escape => "\x1b",
            VirtualKey.Up => "\x1b[A",
            VirtualKey.Down => "\x1b[B",
            VirtualKey.Right => "\x1b[C",
            VirtualKey.Left => "\x1b[D",
            VirtualKey.Home => "\x1b[H",
            VirtualKey.End => "\x1b[F",
            VirtualKey.Delete => "\x1b[3~",
            VirtualKey.PageUp => "\x1b[5~",
            VirtualKey.PageDown => "\x1b[6~",
            _ => null,
        };

        if (sequence is not null)
        {
            Send(sequence);
            e.Handled = true;
        }
    }

    private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        var character = e.Character;
        if (character is '\r' or '\t' or '\b' or '\x1b' or '\x7f')
        {
            return;
        }

        if (char.IsHighSurrogate(character))
        {
            _pendingHighSurrogate = character;
            e.Handled = true;
            return;
        }

        var text = char.IsLowSurrogate(character) && _pendingHighSurrogate != '\0'
            ? new string([_pendingHighSurrogate, character])
            : character.ToString();
        _pendingHighSurrogate = '\0';
        Send(text);
        e.Handled = true;
    }

    private void Send(string text)
    {
        _session?.SendInput(Encoding.UTF8.GetBytes(text));
        _cursorOn = true;
    }
}
