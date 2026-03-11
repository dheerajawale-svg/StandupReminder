using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace StandupReminder.WinUI;

public partial class App : Application
{
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
        _mainWindow ??= new MainWindow();
        _mainWindow.Activate();
    }
}