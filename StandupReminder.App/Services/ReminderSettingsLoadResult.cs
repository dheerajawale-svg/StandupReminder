using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public sealed record ReminderSettingsLoadResult(ReminderScheduleOptions Options, string? WarningMessage);
