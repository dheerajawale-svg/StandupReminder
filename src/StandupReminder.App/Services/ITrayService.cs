using StandupReminder.Core.Services;

namespace StandupReminder.App.Services;

public interface ITrayService : IReminderTrayHost
{
    event EventHandler? OpenRequested;

    event EventHandler? PauseResumeRequested;

    event EventHandler? SettingsRequested;

    event EventHandler? ExitRequested;

    void Initialize();

    void SetPauseMenuLabel(bool isPaused);

    void UpdateStatus(string statusText);
}
