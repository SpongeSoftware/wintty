using System.Diagnostics;
using System.Text;
using Wintty.Core.Abstractions;
using Wintty.Ghostty;
using Wintty.Render;
using Xunit;
using Xunit.Abstractions;

namespace Wintty.Tests.Ghostty;

/// <summary>
/// A reproducible benchmark of the path a frame takes: feeding output to libghostty, snapshotting the screen and
/// planning the draw. The budgets are deliberately very loose (CI runs these under coverage instrumentation) so the tests only fail on a gross regression; the printed
/// numbers are what is recorded in the documentation.
/// </summary>
[Trait("Category", "Integration")]
[Collection("NativeEnvironment")]
public class GhosttyPerformanceTests(ITestOutputHelper output)
{
    private static readonly GhosttyGateway Gateway = new();

    // A screenful of coloured, varied text, repeated: roughly what "cat large-file" or a build log produces.
    private static byte[] BuildOutput(int megabytes)
    {
        var line = new StringBuilder();
        for (var i = 0; i < 12; i++)
        {
            line.Append("\u001b[3").Append(i % 7 + 1).Append("mword").Append(i).Append("\u001b[0m ");
        }

        line.Append("The quick brown fox jumps over the lazy dog 0123456789 éè世界\r\n");
        var bytes = Encoding.UTF8.GetBytes(line.ToString());
        var buffer = new MemoryStream();
        while (buffer.Length < megabytes * 1024L * 1024L)
        {
            buffer.Write(bytes);
        }

        return buffer.ToArray();
    }

    [Fact]
    public void FeedingLargeOutput_RunsAtAHighRate()
    {
        using var engine = Gateway.Create(new TerminalEngineOptions(200, 50, 10_000));
        var data = BuildOutput(32);

        var watch = Stopwatch.StartNew();
        for (var offset = 0; offset < data.Length; offset += 16 * 1024)
        {
            engine.Write(data.AsSpan(offset, Math.Min(16 * 1024, data.Length - offset)));
        }

        watch.Stop();
        var rate = data.Length / 1024.0 / 1024.0 / watch.Elapsed.TotalSeconds;
        output.WriteLine($"feed: {data.Length / 1024 / 1024} MiB in {watch.ElapsedMilliseconds} ms = {rate:F0} MiB/s");

        Assert.True(rate > 1, $"only {rate:F1} MiB/s");
    }

    [Fact]
    public void SnapshotAndPlan_FitInAFrameOnALargeScreen()
    {
        using var engine = Gateway.Create(new TerminalEngineOptions(240, 70, 10_000));
        engine.Write(BuildOutput(2));

        const int frames = 200;
        var snapshotTicks = 0L;
        var planTicks = 0L;
        var worstMilliseconds = 0.0;
        for (var frame = 0; frame < frames; frame++)
        {
            // Touching the screen forces a real snapshot; an unchanged screen is served from the cache.
            engine.Write("[H"u8);
            var start = Stopwatch.GetTimestamp();
            var snapshot = engine.CreateSnapshot();
            var middle = Stopwatch.GetTimestamp();
            _ = RenderPlanner.Build(snapshot);
            var end = Stopwatch.GetTimestamp();
            snapshotTicks += middle - start;
            planTicks += end - middle;
            worstMilliseconds = Math.Max(worstMilliseconds, Stopwatch.GetElapsedTime(start, end).TotalMilliseconds);
        }

        var snapshotMilliseconds = Stopwatch.GetElapsedTime(0, snapshotTicks).TotalMilliseconds / frames;
        var planMilliseconds = Stopwatch.GetElapsedTime(0, planTicks).TotalMilliseconds / frames;
        output.WriteLine($"240x70 screen: snapshot {snapshotMilliseconds:F2} ms, plan {planMilliseconds:F2} ms per frame, worst {worstMilliseconds:F2} ms");

        // Coverage instrumentation slows this path several times over in CI, so the budget only catches gross regressions.
        Assert.True(snapshotMilliseconds + planMilliseconds < 60, $"{snapshotMilliseconds + planMilliseconds:F2} ms per frame");
    }

    [Fact]
    public void FramesTakenWhileOutputArrives_StayWithinTheFrameBudget()
    {
        using var engine = Gateway.Create(new TerminalEngineOptions(120, 40, 10_000));
        var data = BuildOutput(8);
        var worst = 0.0;
        var frames = 0;
        for (var offset = 0; offset < data.Length; offset += 64 * 1024)
        {
            engine.Write(data.AsSpan(offset, Math.Min(64 * 1024, data.Length - offset)));
            var start = Stopwatch.GetTimestamp();
            _ = RenderPlanner.Build(engine.CreateSnapshot());
            worst = Math.Max(worst, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            frames++;
        }

        output.WriteLine($"during output: {frames} frames, worst frame preparation {worst:F2} ms");
        Assert.True(worst < 250, $"worst frame {worst:F1} ms");
    }

    [Fact]
    public void AnUnchangedScreen_ReusesItsSnapshot()
    {
        using var engine = Gateway.Create(new TerminalEngineOptions(40, 10, 100));
        engine.Write("hello"u8);

        var first = engine.CreateSnapshot();

        Assert.Same(first, engine.CreateSnapshot());
        engine.Write(ReadOnlySpan<byte>.Empty);
        Assert.Same(first, engine.CreateSnapshot());
    }

    [Fact]
    public void AnythingThatChangesTheScreen_ProducesAFreshSnapshot()
    {
        using var engine = Gateway.Create(new TerminalEngineOptions(40, 10, 100));
        engine.Write(Encoding.UTF8.GetBytes(string.Join("\r\n", Enumerable.Range(0, 30).Select(i => "line" + i))));
        var seen = new List<TerminalSnapshot> { engine.CreateSnapshot() };

        void Change(Action action)
        {
            action();
            var snapshot = engine.CreateSnapshot();
            Assert.DoesNotContain(seen, s => ReferenceEquals(s, snapshot));
            seen.Add(snapshot);
        }

        Change(() => engine.Write("x"u8));
        Change(() => engine.Resize(30, 8));
        Change(() => engine.ScrollViewport(-3));
        Change(engine.ScrollViewportToBottom);
        Change(() => engine.SetColors(new TerminalColorScheme(Foreground: new TerminalColor(1, 2, 3))));
        Change(() => engine.SetCellSize(9, 18));
        Assert.Equal("line29x", engine.CreateSnapshot().GetRowText(7));
    }
}
