using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public interface IPostureReminderScheduler : IDisposable
{
    event EventHandler? StateChanged;

    ReminderPhase Phase { get; }

    TimeSpan RemainingTime { get; }

    bool IsPaused { get; }

    bool IsManuallyPaused { get; }

    void Start();

    void PauseTimer();

    void ResumeTimer();

    void AcknowledgeSitReminder();

    void UpdateOptions(ReminderScheduleOptions options);

    void UpdateAppearanceSettings(AppearanceSettings settings);

    void HandleSessionLogon();

    void HandleSessionLock();

    void HandleSessionUnlock();
}
