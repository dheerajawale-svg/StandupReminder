using StandupReminder.Core.Models;

namespace StandupReminder.App.Services;

public interface IReminderSettingsStore
{
    ReminderSettingsLoadResult Load();

    void Save(ReminderScheduleOptions options);
}
