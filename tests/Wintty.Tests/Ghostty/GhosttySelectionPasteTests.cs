using System.Text;
using Wintty.Core.Abstractions;
using Wintty.Core.Sessions;
using Wintty.Core.Selection;
using Wintty.Ghostty;
using Wintty.Tests.Fakes;
using Xunit;

namespace Wintty.Tests.Ghostty;

/// <summary>Verifies paste encoding, wrapped rows and copying from history against the real libghostty.</summary>
[Trait("Category", "Integration")]
[Collection("NativeEnvironment")]
public class GhosttySelectionPasteTests
{
    private static readonly GhosttyGateway Gateway = new();

    private static ITerminalEngine Engine(int columns = 10, int rows = 3) =>
        Gateway.Create(new TerminalEngineOptions(columns, rows, 100));

    private static void Feed(ITerminalEngine engine, string text) => engine.Write(Encoding.UTF8.GetBytes(text));

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes).Replace("\u001b", "ESC", StringComparison.Ordinal);

    [Fact]
    public void BracketedPaste_FollowsTheProgramsMode()
    {
        using var engine = Engine();
        Assert.False(engine.BracketedPaste);

        Feed(engine, "\u001b[?2004h");
        Assert.True(engine.BracketedPaste);

        Feed(engine, "\u001b[?2004l");
        Assert.False(engine.BracketedPaste);
    }

    [Fact]
    public void Paste_WithoutBracketedMode_SendsTextWithCarriageReturns()
    {
        using var engine = Engine();

        Assert.Equal("one\rtwo", Text(engine.EncodePaste("one\ntwo")));
    }

    [Fact]
    public void Paste_WithBracketedMode_IsWrappedInMarkersAndKeepsLineBreaks()
    {
        using var engine = Engine();
        Feed(engine, "\u001b[?2004h");

        Assert.Equal("ESC[200~one\ntwoESC[201~", Text(engine.EncodePaste("one\ntwo")));
    }

    [Fact]
    public void Paste_StripsTheBracketedPasteEndMarkerFromTheText()
    {
        using var engine = Engine();
        Feed(engine, "\u001b[?2004h");

        var encoded = Text(engine.EncodePaste("a\u001b[201~b"));

        Assert.Equal(1, encoded.Split("ESC[201~").Length - 1);
    }

    [Fact]
    public void Paste_KeepsNonAsciiText()
    {
        using var engine = Engine();

        Assert.Equal("世界 é", Text(engine.EncodePaste("世界 é")));
    }

    [Fact]
    public void Paste_RejectsNull()
    {
        using var engine = Engine();

        Assert.Throws<ArgumentNullException>(() => engine.EncodePaste(null!));
    }

    [Fact]
    public void AfterDispose_PasteThrows()
    {
        var engine = Engine();
        engine.Dispose();

        Assert.Throws<ObjectDisposedException>(() => engine.EncodePaste("x"));
        Assert.Throws<ObjectDisposedException>(() => engine.BracketedPaste);
    }

    [Fact]
    public void LongLines_MarkTheirContinuationRowsAsWrapped()
    {
        using var engine = Engine(5, 4);
        Feed(engine, "abcdefghijkl\r\nnext");

        var snapshot = engine.CreateSnapshot();

        Assert.False(snapshot.IsWrapped(0));
        Assert.True(snapshot.IsWrapped(1));
        Assert.True(snapshot.IsWrapped(2));
        Assert.False(snapshot.IsWrapped(3));
    }

    [Fact]
    public async Task GetSelectedText_ReadsRowsScrolledOutOfViewAndRestoresTheView()
    {
        var engines = new GhosttyGateway();
        var session = await TerminalSession.StartAsync(engines, new FakePtyConnectionFactory(), new PtyStartOptions("cmd.exe", 10, 3), 100);
        using (session)
        {
            lock (session.SyncRoot)
            {
                Feed(session.Engine, string.Join("\r\n", Enumerable.Range(0, 20).Select(i => "line" + i)));
            }

            // Rows 2 to 5 of history are long out of view; the screen shows line17 to line19.
            var text = session.GetSelectedText(new TerminalSelection(new SelectionPoint(2, 0), new SelectionPoint(5, 5)));

            Assert.Equal("line2\nline3\nline4\nline5", text);
            lock (session.SyncRoot)
            {
                var snapshot = session.Engine.CreateSnapshot();
                Assert.False(snapshot.Scrollbar.IsScrolledBack);
                Assert.Equal("line19", snapshot.GetRowText(2));
            }
        }
    }

    [Fact]
    public async Task GetSelectedText_KeepsAScrolledBackViewWhereItWas()
    {
        var session = await TerminalSession.StartAsync(new GhosttyGateway(), new FakePtyConnectionFactory(), new PtyStartOptions("cmd.exe", 10, 3), 100);
        using (session)
        {
            lock (session.SyncRoot)
            {
                Feed(session.Engine, string.Join("\r\n", Enumerable.Range(0, 20).Select(i => "line" + i)));
            }

            session.ScrollBy(-6);

            session.GetSelectedText(new TerminalSelection(new SelectionPoint(0, 0), new SelectionPoint(19, 5)));

            lock (session.SyncRoot)
            {
                Assert.Equal(11, session.Engine.CreateSnapshot().Scrollbar.Offset);
            }
        }
    }
}
