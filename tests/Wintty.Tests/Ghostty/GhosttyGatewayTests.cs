using System.Text;
using Wintty.Core.Abstractions;
using Wintty.Core.Sessions;
using Wintty.Ghostty;
using Xunit;

namespace Wintty.Tests.Ghostty;

/// <summary>Exercises the real native library through the gateway (Windows x64 only).</summary>
[Trait("Category", "Integration")]
[Collection("NativeEnvironment")]
public class GhosttyGatewayTests
{
    private static readonly GhosttyGateway Gateway = new();

    [Fact]
    public void Create_ReturnsEngineWithRequestedSize()
    {
        using var engine = Gateway.Create(new TerminalEngineOptions(100, 30, 1000));

        Assert.Equal(100, engine.Columns);
        Assert.Equal(30, engine.Rows);
        Assert.Null(engine.Title);
    }

    [Fact]
    public void Create_RejectsNullAndInvalidOptions()
    {
        Assert.Throws<ArgumentNullException>(() => Gateway.Create(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => Gateway.Create(new TerminalEngineOptions(0, 10)));
    }

    [Fact]
    public void Write_OscTitleSequence_SetsTitle()
    {
        using var engine = Gateway.Create(new TerminalEngineOptions(80, 24));

        engine.Write("\u001b]0;wintty test\u0007"u8);

        Assert.Equal("wintty test", engine.Title);
    }

    [Fact]
    public void Write_EmptyData_IsIgnored()
    {
        using var engine = Gateway.Create(new TerminalEngineOptions(80, 24));

        engine.Write(ReadOnlySpan<byte>.Empty);

        Assert.Null(engine.Title);
    }

    [Fact]
    public void Resize_UpdatesDimensionsAndValidates()
    {
        using var engine = Gateway.Create(new TerminalEngineOptions(80, 24));

        engine.Resize(132, 43);

        Assert.Equal(132, engine.Columns);
        Assert.Equal(43, engine.Rows);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Resize(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Resize(10, 0));
    }

    [Fact]
    public void Dispose_IsIdempotentAndBlocksFurtherUse()
    {
        var engine = Gateway.Create(new TerminalEngineOptions(80, 24));

        engine.Dispose();
        engine.Dispose();

        Assert.Throws<ObjectDisposedException>(() => engine.Write("a"u8));
        Assert.Throws<ObjectDisposedException>(() => engine.Resize(10, 10));
        Assert.Throws<ObjectDisposedException>(() => _ = engine.Title);
    }

    [Fact]
    public async Task StartAsync_RunsProcessAndReportsExit()
    {
        var comspec = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
        var options = new PtyStartOptions(comspec, 80, 24) { Arguments = ["/d", "/c", "echo wintty-pty-ok"] };

        using var connection = await Gateway.StartAsync(options);
        using var output = new MemoryStream();
        var exited = new TaskCompletionSource<int>();
        connection.Exited += (_, code) => exited.TrySetResult(code);

        var buffer = new byte[4096];
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!Encoding.UTF8.GetString(output.ToArray()).Contains("wintty-pty-ok") && DateTime.UtcNow < deadline)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var read = await connection.Output.ReadAsync(buffer, cts.Token);
            if (read == 0)
            {
                break;
            }

            output.Write(buffer, 0, read);
        }

        Assert.Contains("wintty-pty-ok", Encoding.UTF8.GetString(output.ToArray()));
        Assert.True(connection.ProcessId > 0);
        Assert.Equal(0, await exited.Task.WaitAsync(TimeSpan.FromSeconds(15)));
        connection.Resize(100, 40);
        connection.Kill();
    }

    [Fact]
    public async Task StartAsync_ValidatesOptions()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Gateway.StartAsync(null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Gateway.StartAsync(new PtyStartOptions("cmd.exe", 0, 0)));
    }

    [Fact]
    public async Task Session_FeedsRealProcessOutputIntoRealEngine()
    {
        var comspec = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
        var options = new PtyStartOptions(comspec, 80, 24)
        {
            Arguments = ["/d", "/c", "title wintty-session-title"],
        };

        using var session = await TerminalSession.StartAsync(Gateway, Gateway, options);
        var deadline = DateTime.UtcNow.AddSeconds(15);
        string? title = null;
        while (DateTime.UtcNow < deadline)
        {
            lock (session.SyncRoot)
            {
                title = session.Engine.Title;
            }

            if (title is not null)
            {
                break;
            }

            await Task.Delay(50);
        }

        Assert.Contains("wintty-session-title", title ?? string.Empty);
    }
}
