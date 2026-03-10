using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public interface IAppearanceSettingsStore
{
    AppearanceSettings Load();

    void Save(AppearanceSettings settings);
}