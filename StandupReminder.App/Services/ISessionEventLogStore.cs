using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public interface ISessionEventLogStore
{
    IReadOnlyList<SessionEventLogEntry> Load();

    void Save(IReadOnlyCollection<SessionEventLogEntry> entries);
}
