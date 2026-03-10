namespace StandupReminder.App.Services;

public interface ITrayService : IDisposable
{
    event EventHandler? OpenRequested;

    event EventHandler? ExitRequested;

    void Initialize();

    void ShowBalloonTip(string title, string message);

    void UpdateStatus(string statusText);
}
