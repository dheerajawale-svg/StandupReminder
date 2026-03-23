namespace StandupReminder.App.Services;

public sealed class SitReminderExtendedEventArgs : EventArgs
{
    public SitReminderExtendedEventArgs(int minutes)
    {
        Minutes = minutes;
        Duration = TimeSpan.FromMinutes(minutes);
    }

    public int Minutes { get; }

    public TimeSpan Duration { get; }
}
