using System.Text;
using Wintty.Core.Abstractions;
using Wintty.Core.Events;
using Wintty.Core.Sessions;
using Wintty.Tests.Fakes;
using Xunit;

namespace Wintty.Tests.Events;

public class ApcScanningTests
{
    private static (VtEventScanner Scanner, List<string> Apcs, int[] Bells) Create()
    {
        var apcs = new List<string>();
        var bells = new int[1];
        return (new VtEventScanner(_ => { }, () => bells[0]++, apcs.Add), apcs, bells);
    }

    private static void Feed(VtEventScanner scanner, string text) => scanner.Feed(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void AnApcString_IsReportedWithoutItsFraming()
    {
        var (scanner, apcs, _) = Create();

        Feed(scanner, "text\u001b_Ga=T,f=100;AAAA\u001b\\more");

        Assert.Equal(["Ga=T,f=100;AAAA"], apcs);
    }

    [Fact]
    public void AnApcSplitAcrossReads_IsStillFound()
    {
        var (scanner, apcs, _) = Create();

        Feed(scanner, "\u001b_Ga=t;AA");
        Feed(scanner, "BB\u001b");
        Feed(scanner, "\\");

        Assert.Equal(["Ga=t;AABB"], apcs);
    }

    [Fact]
    public void ABellInsideAnApc_IsDataNotABell()
    {
        var (scanner, apcs, bells) = Create();

        Feed(scanner, "\u001b_Gx\u0007y\u001b\\");

        Assert.Equal(["Gx\u0007y"], apcs);
        Assert.Equal(0, bells[0]);
    }

    [Fact]
    public void ALoneEscapeInsideAnApc_IsKeptAsData()
    {
        var (scanner, apcs, _) = Create();

        Feed(scanner, "\u001b_Ga\u001bxb\u001b\\");

        Assert.Equal(["Ga\u001bxb"], apcs);
    }

    [Fact]
    public void AnOversizedApc_IsDroppedAndTheScannerRecovers()
    {
        var (scanner, apcs, _) = Create();

        Feed(scanner, "\u001b_G");
        scanner.Feed(Enumerable.Repeat((byte)'A', VtEventScanner.MaxApcBytes + 10).ToArray());
        Feed(scanner, "\u001b\\\u001b_Gok\u001b\\");

        Assert.Equal(["Gok"], apcs);
    }

    [Fact]
    public void WithoutAnApcCallback_TheStringIsSimplySkipped()
    {
        var oscs = new List<OscSequence>();
        var scanner = new VtEventScanner(oscs.Add, () => { });

        Feed(scanner, "\u001b_Gdata\u001b\\\u001b]0;t\u0007");

        Assert.Single(oscs);
    }
}

public class KittyGraphicsCommandTests
{
    [Fact]
    public void ATransmitAndDisplayCommand_IsParsed()
    {
        var command = KittyGraphicsCommand.Parse("Ga=T,f=100,i=7,q=2,c=10,r=4,z=-1,C=1;iVBORw0KGgo=");

        Assert.NotNull(command);
        Assert.Equal(GraphicsAction.TransmitAndDisplay, command.Action);
        Assert.Equal(100, command.Format);
        Assert.Equal(7, command.ImageId);
        Assert.Equal(2, command.Quiet);
        Assert.Equal(10, command.Number('c', 0));
        Assert.Equal(4, command.Number('r', 0));
        Assert.Equal(-1, command.ZIndex);
        Assert.True(command.KeepCursor);
        Assert.Equal("iVBORw0KGgo=", command.Data);
    }

    [Fact]
    public void Defaults_FollowTheProtocol()
    {
        var command = KittyGraphicsCommand.Parse("G;AAAA")!;

        Assert.Equal(GraphicsAction.Transmit, command.Action);
        Assert.Equal(32, command.Format);
        Assert.Equal('d', command.Transmission);
        Assert.False(command.Compressed);
        Assert.False(command.MoreChunks);
        Assert.False(command.KeepCursor);
        Assert.Equal(0, command.ImageId);
        Assert.Equal(0, command.ImageNumber);
        Assert.Equal(0, command.PlacementId);
        Assert.Equal(0, command.Width);
        Assert.Equal(0, command.Height);
        Assert.Equal(0, command.Quiet);
        Assert.Equal(0, command.ZIndex);
    }

    [Fact]
    public void RawDataAndChunking_AreReported()
    {
        var command = KittyGraphicsCommand.Parse("Gf=24,s=10,v=20,t=f,o=z,m=1,I=3,p=9;L3RtcC9pbWc=")!;

        Assert.Equal(24, command.Format);
        Assert.Equal(10, command.Width);
        Assert.Equal(20, command.Height);
        Assert.Equal('f', command.Transmission);
        Assert.True(command.Compressed);
        Assert.True(command.MoreChunks);
        Assert.Equal(3, command.ImageNumber);
        Assert.Equal(9, command.PlacementId);
    }

    [Theory]
    [InlineData("Ga=t", GraphicsAction.Transmit)]
    [InlineData("Ga=T", GraphicsAction.TransmitAndDisplay)]
    [InlineData("Ga=q", GraphicsAction.Query)]
    [InlineData("Ga=p", GraphicsAction.Put)]
    [InlineData("Ga=d", GraphicsAction.Delete)]
    [InlineData("Ga=f", GraphicsAction.TransmitFrame)]
    [InlineData("Ga=a", GraphicsAction.Animate)]
    [InlineData("Ga=c", GraphicsAction.Compose)]
    public void EveryActionIsRecognised(string apc, GraphicsAction expected) =>
        Assert.Equal(expected, KittyGraphicsCommand.Parse(apc)!.Action);

    [Theory]
    [InlineData("")]
    [InlineData("Xa=T")]
    [InlineData("Ga=z")]
    [InlineData("Ga")]
    [InlineData("Gnonsense")]
    public void NotGraphicsOrMalformed_IsRejected(string apc) => Assert.Null(KittyGraphicsCommand.Parse(apc));

    [Fact]
    public void AMultiCharacterAction_IsTreatedAsTheDefaultAction() =>
        Assert.Equal(GraphicsAction.Transmit, KittyGraphicsCommand.Parse("Ga=Tx")!.Action);

    [Fact]
    public void ANonNumericValue_FallsBackToTheDefault()
    {
        var command = KittyGraphicsCommand.Parse("Gi=abc,s=")!;

        Assert.Equal(0, command.ImageId);
        Assert.Equal(0, command.Width);
        Assert.Equal("abc", command.Keys['i']);
    }

    [Fact]
    public void RejectsNull() => Assert.Throws<ArgumentNullException>(() => KittyGraphicsCommand.Parse(null!));
}

public class SessionGraphicsTests
{
    [Fact]
    public async Task AGraphicsCommandInTheOutput_IsRaisedAsAnEvent()
    {
        var ptys = new FakePtyConnectionFactory();
        using var session = await TerminalSession.StartAsync(new FakeTerminalEngineFactory(), ptys, new PtyStartOptions("cmd.exe", 80, 24));
        var events = new List<TerminalEvent>();
        session.TerminalEventRaised += (_, e) =>
        {
            lock (events)
            {
                events.Add(e);
            }
        };

        await ptys.Connection.EmitAsync("\u001b_Ga=T,f=100,i=5;AAAA\u001b\\");

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            lock (events)
            {
                if (events.Count > 0)
                {
                    break;
                }
            }

            await Task.Delay(10);
        }

        GraphicsCommandReceived received;
        lock (events)
        {
            received = Assert.IsType<GraphicsCommandReceived>(Assert.Single(events));
        }

        Assert.Equal(GraphicsAction.TransmitAndDisplay, received.Command.Action);
        Assert.Equal(5, received.Command.ImageId);
    }

    [Fact]
    public async Task ANonGraphicsApc_RaisesNothing()
    {
        var ptys = new FakePtyConnectionFactory();
        using var session = await TerminalSession.StartAsync(new FakeTerminalEngineFactory(), ptys, new PtyStartOptions("cmd.exe", 80, 24));
        var raised = 0;
        var updated = 0;
        session.TerminalEventRaised += (_, _) => Interlocked.Increment(ref raised);
        session.Updated += (_, _) => Interlocked.Increment(ref updated);

        await ptys.Connection.EmitAsync("\u001b_Xnot graphics\u001b\\");

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Volatile.Read(ref updated) == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.Equal(0, Volatile.Read(ref raised));
    }
}
