using System.Windows.Threading;
using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public sealed class PostureReminderScheduler : IPostureReminderScheduler
{
    private readonly ReminderScheduleOptions _options;
    private readonly ITrayService _trayService;
    private readonly DispatcherTimer _timer;

    private ReminderPhase _phase = ReminderPhase.Idle;
    private ReminderPhase _phaseBeforePause = ReminderPhase.Idle;
    private DateTimeOffset _phaseEndsAt;
    private TimeSpan _remainingTime = TimeSpan.Zero;
    private StandUpReminderWindow? _promptWindow;
    private bool _hasStarted;
    private bool _disposed;

    public event EventHandler? StateChanged;

    public event EventHandler<string>? LogGenerated;

    public ReminderPhase Phase => _phase;

    public TimeSpan RemainingTime => _remainingTime < TimeSpan.Zero ? TimeSpan.Zero : _remainingTime;

    public bool IsPaused => _phase == ReminderPhase.PausedForLock;

    public PostureReminderScheduler(ReminderScheduleOptions options, ITrayService trayService)
    {
        _options = options;
        _trayService = trayService;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;
    }

    public void Start()
    {
        if (_hasStarted)
        {
            return;
        }

        _hasStarted = true;
        Log("Reminder scheduler started.");
        BeginSittingCountdown(isInitial: true);
    }

    public void HandleSessionLogon()
    {
        if (_hasStarted)
        {
            Log("Logon detected while the reminder cycle was already running.");
            return;
        }

        Log("Logon detected. Starting the reminder cycle.");
        Start();
    }

    public void HandleSessionLock()
    {
        if (_phase == ReminderPhase.PausedForLock || _phase == ReminderPhase.Idle)
        {
            return;
        }

        _phaseBeforePause = _phase;

        switch (_phase)
        {
            case ReminderPhase.SittingCountdown:
            case ReminderPhase.StandingCountdown:
                CaptureRemainingTime();
                _timer.Stop();
                _phase = ReminderPhase.PausedForLock;
                Log("Session locked. Countdown paused.");
                RaiseStateChanged();
                break;

            case ReminderPhase.StandPromptPending:
                DismissPromptForLock();
                _phase = ReminderPhase.PausedForLock;
                Log("Session locked. Stand-up prompt deferred until unlock.");
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
            case ReminderPhase.StandingCountdown:
                _phase = _phaseBeforePause;
                _phaseEndsAt = DateTimeOffset.Now + _remainingTime;
                _timer.Start();
                Log($"Session unlocked. Resumed {DescribePhase(_phase)} countdown with {FormatDuration(_remainingTime)} remaining.");
                RaiseStateChanged();
                break;

            case ReminderPhase.StandPromptPending:
                _phase = ReminderPhase.StandPromptPending;
                Log("Session unlocked. Stand-up prompt restored.");
                RaiseStateChanged();
                ShowStandPrompt();
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
        ClosePromptForShutdown();
        GC.SuppressFinalize(this);
    }

    private void BeginSittingCountdown(bool isInitial)
    {
        var duration = isInitial ? _options.InitialSit : _options.RecurringSit;
        _phase = ReminderPhase.SittingCountdown;
        StartCountdown(duration);
        Log(isInitial
            ? $"Started the first sitting interval for {FormatDuration(duration)}."
            : $"Started the next sitting interval for {FormatDuration(duration)}.");
    }

    private void BeginStandingCountdown()
    {
        _phase = ReminderPhase.StandingCountdown;
        StartCountdown(_options.Stand);
        Log($"Stand-up confirmed. Standing interval started for {FormatDuration(_options.Stand)}.");
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

        if (_phase is not ReminderPhase.SittingCountdown and not ReminderPhase.StandingCountdown)
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
                _phase = ReminderPhase.StandPromptPending;
                _remainingTime = TimeSpan.Zero;
                Log("Sitting interval finished. Waiting for stand-up confirmation.");
                RaiseStateChanged();
                ShowStandPrompt();
                break;

            case ReminderPhase.StandingCountdown:
                Log("Standing interval finished. Showing the sit notification and restarting the sitting timer.");
                _trayService.ShowBalloonTip("Time to sit", "Your standing interval is done. The next sitting timer has started.");
                BeginSittingCountdown(isInitial: false);
                break;
        }
    }

    private void ShowStandPrompt()
    {
        if (_promptWindow is not null)
        {
            _promptWindow.Activate();
            return;
        }

        _promptWindow = new StandUpReminderWindow();
        _promptWindow.Confirmed += OnPromptConfirmed;
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

    private void OnPromptClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(_promptWindow, sender))
        {
            _promptWindow = null;
        }
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

    private void Log(string message)
    {
        LogGenerated?.Invoke(this, message);
    }

    private static string DescribePhase(ReminderPhase phase)
    {
        return phase switch
        {
            ReminderPhase.SittingCountdown => "sitting",
            ReminderPhase.StandingCountdown => "standing",
            ReminderPhase.StandPromptPending => "stand-up confirmation",
            ReminderPhase.PausedForLock => "paused",
            _ => "idle"
        };
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.ToString(@"hh\:mm\:ss");
    }
}
