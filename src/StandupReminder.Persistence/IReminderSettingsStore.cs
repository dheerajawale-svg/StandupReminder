using StandupReminder.Core.Models;

namespace StandupReminder.Persistence;

public interface IReminderSettingsStore
{
    ReminderSettingsLoadResult Load();

    void Save(ReminderScheduleOptions options);
}
