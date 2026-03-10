using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public interface IPostureReminderScheduler : IDisposable
{
    event EventHandler? StateChanged;

    event EventHandler<string>? LogGenerated;

    ReminderPhase Phase { get; }

    TimeSpan RemainingTime { get; }

    bool IsPaused { get; }

    void Start();

    void HandleSessionLogon();

    void HandleSessionLock();

    void HandleSessionUnlock();
}
