using System.Text;
using Wintty.Core.Abstractions;
using Wintty.Core.Input;
using Wintty.Core.Sessions;
using Wintty.Tests.Fakes;
using Xunit;

namespace Wintty.Tests.Core;

public class TerminalSessionScrollMouseTests
{
    private static async Task<(TerminalSession Session, FakeTerminalEngine Engine, FakePtyConnectionFactory Ptys)> StartAsync()
    {
        var engines = new FakeTerminalEngineFactory();
        var ptys = new FakePtyConnectionFactory();
        var session = await TerminalSession.StartAsync(engines, ptys, new PtyStartOptions("cmd.exe", 80, 24));
        return (session, engines.Created[0], ptys);
    }

    [Fact]
    public async Task ScrollBy_MovesTheEngineViewport()
    {
        var (session, engine, _) = await StartAsync();
        using (session)
        {
            session.ScrollBy(-3);
            session.ScrollBy(2);

            Assert.Equal([-3, 2], engine.Scrolls);
        }
    }

    [Fact]
    public async Task Typing_ReturnsTheViewToTheBottom()
    {
        var (session, engine, ptys) = await StartAsync();
        using (session)
        {
            session.SendInput("x"u8);

            Assert.Equal(1, engine.ScrolledToBottom);
            Assert.Equal("x", Encoding.UTF8.GetString(ptys.Connection.InputBuffer.ToArray()));
        }
    }

    [Fact]
    public async Task EngineReplies_DoNotMoveTheView()
    {
        var (session, engine, _) = await StartAsync();
        using (session)
        {
            engine.Respond("reply"u8.ToArray());

            Assert.Equal(0, engine.ScrolledToBottom);
        }
    }

    [Fact]
    public async Task SendMouse_SendsTheEncodedReport()
    {
        var (session, engine, ptys) = await StartAsync();
        using (session)
        {
            engine.EncodedMouse = "[<0;3;2M"u8.ToArray();
            var input = new MouseInput(MouseAction.Press, MouseButton.Left, 25, 15);

            Assert.True(session.SendMouse(input));

            Assert.Equal([input], engine.MouseInputs);
            Assert.Equal("[<0;3;2M", Encoding.UTF8.GetString(ptys.Connection.InputBuffer.ToArray()));
            Assert.Equal(0, engine.ScrolledToBottom);
        }
    }

    [Fact]
    public async Task SendMouse_WhenReportingIsOff_SendsNothing()
    {
        var (session, engine, ptys) = await StartAsync();
        using (session)
        {
            engine.EncodedMouse = [];

            Assert.False(session.SendMouse(new MouseInput(MouseAction.Motion, MouseButton.None, 0, 0)));
            Assert.Equal(0, ptys.Connection.InputBuffer.Length);
        }
    }

    [Fact]
    public async Task MouseTracking_ReflectsTheEngine()
    {
        var (session, engine, _) = await StartAsync();
        using (session)
        {
            Assert.False(session.MouseTracking);
            engine.MouseTracking = true;
            Assert.True(session.MouseTracking);
        }
    }

    [Fact]
    public async Task AfterDispose_ScrollAndMouseThrow()
    {
        var (session, _, _) = await StartAsync();
        session.Dispose();

        Assert.Throws<ObjectDisposedException>(() => session.ScrollBy(1));
        Assert.Throws<ObjectDisposedException>(() => session.SendMouse(default));
        Assert.Throws<ObjectDisposedException>(() => session.MouseTracking);
    }
}
