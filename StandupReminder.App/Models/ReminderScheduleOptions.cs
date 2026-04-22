namespace StandupReminder.App.Models;

public sealed class ReminderScheduleOptions
{
    public TimeSpan InitialSit { get; init; } = TimeSpan.FromMinutes(60);

    public TimeSpan RecurringSit { get; init; } = TimeSpan.FromMinutes(50);

    public TimeSpan Stand { get; init; } = TimeSpan.FromMinutes(20);

    public TimeSpan Snooze { get; init; } = TimeSpan.FromMinutes(5);
}
