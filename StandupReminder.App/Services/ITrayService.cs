namespace StandupReminder.App.Services;

public interface ITrayService : IDisposable
{
    event EventHandler? OpenRequested;

    event EventHandler? PauseResumeRequested;

    event EventHandler? SettingsRequested;

    event EventHandler? ExitRequested;

    void Initialize();

    void ShowBalloonTip(string title, string message);

    void ShowPersistentBalloonTip(string title, string message);

    void DismissPersistentBalloonTip();

    void SetPauseMenuLabel(bool isPaused);

    void UpdateStatus(string statusText);
}
