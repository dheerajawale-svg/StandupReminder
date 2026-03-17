using System;
using System.Windows;
using StandupReminder.App.Services;
using StandupReminder.App.ViewModels;
using StandupReminder.WindowsInterop;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace StandupReminder.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IPostureReminderScheduler _scheduler;

    private WindowsSessionEventMonitor? _sessionMonitor;
    private bool _allowClose;
    private bool _backgroundLaunchPending;

    public MainWindow(MainWindowViewModel viewModel, IPostureReminderScheduler scheduler)
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        _viewModel = viewModel;
        _scheduler = scheduler;
        DataContext = _viewModel;

        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
        Closing += OnWindowClosing;
    }

    public void PrepareForBackgroundLaunch()
    {
        _backgroundLaunchPending = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        WindowState = WindowState.Minimized;
        Opacity = 0;
    }

    public void ShowFromTray()
    {
        ShowActivated = true;
        ShowInTaskbar = true;
        Opacity = 1;

        if (!IsVisible)
        {
            Show();
        }

        WindowState = WindowState.Normal;
        Activate();
        Focus();
        _viewModel.LogSystemMessage("Main window restored from the tray.");
    }

    public void PrepareForExit()
    {
        _allowClose = true;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _sessionMonitor = CreateSessionEventMonitor(this, _viewModel);
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (!_backgroundLaunchPending)
        {
            return;
        }

        _backgroundLaunchPending = false;
        HideToTray("Main window hidden. Use the tray icon to reopen the dashboard.");
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _ = sender;

        if (!_allowClose)
        {
            e.Cancel = true;
            HideToTray("Main window hidden. The reminder loop is still running in the tray.");
            return;
        }

        _sessionMonitor?.Dispose();
        _sessionMonitor = null;
    }

    private void HideToTray(string message)
    {
        ShowInTaskbar = false;
        Hide();
        Opacity = 1;
        _viewModel.LogSystemMessage(message);
    }

    // Static entry point requested for wiring session detection into MainWindow.
    private WindowsSessionEventMonitor CreateSessionEventMonitor(Window window, MainWindowViewModel viewModel)
    {
        return WindowsSessionEventMonitor.Attach(
            window,
            onSessionChanged: sessionEvent => HandleSessionEvent(viewModel, sessionEvent),
            onLifecycleChanged: lifecycleEvent => LogMonitorLifecycleEvent(viewModel, lifecycleEvent)
        );
    }

    private void HandleSessionEvent(MainWindowViewModel viewModel, SessionChangeEvent sessionEvent)
    {
        switch (sessionEvent.Type)
        {
            case SessionChangeType.Lock:
                viewModel.LogSessionLock(sessionEvent.SessionId);
                _scheduler.HandleSessionLock();
                break;

            case SessionChangeType.Unlock:
                viewModel.LogSessionUnlock(sessionEvent.SessionId);
                _scheduler.HandleSessionUnlock();
                break;

            case SessionChangeType.Logon:
                viewModel.LogSessionLogon(sessionEvent.SessionId);
                _scheduler.HandleSessionLogon();
                break;

            default:
                viewModel.LogUnknownSessionEvent(sessionEvent.RawEventCode, sessionEvent.SessionId);
                break;
        }
    }

    private static void LogMonitorLifecycleEvent(MainWindowViewModel viewModel, SessionMonitorLifecycleEvent lifecycleEvent)
    {
        switch (lifecycleEvent.Type)
        {
            case SessionMonitorLifecycleEventType.HwndSourceUnavailable:
                viewModel.LogHwndSourceInitializationFailed();
                break;

            case SessionMonitorLifecycleEventType.WindowHandleUnavailable:
                viewModel.LogWindowHandleUnavailable();
                break;

            case SessionMonitorLifecycleEventType.RegistrationFailed:
                viewModel.LogWtsRegistrationFailed(lifecycleEvent.Win32Error ?? -1);
                break;

            case SessionMonitorLifecycleEventType.ListeningStarted:
                viewModel.LogSessionNotificationListening();
                break;
        }
    }
}
