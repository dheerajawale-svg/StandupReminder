namespace StandupReminder.Core.Services;

public interface IReminderTickSource : IDisposable
{
    event EventHandler? Tick;

    bool IsRunning { get; }

    void Start(TimeSpan interval);

    void Stop();
}
