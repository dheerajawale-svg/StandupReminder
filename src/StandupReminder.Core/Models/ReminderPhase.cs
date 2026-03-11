namespace StandupReminder.Core.Models;

public enum ReminderPhase
{
    Idle,
    SittingCountdown,
    StandPromptPending,
    SnoozedCountdown,
    StandingCountdown,
    PausedManually,
    PausedForLock
}