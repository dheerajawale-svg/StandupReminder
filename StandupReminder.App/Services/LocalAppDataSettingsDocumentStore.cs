using System.IO;
using System.Text.Json;
using StandupReminder.App.Models;
using StandupReminder.Core.Models;

namespace StandupReminder.App.Services;

internal sealed class LocalAppDataSettingsDocumentStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath;

    public LocalAppDataSettingsDocumentStore()
    {
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StandupReminder");

        _settingsFilePath = Path.Combine(settingsDirectory, "settings.json");
    }

    public string SettingsFilePath => _settingsFilePath;

    public bool TryLoad(out SettingsDocument document)
    {
        if (!File.Exists(_settingsFilePath))
        {
            document = new SettingsDocument();
            return true;
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            document = JsonSerializer.Deserialize<SettingsDocument>(json, SerializerOptions) ?? new SettingsDocument();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            document = new SettingsDocument();
            return false;
        }
    }

    public void Save(SettingsDocument document)
    {
        var directoryPath = Path.GetDirectoryName(_settingsFilePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = JsonSerializer.Serialize(document, SerializerOptions);
        File.WriteAllText(_settingsFilePath, json);
    }

    internal sealed class SettingsDocument
    {
        public ReminderScheduleSection? ReminderSchedule { get; set; }

        public AppearanceSection? Appearance { get; set; }
    }

    internal sealed class ReminderScheduleSection
    {
        public int InitialSitMinutes { get; set; }

        public int RecurringSitMinutes { get; set; }

        public int StandMinutes { get; set; }

        public static ReminderScheduleSection FromOptions(ReminderScheduleOptions options)
        {
            return new ReminderScheduleSection
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

    internal sealed class AppearanceSection
    {
        public string WindowBackgroundArgbHex { get; set; } = AppearanceSettings.DefaultWindowBackgroundArgbHex;

        public static AppearanceSection FromSettings(AppearanceSettings settings)
        {
            return new AppearanceSection
            {
                WindowBackgroundArgbHex = settings.WindowBackgroundArgbHex
            };
        }
    }
}