using System.Threading;
using StandupReminder.Core.Services;

namespace StandupReminder.Windows;

public sealed class SynchronizationContextReminderTickSource : IReminderTickSource
{
    private readonly SynchronizationContext _synchronizationContext;
    private readonly System.Threading.Timer _timer;
    private bool _disposed;

    public SynchronizationContextReminderTickSource(SynchronizationContext? synchronizationContext = null)
    {
        _synchronizationContext = synchronizationContext ?? SynchronizationContext.Current
            ?? throw new InvalidOperationException("A UI SynchronizationContext is required to create the reminder tick source.");
        _timer = new System.Threading.Timer(OnTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public event EventHandler? Tick;

    public bool IsRunning { get; private set; }

    public void Start(TimeSpan interval)
    {
        ThrowIfDisposed();

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "The reminder tick interval must be greater than zero.");
        }

        _timer.Change(interval, interval);
        IsRunning = true;
    }

    public void Stop()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        IsRunning = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _timer.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnTimerElapsed(object? state)
    {
        _ = state;
        _synchronizationContext.Post(_ => Tick?.Invoke(this, EventArgs.Empty), null);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
