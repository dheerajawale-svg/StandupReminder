namespace StandupReminder.Core.Services;

public interface IReminderPromptHost
{
    event EventHandler? Confirmed;

    event EventHandler? Snoozed;

    bool IsVisible { get; }

    void Show(TimeSpan standDuration, string backgroundArgbHex);

    void Activate();

    void UpdateStandDuration(TimeSpan standDuration);

    void UpdateBackground(string backgroundArgbHex);

    void DismissForLock();

    void DismissForPause();

    void DismissForShutdown();
}