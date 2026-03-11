using StandupReminder.Core.Models;

namespace StandupReminder.Core.Services;

public sealed class ReminderSchedulerRuntime : IDisposable, IReminderSessionEventSink
{
    public static readonly TimeSpan DefaultTickInterval = TimeSpan.FromSeconds(1);

    private readonly IReminderTickSource _tickSource;
    private readonly ReminderSchedulerEngine _engine;
    private bool _disposed;

    public ReminderSchedulerRuntime(
        ReminderScheduleOptions options,
        string promptBackgroundArgbHex,
        IReminderTrayNotifier trayNotifier,
        IReminderPromptHost promptHost,
        IReminderTickSource tickSource,
        Func<DateTimeOffset>? clock = null)
    {
        _tickSource = tickSource;
        _engine = new ReminderSchedulerEngine(options, promptBackgroundArgbHex, trayNotifier, promptHost, clock);
        _tickSource.Tick += OnTickSourceTick;
        _engine.StateChanged += OnEngineStateChanged;
    }

    public event EventHandler? StateChanged;

    public ReminderPhase Phase => _engine.Phase;

    public TimeSpan RemainingTime => _engine.RemainingTime;

    public bool IsPaused => _engine.IsPaused;

    public bool IsManuallyPaused => _engine.IsManuallyPaused;

    public bool IsTimerRunning => _engine.IsTimerRunning;

    public void Start()
    {
        ThrowIfDisposed();
        _engine.Start();
    }

    public void PauseTimer()
    {
        ThrowIfDisposed();
        _engine.PauseTimer();
    }

    public void ResumeTimer()
    {
        ThrowIfDisposed();
        _engine.ResumeTimer();
    }

    public void UpdateOptions(ReminderScheduleOptions options)
    {
        ThrowIfDisposed();
        _engine.UpdateOptions(options);
    }

    public void UpdatePromptBackground(string backgroundArgbHex)
    {
        ThrowIfDisposed();
        _engine.UpdatePromptBackground(backgroundArgbHex);
    }

    public void HandleSessionEvent(ReminderSessionEvent sessionEvent)
    {
        ThrowIfDisposed();
        _engine.HandleSessionEvent(sessionEvent);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tickSource.Stop();
        _tickSource.Tick -= OnTickSourceTick;
        _engine.StateChanged -= OnEngineStateChanged;
        _engine.Dispose();
        _tickSource.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnTickSourceTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _engine.Tick();
    }

    private void OnEngineStateChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_engine.IsTimerRunning)
        {
            _tickSource.Start(DefaultTickInterval);
        }
        else
        {
            _tickSource.Stop();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
