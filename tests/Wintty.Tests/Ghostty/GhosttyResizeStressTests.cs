using Wintty.Core.Abstractions;
using Wintty.Core.Sessions;
using Wintty.Ghostty;
using Xunit;

namespace Wintty.Tests.Ghostty;

/// <summary>
/// Resizes a live ConPTY session over and over while a program prints, then checks the engine, the
/// pseudo-console and the program all agree on the final size. Uses bash from Git for Windows and passes
/// vacuously when it is not installed.
/// </summary>
[Trait("Category", "Integration")]
[Collection("NativeEnvironment")]
public class GhosttyResizeStressTests
{
    private static readonly string[] BashPaths =
    [
        @"C:\Program Files\Git\usr\bin\bash.exe",
        @"C:\Program Files (x86)\Git\usr\bin\bash.exe",
    ];

    [Fact]
    public async Task RepeatedResizesWhileOutputIsPrinting_NeverThrowAndEndConsistent()
    {
        var bash = BashPaths.FirstOrDefault(File.Exists);
        if (bash is null)
        {
            return;
        }

        const string script = "for i in $(seq 1 300); do echo \"row $i\"; sleep 0.01; done; sleep 1; echo \"SIZE=$(stty size)\"; echo DONE";
        var gateway = new GhosttyGateway();
        var options = new PtyStartOptions(bash, 100, 30) { Arguments = ["-c", script] };

        using var session = await TerminalSession.StartAsync(gateway, gateway, options);
        session.SetCellSize(9, 19);

        var random = new Random(7);
        for (var step = 0; step < 80; step++)
        {
            session.Resize(random.Next(20, 160), random.Next(5, 50));
            await Task.Delay(random.Next(0, 8));
        }

        session.Resize(90, 25);

        var text = string.Empty;
        var deadline = DateTime.UtcNow.AddSeconds(40);
        while (DateTime.UtcNow < deadline && !text.Contains("DONE", StringComparison.Ordinal))
        {
            await Task.Delay(250);
            lock (session.SyncRoot)
            {
                var snapshot = session.Engine.CreateSnapshot();
                text = string.Join("\n", Enumerable.Range(0, snapshot.Rows).Select(snapshot.GetRowText));
            }
        }

        Assert.Contains("DONE", text);
        Assert.Contains("SIZE=25 90", text);
        lock (session.SyncRoot)
        {
            Assert.Equal(90, session.Engine.Columns);
            Assert.Equal(25, session.Engine.Rows);
            var snapshot = session.Engine.CreateSnapshot();
            Assert.Equal(90, snapshot.Columns);
            Assert.Equal(25, snapshot.Rows);
        }
    }
}
