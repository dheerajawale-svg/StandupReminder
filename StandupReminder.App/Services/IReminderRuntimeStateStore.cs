using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public interface IReminderRuntimeStateStore
{
    ReminderRuntimeStateLoadResult Load();

    void Save(ReminderRuntimeState state);

    void Delete();
}
