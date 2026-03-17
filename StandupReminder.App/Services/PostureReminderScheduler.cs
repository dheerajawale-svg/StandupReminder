using System.Windows.Threading;
using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public sealed class PostureReminderScheduler : IPostureReminderScheduler
{
    private static readonly TimeSpan SnoozeDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SitReminderExtensionDuration = TimeSpan.FromMinutes(5);

    private ReminderScheduleOptions _options;
    private AppearanceSettings _appearanceSettings;
    private readonly ITrayService _trayService;
    private readonly DispatcherTimer _timer;

    private ReminderPhase _phase = ReminderPhase.Idle;
    private ReminderPhase _phaseBeforePause = ReminderPhase.Idle;
    private ReminderPhase _phaseBeforeManualPause = ReminderPhase.Idle;
    private DateTimeOffset _phaseEndsAt;
    private TimeSpan _remainingTime = TimeSpan.Zero;
    private StandUpReminderWindow? _promptWindow;
    private TimeSpan _pendingRecurringSitExtension = TimeSpan.Zero;
    private bool _hasStarted;
    private bool _disposed;

    public event EventHandler? StateChanged;

    public ReminderPhase Phase => _phase;

    public TimeSpan RemainingTime => _remainingTime < TimeSpan.Zero ? TimeSpan.Zero : _remainingTime;

    public bool IsPaused => _phase is ReminderPhase.PausedForLock or ReminderPhase.PausedManually;

    public bool IsManuallyPaused => _phase == ReminderPhase.PausedManually;

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
        BeginSittingCountdown(isInitial: true);
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
                CaptureRemainingTime();
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

    public void AcknowledgeSitReminder()
    {
        if (_phase != ReminderPhase.SitPromptPending)
        {
            return;
        }

        _trayService.DismissPersistentNotification();
        BeginSittingCountdown(isInitial: false);
    }

    public void ExtendSitReminder()
    {
        if (_phase != ReminderPhase.SitPromptPending)
        {
            return;
        }

        _pendingRecurringSitExtension += SitReminderExtensionDuration;
        _trayService.DismissPersistentNotification();
        _phase = ReminderPhase.StandingCountdown;
        StartCountdown(SitReminderExtensionDuration);
    }

    public void UpdateOptions(ReminderScheduleOptions options)
    {
        _options = CloneOptions(options);

        if (_phase == ReminderPhase.StandPromptPending && _promptWindow is not null)
        {
            _promptWindow.UpdateStandDuration(_options.Stand);
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

        _phaseBeforePause = _phase;

        switch (_phase)
        {
            case ReminderPhase.SittingCountdown:
            case ReminderPhase.SnoozedCountdown:
            case ReminderPhase.StandingCountdown:
                CaptureRemainingTime();
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

        switch (_phaseBeforePause)
        {
            case ReminderPhase.SittingCountdown:
            case ReminderPhase.SnoozedCountdown:
            case ReminderPhase.StandingCountdown:
                _phase = _phaseBeforePause;
                _phaseEndsAt = DateTimeOffset.Now + _remainingTime;
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

    private void BeginSittingCountdown(bool isInitial)
    {
        var duration = isInitial
            ? _options.InitialSit
            : _options.RecurringSit + _pendingRecurringSitExtension;

        if (!isInitial)
        {
            _pendingRecurringSitExtension = TimeSpan.Zero;
        }

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
        _phaseEndsAt = DateTimeOffset.Now + duration;
        _timer.Start();
        RaiseStateChanged();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_phase is not ReminderPhase.SittingCountdown
            and not ReminderPhase.SnoozedCountdown
            and not ReminderPhase.StandingCountdown)
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
        _trayService.ShowPersistentNotification("Time to sit", "Your standing interval is done. Click OK to start the next sitting timer, or Extend for 5 more standing minutes.");
    }

    private void ShowStandPrompt()
    {
        if (_promptWindow is not null)
        {
            _promptWindow.Activate();
            return;
        }

        _promptWindow = new StandUpReminderWindow(_options.Stand, _appearanceSettings.WindowBackgroundArgbHex);
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

    private void OnPromptSnoozed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        _promptWindow = null;
        BeginSnoozedCountdown();
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

    private void OnSitReminderExtended(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        ExtendSitReminder();
    }

    private void CaptureRemainingTime()
    {
        var remaining = _phaseEndsAt - DateTimeOffset.Now;
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
