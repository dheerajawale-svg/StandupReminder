using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using StandupReminder.Core.Services;

namespace StandupReminder.WinUI;

internal sealed class HNotifyIconReminderTrayHost : IReminderTrayHost
{
    private const int MaxTooltipLength = 63;
    private const int BalloonTipTimeoutMilliseconds = 5000;
    private const int PersistentBalloonIntervalMilliseconds = 10000;

    private readonly TaskbarIcon _trayIcon;
    private readonly XamlUICommand _openCommand;
    private readonly XamlUICommand _pauseResumeCommand;
    private readonly XamlUICommand _settingsCommand;
    private readonly XamlUICommand _exitCommand;
    private readonly DispatcherQueueTimer _persistentBalloonTimer;
    private string? _persistentBalloonTitle;
    private string? _persistentBalloonMessage;
    private bool _initialized;
    private bool _disposed;

    public HNotifyIconReminderTrayHost()
    {
        _trayIcon = GetResource<TaskbarIcon>("TrayIcon");
        _openCommand = GetResource<XamlUICommand>("OpenTrayCommand");
        _pauseResumeCommand = GetResource<XamlUICommand>("PauseResumeTrayCommand");
        _settingsCommand = GetResource<XamlUICommand>("SettingsTrayCommand");
        _exitCommand = GetResource<XamlUICommand>("ExitTrayCommand");

        var dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("Tray host must be created on the WinUI dispatcher thread.");

        _persistentBalloonTimer = dispatcherQueue.CreateTimer();
        _persistentBalloonTimer.Interval = TimeSpan.FromMilliseconds(PersistentBalloonIntervalMilliseconds);
        _persistentBalloonTimer.IsRepeating = true;

        _openCommand.ExecuteRequested += OnOpenCommandExecuteRequested;
        _pauseResumeCommand.ExecuteRequested += OnPauseResumeCommandExecuteRequested;
        _settingsCommand.ExecuteRequested += OnSettingsCommandExecuteRequested;
        _exitCommand.ExecuteRequested += OnExitCommandExecuteRequested;
        _persistentBalloonTimer.Tick += OnPersistentBalloonTimerTick;
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? PauseResumeRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        ThrowIfDisposed();

        if (_initialized)
        {
            return;
        }

        _trayIcon.ForceCreate(enablesEfficiencyMode: false);
        _initialized = true;
    }

    public void ShowBalloonTip(string title, string message)
    {
        ThrowIfDisposed();

        _trayIcon.ShowNotification(
            title,
            message,
            NotificationIcon.Info,
            timeout: TimeSpan.FromMilliseconds(BalloonTipTimeoutMilliseconds));
    }

    public void ShowPersistentBalloonTip(string title, string message)
    {
        ThrowIfDisposed();

        _persistentBalloonTitle = title;
        _persistentBalloonMessage = message;

        ShowBalloonTip(title, message);
        _persistentBalloonTimer.Stop();
        _persistentBalloonTimer.Start();
    }

    public void DismissPersistentBalloonTip()
    {
        if (_disposed)
        {
            return;
        }

        _persistentBalloonTimer.Stop();
        _persistentBalloonTitle = null;
        _persistentBalloonMessage = null;
        _trayIcon.ClearNotifications();
    }

    public void SetPauseMenuLabel(bool isPaused)
    {
        ThrowIfDisposed();
        _pauseResumeCommand.Label = isPaused ? "Resume timer" : "Pause timer";
    }

    public void UpdateStatus(string statusText)
    {
        ThrowIfDisposed();

        var tooltip = $"Standup Reminder - {statusText}";
        _trayIcon.ToolTipText = tooltip.Length <= MaxTooltipLength
            ? tooltip
            : tooltip[..MaxTooltipLength];
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DismissPersistentBalloonTip();
        _openCommand.ExecuteRequested -= OnOpenCommandExecuteRequested;
        _pauseResumeCommand.ExecuteRequested -= OnPauseResumeCommandExecuteRequested;
        _settingsCommand.ExecuteRequested -= OnSettingsCommandExecuteRequested;
        _exitCommand.ExecuteRequested -= OnExitCommandExecuteRequested;
        _persistentBalloonTimer.Tick -= OnPersistentBalloonTimerTick;
        _trayIcon.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static T GetResource<T>(string key) where T : class
    {
        return Application.Current.Resources[key] as T
            ?? throw new InvalidOperationException($"App resource '{key}' was not found.");
    }

    private void OnOpenCommandExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        _ = sender;
        _ = args;
        OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPauseResumeCommandExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        _ = sender;
        _ = args;
        PauseResumeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsCommandExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        _ = sender;
        _ = args;
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnExitCommandExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        _ = sender;
        _ = args;
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPersistentBalloonTimerTick(DispatcherQueueTimer sender, object args)
    {
        _ = sender;
        _ = args;

        if (string.IsNullOrWhiteSpace(_persistentBalloonTitle)
            || string.IsNullOrWhiteSpace(_persistentBalloonMessage))
        {
            DismissPersistentBalloonTip();
            return;
        }

        ShowBalloonTip(_persistentBalloonTitle, _persistentBalloonMessage);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
