using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using StandupReminder.App.Models;
using StandupReminder.App.Services;

namespace StandupReminder.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private static readonly TimeSpan EventHistoryRetention = TimeSpan.FromHours(48);

    private readonly ISessionEventLogStore _eventLogStore;
    private readonly List<SessionEventLogEntry> _eventHistory;

    public ObservableCollection<string> EventLog { get; } = [];

    private string _currentPhaseTitle = "Starting";
    private string _currentPhaseDescription = "Preparing the reminder scheduler.";
    private string _remainingTimeText = "--:--:--";
    private string _pauseStateText = "Starting";
    private string _trayHintText = "The app will keep running from the tray.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindowViewModel(ISessionEventLogStore eventLogStore)
    {
        _eventLogStore = eventLogStore;
        _eventHistory = [.. eventLogStore.Load()];

        TrimExpiredEventHistory();
        RebuildEventLog();
        AddEventLog("Application started.");
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
        AddEventLog(message);
    }

    public void LogHwndSourceInitializationFailed()
    {
        AddEventLog("Failed to initialize HWND source. Event hooks were not registered.");
    }

    public void LogWindowHandleUnavailable()
    {
        AddEventLog("Window handle is not available. Session notifications were not registered.");
    }

    public void LogWtsRegistrationFailed(int errorCode)
    {
        AddEventLog($"WTSRegisterSessionNotification failed. Win32Error={errorCode}.");
    }

    public void LogSessionNotificationListening()
    {
        AddEventLog("Listening for WM_WTSSESSION_CHANGE notifications.");
    }

    public void LogSessionLock(int sessionId)
    {
        AddEventLog($"Session event: lock (session {sessionId}).");
    }

    public void LogSessionUnlock(int sessionId)
    {
        AddEventLog($"Session event: unlock (session {sessionId}).");
    }

    public void LogSessionLogon(int sessionId)
    {
        AddEventLog($"Session event: logon (session {sessionId}).");
    }

    public void LogUnknownSessionEvent(int sessionEvent, int sessionId)
    {
        AddEventLog($"Session event: code=0x{sessionEvent:X} (session {sessionId}).");
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
                CurrentPhaseDescription = "A blocking reminder is waiting for you to confirm that you stood up.";
                PauseStateText = "Awaiting confirmation";
                TrayHintText = "The stand-up popup cannot be dismissed until you confirm.";
                break;

            case ReminderPhase.StandingCountdown:
                CurrentPhaseTitle = "Standing";
                CurrentPhaseDescription = "The standing interval is running. You will get a sit notification when it finishes.";
                PauseStateText = "Active";
                TrayHintText = "The sit reminder is informational and restarts the next sit interval automatically.";
                break;

            case ReminderPhase.PausedForLock:
                CurrentPhaseTitle = "Paused";
                CurrentPhaseDescription = "The screen is locked. The current interval will resume from the exact remaining time after unlock.";
                PauseStateText = "Paused for lock";
                TrayHintText = "No time is consumed while the session stays locked.";
                break;

            default:
                CurrentPhaseTitle = "Starting";
                CurrentPhaseDescription = "Preparing the reminder scheduler.";
                PauseStateText = "Starting";
                TrayHintText = "The app will keep running from the tray.";
                break;
        }

        RemainingTimeText = scheduler.Phase == ReminderPhase.StandPromptPending
            ? "Awaiting confirmation"
            : scheduler.RemainingTime.ToString(@"hh\:mm\:ss");
    }

    private void AddEventLog(string message)
    {
        _eventHistory.Insert(0, new SessionEventLogEntry(DateTimeOffset.Now, message));
        TrimExpiredEventHistory();
        RebuildEventLog();
        _eventLogStore.Save(_eventHistory);
    }

    private void RebuildEventLog()
    {
        EventLog.Clear();

        foreach (var entry in _eventHistory.OrderByDescending(item => item.Timestamp))
        {
            EventLog.Add($"[{entry.Timestamp.LocalDateTime:yyyy-MM-dd HH:mm:ss}] {entry.Message}");
        }
    }

    private void TrimExpiredEventHistory()
    {
        var cutoff = DateTimeOffset.Now - EventHistoryRetention;
        _eventHistory.RemoveAll(entry => entry.Timestamp < cutoff);
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
