namespace StandupReminder.WindowsInterop;

public enum SessionChangeType
{
    Lock,
    Unlock,
    Logon,
    Unknown
}

public readonly record struct SessionChangeEvent(SessionChangeType Type, int SessionId, int RawEventCode);
