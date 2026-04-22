namespace StandupReminder.App.Models;

public sealed class ReminderRuntimeState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public DateOnly LocalDate { get; init; } = DateOnly.FromDateTime(DateTime.Now);

    public ReminderPhase Phase { get; init; } = ReminderPhase.Idle;

    public TimeSpan RemainingTime { get; init; } = TimeSpan.Zero;

    public ReminderPhase PhaseBeforePause { get; init; } = ReminderPhase.Idle;

    public ReminderPhase PhaseBeforeManualPause { get; init; } = ReminderPhase.Idle;

    public TimeSpan LastStartedSittingDuration { get; init; } = TimeSpan.Zero;

    public TimeSpan PendingRecurringSitExtension { get; init; } = TimeSpan.Zero;

    public TimeSpan? SnoozedDuration { get; init; }
}
