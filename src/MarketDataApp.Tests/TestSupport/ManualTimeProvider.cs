namespace MarketDataApp.Tests.TestSupport;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock only moves when <see cref="Advance"/> is called and
/// whose timers fire deterministically as time passes. Lets tests drive the SDK's fixed 99s
/// request-timeout (a <see cref="System.Threading.CancellationTokenSource"/> backed by a
/// <see cref="TimeProvider"/>) without any real waiting.
/// </summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = new();
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    public void Advance(TimeSpan delta)
    {
        ManualTimer[] snapshot;
        DateTimeOffset now;
        lock (_gate)
        {
            _now += delta;
            now = _now;
            snapshot = _timers.ToArray();
        }

        foreach (var timer in snapshot)
        {
            timer.FireIfDue(now);
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state, dueTime, period);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    private void Remove(ManualTimer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _provider;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private DateTimeOffset? _dueAt;
        private TimeSpan _period;

        public ManualTimer(
            ManualTimeProvider provider,
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            _provider = provider;
            _callback = callback;
            _state = state;
            Change(dueTime, period);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (_provider._gate)
            {
                _dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : _provider._now + dueTime;
                _period = period;
            }

            return true;
        }

        public void FireIfDue(DateTimeOffset now)
        {
            var fire = false;
            lock (_provider._gate)
            {
                if (_dueAt is { } due && due <= now)
                {
                    fire = true;
                    _dueAt = _period <= TimeSpan.Zero || _period == Timeout.InfiniteTimeSpan
                        ? null
                        : now + _period;
                }
            }

            if (fire)
            {
                _callback(_state);
            }
        }

        public void Dispose() => _provider.Remove(this);

        public ValueTask DisposeAsync()
        {
            _provider.Remove(this);
            return ValueTask.CompletedTask;
        }
    }
}
