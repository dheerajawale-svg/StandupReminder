namespace StandupReminder.App.Models;

public enum ReminderPhase
{
    Idle,
    SittingCountdown,
    StandPromptPending,
    StandingCountdown,
    PausedForLock
}
