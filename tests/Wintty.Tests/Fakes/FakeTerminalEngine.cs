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

    public TerminalSnapshot CreateSnapshot()
    {
        var black = new TerminalColor(0, 0, 0);
        var white = new TerminalColor(255, 255, 255);
        var cells = new TerminalCell[Columns * Rows];
        Array.Fill(cells, new TerminalCell(string.Empty, white, black));
        return new TerminalSnapshot(Columns, Rows, cells, new CursorState(0, 0, true, CursorStyle.Block), white, black);
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
