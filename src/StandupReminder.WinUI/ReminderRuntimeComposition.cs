using StandupReminder.Core.Services;
using StandupReminder.Persistence;

namespace StandupReminder.WinUI;

internal sealed class ReminderRuntimeComposition : IDisposable
{
    private readonly ShellViewModel _shellViewModel;
    private readonly ReminderSchedulerRuntime _runtime;
    private readonly IReminderTrayHost _trayHost;
    private readonly IReminderSessionEventSource _sessionEventSource;

    public ReminderRuntimeComposition(
        ReminderSettingsLoadResult settingsLoadResult,
        string settingsFilePath,
        ReminderSchedulerRuntime runtime,
        IReminderTrayHost trayHost,
        IReminderSessionEventSource sessionEventSource)
    {
        _runtime = runtime;
        _trayHost = trayHost;
        _sessionEventSource = sessionEventSource;
        _sessionEventSource.SessionEvent += _runtime.HandleSessionEvent;
        _shellViewModel = new ShellViewModel(settingsLoadResult.Options, settingsLoadResult.WarningMessage, settingsFilePath, runtime);
        _runtime.Start();
    }

    public MainWindow CreateMainWindow()
    {
        return new MainWindow(_shellViewModel);
    }

    public void Dispose()
    {
        _sessionEventSource.SessionEvent -= _runtime.HandleSessionEvent;
        _sessionEventSource.Dispose();
        _shellViewModel.Dispose();
        _runtime.Dispose();
        _trayHost.Dispose();
    }
}

internal sealed class NullReminderPromptHost : IReminderPromptHost
{
    public event EventHandler? Confirmed
    {
        add { }
        remove { }
    }

    public event EventHandler? Snoozed
    {
        add { }
        remove { }
    }

    public bool IsVisible => false;

    public void Show(TimeSpan standDuration, string backgroundArgbHex)
    {
        _ = standDuration;
        _ = backgroundArgbHex;
    }

    public void Activate()
    {
    }

    public void UpdateStandDuration(TimeSpan standDuration)
    {
        _ = standDuration;
    }

    public void UpdateBackground(string backgroundArgbHex)
    {
        _ = backgroundArgbHex;
    }

    public void DismissForLock()
    {
    }

    public void DismissForPause()
    {
    }

    public void DismissForShutdown()
    {
    }
}
