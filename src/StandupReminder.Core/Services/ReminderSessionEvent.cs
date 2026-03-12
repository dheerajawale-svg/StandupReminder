namespace StandupReminder.Core.Services;

// The app intentionally keeps session handling to the Windows 11 transitions that affect reminder timing.
public enum ReminderSessionEvent
{
    Logon,
    Lock,
    Unlock
}
