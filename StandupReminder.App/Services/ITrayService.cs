namespace StandupReminder.App.Services;

public interface ITrayService : IDisposable
{
    event EventHandler? OpenRequested;

    event EventHandler? PauseResumeRequested;

    event EventHandler? SettingsRequested;

    event EventHandler? ExitRequested;

    void Initialize();

    void ShowNotification(string title, string message);

    void ShowPersistentNotification(string title, string message);

    void DismissPersistentNotification();

    void SetPauseMenuLabel(bool isPaused);

    void UpdateStatus(string statusText);
}
