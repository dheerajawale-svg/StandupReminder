using StandupReminder.Core.Models;

namespace StandupReminder.Core.Services;

public sealed class ReminderSchedulerEngine : IDisposable, IReminderSessionEventSink
{
    private static readonly TimeSpan SnoozeDuration = TimeSpan.FromMinutes(5);

    private readonly IReminderTrayNotifier _trayNotifier;
    private readonly IReminderPromptHost _promptHost;
    private readonly Func<DateTimeOffset> _clock;
    private ReminderScheduleOptions _options;
    private string _promptBackgroundArgbHex;
    private ReminderPhase _phase = ReminderPhase.Idle;
    private ReminderPhase _phaseBeforePause = ReminderPhase.Idle;
    private ReminderPhase _phaseBeforeManualPause = ReminderPhase.Idle;
    private DateTimeOffset _phaseEndsAt;
    private TimeSpan _remainingTime = TimeSpan.Zero;
    private bool _hasStarted;
    private bool _disposed;

    public ReminderSchedulerEngine(
        ReminderScheduleOptions options,
        string promptBackgroundArgbHex,
        IReminderTrayNotifier trayNotifier,
        IReminderPromptHost promptHost,
        Func<DateTimeOffset>? clock = null)
    {
        _options = CloneOptions(options);
        _promptBackgroundArgbHex = promptBackgroundArgbHex ?? string.Empty;
        _trayNotifier = trayNotifier;
        _promptHost = promptHost;
        _clock = clock ?? (() => DateTimeOffset.Now);

        _promptHost.Confirmed += OnPromptConfirmed;
        _promptHost.Snoozed += OnPromptSnoozed;
    }

    public event EventHandler? StateChanged;

    public ReminderPhase Phase => _phase;

    public TimeSpan RemainingTime => _remainingTime < TimeSpan.Zero ? TimeSpan.Zero : _remainingTime;

    public bool IsPaused => _phase is ReminderPhase.PausedForLock or ReminderPhase.PausedManually;

    public bool IsManuallyPaused => _phase == ReminderPhase.PausedManually;

    public bool IsTimerRunning => _phase is ReminderPhase.SittingCountdown or ReminderPhase.SnoozedCountdown or ReminderPhase.StandingCountdown;

    public void Start()
    {
        ThrowIfDisposed();

        if (_hasStarted)
        {
            return;
        }

        _hasStarted = true;
        BeginSittingCountdown(isInitial: true);
    }

    public void Tick()
    {
        ThrowIfDisposed();

        if (!IsTimerRunning)
        {
            return;
        }

        CaptureRemainingTime();
        RaiseStateChanged();

        if (_remainingTime <= TimeSpan.Zero)
        {
            CompleteCurrentPhase();
        }
    }

    public void PauseTimer()
    {
        ThrowIfDisposed();

        if (_phase is ReminderPhase.Idle or ReminderPhase.PausedForLock or ReminderPhase.PausedManually)
        {
            return;
        }

        _phaseBeforeManualPause = _phase;

        switch (_phase)
        {
            case ReminderPhase.SittingCountdown:
            case ReminderPhase.SnoozedCountdown:
            case ReminderPhase.StandingCountdown:
                CaptureRemainingTime();
                _phase = ReminderPhase.PausedManually;
                RaiseStateChanged();
                break;

            case ReminderPhase.StandPromptPending:
                _remainingTime = TimeSpan.Zero;
                _promptHost.DismissForPause();
                _phase = ReminderPhase.PausedManually;
                RaiseStateChanged();
                break;
        }
    }

    public void ResumeTimer()
    {
        ThrowIfDisposed();

        if (_phase != ReminderPhase.PausedManually)
        {
            return;
        }

        switch (_phaseBeforeManualPause)
        {
            case ReminderPhase.SittingCountdown:
            case ReminderPhase.SnoozedCountdown:
            case ReminderPhase.StandingCountdown:
                _phase = _phaseBeforeManualPause;
                _phaseEndsAt = _clock() + _remainingTime;
                RaiseStateChanged();
                break;

            case ReminderPhase.StandPromptPending:
                _phase = ReminderPhase.StandPromptPending;
                _remainingTime = TimeSpan.Zero;
                RaiseStateChanged();
                ShowStandPrompt();
                break;
        }
    }

    public void UpdateOptions(ReminderScheduleOptions options)
    {
        ThrowIfDisposed();

        _options = CloneOptions(options);

        if (_phase == ReminderPhase.StandPromptPending && _promptHost.IsVisible)
        {
            _promptHost.UpdateStandDuration(_options.Stand);
        }

        RaiseStateChanged();
    }

    public void UpdatePromptBackground(string backgroundArgbHex)
    {
        ThrowIfDisposed();

        _promptBackgroundArgbHex = backgroundArgbHex ?? string.Empty;

        if (_phase == ReminderPhase.StandPromptPending && _promptHost.IsVisible)
        {
            _promptHost.UpdateBackground(_promptBackgroundArgbHex);
        }

        RaiseStateChanged();
    }

    public void HandleSessionEvent(ReminderSessionEvent sessionEvent)
    {
        ThrowIfDisposed();

        switch (sessionEvent)
        {
            case ReminderSessionEvent.Logon:
                HandleSessionLogon();
                break;

            case ReminderSessionEvent.Lock:
                HandleSessionLock();
                break;

            case ReminderSessionEvent.Unlock:
                HandleSessionUnlock();
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _promptHost.Confirmed -= OnPromptConfirmed;
        _promptHost.Snoozed -= OnPromptSnoozed;
        _promptHost.DismissForShutdown();
        GC.SuppressFinalize(this);
    }

    private void HandleSessionLogon()
    {
        if (_hasStarted)
        {
            return;
        }

        Start();
    }

    private void HandleSessionLock()
    {
        if (_phase is ReminderPhase.PausedForLock or ReminderPhase.PausedManually or ReminderPhase.Idle)
        {
            return;
        }

        _phaseBeforePause = _phase;

        switch (_phase)
        {
            case ReminderPhase.SittingCountdown:
            case ReminderPhase.SnoozedCountdown:
            case ReminderPhase.StandingCountdown:
                CaptureRemainingTime();
                _phase = ReminderPhase.PausedForLock;
                RaiseStateChanged();
                break;

            case ReminderPhase.StandPromptPending:
                _promptHost.DismissForLock();
                _phase = ReminderPhase.PausedForLock;
                RaiseStateChanged();
                break;
        }
    }

    private void HandleSessionUnlock()
    {
        if (_phase != ReminderPhase.PausedForLock)
        {
            return;
        }

        switch (_phaseBeforePause)
        {
            case ReminderPhase.SittingCountdown:
            case ReminderPhase.SnoozedCountdown:
            case ReminderPhase.StandingCountdown:
                _phase = _phaseBeforePause;
                _phaseEndsAt = _clock() + _remainingTime;
                RaiseStateChanged();
                break;

            case ReminderPhase.StandPromptPending:
                _phase = ReminderPhase.StandPromptPending;
                RaiseStateChanged();
                ShowStandPrompt();
                break;
        }
    }

    private void BeginSittingCountdown(bool isInitial)
    {
        var duration = isInitial ? _options.InitialSit : _options.RecurringSit;
        _phase = ReminderPhase.SittingCountdown;
        StartCountdown(duration);
    }

    private void BeginStandingCountdown()
    {
        _phase = ReminderPhase.StandingCountdown;
        StartCountdown(_options.Stand);
    }

    private void BeginSnoozedCountdown()
    {
        _phase = ReminderPhase.SnoozedCountdown;
        StartCountdown(SnoozeDuration);
    }

    private void StartCountdown(TimeSpan duration)
    {
        _remainingTime = duration;
        _phaseEndsAt = _clock() + duration;
        RaiseStateChanged();
    }

    private void CompleteCurrentPhase()
    {
        switch (_phase)
        {
            case ReminderPhase.SittingCountdown:
            case ReminderPhase.SnoozedCountdown:
                _trayNotifier.DismissPersistentBalloonTip();
                _phase = ReminderPhase.StandPromptPending;
                _remainingTime = TimeSpan.Zero;
                RaiseStateChanged();
                ShowStandPrompt();
                break;

            case ReminderPhase.StandingCountdown:
                _trayNotifier.ShowPersistentBalloonTip("Time to sit", "Your standing interval is done. The next sitting timer has started.");
                BeginSittingCountdown(isInitial: false);
                break;
        }
    }

    private void ShowStandPrompt()
    {
        if (_promptHost.IsVisible)
        {
            _promptHost.Activate();
            return;
        }

        _promptHost.Show(_options.Stand, _promptBackgroundArgbHex);
        _promptHost.Activate();
    }

    private void OnPromptConfirmed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        BeginStandingCountdown();
    }

    private void OnPromptSnoozed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        BeginSnoozedCountdown();
    }

    private void CaptureRemainingTime()
    {
        var remaining = _phaseEndsAt - _clock();
        _remainingTime = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ReminderScheduleOptions CloneOptions(ReminderScheduleOptions options)
    {
        ValidateDuration(options.InitialSit, nameof(options.InitialSit));
        ValidateDuration(options.RecurringSit, nameof(options.RecurringSit));
        ValidateDuration(options.Stand, nameof(options.Stand));

        return new ReminderScheduleOptions
        {
            InitialSit = options.InitialSit,
            RecurringSit = options.RecurringSit,
            Stand = options.Stand
        };
    }

    private static void ValidateDuration(TimeSpan duration, string parameterName)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Reminder durations must be greater than zero.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}