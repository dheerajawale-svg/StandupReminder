using System.IO;
using System.Text.Json;
using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public sealed class LocalAppDataReminderSettingsStore : IReminderSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath;

    public LocalAppDataReminderSettingsStore()
    {
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StandupReminder");

        _settingsFilePath = Path.Combine(settingsDirectory, "settings.json");
    }

    public ReminderSettingsLoadResult Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new ReminderSettingsLoadResult(new ReminderScheduleOptions(), null);
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            var data = JsonSerializer.Deserialize<ReminderSettingsData>(json, SerializerOptions);

            if (data is null || !TryCreateOptions(data, out var options))
            {
                return CreateFallbackResult();
            }

            return new ReminderSettingsLoadResult(options, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return CreateFallbackResult();
        }
    }

    public void Save(ReminderScheduleOptions options)
    {
        var data = ReminderSettingsData.FromOptions(options);
        var directoryPath = Path.GetDirectoryName(_settingsFilePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = JsonSerializer.Serialize(data, SerializerOptions);
        File.WriteAllText(_settingsFilePath, json);
    }

    private ReminderSettingsLoadResult CreateFallbackResult()
    {
        return new ReminderSettingsLoadResult(
            new ReminderScheduleOptions(),
            $"Could not read reminder settings from {_settingsFilePath}. Using default reminder intervals.");
    }

    private static bool TryCreateOptions(ReminderSettingsData data, out ReminderScheduleOptions options)
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

    private sealed class ReminderSettingsData
    {
        public int InitialSitMinutes { get; set; }

        public int RecurringSitMinutes { get; set; }

        public int StandMinutes { get; set; }

        public static ReminderSettingsData FromOptions(ReminderScheduleOptions options)
        {
            return new ReminderSettingsData
            {
                InitialSitMinutes = ToWholeMinutes(options.InitialSit),
                RecurringSitMinutes = ToWholeMinutes(options.RecurringSit),
                StandMinutes = ToWholeMinutes(options.Stand)
            };
        }

        private static int ToWholeMinutes(TimeSpan duration)
        {
            return Math.Max(1, (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero));
        }
    }
}
