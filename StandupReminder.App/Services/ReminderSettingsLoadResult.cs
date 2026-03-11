using StandupReminder.Core.Models;

namespace StandupReminder.App.Services;

public sealed record ReminderSettingsLoadResult(ReminderScheduleOptions Options, string? WarningMessage);
