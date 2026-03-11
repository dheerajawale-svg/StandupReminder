using System.IO;
using StandupReminder.Core.Models;
using StandupReminder.Persistence;

namespace StandupReminder.Core.Tests;

public sealed class ReminderSettingsStoreTests
{
    [Fact]
    public void Load_WhenSettingsFileIsMissing_ReturnsDefaultsWithoutWarning()
    {
        var settingsFilePath = CreateTempSettingsFilePath(out var tempDirectory);

        try
        {
            var result = CreateStore(settingsFilePath).Load();

            AssertDefaultOptions(result.Options);
            Assert.Null(result.WarningMessage);
        }
        finally
        {
            DeleteTempDirectory(tempDirectory);
        }
    }

    [Fact]
    public void Load_WhenSavedReminderScheduleIsInvalid_ReturnsDefaultsWithWarning()
    {
        var settingsFilePath = CreateTempSettingsFilePath(out var tempDirectory);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
            File.WriteAllText(settingsFilePath, """
                {
                  "ReminderSchedule": {
                    "InitialSitMinutes": 0,
                    "RecurringSitMinutes": 50,
                    "StandMinutes": 20
                  }
                }
                """);

            var result = CreateStore(settingsFilePath).Load();

            AssertDefaultOptions(result.Options);
            Assert.NotNull(result.WarningMessage);
            Assert.Contains(settingsFilePath, result.WarningMessage);
        }
        finally
        {
            DeleteTempDirectory(tempDirectory);
        }
    }

    [Fact]
    public void Save_PreservesAppearanceSectionAndRoundTripsReminderSchedule()
    {
        var settingsFilePath = CreateTempSettingsFilePath(out var tempDirectory);

        try
        {
            var documentStore = new LocalAppDataSettingsDocumentStore(settingsFilePath);
            documentStore.Save(new LocalAppDataSettingsDocumentStore.SettingsDocument
            {
                Appearance = LocalAppDataSettingsDocumentStore.AppearanceSection.FromArgbHex("#FF010203")
            });

            var expected = new ReminderScheduleOptions
            {
                InitialSit = TimeSpan.FromMinutes(75),
                RecurringSit = TimeSpan.FromMinutes(45),
                Stand = TimeSpan.FromMinutes(15)
            };

            var store = new LocalAppDataReminderSettingsStore(documentStore);
            store.Save(expected);
            var result = store.Load();

            Assert.Equal(expected.InitialSit, result.Options.InitialSit);
            Assert.Equal(expected.RecurringSit, result.Options.RecurringSit);
            Assert.Equal(expected.Stand, result.Options.Stand);
            Assert.Null(result.WarningMessage);

            Assert.True(documentStore.TryLoad(out var savedDocument));
            Assert.Equal("#FF010203", savedDocument.Appearance?.WindowBackgroundArgbHex);
        }
        finally
        {
            DeleteTempDirectory(tempDirectory);
        }
    }

    [Fact]
    public void AppearanceStore_Load_NormalizesRgbHexToArgbHex()
    {
        var settingsFilePath = CreateTempSettingsFilePath(out var tempDirectory);

        try
        {
            var documentStore = new LocalAppDataSettingsDocumentStore(settingsFilePath);
            documentStore.Save(new LocalAppDataSettingsDocumentStore.SettingsDocument
            {
                Appearance = LocalAppDataSettingsDocumentStore.AppearanceSection.FromArgbHex("010203")
            });

            var result = new LocalAppDataAppearanceSettingsStore(documentStore).Load();

            Assert.Equal("#FF010203", result.WindowBackgroundArgbHex);
        }
        finally
        {
            DeleteTempDirectory(tempDirectory);
        }
    }

    [Fact]
    public void AppearanceStore_Load_WhenAppearanceHexIsInvalid_ReturnsDefaultAppearance()
    {
        var settingsFilePath = CreateTempSettingsFilePath(out var tempDirectory);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
            File.WriteAllText(settingsFilePath, """
                {
                  "Appearance": {
                    "WindowBackgroundArgbHex": "not-a-color"
                  }
                }
                """);

            var result = new LocalAppDataAppearanceSettingsStore(new LocalAppDataSettingsDocumentStore(settingsFilePath)).Load();

            Assert.Equal(AppearanceSettings.DefaultWindowBackgroundArgbHex, result.WindowBackgroundArgbHex);
        }
        finally
        {
            DeleteTempDirectory(tempDirectory);
        }
    }

    [Fact]
    public void AppearanceStore_Save_PreservesReminderScheduleAndNormalizesAppearance()
    {
        var settingsFilePath = CreateTempSettingsFilePath(out var tempDirectory);

        try
        {
            var documentStore = new LocalAppDataSettingsDocumentStore(settingsFilePath);
            documentStore.Save(new LocalAppDataSettingsDocumentStore.SettingsDocument
            {
                ReminderSchedule = LocalAppDataSettingsDocumentStore.ReminderScheduleSection.FromOptions(new ReminderScheduleOptions
                {
                    InitialSit = TimeSpan.FromMinutes(12),
                    RecurringSit = TimeSpan.FromMinutes(9),
                    Stand = TimeSpan.FromMinutes(4)
                })
            });

            var store = new LocalAppDataAppearanceSettingsStore(documentStore);
            store.Save(new AppearanceSettings
            {
                WindowBackgroundArgbHex = "#010203"
            });

            Assert.True(documentStore.TryLoad(out var savedDocument));
            Assert.Equal(12, savedDocument.ReminderSchedule?.InitialSitMinutes);
            Assert.Equal(9, savedDocument.ReminderSchedule?.RecurringSitMinutes);
            Assert.Equal(4, savedDocument.ReminderSchedule?.StandMinutes);
            Assert.Equal("#FF010203", savedDocument.Appearance?.WindowBackgroundArgbHex);
        }
        finally
        {
            DeleteTempDirectory(tempDirectory);
        }
    }

    [Fact]
    public void SessionEventLogStore_SaveAndLoad_RoundTripsEntries()
    {
        var logFilePath = CreateTempSessionLogFilePath(out var tempDirectory);

        try
        {
            var store = new LocalAppDataSessionEventLogStore(logFilePath);
            var expected = new[]
            {
                new SessionEventLogEntry(new DateTimeOffset(2026, 1, 2, 8, 30, 0, TimeSpan.Zero), "Application started."),
                new SessionEventLogEntry(new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero), "Reminder timer paused from the tray.")
            };

            store.Save(expected);
            var loaded = store.Load();

            Assert.Equal(expected, loaded);
        }
        finally
        {
            DeleteTempDirectory(tempDirectory);
        }
    }

    private static LocalAppDataReminderSettingsStore CreateStore(string settingsFilePath)
    {
        return new LocalAppDataReminderSettingsStore(new LocalAppDataSettingsDocumentStore(settingsFilePath));
    }

    private static string CreateTempSettingsFilePath(out string tempDirectory)
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "StandupReminder.Tests", Guid.NewGuid().ToString("N"));
        return Path.Combine(tempDirectory, "settings.json");
    }

    private static string CreateTempSessionLogFilePath(out string tempDirectory)
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "StandupReminder.Tests", Guid.NewGuid().ToString("N"));
        return Path.Combine(tempDirectory, "session-events.json");
    }

    private static void DeleteTempDirectory(string tempDirectory)
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static void AssertDefaultOptions(ReminderScheduleOptions options)
    {
        Assert.Equal(TimeSpan.FromMinutes(60), options.InitialSit);
        Assert.Equal(TimeSpan.FromMinutes(50), options.RecurringSit);
        Assert.Equal(TimeSpan.FromMinutes(20), options.Stand);
    }
}
