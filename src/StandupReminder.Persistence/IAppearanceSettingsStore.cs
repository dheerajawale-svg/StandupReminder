namespace StandupReminder.Persistence;

public interface IAppearanceSettingsStore
{
    AppearanceSettings Load();

    void Save(AppearanceSettings settings);
}
