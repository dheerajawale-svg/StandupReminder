namespace StandupReminder.App.Models;

public sealed class StandReminderSnoozedEventArgs : EventArgs
{
    public StandReminderSnoozedEventArgs(TimeSpan duration)
    {
        Duration = duration;
    }

    public TimeSpan Duration { get; }
}
