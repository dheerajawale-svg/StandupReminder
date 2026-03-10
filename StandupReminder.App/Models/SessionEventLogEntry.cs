namespace StandupReminder.App.Models;

public sealed record SessionEventLogEntry(DateTimeOffset Timestamp, string Message);
