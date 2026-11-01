using System.Text;
using Wintty.Core.Abstractions;
using Wintty.Core.Input;

namespace Wintty.Tests.Fakes;

/// <summary>In-memory <see cref="ITerminalEngine"/> that records everything written to it.</summary>
internal sealed class FakeTerminalEngine : ITerminalEngine
{
    private readonly StringBuilder _text = new();

    public FakeTerminalEngine(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public TerminalResponseHandler? ResponseHandler { get; set; }

    /// <summary>Simulates the terminal producing a reply to a query.</summary>
    public void Respond(byte[] reply) => ResponseHandler?.Invoke(reply);

    public int Columns { get; private set; }

    public int Rows { get; private set; }

    public string? Title { get; set; }

    public bool IsDisposed { get; private set; }

    public string Text
    {
        get
        {
            lock (_text)
            {
                return _text.ToString();
            }
        }
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        lock (_text)
        {
            _text.Append(Encoding.UTF8.GetString(data));
        }
    }

    public (int Width, int Height)? CellSize { get; private set; }

    public void SetCellSize(int widthPixels, int heightPixels) => CellSize = (widthPixels, heightPixels);

    public TerminalColorScheme? Colors { get; private set; }

    public void SetColors(TerminalColorScheme scheme) => Colors = scheme;

    public void Resize(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public List<KeyInput> KeyInputs { get; } = [];

    public byte[] EncodedKey { get; set; } = [];

    public byte[] EncodeKey(KeyInput input)
    {
        KeyInputs.Add(input);
        return EncodedKey;
    }

    public List<MouseInput> MouseInputs { get; } = [];

    public byte[] EncodedMouse { get; set; } = [];

    public bool MouseTracking { get; set; }

    public byte[] EncodeMouse(MouseInput input)
    {
        MouseInputs.Add(input);
        return EncodedMouse;
    }

    public bool BracketedPaste { get; set; }

    public bool SynchronizedOutput { get; set; }

    public bool FocusEvents { get; set; }

    public bool ColorSchemeNotifications { get; set; }

    public List<string> Pastes { get; } = [];

    public byte[] EncodePaste(string text)
    {
        Pastes.Add(text);
        var encoded = Encoding.UTF8.GetBytes(text.Replace("\r\n", "\r", StringComparison.Ordinal).Replace('\n', '\r'));
        return BracketedPaste ? [.. "\u001b[200~"u8, .. encoded, .. "\u001b[201~"u8] : encoded;
    }

    public List<int> Scrolls { get; } = [];

    public int ScrolledToBottom { get; private set; }

    public ScrollbarState Scrollbar { get; set; }

    public void ScrollViewport(int rows) => Scrolls.Add(rows);

    public void ScrollViewportToBottom() => ScrolledToBottom++;

    public TerminalSnapshot CreateSnapshot()
    {
        var black = new TerminalColor(0, 0, 0);
        var white = new TerminalColor(255, 255, 255);
        var cells = new TerminalCell[Columns * Rows];
        Array.Fill(cells, new TerminalCell(string.Empty, white, black));
        return new TerminalSnapshot(Columns, Rows, cells, new CursorState(0, 0, true, CursorStyle.Block), white, black, Scrollbar);
    }

    public void Dispose() => IsDisposed = true;
}

/// <summary>Factory that hands out <see cref="FakeTerminalEngine"/> instances and remembers them.</summary>
internal sealed class FakeTerminalEngineFactory : ITerminalEngineFactory
{
    public List<FakeTerminalEngine> Created { get; } = [];

    public TerminalEngineOptions? LastOptions { get; private set; }

    public ITerminalEngine Create(TerminalEngineOptions options)
    {
        LastOptions = options;
        var engine = new FakeTerminalEngine(options.Columns, options.Rows);
        Created.Add(engine);
        return engine;
    }
}
