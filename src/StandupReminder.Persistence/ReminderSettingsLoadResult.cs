using StandupReminder.Core.Models;

namespace StandupReminder.Persistence;

public sealed record ReminderSettingsLoadResult(ReminderScheduleOptions Options, string? WarningMessage);
