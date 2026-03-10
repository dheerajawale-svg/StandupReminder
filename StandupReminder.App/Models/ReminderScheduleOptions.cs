namespace StandupReminder.App.Models;

public sealed class ReminderScheduleOptions
{
    public TimeSpan InitialSit { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan RecurringSit { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan Stand { get; init; } = TimeSpan.FromMinutes(2);
}
