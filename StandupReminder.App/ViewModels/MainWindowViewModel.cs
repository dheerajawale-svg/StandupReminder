using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using StandupReminder.App.Models;
using StandupReminder.App.Services;

namespace StandupReminder.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ILogger<MainWindowViewModel> _logger;

    private string _currentPhaseTitle = "Starting";
    private string _currentPhaseDescription = "Preparing the reminder scheduler.";
    private string _remainingTimeText = "--:--:--";
    private string _pauseStateText = "Starting";
    private string _trayHintText = "The app will keep running from the tray.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindowViewModel(ISessionEventLogStore eventLogStore)
    {
        _logger = ApplicationLogger.CreateLogger<MainWindowViewModel>();
        _logger.LogInformation("Application started.");
    }

    public string CurrentPhaseTitle
    {
        get => _currentPhaseTitle;
        private set => SetProperty(ref _currentPhaseTitle, value);
    }

    public string CurrentPhaseDescription
    {
        get => _currentPhaseDescription;
        private set => SetProperty(ref _currentPhaseDescription, value);
    }

    public string RemainingTimeText
    {
        get => _remainingTimeText;
        private set => SetProperty(ref _remainingTimeText, value);
    }

    public string PauseStateText
    {
        get => _pauseStateText;
        private set => SetProperty(ref _pauseStateText, value);
    }

    public string TrayHintText
    {
        get => _trayHintText;
        private set => SetProperty(ref _trayHintText, value);
    }

    public void AttachScheduler(IPostureReminderScheduler scheduler)
    {
        UpdateReminderState(scheduler);
        scheduler.StateChanged += OnSchedulerStateChanged;
    }

    public void LogSystemMessage(string message)
    {
        _logger.LogInformation("{Message}", message);
    }

    public void LogHwndSourceInitializationFailed()
    {
        _logger.LogWarning("Failed to initialize HWND source. Event hooks were not registered.");
    }

    public void LogWindowHandleUnavailable()
    {
        _logger.LogWarning("Window handle is not available. Session notifications were not registered.");
    }

    public void LogWtsRegistrationFailed(int errorCode)
    {
        _logger.LogError("WTSRegisterSessionNotification failed. Win32Error={ErrorCode}.", errorCode);
    }

    public void LogSessionNotificationListening()
    {
        _logger.LogInformation("Listening for WM_WTSSESSION_CHANGE notifications.");
    }

    public void LogSessionLock(int sessionId)
    {
        _logger.LogInformation("Session event: lock (session {SessionId}).", sessionId);
    }

    public void LogSessionUnlock(int sessionId)
    {
        _logger.LogInformation("Session event: unlock (session {SessionId}).", sessionId);
    }

    public void LogSessionLogon(int sessionId)
    {
        _logger.LogInformation("Session event: logon (session {SessionId}).", sessionId);
    }

    public void LogUnknownSessionEvent(int sessionEvent, int sessionId)
    {
        _logger.LogInformation("Session event: code=0x{SessionEvent:X} (session {SessionId}).", sessionEvent, sessionId);
    }

    private void OnSchedulerStateChanged(object? sender, EventArgs e)
    {
        if (sender is IPostureReminderScheduler scheduler)
        {
            UpdateReminderState(scheduler);
        }
    }

    private void UpdateReminderState(IPostureReminderScheduler scheduler)
    {
        switch (scheduler.Phase)
        {
            case ReminderPhase.SittingCountdown:
                CurrentPhaseTitle = "Sitting";
                CurrentPhaseDescription = "The sitting countdown is running. The next reminder will ask you to stand up.";
                PauseStateText = "Active";
                TrayHintText = "Close the window to hide it; the reminder loop stays active in the tray.";
                break;

            case ReminderPhase.StandPromptPending:
                CurrentPhaseTitle = "Stand-Up Confirmation";
                CurrentPhaseDescription = "A blocking reminder is waiting for you to confirm that you stood up or snooze for 5 minutes.";
                PauseStateText = "Awaiting confirmation";
                TrayHintText = "The stand-up popup stays on screen until you confirm or snooze it.";
                break;

            case ReminderPhase.SnoozedCountdown:
                CurrentPhaseTitle = "Snoozed";
                CurrentPhaseDescription = "The stand-up reminder was deferred for 5 minutes and will reappear when the snooze countdown ends.";
                PauseStateText = "Snoozed";
                TrayHintText = "The reminder loop is still active and will prompt you again after the snooze interval.";
                break;

            case ReminderPhase.StandingCountdown:
                CurrentPhaseTitle = "Standing";
                CurrentPhaseDescription = "The standing interval is running. You will get a sit notification when it finishes.";
                PauseStateText = "Active";
                TrayHintText = "When the standing interval ends, the sit reminder must be acknowledged before the next sitting timer starts.";
                break;

            case ReminderPhase.SitPromptPending:
                CurrentPhaseTitle = "Sit Confirmation";
                CurrentPhaseDescription = "A blocking Windows notification is waiting for you to click OK to sit down, or choose extra standing minutes and click Extend.";
                PauseStateText = "Awaiting action";
                TrayHintText = "The sit reminder keeps reappearing until you choose OK or Extend from the Windows notification.";
                break;

            case ReminderPhase.PausedForLock:
                CurrentPhaseTitle = "Paused";
                CurrentPhaseDescription = "The screen is locked. The current interval will resume from the exact remaining time after unlock.";
                PauseStateText = "Paused for lock";
                TrayHintText = "No time is consumed while the session stays locked.";
                break;

            case ReminderPhase.PausedManually:
                CurrentPhaseTitle = "Paused";
                CurrentPhaseDescription = "The reminder loop is paused from the tray and will stay paused until you choose Resume timer.";
                PauseStateText = "Paused manually";
                TrayHintText = "Use the tray menu to resume the current reminder state.";
                break;

            default:
                CurrentPhaseTitle = "Starting";
                CurrentPhaseDescription = "Preparing the reminder scheduler.";
                PauseStateText = "Starting";
                TrayHintText = "The app will keep running from the tray.";
                break;
        }

        RemainingTimeText = scheduler.Phase switch
        {
            ReminderPhase.StandPromptPending => "Awaiting confirmation",
            ReminderPhase.SitPromptPending => "Awaiting OK or Extend",
            _ => scheduler.RemainingTime.ToString(@"hh\:mm\:ss")
        };
    }

    private void SetProperty(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
