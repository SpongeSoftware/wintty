using System.Text;
using Wintty.Core.Abstractions;
using Wintty.Core.Events;
using Wintty.Core.Sessions;
using Wintty.Tests.Fakes;
using Xunit;

namespace Wintty.Tests.Events;

public class VtEventScannerTests
{
    private static (VtEventScanner Scanner, List<OscSequence> Oscs, int[] Bells) Create()
    {
        var oscs = new List<OscSequence>();
        var bells = new int[1];
        return (new VtEventScanner(oscs.Add, () => bells[0]++), oscs, bells);
    }

    private static void Feed(VtEventScanner scanner, string text) => scanner.Feed(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void AnOscEndedByBell_IsReported()
    {
        var (scanner, oscs, bells) = Create();

        Feed(scanner, "before\u001b]7;file://host/tmp\u0007after");

        Assert.Equal([new OscSequence(7, "file://host/tmp")], oscs);
        Assert.Equal(0, bells[0]);
    }

    [Fact]
    public void AnOscEndedByStringTerminator_IsReported()
    {
        var (scanner, oscs, _) = Create();

        Feed(scanner, "\u001b]0;title\u001b\\");

        Assert.Equal([new OscSequence(0, "title")], oscs);
    }

    [Fact]
    public void ASequenceSplitAcrossReads_IsStillFound()
    {
        var (scanner, oscs, _) = Create();

        Feed(scanner, "\u001b]5");
        Feed(scanner, "2;c;aGVs");
        Feed(scanner, "bG8=\u001b");
        Feed(scanner, "\\");

        Assert.Equal([new OscSequence(52, "c;aGVsbG8=")], oscs);
    }

    [Fact]
    public void ABellOutsideASequence_IsCountedEachTime()
    {
        var (scanner, oscs, bells) = Create();

        Feed(scanner, "\u0007x\u0007");

        Assert.Equal(2, bells[0]);
        Assert.Empty(oscs);
    }

    [Fact]
    public void ABellInsideADeviceControlString_IsNotABell()
    {
        var (scanner, _, bells) = Create();

        Feed(scanner, "\u001bP1$r\u0007\u001b\\");
        Assert.Equal(0, bells[0]);

        Feed(scanner, "\u001b_Gdata\u0007\u001bX\u0007\u001b\\\u0007");
        Assert.Equal(1, bells[0]);
    }

    [Fact]
    public void AnEscapeInsideAnOsc_CancelsItAndStartsTheNextSequence()
    {
        var (scanner, oscs, _) = Create();

        Feed(scanner, "\u001b]7;partial\u001b]0;next\u0007");

        Assert.Equal([new OscSequence(0, "next")], oscs);
    }

    [Fact]
    public void OtherEscapeSequences_AreIgnoredAndDoNotConfuseTheScanner()
    {
        var (scanner, oscs, bells) = Create();

        Feed(scanner, "\u001b[31mred\u001b[0m\u001b\u001b]0;t\u0007\u001bM\u0007");

        Assert.Equal([new OscSequence(0, "t")], oscs);
        Assert.Equal(1, bells[0]);
    }

    [Fact]
    public void ACommandWithoutAPayload_HasAnEmptyPayload()
    {
        var (scanner, oscs, _) = Create();

        Feed(scanner, "\u001b]104\u0007");

        Assert.Equal([new OscSequence(104, string.Empty)], oscs);
    }

    [Fact]
    public void ANonNumericCommand_IsDropped()
    {
        var (scanner, oscs, _) = Create();

        Feed(scanner, "\u001b]abc;x\u0007");

        Assert.Empty(oscs);
    }

    [Fact]
    public void AnOversizedPayload_IsDroppedAndTheScannerRecovers()
    {
        var (scanner, oscs, _) = Create();

        Feed(scanner, "\u001b]52;c;");
        scanner.Feed(new byte[VtEventScanner.MaxPayloadBytes + 10].AsSpan().ToArray().Select(_ => (byte)'A').ToArray());
        Feed(scanner, "\u0007\u001b]0;ok\u0007");

        Assert.Equal([new OscSequence(0, "ok")], oscs);
    }

    [Fact]
    public void MultiByteText_IsDecoded()
    {
        var (scanner, oscs, _) = Create();

        Feed(scanner, "\u001b]0;日本語 é\u0007");

        Assert.Equal("日本語 é", oscs[0].Payload);
    }

    [Fact]
    public void RejectsNullCallbacks()
    {
        Assert.Throws<ArgumentNullException>(() => new VtEventScanner(null!, () => { }));
        Assert.Throws<ArgumentNullException>(() => new VtEventScanner(_ => { }, null!));
    }
}

public class TerminalEventTests
{
    private static TerminalEvent? From(int command, string payload) => TerminalEvent.From(new OscSequence(command, payload));

    [Fact]
    public void WorkingDirectory_ReadsWindowsPaths()
    {
        var reported = Assert.IsType<WorkingDirectoryReported>(From(7, "file://PC/C:/Users/me/My%20Docs"));

        Assert.Equal("PC", reported.Host);
        Assert.Equal("/C:/Users/me/My Docs", reported.Path);
        Assert.Equal(@"C:\Users\me\My Docs", reported.WindowsPath);
    }

    [Fact]
    public void WorkingDirectory_WithAnEmptyHost_IsLocal()
    {
        var reported = Assert.IsType<WorkingDirectoryReported>(From(7, "file:///D:/code"));

        Assert.Equal(string.Empty, reported.Host);
        Assert.Equal(@"D:\code", reported.WindowsPath);
    }

    [Fact]
    public void WorkingDirectory_InsideALinuxDistribution_HasNoWindowsPath()
    {
        var reported = Assert.IsType<WorkingDirectoryReported>(From(7, "file://ubuntu/home/me"));

        Assert.Equal("/home/me", reported.Path);
        Assert.Null(reported.WindowsPath);
    }

    [Theory]
    [InlineData("http://example.com/x")]
    [InlineData("file://hostonly")]
    [InlineData("")]
    public void WorkingDirectory_RejectsReportsThatAreNotFileUrls(string payload) => Assert.Null(From(7, payload));

    [Fact]
    public void Osc9_IsAPlainNotification() =>
        Assert.Equal(new NotificationRequested(null, "Build done"), From(9, "Build done"));

    [Fact]
    public void Osc9_WithNothingToSay_IsIgnored() => Assert.Null(From(9, string.Empty));

    [Theory]
    [InlineData("4;1;42", ProgressState.Normal, 42)]
    [InlineData("4;0", ProgressState.Hidden, 0)]
    [InlineData("4;2;150", ProgressState.Error, 100)]
    [InlineData("4;3", ProgressState.Indeterminate, 0)]
    [InlineData("4;4;10", ProgressState.Warning, 10)]
    public void Osc9Progress_IsReported(string payload, ProgressState state, int percent) =>
        Assert.Equal(new ProgressReported(state, percent), From(9, payload));

    [Theory]
    [InlineData("4;9;1")]
    [InlineData("4;x")]
    [InlineData("4;1;y")]
    public void Osc9Progress_RejectsBadValues(string payload) => Assert.Null(From(9, payload));

    [Fact]
    public void Osc777_CarriesATitleAndBody()
    {
        Assert.Equal(new NotificationRequested("Tests", "3 failed; see log"), From(777, "notify;Tests;3 failed; see log"));
        Assert.Equal(new NotificationRequested("Only title", string.Empty), From(777, "notify;Only title"));
    }

    [Theory]
    [InlineData("other;x;y")]
    [InlineData("notify")]
    public void Osc777_RejectsOtherForms(string payload) => Assert.Null(From(777, payload));

    [Fact]
    public void Osc99_UnderstandsTitlesAndBodies()
    {
        Assert.Equal(new NotificationRequested("Hi", string.Empty), From(99, "i=1:d=0:p=title;Hi"));
        Assert.Equal(new NotificationRequested(null, "Body text"), From(99, "i=1;Body text"));
        Assert.Null(From(99, "no separator"));
        Assert.Null(From(99, "i=1;"));
    }

    [Fact]
    public void Osc52_WritesAndReadsTheClipboard()
    {
        var write = Assert.IsType<ClipboardRequested>(From(52, "c;aGVsbG8="));
        Assert.Equal("c", write.Selection);
        Assert.Equal("hello", write.Text);
        Assert.False(write.IsRead);

        var read = Assert.IsType<ClipboardRequested>(From(52, ";?"));
        Assert.Equal("c", read.Selection);
        Assert.True(read.IsRead);
    }

    [Theory]
    [InlineData("c;%%%")]
    [InlineData("nosemicolon")]
    public void Osc52_RejectsBadPayloads(string payload) => Assert.Null(From(52, payload));

    [Fact]
    public void OtherCommands_AreNotActedOn() => Assert.Null(From(0, "title"));
}

public class TerminalSessionEventTests
{
    private static async Task<(TerminalSession Session, FakePtyConnectionFactory Ptys)> StartAsync()
    {
        var ptys = new FakePtyConnectionFactory();
        var session = await TerminalSession.StartAsync(new FakeTerminalEngineFactory(), ptys, new PtyStartOptions("cmd.exe", 80, 24));
        return (session, ptys);
    }

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
    public async Task ADirectoryReport_UpdatesTheWorkingDirectoryAndRaisesTheEvent()
    {
        var (session, ptys) = await StartAsync();
        using (session)
        {
            var events = new List<TerminalEvent>();
            session.TerminalEventRaised += (_, e) => events.Add(e);
            Assert.Null(session.WorkingDirectory);

            await ptys.Connection.EmitAsync("\u001b]7;file:///C:/work/proj\u0007");
            await WaitForAsync(() => events.Count == 1);

            Assert.Equal(@"C:\work\proj", session.WorkingDirectory);
            Assert.IsType<WorkingDirectoryReported>(Assert.Single(events));
        }
    }

    [Fact]
    public async Task ALinuxDirectoryReport_KeepsTheLastWindowsDirectory()
    {
        var (session, ptys) = await StartAsync();
        using (session)
        {
            var count = 0;
            session.TerminalEventRaised += (_, _) => Interlocked.Increment(ref count);

            await ptys.Connection.EmitAsync("\u001b]7;file:///C:/a\u0007\u001b]7;file://distro/home/me\u0007");
            await WaitForAsync(() => Volatile.Read(ref count) == 2);

            Assert.Equal(@"C:\a", session.WorkingDirectory);
        }
    }

    [Fact]
    public async Task NotificationsAndBells_AreRaised()
    {
        var (session, ptys) = await StartAsync();
        using (session)
        {
            var events = new List<TerminalEvent>();
            var bells = 0;
            session.TerminalEventRaised += (_, e) => events.Add(e);
            session.BellRang += (_, _) => Interlocked.Increment(ref bells);

            await ptys.Connection.EmitAsync("\u0007\u001b]9;done\u0007");
            await WaitForAsync(() => Volatile.Read(ref bells) == 1 && events.Count == 1);

            Assert.Equal(new NotificationRequested(null, "done"), events[0]);
        }
    }

    [Fact]
    public async Task SequencesTheDesktopDoesNotActOn_RaiseNothing()
    {
        var (session, ptys) = await StartAsync();
        using (session)
        {
            var raised = false;
            session.TerminalEventRaised += (_, _) => raised = true;
            var updated = 0;
            session.Updated += (_, _) => Interlocked.Increment(ref updated);

            await ptys.Connection.EmitAsync("\u001b]0;just a title\u0007");
            await WaitForAsync(() => Volatile.Read(ref updated) > 0);

            Assert.False(raised);
        }
    }
}
