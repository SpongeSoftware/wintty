using System.Text;
using Wintty.Core.Abstractions;
using Wintty.Core.Input;
using Wintty.Core.Sessions;
using Wintty.Ghostty;
using Wintty.Tests.Fakes;
using Xunit;

namespace Wintty.Tests.Ghostty;

/// <summary>Verifies the modern terminal modes against the real libghostty.</summary>
[Trait("Category", "Integration")]
[Collection("NativeEnvironment")]
public class GhosttyModernModesTests
{
    private static readonly GhosttyGateway Gateway = new();

    private static ITerminalEngine Engine() => Gateway.Create(new TerminalEngineOptions(40, 10, 100));

    private static void Feed(ITerminalEngine engine, string text) => engine.Write(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void SynchronizedOutput_FollowsMode2026()
    {
        using var engine = Engine();
        Assert.False(engine.SynchronizedOutput);

        Feed(engine, "\u001b[?2026h");
        Assert.True(engine.SynchronizedOutput);

        Feed(engine, "\u001b[?2026l");
        Assert.False(engine.SynchronizedOutput);
    }

    [Fact]
    public void FocusEvents_FollowMode1004()
    {
        using var engine = Engine();
        Assert.False(engine.FocusEvents);

        Feed(engine, "\u001b[?1004h");
        Assert.True(engine.FocusEvents);

        Feed(engine, "\u001b[?1004l");
        Assert.False(engine.FocusEvents);
    }

    [Fact]
    public void ColorSchemeNotifications_FollowMode2031()
    {
        using var engine = Engine();
        Assert.False(engine.ColorSchemeNotifications);

        Feed(engine, "\u001b[?2031h");
        Assert.True(engine.ColorSchemeNotifications);

        Feed(engine, "\u001b[?2031l");
        Assert.False(engine.ColorSchemeNotifications);
    }

    [Fact]
    public void PixelPrecisionMouse_ReportsPixelCoordinates()
    {
        using var engine = Engine();
        engine.SetCellSize(10, 20);
        Feed(engine, "\u001b[?1000h\u001b[?1016h");

        var bytes = engine.EncodeMouse(new MouseInput(MouseAction.Press, MouseButton.Left, 123, 45));

        Assert.Equal("\u001b[<0;123;45M", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void AfterDispose_ModeReadsThrow()
    {
        var engine = Engine();
        engine.Dispose();

        Assert.Throws<ObjectDisposedException>(() => engine.SynchronizedOutput);
        Assert.Throws<ObjectDisposedException>(() => engine.FocusEvents);
        Assert.Throws<ObjectDisposedException>(() => engine.ColorSchemeNotifications);
    }
}

public class TerminalSessionModesTests
{
    private static async Task<(TerminalSession Session, FakeTerminalEngine Engine, FakePtyConnectionFactory Ptys)> StartAsync()
    {
        var engines = new FakeTerminalEngineFactory();
        var ptys = new FakePtyConnectionFactory();
        var session = await TerminalSession.StartAsync(engines, ptys, new PtyStartOptions("cmd.exe", 80, 24));
        return (session, engines.Created[0], ptys);
    }

    private static string Sent(FakePtyConnectionFactory ptys) => Encoding.UTF8.GetString(ptys.Connection.InputBuffer.ToArray());

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "timed out waiting");
    }

    [Fact]
    public async Task FocusReports_AreSentOnlyWhenTheProgramAsked()
    {
        var (session, engine, ptys) = await StartAsync();
        using (session)
        {
            Assert.False(session.SetFocus(true));
            Assert.Equal(string.Empty, Sent(ptys));

            engine.FocusEvents = true;

            Assert.True(session.SetFocus(true));
            Assert.True(session.SetFocus(false));
            Assert.Equal("\u001b[I\u001b[O", Sent(ptys));
        }
    }

    [Fact]
    public async Task ColorSchemeNotifications_AreSentOnlyWhenTheProgramAsked()
    {
        var (session, engine, ptys) = await StartAsync();
        using (session)
        {
            Assert.False(session.NotifyColorScheme(true));

            engine.ColorSchemeNotifications = true;

            Assert.True(session.NotifyColorScheme(true));
            Assert.True(session.NotifyColorScheme(false));
            Assert.Equal("\u001b[?997;1n\u001b[?997;2n", Sent(ptys));
        }
    }

    [Fact]
    public async Task ASynchronisedUpdate_HoldsTheRedrawUntilItEndsOrTimesOut()
    {
        var (session, engine, ptys) = await StartAsync();
        using (session)
        {
            var updates = 0;
            session.Updated += (_, _) => Interlocked.Increment(ref updates);

            engine.SynchronizedOutput = true;
            await ptys.Connection.EmitAsync("frame part one");

            // Check as soon as the engine has the text, so a slow machine cannot run into the one second timeout.
            await WaitForAsync(() => engine.Text.Contains("frame part one", StringComparison.Ordinal));
            Assert.Equal(0, Volatile.Read(ref updates));

            engine.SynchronizedOutput = false;
            await ptys.Connection.EmitAsync("end of frame");
            await WaitForAsync(() => Volatile.Read(ref updates) >= 1);
        }
    }

    [Fact]
    public async Task AForgottenSynchronisedUpdate_IsPresentedAfterATimeout()
    {
        var (session, engine, ptys) = await StartAsync();
        using (session)
        {
            var updates = 0;
            session.Updated += (_, _) => Interlocked.Increment(ref updates);

            engine.SynchronizedOutput = true;
            await ptys.Connection.EmitAsync("never ended");

            await WaitForAsync(() => Volatile.Read(ref updates) >= 1);
        }
    }

    [Fact]
    public async Task AfterDispose_ModeReportsThrow()
    {
        var (session, _, _) = await StartAsync();
        session.Dispose();

        Assert.Throws<ObjectDisposedException>(() => session.SetFocus(true));
        Assert.Throws<ObjectDisposedException>(() => session.NotifyColorScheme(true));
    }
}
