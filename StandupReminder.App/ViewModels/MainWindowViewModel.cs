using System;
using System.Collections.ObjectModel;

namespace StandupReminder.App.ViewModels;

public sealed class MainWindowViewModel
{
    public ObservableCollection<string> EventLog { get; } = [];

    public MainWindowViewModel()
    {
        AddLog("Application started.");
    }

    public void LogHwndSourceInitializationFailed()
    {
        AddLog("Failed to initialize HWND source. Event hooks were not registered.");
    }

    public void LogWindowHandleUnavailable()
    {
        AddLog("Window handle is not available. Session notifications were not registered.");
    }

    public void LogWtsRegistrationFailed(int errorCode)
    {
        AddLog($"WTSRegisterSessionNotification failed. Win32Error={errorCode}.");
    }

    public void LogSessionNotificationListening()
    {
        AddLog("Listening for WM_WTSSESSION_CHANGE notifications.");
    }

    public void LogSessionLock(int sessionId)
    {
        AddLog($"Session event: lock (session {sessionId}).");
    }

    public void LogSessionUnlock(int sessionId)
    {
        AddLog($"Session event: unlock (session {sessionId}).");
    }

    public void LogSessionLogon(int sessionId)
    {
        AddLog($"Session event: logon (session {sessionId}).");
    }

    public void LogUnknownSessionEvent(int sessionEvent, int sessionId)
    {
        AddLog($"Session event: code=0x{sessionEvent:X} (session {sessionId}).");
    }

    private void AddLog(string message)
    {
        EventLog.Insert(0, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
    }
}
