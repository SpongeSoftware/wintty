using System.Text;
using Wintty.Core.Abstractions;
using Wintty.Core.Input;
using Wintty.Ghostty;
using Xunit;

namespace Wintty.Tests.Ghostty;

/// <summary>Verifies scrollback and mouse reporting against the real libghostty.</summary>
[Trait("Category", "Integration")]
[Collection("NativeEnvironment")]
public class GhosttyScrollMouseTests
{
    private static readonly GhosttyGateway Gateway = new();

    private static ITerminalEngine Engine()
    {
        var engine = Gateway.Create(new TerminalEngineOptions(10, 3, 100));
        engine.SetCellSize(10, 10);
        return engine;
    }

    private static void Feed(ITerminalEngine engine, string text) => engine.Write(Encoding.UTF8.GetBytes(text));

    private static void FillHistory(ITerminalEngine engine) =>
        Feed(engine, string.Join("\r\n", Enumerable.Range(0, 20).Select(i => "line" + i)));

    private static string Encode(ITerminalEngine engine, MouseInput input) =>
        Encoding.ASCII.GetString(engine.EncodeMouse(input)).Replace("\u001b", "ESC", StringComparison.Ordinal);

    [Fact]
    public void ScrollingBack_ShowsOlderRowsAndReportsPosition()
    {
        using var engine = Engine();
        FillHistory(engine);
        Assert.False(engine.CreateSnapshot().Scrollbar.IsScrolledBack);
        Assert.Equal("line19", engine.CreateSnapshot().GetRowText(2));

        engine.ScrollViewport(-5);

        var snapshot = engine.CreateSnapshot();
        Assert.True(snapshot.Scrollbar.IsScrolledBack);
        Assert.Equal(12, snapshot.Scrollbar.Offset);
        Assert.Equal("line12", snapshot.GetRowText(0));
    }

    [Fact]
    public void ScrollViewportToBottom_ReturnsToTheLiveScreen()
    {
        using var engine = Engine();
        FillHistory(engine);
        engine.ScrollViewport(-10);

        engine.ScrollViewportToBottom();

        var snapshot = engine.CreateSnapshot();
        Assert.False(snapshot.Scrollbar.IsScrolledBack);
        Assert.Equal("line19", snapshot.GetRowText(2));
    }

    [Fact]
    public void ScrollingPastTheTop_StopsAtTheOldestRow()
    {
        using var engine = Engine();
        FillHistory(engine);

        engine.ScrollViewport(-1000);

        Assert.Equal(0, engine.CreateSnapshot().Scrollbar.Offset);
    }

    [Fact]
    public void WithoutMouseMode_NothingIsReported()
    {
        using var engine = Engine();

        Assert.False(engine.MouseTracking);
        Assert.Empty(engine.EncodeMouse(new MouseInput(MouseAction.Press, MouseButton.Left, 25, 15)));
    }

    [Fact]
    public void SgrMode_ReportsClicksAtTheCell()
    {
        using var engine = Engine();
        Feed(engine, "\u001b[?1000h\u001b[?1006h");

        Assert.True(engine.MouseTracking);
        Assert.Equal("ESC[<0;3;2M", Encode(engine, new MouseInput(MouseAction.Press, MouseButton.Left, 25, 15)));
        Assert.Equal("ESC[<0;3;2m", Encode(engine, new MouseInput(MouseAction.Release, MouseButton.Left, 25, 15)));
        Assert.Equal("ESC[<2;1;1M", Encode(engine, new MouseInput(MouseAction.Press, MouseButton.Right, 0, 0)));
    }

    [Fact]
    public void SgrMode_ReportsWheelStepsAndModifiers()
    {
        using var engine = Engine();
        Feed(engine, "\u001b[?1000h\u001b[?1006h");

        Assert.Equal("ESC[<64;1;1M", Encode(engine, new MouseInput(MouseAction.Press, MouseButton.WheelUp, 0, 0)));
        Assert.Equal("ESC[<65;1;1M", Encode(engine, new MouseInput(MouseAction.Press, MouseButton.WheelDown, 0, 0)));
        Assert.Equal("ESC[<20;1;1M", Encode(engine, new MouseInput(MouseAction.Press, MouseButton.Left, 0, 0, KeyModifiers.Shift | KeyModifiers.Control)));
    }

    [Fact]
    public void DragMode_ReportsMotionWithTheButtonHeld()
    {
        using var engine = Engine();
        Feed(engine, "\u001b[?1002h\u001b[?1006h");

        Assert.Equal("ESC[<32;5;1M", Encode(engine, new MouseInput(MouseAction.Motion, MouseButton.Left, 45, 5)));
    }

    [Fact]
    public void DragMode_IgnoresMotionWithNoButton()
    {
        using var engine = Engine();
        Feed(engine, "\u001b[?1002h\u001b[?1006h");

        Assert.Empty(engine.EncodeMouse(new MouseInput(MouseAction.Motion, MouseButton.None, 45, 5)));
    }

    [Fact]
    public void LegacyMode_UsesTheNormalEncoding()
    {
        using var engine = Engine();
        Feed(engine, "\u001b[?1000h");

        var bytes = engine.EncodeMouse(new MouseInput(MouseAction.Press, MouseButton.Left, 0, 0));

        Assert.Equal([0x1b, (byte)'[', (byte)'M', 32, 33, 33], bytes);
    }

    [Fact]
    public void AfterDispose_ScrollAndMouseThrow()
    {
        var engine = Engine();
        engine.Dispose();

        Assert.Throws<ObjectDisposedException>(() => engine.ScrollViewport(1));
        Assert.Throws<ObjectDisposedException>(() => engine.ScrollViewportToBottom());
        Assert.Throws<ObjectDisposedException>(() => engine.EncodeMouse(default));
        Assert.Throws<ObjectDisposedException>(() => engine.MouseTracking);
    }
}
