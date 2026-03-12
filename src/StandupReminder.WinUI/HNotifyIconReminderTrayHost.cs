using System.Drawing;
using System.IO;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using StandupReminder.Core.Services;
using Windows.Graphics;
using POINT = System.Drawing.Point;

namespace StandupReminder.WinUI;

internal sealed class HNotifyIconReminderTrayHost : IReminderTrayHost
{
    private const int MaxTooltipLength = 63;
    private const int BalloonTipTimeoutMilliseconds = 5000;
    private const int PersistentBalloonIntervalMilliseconds = 10000;

    private readonly TaskbarIcon _trayIcon;
    private readonly XamlUICommand _openCommand;
    private readonly XamlUICommand _showTrayMenuCommand;
    private readonly DispatcherQueueTimer _persistentBalloonTimer;
    private readonly TrayMenuWindow _trayMenuWindow;
    private Icon? _resolvedIcon;
    private string? _persistentBalloonTitle;
    private string? _persistentBalloonMessage;
    private bool _initialized;
    private bool _disposed;

    public HNotifyIconReminderTrayHost()
    {
        _trayIcon = GetResource<TaskbarIcon>("TrayIcon");
        _openCommand = GetResource<XamlUICommand>("OpenTrayCommand");
        _showTrayMenuCommand = GetResource<XamlUICommand>("ShowTrayMenuCommand");
        _trayMenuWindow = new TrayMenuWindow();

        var dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("Tray host must be created on the WinUI dispatcher thread.");

        _persistentBalloonTimer = dispatcherQueue.CreateTimer();
        _persistentBalloonTimer.Interval = TimeSpan.FromMilliseconds(PersistentBalloonIntervalMilliseconds);
        _persistentBalloonTimer.IsRepeating = true;

        _openCommand.ExecuteRequested += OnOpenCommandExecuteRequested;
        _showTrayMenuCommand.ExecuteRequested += OnShowTrayMenuCommandExecuteRequested;
        _persistentBalloonTimer.Tick += OnPersistentBalloonTimerTick;
        _trayMenuWindow.OpenRequested += OnTrayMenuWindowOpenRequested;
        _trayMenuWindow.PauseResumeRequested += OnTrayMenuWindowPauseResumeRequested;
        _trayMenuWindow.SettingsRequested += OnTrayMenuWindowSettingsRequested;
        _trayMenuWindow.ExitRequested += OnTrayMenuWindowExitRequested;
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
        _resolvedIcon = LoadApplicationIcon();
        _trayIcon.UpdateIcon(_resolvedIcon);
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
        _trayMenuWindow.PauseResumeLabel = isPaused ? "Resume timer" : "Pause timer";
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
        _showTrayMenuCommand.ExecuteRequested -= OnShowTrayMenuCommandExecuteRequested;
        _persistentBalloonTimer.Tick -= OnPersistentBalloonTimerTick;
        _trayMenuWindow.OpenRequested -= OnTrayMenuWindowOpenRequested;
        _trayMenuWindow.PauseResumeRequested -= OnTrayMenuWindowPauseResumeRequested;
        _trayMenuWindow.SettingsRequested -= OnTrayMenuWindowSettingsRequested;
        _trayMenuWindow.ExitRequested -= OnTrayMenuWindowExitRequested;
        _trayMenuWindow.Close();
        _trayIcon.Dispose();
        _resolvedIcon?.Dispose();
        _resolvedIcon = null;
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

    private void OnShowTrayMenuCommandExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        _ = sender;
        _ = args;

        if (_trayMenuWindow.IsVisible)
        {
            _trayMenuWindow.HideMenu();
            return;
        }

        _trayMenuWindow.ShowAt(GetCursorPosition());
    }

    private void OnTrayMenuWindowOpenRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnTrayMenuWindowPauseResumeRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        PauseResumeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnTrayMenuWindowSettingsRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnTrayMenuWindowExitRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
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

    private static Icon LoadApplicationIcon()
    {
        var executablePath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
        {
            var extractedIcon = Icon.ExtractAssociatedIcon(executablePath);
            if (extractedIcon is not null)
            {
                return extractedIcon;
            }
        }

        return SystemIcons.Application;
    }

    private static PointInt32 GetCursorPosition()
    {
        if (!GetCursorPos(out var point))
        {
            throw new InvalidOperationException("Failed to get the cursor position.");
        }

        return new PointInt32(point.X, point.Y);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
