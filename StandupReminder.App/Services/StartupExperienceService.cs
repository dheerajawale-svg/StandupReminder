using System.IO;

namespace StandupReminder.App.Services;

public sealed class StartupExperienceService
{
    private readonly LocalAppDataSettingsDocumentStore _documentStore = new();

    public bool ShouldShowFirstRunSplash()
    {
        if (!_documentStore.TryLoad(out var document))
        {
            return true;
        }

        return document.StartupExperience?.HasShownFirstRunSplash != true;
    }

    public bool TryMarkFirstRunSplashShown()
    {
        try
        {
            _ = _documentStore.TryLoad(out var document);
            document.StartupExperience ??= new LocalAppDataSettingsDocumentStore.StartupExperienceSection();
            document.StartupExperience.HasShownFirstRunSplash = true;
            _documentStore.Save(document);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
