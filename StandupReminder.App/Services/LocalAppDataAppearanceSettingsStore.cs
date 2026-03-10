using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public sealed class LocalAppDataAppearanceSettingsStore : IAppearanceSettingsStore
{
    private readonly LocalAppDataSettingsDocumentStore _documentStore = new();

    public AppearanceSettings Load()
    {
        if (!_documentStore.TryLoad(out var document))
        {
            return new AppearanceSettings();
        }

        if (!TryCreateAppearanceSettings(document.Appearance, out var settings))
        {
            return new AppearanceSettings();
        }

        return settings;
    }

    public void Save(AppearanceSettings settings)
    {
        _ = _documentStore.TryLoad(out var document);
        document.Appearance = LocalAppDataSettingsDocumentStore.AppearanceSection.FromSettings(Normalize(settings));
        _documentStore.Save(document);
    }

    private static bool TryCreateAppearanceSettings(LocalAppDataSettingsDocumentStore.AppearanceSection? data, out AppearanceSettings settings)
    {
        settings = new AppearanceSettings();

        if (data is null || !ColorUtil.TryParseArgbHex(data.WindowBackgroundArgbHex, out var color))
        {
            return false;
        }

        settings = new AppearanceSettings
        {
            WindowBackgroundArgbHex = ColorUtil.ToArgbHex(color)
        };

        return true;
    }

    private static AppearanceSettings Normalize(AppearanceSettings settings)
    {
        if (!ColorUtil.TryParseArgbHex(settings.WindowBackgroundArgbHex, out var color))
        {
            return new AppearanceSettings();
        }

        return new AppearanceSettings
        {
            WindowBackgroundArgbHex = ColorUtil.ToArgbHex(color)
        };
    }
}