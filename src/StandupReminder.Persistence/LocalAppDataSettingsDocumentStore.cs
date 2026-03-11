using System.IO;
using System.Text.Json;
using StandupReminder.Core.Models;

namespace StandupReminder.Persistence;

public sealed class LocalAppDataSettingsDocumentStore
{
    public const string DefaultWindowBackgroundArgbHex = AppearanceSettings.DefaultWindowBackgroundArgbHex;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath;

    public LocalAppDataSettingsDocumentStore(string? settingsFilePath = null)
    {
        _settingsFilePath = settingsFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StandupReminder",
            "settings.json");
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

    public sealed class SettingsDocument
    {
        public ReminderScheduleSection? ReminderSchedule { get; set; }

        public AppearanceSection? Appearance { get; set; }
    }

    public sealed class ReminderScheduleSection
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

    public sealed class AppearanceSection
    {
        public string WindowBackgroundArgbHex { get; set; } = DefaultWindowBackgroundArgbHex;

        public static AppearanceSection FromArgbHex(string argbHex)
        {
            return new AppearanceSection
            {
                WindowBackgroundArgbHex = ArgbHexColor.NormalizeOrDefault(argbHex)
            };
        }
    }
}
