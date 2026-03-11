namespace StandupReminder.Core.Services;

public interface IReminderSessionEventSink
{
    void HandleSessionEvent(ReminderSessionEvent sessionEvent);
}