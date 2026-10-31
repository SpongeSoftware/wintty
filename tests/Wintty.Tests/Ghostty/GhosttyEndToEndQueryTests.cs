using Wintty.Core.Abstractions;
using Wintty.Core.Sessions;
using Wintty.Ghostty;
using Xunit;

namespace Wintty.Tests.Ghostty;

/// <summary>
/// A real console program sends terminal queries through ConPTY and must receive libghostty's replies through
/// <see cref="TerminalSession"/>. Uses Git's bash, which ships with Git for Windows; the test passes vacuously
/// when it is not installed.
/// </summary>
[Trait("Category", "Integration")]
[Collection("NativeEnvironment")]
public class GhosttyEndToEndQueryTests
{
    private static readonly string[] BashPaths =
    [
        @"C:\Program Files\Git\usr\bin\bash.exe",
        @"C:\Program Files (x86)\Git\usr\bin\bash.exe",
    ];

    [Fact]
    public async Task ProgramsReceiveRepliesToTheirQueries()
    {
        var bash = BashPaths.FirstOrDefault(File.Exists);
        if (bash is null)
        {
            return;
        }

        // Each query is sent, then up to 40 reply bytes are read back and printed in a recognisable form.
        const string script =
            "for q in '\\033[?u' '\\033[c' '\\033[>q' '\\033[?2026$p'; do " +
            "printf \"$q\"; IFS= read -rsn 40 -t 5 r; printf 'REPLY[%s]=%q\\n' \"$q\" \"$r\"; done";
        var gateway = new GhosttyGateway();
        var options = new PtyStartOptions(bash, 100, 30) { Arguments = ["-c", script] };

        using var session = await TerminalSession.StartAsync(gateway, gateway, options);
        session.SetCellSize(9, 19);

        var text = string.Empty;
        var deadline = DateTime.UtcNow.AddSeconds(40);
        while (DateTime.UtcNow < deadline && !text.Contains("REPLY[\\033[?2026$p]", StringComparison.Ordinal))
        {
            await Task.Delay(250);
            lock (session.SyncRoot)
            {
                var snapshot = session.Engine.CreateSnapshot();
                text = string.Join("\n", Enumerable.Range(0, snapshot.Rows).Select(snapshot.GetRowText));
            }
        }

        Assert.Contains("REPLY[\\033[?u]=$'\\E[?0u'", text);
        Assert.Contains("REPLY[\\033[c]=$'\\E[?62;22c'", text);
        Assert.Contains("libghostty", text);
        Assert.Contains("$'\\E[?2026;2$y'", text);
    }
}
