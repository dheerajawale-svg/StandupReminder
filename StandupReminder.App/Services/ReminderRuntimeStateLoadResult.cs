using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public sealed record ReminderRuntimeStateLoadResult(ReminderRuntimeState? State, string? WarningMessage);
