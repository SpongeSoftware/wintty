using System.Collections.Concurrent;
using Wintty.Core.Configuration;
using Xunit;

namespace Wintty.Tests.Configuration;

public sealed class ConfigWatcherTests : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wintty-watch-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static bool WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(20);
        }

        return condition();
    }

    [Fact]
    public void Constructor_ValidatesArguments()
    {
        Assert.Throws<ArgumentException>(() => new ConfigWatcher(" ", TimeSpan.Zero, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new ConfigWatcher(Path.Combine(_directory, "c"), TimeSpan.Zero, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConfigWatcher(Path.Combine(_directory, "c"), TimeSpan.FromSeconds(-1), _ => { }));
    }

    [Fact]
    public void Watcher_CreatesMissingFolderAndReportsNewFile()
    {
        var path = Path.Combine(_directory, "nested", "config");
        var results = new ConcurrentQueue<ConfigLoadResult>();
        using var watcher = new ConfigWatcher(path, TimeSpan.FromMilliseconds(50), results.Enqueue);

        Assert.True(Directory.Exists(Path.GetDirectoryName(path)));
        File.WriteAllText(path, "font-size = 19");

        Assert.True(WaitFor(() => results.Any(r => r.Config.FontSize == 19f)));
    }

    [Fact]
    public void Watcher_BurstOfWritesProducesOneReloadWithTheFinalContent()
    {
        var path = Path.Combine(_directory, "config");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "font-size = 10");
        var results = new ConcurrentQueue<ConfigLoadResult>();
        using var watcher = new ConfigWatcher(path, TimeSpan.FromMilliseconds(400), results.Enqueue);

        for (var size = 11; size <= 15; size++)
        {
            File.WriteAllText(path, $"font-size = {size}");
        }

        Assert.True(WaitFor(() => !results.IsEmpty));
        Thread.Sleep(800);
        var only = Assert.Single(results);
        Assert.Equal(15f, only.Config.FontSize);
    }

    [Fact]
    public void Watcher_DeletedFileFallsBackToDefaults()
    {
        var path = Path.Combine(_directory, "config");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "font-size = 10");
        var results = new ConcurrentQueue<ConfigLoadResult>();
        using var watcher = new ConfigWatcher(path, TimeSpan.FromMilliseconds(50), results.Enqueue);

        File.Delete(path);

        Assert.True(WaitFor(() => results.Any(r => r.Config == new WinttyConfig())));
    }

    [Fact]
    public void Watcher_InvalidEditStillDeliversTheValidLinesAndDiagnostics()
    {
        var path = Path.Combine(_directory, "config");
        var results = new ConcurrentQueue<ConfigLoadResult>();
        using var watcher = new ConfigWatcher(path, TimeSpan.FromMilliseconds(50), results.Enqueue);

        File.WriteAllText(path, "font-size = 17\nnonsense");

        Assert.True(WaitFor(() => results.Any(r => r.Config.FontSize == 17f && r.Diagnostics.Count == 1)));
    }

    [Fact]
    public void Dispose_StopsReloadsAndIsIdempotent()
    {
        var path = Path.Combine(_directory, "config");
        var count = 0;
        var watcher = new ConfigWatcher(path, TimeSpan.FromMilliseconds(100), _ => Interlocked.Increment(ref count));

        File.WriteAllText(path, "font-size = 12");
        watcher.Dispose();
        watcher.Dispose();
        Thread.Sleep(400);

        Assert.Equal(0, count);
    }
}
