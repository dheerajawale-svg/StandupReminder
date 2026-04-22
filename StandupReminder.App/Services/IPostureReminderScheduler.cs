using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public interface IPostureReminderScheduler : IDisposable
{
    event EventHandler? StateChanged;

    ReminderPhase Phase { get; }

    TimeSpan RemainingTime { get; }

    TimeSpan? SnoozedDuration { get; }

    bool IsPaused { get; }

    bool IsManuallyPaused { get; }

    bool CanSwitchMode { get; }

    void Start();

    void PauseTimer();

    void ResumeTimer();

    void SwitchMode();

    void AcknowledgeSitReminder();

    void UpdateOptions(ReminderScheduleOptions options);

    void UpdateAppearanceSettings(AppearanceSettings settings);

    void HandleSessionLogon();

    void HandleSessionLock();

    void HandleSessionUnlock();
}
