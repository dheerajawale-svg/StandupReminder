using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace StandupReminder.WinUI;

public partial class App : Application
{
    private readonly AppBootstrapper _bootstrapper = new();
    private DispatcherQueue? _dispatcherQueue;
    private ReminderRuntimeComposition? _composition;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private bool _isShuttingDown;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ = args;

        EnsureDispatcherQueue();
        EnsureComposition();
    }

    internal void HandleActivated(AppActivationArguments args)
    {
        _ = args;

        EnsureDispatcherQueue();
        EnsureComposition();
        ActivateMainWindow();
    }

    private void EnsureDispatcherQueue()
    {
        _dispatcherQueue ??= DispatcherQueue.GetForCurrentThread();
    }

    private void EnsureComposition()
    {
        if (_composition is not null)
        {
            return;
        }

        _composition = _bootstrapper.CreateRuntimeComposition();
        _composition.WindowActivationRequested += OnWindowActivationRequested;
        _composition.SettingsRequested += OnSettingsRequested;
        _composition.ShutdownRequested += OnShutdownRequested;
    }

    private void ActivateMainWindow()
    {
        if (_composition is null)
        {
            return;
        }

        if (_mainWindow is null)
        {
            _mainWindow = _composition.CreateMainWindow();
            _mainWindow.Activate();
        }
        else
        {
            _mainWindow.RestoreFromTray();
        }
    }

    private void OnWindowActivationRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        EnqueueOnUiThread(ActivateMainWindow);
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        EnqueueOnUiThread(ShowSettingsWindow);
    }

    private void OnShutdownRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        EnqueueOnUiThread(PerformShutdown);
    }

    private void ShowSettingsWindow()
    {
        if (_composition is null)
        {
            return;
        }

        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var settingsWindow = new SettingsWindow(_composition.CreateSettingsViewModel(), _composition.SaveSettings);
        settingsWindow.Closed += OnSettingsWindowClosed;
        _settingsWindow = settingsWindow;
        settingsWindow.Activate();
    }

    private void OnSettingsWindowClosed(object sender, WindowEventArgs args)
    {
        _ = args;

        if (sender is not SettingsWindow settingsWindow)
        {
            return;
        }

        settingsWindow.Closed -= OnSettingsWindowClosed;

        if (ReferenceEquals(_settingsWindow, settingsWindow))
        {
            _settingsWindow = null;
        }
    }

    private void EnqueueOnUiThread(Action action)
    {
        EnsureDispatcherQueue();

        if (_dispatcherQueue?.HasThreadAccess == true)
        {
            action();
            return;
        }

        _dispatcherQueue?.TryEnqueue(() => action());
    }

    private void PerformShutdown()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;

        if (_settingsWindow is not null)
        {
            _settingsWindow.Closed -= OnSettingsWindowClosed;
            _settingsWindow.Close();
            _settingsWindow = null;
        }

        if (_composition is not null)
        {
            _composition.WindowActivationRequested -= OnWindowActivationRequested;
            _composition.SettingsRequested -= OnSettingsRequested;
            _composition.ShutdownRequested -= OnShutdownRequested;
            _composition.Dispose();
            _composition = null;
        }

        if (_mainWindow is not null)
        {
            _mainWindow.PrepareForExit();
            _mainWindow.Close();
            _mainWindow = null;
        }

        Exit();
    }
}