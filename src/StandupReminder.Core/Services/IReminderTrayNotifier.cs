namespace StandupReminder.Core.Services;

public interface IReminderTrayNotifier
{
    void ShowBalloonTip(string title, string message);

    void ShowPersistentBalloonTip(string title, string message);

    void DismissPersistentBalloonTip();
}