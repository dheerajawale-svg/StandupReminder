using System.Windows.Threading;
using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public sealed class PostureReminderScheduler : IPostureReminderScheduler
{
    private enum SittingCountdownStartReason
    {
        Initial,
        Recurring,
        ManualSwitch
    }

    private ReminderScheduleOptions _options;
    private AppearanceSettings _appearanceSettings;
    private readonly ITrayService _trayService;
    private readonly DispatcherTimer _timer;

    private ReminderPhase _phase = ReminderPhase.Idle;
    private ReminderPhase _phaseBeforePause = ReminderPhase.Idle;
    private ReminderPhase _phaseBeforeManualPause = ReminderPhase.Idle;
    private DateTimeOffset _phaseEndsAt;
    private DateTimeOffset? _lockedAt;
    private TimeSpan _remainingTime = TimeSpan.Zero;
    private TimeSpan _lastStartedSittingDuration = TimeSpan.Zero;
    private StandUpReminderWindow? _promptWindow;
    private TimeSpan _pendingRecurringSitExtension = TimeSpan.Zero;
    private TimeSpan? _snoozedDuration;
    private bool _hasStarted;
    private bool _disposed;

    public event EventHandler? StateChanged;

    public ReminderPhase Phase => _phase;

    public TimeSpan RemainingTime => _remainingTime < TimeSpan.Zero ? TimeSpan.Zero : _remainingTime;

    public TimeSpan? SnoozedDuration => _phase == ReminderPhase.SnoozedCountdown ? _snoozedDuration : null;

    public bool IsPaused => _phase is ReminderPhase.PausedForLock or ReminderPhase.PausedManually;

    public bool IsManuallyPaused => _phase == ReminderPhase.PausedManually;

    public bool CanSwitchMode => _phase is ReminderPhase.SittingCountdown
        or ReminderPhase.SnoozedCountdown
        or ReminderPhase.StandingCountdown;

    public ReminderRuntimeState CaptureRuntimeState()
    {
        var now = DateTimeOffset.Now;

        if (_phase is ReminderPhase.SittingCountdown
            or ReminderPhase.SnoozedCountdown
            or ReminderPhase.StandingCountdown)
        {
            CaptureRemainingTime(now);
        }

        return new ReminderRuntimeState
        {
            SchemaVersion = ReminderRuntimeState.CurrentSchemaVersion,
            LocalDate = DateOnly.FromDateTime(now.LocalDateTime),
            Phase = _phase,
            RemainingTime = _remainingTime,
            PhaseBeforePause = _phaseBeforePause,
            PhaseBeforeManualPause = _phaseBeforeManualPause,
            LastStartedSittingDuration = _lastStartedSittingDuration,
            PendingRecurringSitExtension = _pendingRecurringSitExtension,
            SnoozedDuration = _snoozedDuration
        };
    }

    public void RestoreRuntimeState(ReminderRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _timer.Stop();
        _lockedAt = null;
        _trayService.DismissPersistentNotification();
        ClosePromptForShutdown();

        _hasStarted = true;
        _phaseBeforePause = state.PhaseBeforePause;
        _phaseBeforeManualPause = state.PhaseBeforeManualPause;
        _lastStartedSittingDuration = state.LastStartedSittingDuration;
        _pendingRecurringSitExtension = state.PendingRecurringSitExtension;
        _snoozedDuration = state.SnoozedDuration;

        switch (state.Phase)
        {
            case ReminderPhase.SittingCountdown:
            case ReminderPhase.SnoozedCountdown:
            case ReminderPhase.StandingCountdown:
                RestoreActiveCountdown(state.Phase, state.RemainingTime, state.SnoozedDuration);
                break;

            case ReminderPhase.StandPromptPending:
                _phase = ReminderPhase.StandPromptPending;
                _remainingTime = TimeSpan.Zero;
                RaiseStateChanged();
                ShowStandPrompt();
                break;

            case ReminderPhase.SitPromptPending:
                _phase = ReminderPhase.SitPromptPending;
                _remainingTime = TimeSpan.Zero;
                RaiseStateChanged();
                ShowSitPrompt();
                break;

            case ReminderPhase.PausedManually:
                RestorePausedManually(state);
                break;

            case ReminderPhase.PausedForLock:
                RestorePausedForLock(state);
                break;

            default:
                BeginSittingCountdown(SittingCountdownStartReason.Initial);
                break;
        }
    }

    public PostureReminderScheduler(ReminderScheduleOptions options, AppearanceSettings appearanceSettings, ITrayService trayService)
    {
        _options = CloneOptions(options);
        _appearanceSettings = NormalizeAppearanceSettings(appearanceSettings);
        _trayService = trayService;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;
        _trayService.SitReminderAcknowledged += OnSitReminderAcknowledged;
        _trayService.SitReminderExtended += OnSitReminderExtended;
    }

    public void Start()
    {
        if (_hasStarted)
        {
            return;
        }

        _hasStarted = true;
        BeginSittingCountdown(SittingCountdownStartReason.Initial);
    }

    public void PauseTimer()
    {
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
                CaptureRemainingTime(DateTimeOffset.Now);
                _timer.Stop();
                _phase = ReminderPhase.PausedManually;
                RaiseStateChanged();
                break;

            case ReminderPhase.StandPromptPending:
                _remainingTime = TimeSpan.Zero;
                DismissPromptForPause();
                _phase = ReminderPhase.PausedManually;
                RaiseStateChanged();
                break;

            case ReminderPhase.SitPromptPending:
                _remainingTime = TimeSpan.Zero;
                _trayService.DismissPersistentNotification();
                _phase = ReminderPhase.PausedManually;
                RaiseStateChanged();
                break;
        }
    }

    public void ResumeTimer()
    {
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
                _phaseEndsAt = DateTimeOffset.Now + _remainingTime;
                _timer.Start();
                RaiseStateChanged();
                break;

            case ReminderPhase.StandPromptPending:
                _phase = ReminderPhase.StandPromptPending;
                _remainingTime = TimeSpan.Zero;
                RaiseStateChanged();
                ShowStandPrompt();
                break;

            case ReminderPhase.SitPromptPending:
                _phase = ReminderPhase.SitPromptPending;
                _remainingTime = TimeSpan.Zero;
                RaiseStateChanged();
                ShowSitPrompt();
                break;
        }
    }

    public void SwitchMode()
    {
        if (!CanSwitchMode)
        {
            return;
        }

        _timer.Stop();

        switch (_phase)
        {
            case ReminderPhase.SittingCountdown:
            case ReminderPhase.SnoozedCountdown:
                BeginStandingCountdown();
                break;

            case ReminderPhase.StandingCountdown:
                _pendingRecurringSitExtension = TimeSpan.Zero;
                BeginSittingCountdown(SittingCountdownStartReason.ManualSwitch);
                break;
        }
    }

    public void AcknowledgeSitReminder()
    {
        if (_phase != ReminderPhase.SitPromptPending)
        {
            return;
        }

        _trayService.DismissPersistentNotification();
        BeginSittingCountdown(SittingCountdownStartReason.Recurring);
    }

    public void ExtendSitReminder(TimeSpan extensionDuration)
    {
        if (_phase != ReminderPhase.SitPromptPending)
        {
            return;
        }

        _pendingRecurringSitExtension += extensionDuration;
        _trayService.DismissPersistentNotification();
        _phase = ReminderPhase.StandingCountdown;
        StartCountdown(extensionDuration);
    }

    public void UpdateOptions(ReminderScheduleOptions options)
    {
        _options = CloneOptions(options);

        if (_phase == ReminderPhase.StandPromptPending && _promptWindow is not null)
        {
            _promptWindow.UpdateStandDuration(_options.Stand);
            _promptWindow.UpdateSnoozeDuration(_options.Snooze);
        }
        RaiseStateChanged();
    }

    public void UpdateAppearanceSettings(AppearanceSettings settings)
    {
        _appearanceSettings = NormalizeAppearanceSettings(settings);

        if (_phase == ReminderPhase.StandPromptPending && _promptWindow is not null)
        {
            _promptWindow.UpdateBackground(_appearanceSettings.WindowBackgroundArgbHex);
        }

        RaiseStateChanged();
    }

    public void HandleSessionLogon()
    {
        if (_hasStarted)
        {
            return;
        }

        Start();
    }

    public void HandleSessionLock()
    {
        if (_phase is ReminderPhase.PausedForLock or ReminderPhase.PausedManually or ReminderPhase.Idle)
        {
            return;
        }

        _lockedAt = DateTimeOffset.Now;

        _phaseBeforePause = _phase;

        switch (_phase)
        {
            case ReminderPhase.SittingCountdown:
            case ReminderPhase.SnoozedCountdown:
            case ReminderPhase.StandingCountdown:
                CaptureRemainingTime(DateTimeOffset.Now);
                _timer.Stop();
                _phase = ReminderPhase.PausedForLock;
                RaiseStateChanged();
                break;

            case ReminderPhase.StandPromptPending:
                DismissPromptForLock();
                _phase = ReminderPhase.PausedForLock;
                RaiseStateChanged();
                break;

            case ReminderPhase.SitPromptPending:
                _trayService.DismissPersistentNotification();
                _phase = ReminderPhase.PausedForLock;
                RaiseStateChanged();
                break;
        }
    }

    public void HandleSessionUnlock()
    {
        if (_phase != ReminderPhase.PausedForLock)
        {
            return;
        }


        if (_lockedAt is not null)
        {
            var lockedDuration = DateTimeOffset.Now - _lockedAt.Value;
            if (lockedDuration > TimeSpan.Zero)
            {
                _phaseEndsAt += lockedDuration;
            }
        }

        _lockedAt = null;

        switch (_phaseBeforePause)
        {
            case ReminderPhase.SittingCountdown:
            case ReminderPhase.SnoozedCountdown:
            case ReminderPhase.StandingCountdown:
                _phase = _phaseBeforePause;
                CaptureRemainingTime(DateTimeOffset.Now);
                _timer.Start();
                RaiseStateChanged();
                break;

            case ReminderPhase.StandPromptPending:
                _phase = ReminderPhase.StandPromptPending;
                RaiseStateChanged();
                ShowStandPrompt();
                break;

            case ReminderPhase.SitPromptPending:
                _phase = ReminderPhase.SitPromptPending;
                _remainingTime = TimeSpan.Zero;
                RaiseStateChanged();
                ShowSitPrompt();
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
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _trayService.SitReminderAcknowledged -= OnSitReminderAcknowledged;
        _trayService.SitReminderExtended -= OnSitReminderExtended;
        _trayService.DismissPersistentNotification();
        ClosePromptForShutdown();
        GC.SuppressFinalize(this);
    }

    private void BeginSittingCountdown(SittingCountdownStartReason startReason)
    {
        _snoozedDuration = null;
        var baseDuration = startReason switch
        {
            SittingCountdownStartReason.Initial => _options.InitialSit,
            SittingCountdownStartReason.ManualSwitch => _options.RecurringSit,
            _ => _pendingRecurringSitExtension > TimeSpan.Zero && _lastStartedSittingDuration > TimeSpan.Zero
                ? _lastStartedSittingDuration
                : _options.RecurringSit
        };
        var duration = baseDuration + _pendingRecurringSitExtension;
        _pendingRecurringSitExtension = TimeSpan.Zero;
        _lastStartedSittingDuration = duration;

        _phase = ReminderPhase.SittingCountdown;
        StartCountdown(duration);
    }

    private void BeginStandingCountdown()
    {
        _snoozedDuration = null;
        _phase = ReminderPhase.StandingCountdown;
        StartCountdown(_options.Stand);
    }

    private void BeginSnoozedCountdown(TimeSpan duration)
    {
        ValidateDuration(duration, nameof(duration));
        _phase = ReminderPhase.SnoozedCountdown;
        _snoozedDuration = duration;
        StartCountdown(duration);
    }

    private void StartCountdown(TimeSpan duration)
    {
        _remainingTime = duration;
        _phaseEndsAt = DateTimeOffset.Now + duration;
        _timer.Start();
        RaiseStateChanged();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_phase == ReminderPhase.PausedForLock)
        {
            return;
        }

        if (_phase is not ReminderPhase.SittingCountdown
            and not ReminderPhase.SnoozedCountdown
            and not ReminderPhase.StandingCountdown)
        {
            return;
        }

        CaptureRemainingTime(DateTimeOffset.Now);
        RaiseStateChanged();

        if (_remainingTime <= TimeSpan.Zero)
        {
            CompleteCurrentPhase();
        }
    }

    private void CompleteCurrentPhase()
    {
        _timer.Stop();

        switch (_phase)
        {
            case ReminderPhase.SittingCountdown:
            case ReminderPhase.SnoozedCountdown:
                _trayService.DismissPersistentNotification();
                _phase = ReminderPhase.StandPromptPending;
                _remainingTime = TimeSpan.Zero;
                RaiseStateChanged();
                ShowStandPrompt();
                break;

            case ReminderPhase.StandingCountdown:
                _phase = ReminderPhase.SitPromptPending;
                _remainingTime = TimeSpan.Zero;
                RaiseStateChanged();
                ShowSitPrompt();
                break;
        }
    }

    private void ShowSitPrompt()
    {
        _trayService.ShowPersistentNotification("Time to sit", "Your standing interval is done. Click OK to start the next sitting timer, or choose extra standing minutes and click Extend.");
    }

    private void ShowStandPrompt()
    {
        if (_promptWindow is not null)
        {
            _promptWindow.Activate();
            return;
        }

        _promptWindow = new StandUpReminderWindow(_options.Stand, _options.Snooze, _appearanceSettings.WindowBackgroundArgbHex);
        _promptWindow.Confirmed += OnPromptConfirmed;
        _promptWindow.Snoozed += OnPromptSnoozed;
        _promptWindow.Closed += OnPromptClosed;
        _promptWindow.Show();
        _promptWindow.Activate();
    }

    private void DismissPromptForLock()
    {
        if (_promptWindow is null)
        {
            return;
        }

        _promptWindow.DismissForLock();
        _promptWindow = null;
    }

    private void DismissPromptForPause()
    {
        if (_promptWindow is null)
        {
            return;
        }

        _promptWindow.DismissForPause();
        _promptWindow = null;
    }

    private void ClosePromptForShutdown()
    {
        if (_promptWindow is null)
        {
            return;
        }

        _promptWindow.DismissForShutdown();
        _promptWindow = null;
    }

    private void OnPromptConfirmed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        _promptWindow = null;
        BeginStandingCountdown();
    }

    private void OnPromptSnoozed(object? sender, StandReminderSnoozedEventArgs e)
    {
        _ = sender;

        _promptWindow = null;
        BeginSnoozedCountdown(e.Duration);
    }

    private void OnPromptClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(_promptWindow, sender))
        {
            _promptWindow = null;
        }
    }

    private void OnSitReminderAcknowledged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        AcknowledgeSitReminder();
    }

    private void OnSitReminderExtended(object? sender, SitReminderExtendedEventArgs e)
    {
        _ = sender;
        ExtendSitReminder(e.Duration);
    }

    private void CaptureRemainingTime(DateTimeOffset now)
    {
        var remaining = _phaseEndsAt - now;
        _remainingTime = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private void RestoreActiveCountdown(ReminderPhase phase, TimeSpan remainingTime, TimeSpan? snoozedDuration)
    {
        _phase = phase;
        _snoozedDuration = phase == ReminderPhase.SnoozedCountdown
            ? snoozedDuration ?? remainingTime
            : null;
        _remainingTime = remainingTime > TimeSpan.Zero ? remainingTime : TimeSpan.Zero;

        if (_remainingTime == TimeSpan.Zero)
        {
            CompleteCurrentPhase();
            return;
        }

        _phaseEndsAt = DateTimeOffset.Now + _remainingTime;
        _timer.Start();
        RaiseStateChanged();
    }

    private void RestorePausedManually(ReminderRuntimeState state)
    {
        _phase = ReminderPhase.PausedManually;
        _remainingTime = state.RemainingTime > TimeSpan.Zero ? state.RemainingTime : TimeSpan.Zero;
        _snoozedDuration = state.PhaseBeforeManualPause == ReminderPhase.SnoozedCountdown
            ? state.SnoozedDuration
            : null;
        RaiseStateChanged();
    }

    private void RestorePausedForLock(ReminderRuntimeState state)
    {
        switch (state.PhaseBeforePause)
        {
            case ReminderPhase.SittingCountdown:
            case ReminderPhase.SnoozedCountdown:
            case ReminderPhase.StandingCountdown:
                RestoreActiveCountdown(state.PhaseBeforePause, state.RemainingTime, state.SnoozedDuration);
                break;

            case ReminderPhase.StandPromptPending:
                _phase = ReminderPhase.StandPromptPending;
                _remainingTime = TimeSpan.Zero;
                RaiseStateChanged();
                ShowStandPrompt();
                break;

            case ReminderPhase.SitPromptPending:
                _phase = ReminderPhase.SitPromptPending;
                _remainingTime = TimeSpan.Zero;
                RaiseStateChanged();
                ShowSitPrompt();
                break;

            default:
                BeginSittingCountdown(SittingCountdownStartReason.Initial);
                break;
        }
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
        ValidateDuration(options.Snooze, nameof(options.Snooze));

        return new ReminderScheduleOptions
        {
            InitialSit = options.InitialSit,
            RecurringSit = options.RecurringSit,
            Stand = options.Stand,
            Snooze = options.Snooze
        };
    }

    private static AppearanceSettings NormalizeAppearanceSettings(AppearanceSettings settings)
    {
        if (!ColorUtil.TryParseArgbHex(settings.WindowBackgroundArgbHex, out var color))
        {
            return new AppearanceSettings();
        }

        return new AppearanceSettings
        {
            WindowBackgroundArgbHex = ColorUtil.ToArgbHex(color)
        };
    }

    private static void ValidateDuration(TimeSpan duration, string parameterName)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Reminder durations must be greater than zero.");
        }
    }
}
