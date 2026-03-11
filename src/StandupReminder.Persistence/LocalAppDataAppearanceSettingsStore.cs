namespace StandupReminder.Persistence;

public sealed class LocalAppDataAppearanceSettingsStore : IAppearanceSettingsStore
{
    private readonly LocalAppDataSettingsDocumentStore _documentStore;

    public LocalAppDataAppearanceSettingsStore(LocalAppDataSettingsDocumentStore? documentStore = null)
    {
        _documentStore = documentStore ?? new LocalAppDataSettingsDocumentStore();
    }

    public string SettingsFilePath => _documentStore.SettingsFilePath;

    public AppearanceSettings Load()
    {
        if (!_documentStore.TryLoad(out var document) || document.Appearance is null)
        {
            return new AppearanceSettings();
        }

        return new AppearanceSettings
        {
            WindowBackgroundArgbHex = ArgbHexColor.NormalizeOrDefault(document.Appearance.WindowBackgroundArgbHex)
        };
    }

    public void Save(AppearanceSettings settings)
    {
        _ = _documentStore.TryLoad(out var document);
        document.Appearance = LocalAppDataSettingsDocumentStore.AppearanceSection.FromArgbHex(settings.WindowBackgroundArgbHex);
        _documentStore.Save(document);
    }
}
