using System.Text;
using Wintty.Core.Abstractions;
using Wintty.Core.Selection;
using Wintty.Core.Sessions;
using Wintty.Tests.Fakes;
using Xunit;

namespace Wintty.Tests.Core;

public class TerminalSessionPasteTests
{
    private static async Task<(TerminalSession Session, FakeTerminalEngine Engine, FakePtyConnectionFactory Ptys)> StartAsync()
    {
        var engines = new FakeTerminalEngineFactory();
        var ptys = new FakePtyConnectionFactory();
        var session = await TerminalSession.StartAsync(engines, ptys, new PtyStartOptions("cmd.exe", 80, 24));
        return (session, engines.Created[0], ptys);
    }

    [Fact]
    public async Task Paste_SendsTheEncodedTextAndReturnsToTheBottom()
    {
        var (session, engine, ptys) = await StartAsync();
        using (session)
        {
            session.Paste("a\nb");

            Assert.Equal(["a\nb"], engine.Pastes);
            Assert.Equal("a\rb", Encoding.UTF8.GetString(ptys.Connection.InputBuffer.ToArray()));
            Assert.Equal(1, engine.ScrolledToBottom);
        }
    }

    [Fact]
    public async Task Paste_WithBracketedModeIsWrapped()
    {
        var (session, engine, ptys) = await StartAsync();
        using (session)
        {
            engine.BracketedPaste = true;

            Assert.True(session.BracketedPaste);
            session.Paste("x");

            Assert.Equal("\u001b[200~x\u001b[201~", Encoding.UTF8.GetString(ptys.Connection.InputBuffer.ToArray()));
        }
    }

    [Fact]
    public async Task Paste_OfNothing_SendsNothing()
    {
        var (session, engine, ptys) = await StartAsync();
        using (session)
        {
            session.Paste(string.Empty);

            Assert.Empty(engine.Pastes);
            Assert.Equal(0, ptys.Connection.InputBuffer.Length);
        }
    }

    [Fact]
    public async Task GetSelectedText_ReadsThroughTheEngineAndRestoresTheView()
    {
        var (session, engine, _) = await StartAsync();
        using (session)
        {
            var text = session.GetSelectedText(new TerminalSelection(new SelectionPoint(0, 0), new SelectionPoint(0, 3)));

            Assert.Equal(string.Empty, text);
            Assert.NotEmpty(engine.Scrolls);
        }
    }

    [Fact]
    public async Task ArgumentsAndDisposal_AreChecked()
    {
        var (session, _, _) = await StartAsync();

        Assert.Throws<ArgumentNullException>(() => session.Paste(null!));
        Assert.Throws<ArgumentNullException>(() => session.GetSelectedText(null!));
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.Paste("x"));
        Assert.Throws<ObjectDisposedException>(() => session.BracketedPaste);
        Assert.Throws<ObjectDisposedException>(() => session.GetSelectedText(new TerminalSelection(default, default)));
    }
}
