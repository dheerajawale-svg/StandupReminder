using Windows.Foundation.Collections;

namespace StandupReminder.App.Services;

public interface ITrayService : IDisposable
{
    event EventHandler? OpenRequested;

    event EventHandler? PauseResumeRequested;

    event EventHandler? SettingsRequested;

    event EventHandler? ExitRequested;

    event EventHandler? SitReminderAcknowledged;

    event EventHandler<SitReminderExtendedEventArgs>? SitReminderExtended;

    event EventHandler? SitReminderBodyActivated;

    event EventHandler? SitReminderDismissed;

    void Initialize();

    void ShowNotification(string title, string message);

    void ShowPersistentNotification(string title, string message);

    void HandlePersistentNotificationActivation(string arguments, ValueSet? userInput);

    void DismissPersistentNotification();

    void SetPauseMenuLabel(bool isPaused);

    void UpdateStatus(string statusText);
}
