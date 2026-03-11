namespace StandupReminder.Persistence;

public interface ISessionEventLogStore
{
    IReadOnlyList<SessionEventLogEntry> Load();

    void Save(IReadOnlyCollection<SessionEventLogEntry> entries);
}
