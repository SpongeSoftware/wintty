using System.Text;
using Wintty.Core.Abstractions;
using Wintty.Core.Sessions;
using Wintty.Tests.Fakes;
using Xunit;

namespace Wintty.Tests.Core;

public class TerminalSessionTests
{
    private static readonly PtyStartOptions Options = new("pwsh.exe", 80, 24);

    private static async Task<(TerminalSession Session, FakeTerminalEngineFactory Engines, FakePtyConnectionFactory Ptys)> StartAsync()
    {
        var engines = new FakeTerminalEngineFactory();
        var ptys = new FakePtyConnectionFactory();
        var session = await TerminalSession.StartAsync(engines, ptys, Options, scrollback: 500);
        return (session, engines, ptys);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for condition.");
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task StartAsync_CreatesEngineWithOptionsAndStartsProcess()
    {
        var (session, engines, ptys) = await StartAsync();
        using (session)
        {
            Assert.Equal(new TerminalEngineOptions(80, 24, 500), engines.LastOptions);
            Assert.Same(Options, ptys.LastOptions);
            Assert.Same(engines.Created[0], session.Engine);
        }
    }

    [Fact]
    public async Task Output_IsPumpedIntoEngineAndRaisesUpdated()
    {
        var (session, engines, ptys) = await StartAsync();
        using (session)
        {
            var updated = 0;
            session.Updated += (_, _) => Interlocked.Increment(ref updated);

            await ptys.Connection.EmitAsync("hello");

            await WaitForAsync(() => engines.Created[0].Text == "hello");
            await WaitForAsync(() => Volatile.Read(ref updated) > 0);
        }
    }

    [Fact]
    public async Task Exited_IsForwardedWithExitCode()
    {
        var (session, _, ptys) = await StartAsync();
        using (session)
        {
            int? code = null;
            session.Exited += (_, c) => code = c;

            ptys.Connection.RaiseExited(7);

            Assert.Equal(7, code);
        }
    }

    [Fact]
    public async Task SendInput_WritesBytesToProcess()
    {
        var (session, _, ptys) = await StartAsync();
        using (session)
        {
            session.SendInput("ls\r"u8);

            Assert.Equal("ls\r", Encoding.UTF8.GetString(ptys.Connection.InputBuffer.ToArray()));
        }
    }

    [Fact]
    public async Task Resize_UpdatesEngineAndPty()
    {
        var (session, engines, ptys) = await StartAsync();
        using (session)
        {
            session.Resize(120, 40);

            Assert.Equal((120, 40), (engines.Created[0].Columns, engines.Created[0].Rows));
            Assert.Equal((120, 40), ptys.Connection.LastResize);
        }
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    public async Task Resize_RejectsNonPositiveSize(int columns, int rows)
    {
        var (session, _, _) = await StartAsync();
        using (session)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Resize(columns, rows));
        }
    }

    [Fact]
    public async Task OutputEnding_StopsPumpWithoutError()
    {
        var (session, engines, ptys) = await StartAsync();
        using (session)
        {
            await ptys.Connection.EmitAsync("x");
            await ptys.Connection.CloseOutputAsync();

            await WaitForAsync(() => engines.Created[0].Text == "x");
        }
    }

    [Fact]
    public async Task Dispose_KillsProcessDisposesEverythingAndIsIdempotent()
    {
        var (session, engines, ptys) = await StartAsync();

        session.Dispose();
        session.Dispose();

        Assert.True(ptys.Connection.Killed);
        Assert.True(ptys.Connection.Disposed);
        Assert.True(engines.Created[0].IsDisposed);
    }

    [Fact]
    public async Task AfterDispose_OperationsThrow()
    {
        var (session, _, _) = await StartAsync();
        session.Dispose();

        Assert.Throws<ObjectDisposedException>(() => session.SendInput("a"u8));
        Assert.Throws<ObjectDisposedException>(() => session.Resize(10, 10));
    }

    [Fact]
    public async Task StartAsync_WhenProcessFails_DisposesEngineAndRethrows()
    {
        var engines = new FakeTerminalEngineFactory();
        var ptys = new FakePtyConnectionFactory { FailWith = new InvalidOperationException("boom") };

        await Assert.ThrowsAsync<InvalidOperationException>(() => TerminalSession.StartAsync(engines, ptys, Options));

        Assert.True(engines.Created[0].IsDisposed);
    }

    [Fact]
    public async Task StartAsync_ValidatesArguments()
    {
        var engines = new FakeTerminalEngineFactory();
        var ptys = new FakePtyConnectionFactory();

        await Assert.ThrowsAsync<ArgumentNullException>(() => TerminalSession.StartAsync(null!, ptys, Options));
        await Assert.ThrowsAsync<ArgumentNullException>(() => TerminalSession.StartAsync(engines, null!, Options));
        await Assert.ThrowsAsync<ArgumentNullException>(() => TerminalSession.StartAsync(engines, ptys, null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => TerminalSession.StartAsync(engines, ptys, new PtyStartOptions("a", 0, 1)));
    }

    [Fact]
    public async Task SyncRoot_GuardsEngine()
    {
        var (session, _, _) = await StartAsync();
        using (session)
        {
            Assert.NotNull(session.SyncRoot);
        }
    }
}
