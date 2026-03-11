namespace StandupReminder.Core.Services;

public interface IReminderSessionEventSource : IDisposable
{
    event Action<ReminderSessionEvent>? SessionEvent;
}
