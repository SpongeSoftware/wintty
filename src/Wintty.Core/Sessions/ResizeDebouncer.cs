namespace Wintty.Core.Sessions;

/// <summary>
/// Collapses a burst of resize requests (a window being dragged larger or smaller) into one resize shortly
/// after the burst ends. ConPTY re-sends the whole screen on every resize, so applying each intermediate size
/// makes full-screen programs flicker and can leave stale rows behind.
/// </summary>
public sealed class ResizeDebouncer : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly Action<int, int> _apply;
    private readonly Action<Exception>? _onError;
    private readonly ITimer _timer;
    private readonly object _gate = new();
    private (int Columns, int Rows)? _pending;
    private bool _disposed;

    /// <summary>Creates a debouncer.</summary>
    /// <param name="delay">How long the size must stay unchanged before it is applied.</param>
    /// <param name="apply">Applies a size. Calls never overlap.</param>
    /// <param name="onError">Receives exceptions thrown by <paramref name="apply"/> on the timer thread.</param>
    /// <param name="timeProvider">Clock used for the delay; the system clock when omitted.</param>
    public ResizeDebouncer(TimeSpan delay, Action<int, int> apply, Action<Exception>? onError = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        _delay = delay;
        _apply = apply;
        _onError = onError;
        _timer = (timeProvider ?? TimeProvider.System).CreateTimer(OnTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Schedules a size; a newer request before the delay passes replaces it.</summary>
    /// <param name="columns">Width in cells.</param>
    /// <param name="rows">Height in cells.</param>
    public void Request(int columns, int rows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(columns, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rows, 0);
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _pending = (columns, rows);
            _timer.Change(_delay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Applies a size straight away and drops any request still waiting.</summary>
    /// <param name="columns">Width in cells.</param>
    /// <param name="rows">Height in cells.</param>
    public void ApplyNow(int columns, int rows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(columns, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rows, 0);
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _pending = null;
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _apply(columns, rows);
        }
    }

    /// <summary>Stops the timer; a request still waiting is dropped.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _pending = null;
        }

        _timer.Dispose();
    }

    private void OnTimer(object? state)
    {
        lock (_gate)
        {
            if (_disposed || _pending is not var (columns, rows))
            {
                return;
            }

            _pending = null;
            try
            {
                _apply(columns, rows);
            }
            catch (Exception ex) when (_onError is not null)
            {
                _onError(ex);
            }
        }
    }
}
