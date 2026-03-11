using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace StandupReminder.WinUI;

public partial class App : Application
{
    private readonly AppBootstrapper _bootstrapper = new();
    private ReminderRuntimeComposition? _composition;
    private MainWindow? _mainWindow;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ = args;

        ActivateMainWindow();
    }

    internal void HandleActivated(AppActivationArguments args)
    {
        _ = args;

        ActivateMainWindow();
    }

    private void ActivateMainWindow()
    {
        _composition ??= _bootstrapper.CreateRuntimeComposition();
        _mainWindow ??= _composition.CreateMainWindow();
        _mainWindow.Activate();
    }
}