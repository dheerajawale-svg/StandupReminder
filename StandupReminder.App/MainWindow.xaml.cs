using System;
using System.Windows;
using StandupReminder.App.Services;
using StandupReminder.App.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace StandupReminder.App;

public partial class MainWindow : FluentWindow
{
    private const int WTS_SESSION_LOGON = 0x0005;
    private const int WTS_SESSION_LOCK = 0x0007;
    private const int WTS_SESSION_UNLOCK = 0x0008;

    private readonly MainWindowViewModel _viewModel;

    private WindowsSessionEventMonitor? _sessionMonitor;

    public MainWindow()
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;

        SourceInitialized += OnSourceInitialized;
        Closing += OnWindowClosing;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _sessionMonitor = CreateSessionEventMonitor(this, _viewModel);
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _ = sender;
        _ = e;

        _sessionMonitor?.Dispose();
        _sessionMonitor = null;
    }

    // Static entry point requested for wiring session detection into MainWindow.
    private static WindowsSessionEventMonitor CreateSessionEventMonitor(Window window, MainWindowViewModel viewModel)
    {
        return WindowsSessionEventMonitor.Attach(
            window,
            onSessionChanged: (sessionEvent, sessionId) => LogSessionEvent(viewModel, sessionEvent, sessionId),
            onLifecycleChanged: lifecycleEvent => LogMonitorLifecycleEvent(viewModel, lifecycleEvent)
        );
    }

    private static void LogSessionEvent(MainWindowViewModel viewModel, int sessionEvent, int sessionId)
    {
        switch (sessionEvent)
        {
            case WTS_SESSION_LOCK:
                viewModel.LogSessionLock(sessionId);
                break;

            case WTS_SESSION_UNLOCK:
                viewModel.LogSessionUnlock(sessionId);
                break;

            case WTS_SESSION_LOGON:
                viewModel.LogSessionLogon(sessionId);
                break;

            default:
                viewModel.LogUnknownSessionEvent(sessionEvent, sessionId);
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
