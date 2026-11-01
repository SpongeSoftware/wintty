namespace Wintty.Core.Configuration;

/// <summary>
/// Watches a config file and reloads it after changes settle. Editors typically write a file several times in
/// quick succession (truncate, write, rename), so notifications are debounced into a single reload.
/// </summary>
public sealed class ConfigWatcher : IDisposable
{
    private readonly string _path;
    private readonly string? _themesDirectory;
    private readonly Action<ConfigLoadResult> _onReload;
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _timer;
    private readonly TimeSpan _debounce;
    private int _disposed;

    /// <summary>Starts watching.</summary>
    /// <param name="path">The config file; its folder is created if missing.</param>
    /// <param name="debounce">How long changes must stop before the file is reloaded.</param>
    /// <param name="onReload">Called, on a thread-pool thread, with every reload result.</param>
    /// <param name="themesDirectory">Folder of user theme files used to resolve <c>theme</c>, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="onReload"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="debounce"/> is negative.</exception>
    public ConfigWatcher(string path, TimeSpan debounce, Action<ConfigLoadResult> onReload, string? themesDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(onReload);
        ArgumentOutOfRangeException.ThrowIfLessThan(debounce, TimeSpan.Zero);

        _path = Path.GetFullPath(path);
        _debounce = debounce;
        _onReload = onReload;
        _themesDirectory = themesDirectory;
        string directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);

        _timer = new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);
        _watcher = new FileSystemWatcher(directory, Path.GetFileName(_path))
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
        };
        _watcher.Changed += (_, _) => Schedule();
        _watcher.Created += (_, _) => Schedule();
        _watcher.Deleted += (_, _) => Schedule();
        _watcher.Renamed += (_, _) => Schedule();
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>Stops watching. Pending reloads are dropped.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _watcher.Dispose();
        _timer.Dispose();
    }

    private void Schedule()
    {
        try
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
            }
        }
        catch (ObjectDisposedException)
        {
            // Disposed between the check and the call; nothing left to reload.
        }
    }

    private void Reload()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        _onReload(ConfigLoader.Load(_path, _themesDirectory));
    }
}
