namespace StandupReminder.WindowsInterop;

public enum SessionMonitorLifecycleEventType
{
    HwndSourceUnavailable,
    WindowHandleUnavailable,
    RegistrationFailed,
    ListeningStarted
}

public readonly record struct SessionMonitorLifecycleEvent(SessionMonitorLifecycleEventType Type, int? Win32Error = null);
