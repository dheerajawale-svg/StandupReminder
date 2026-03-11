namespace StandupReminder.Persistence;

public sealed record SessionEventLogEntry(DateTimeOffset Timestamp, string Message);
