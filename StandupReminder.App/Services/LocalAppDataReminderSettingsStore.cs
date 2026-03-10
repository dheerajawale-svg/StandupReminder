using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public sealed class LocalAppDataReminderSettingsStore : IReminderSettingsStore
{
    private readonly LocalAppDataSettingsDocumentStore _documentStore = new();

    public ReminderSettingsLoadResult Load()
    {
        if (!_documentStore.TryLoad(out var document))
        {
            return CreateFallbackResult();
        }

        if (document.ReminderSchedule is null)
        {
            return new ReminderSettingsLoadResult(new ReminderScheduleOptions(), null);
        }

        if (!TryCreateOptions(document.ReminderSchedule, out var options))
        {
            return CreateFallbackResult();
        }

        return new ReminderSettingsLoadResult(options, null);
    }

    public void Save(ReminderScheduleOptions options)
    {
        _ = _documentStore.TryLoad(out var document);
        document.ReminderSchedule = LocalAppDataSettingsDocumentStore.ReminderScheduleSection.FromOptions(options);
        _documentStore.Save(document);
    }

    private ReminderSettingsLoadResult CreateFallbackResult()
    {
        return new ReminderSettingsLoadResult(
            new ReminderScheduleOptions(),
            $"Could not read reminder settings from {_documentStore.SettingsFilePath}. Using default reminder intervals.");
    }

    private static bool TryCreateOptions(LocalAppDataSettingsDocumentStore.ReminderScheduleSection data, out ReminderScheduleOptions options)
    {
        options = new ReminderScheduleOptions();

        if (data.InitialSitMinutes <= 0 || data.RecurringSitMinutes <= 0 || data.StandMinutes <= 0)
        {
            return false;
        }

        options = new ReminderScheduleOptions
        {
            InitialSit = TimeSpan.FromMinutes(data.InitialSitMinutes),
            RecurringSit = TimeSpan.FromMinutes(data.RecurringSitMinutes),
            Stand = TimeSpan.FromMinutes(data.StandMinutes)
        };

        return true;
    }
}
