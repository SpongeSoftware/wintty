using Wintty.Core.Sessions;
using Xunit;

namespace Wintty.Tests.Core;

public class ResizeDebouncerTests
{
    /// <summary>A clock whose timers fire only when the test advances it.</summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan by)
        {
            _now += by;
            foreach (var timer in _timers.ToList())
            {
                timer.FireIfDue(_now);
            }
        }

        private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
        {
            private DateTimeOffset? _due;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                _due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
                return true;
            }

            public void FireIfDue(DateTimeOffset now)
            {
                if (_due is { } due && due <= now)
                {
                    _due = null;
                    callback(state);
                }
            }

            public void Dispose() => _due = null;

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(50);

    [Fact]
    public void ABurstOfRequests_AppliesOnlyTheLastSizeAfterTheDelay()
    {
        var clock = new ManualTimeProvider();
        var applied = new List<(int, int)>();
        using var debouncer = new ResizeDebouncer(Delay, (c, r) => applied.Add((c, r)), timeProvider: clock);

        debouncer.Request(80, 24);
        clock.Advance(TimeSpan.FromMilliseconds(30));
        debouncer.Request(90, 30);
        clock.Advance(TimeSpan.FromMilliseconds(30));
        debouncer.Request(100, 40);
        Assert.Empty(applied);

        clock.Advance(Delay);

        Assert.Equal([(100, 40)], applied);
    }

    [Fact]
    public void ApplyNow_AppliesImmediatelyAndCancelsWhatWasWaiting()
    {
        var clock = new ManualTimeProvider();
        var applied = new List<(int, int)>();
        using var debouncer = new ResizeDebouncer(Delay, (c, r) => applied.Add((c, r)), timeProvider: clock);
        debouncer.Request(80, 24);

        debouncer.ApplyNow(120, 50);
        clock.Advance(Delay * 2);

        Assert.Equal([(120, 50)], applied);
    }

    [Fact]
    public void AfterDispose_NothingIsApplied()
    {
        var clock = new ManualTimeProvider();
        var applied = new List<(int, int)>();
        var debouncer = new ResizeDebouncer(Delay, (c, r) => applied.Add((c, r)), timeProvider: clock);
        debouncer.Request(80, 24);

        debouncer.Dispose();
        debouncer.Request(10, 10);
        debouncer.ApplyNow(10, 10);
        clock.Advance(Delay * 2);

        Assert.Empty(applied);
    }

    [Fact]
    public void AFailingApply_IsReportedAndTheNextRequestStillWorks()
    {
        var clock = new ManualTimeProvider();
        var errors = new List<Exception>();
        var calls = 0;
        using var debouncer = new ResizeDebouncer(
            Delay,
            (_, _) =>
            {
                calls++;
                if (calls == 1)
                {
                    throw new InvalidOperationException("boom");
                }
            },
            errors.Add,
            clock);

        debouncer.Request(1, 1);
        clock.Advance(Delay);
        debouncer.Request(2, 2);
        clock.Advance(Delay);

        Assert.Equal(2, calls);
        Assert.Equal("boom", Assert.Single(errors).Message);
    }

    [Fact]
    public void BadArguments_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ResizeDebouncer(Delay, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResizeDebouncer(TimeSpan.FromSeconds(-1), (_, _) => { }));
        using var debouncer = new ResizeDebouncer(Delay, (_, _) => { });
        Assert.Throws<ArgumentOutOfRangeException>(() => debouncer.Request(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => debouncer.Request(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => debouncer.ApplyNow(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => debouncer.ApplyNow(1, 0));
    }

    [Fact]
    public void WithTheSystemClock_TheLastSizeIsEventuallyApplied()
    {
        using var applied = new ManualResetEventSlim();
        (int, int) last = default;
        using var debouncer = new ResizeDebouncer(TimeSpan.FromMilliseconds(20), (c, r) =>
        {
            last = (c, r);
            applied.Set();
        });

        debouncer.Request(70, 20);
        debouncer.Request(71, 21);

        Assert.True(applied.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal((71, 21), last);
    }
}
