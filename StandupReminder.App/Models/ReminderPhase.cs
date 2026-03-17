namespace StandupReminder.App.Models;

public enum ReminderPhase
{
    Idle,
    SittingCountdown,
    StandPromptPending,
    SnoozedCountdown,
    StandingCountdown,
    SitPromptPending,
    PausedManually,
    PausedForLock
}
