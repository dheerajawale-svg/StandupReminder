namespace StandupReminder.Core.Services;

public interface IReminderTrayHost : IReminderTrayNotifier, IDisposable
{
    event EventHandler? OpenRequested;

    event EventHandler? PauseResumeRequested;

    event EventHandler? SettingsRequested;

    event EventHandler? ExitRequested;

    void Initialize();

    void SetPauseMenuLabel(bool isPaused);

    void UpdateStatus(string statusText);
}
