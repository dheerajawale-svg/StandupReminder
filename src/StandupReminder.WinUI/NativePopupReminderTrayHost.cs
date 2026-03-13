using Microsoft.UI.Dispatching;
using StandupReminder.Core.Services;
using StandupReminder.Windows;

namespace StandupReminder.WinUI;

internal sealed class NativePopupReminderTrayHost : IReminderTrayHost
{
    private readonly NotifyIconTrayBackend _trayBackend;
    private readonly NativeTrayPopupMenuHost _popupMenuHost;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _disposed;

    public NativePopupReminderTrayHost()
    {
        _trayBackend = new NotifyIconTrayBackend();
        _popupMenuHost = new NativeTrayPopupMenuHost();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("Tray host must be created on the WinUI dispatcher thread.");

        _trayBackend.LeftClickRequested += OnTrayBackendLeftClickRequested;
        _trayBackend.RightClickRequested += OnTrayBackendRightClickRequested;
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? PauseResumeRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        ThrowIfDisposed();
        _trayBackend.Initialize();
    }

    public void ShowBalloonTip(string title, string message)
    {
        ThrowIfDisposed();
        _trayBackend.ShowBalloonTip(title, message);
    }

    public void ShowPersistentBalloonTip(string title, string message)
    {
        ThrowIfDisposed();
        _trayBackend.ShowPersistentBalloonTip(title, message);
    }

    public void DismissPersistentBalloonTip()
    {
        if (_disposed)
        {
            return;
        }

        _trayBackend.DismissPersistentBalloonTip();
    }

    public void SetPauseMenuLabel(bool isPaused)
    {
        ThrowIfDisposed();
        _popupMenuHost.PauseResumeLabel = isPaused ? "Resume timer" : "Pause timer";
    }

    public void UpdateStatus(string statusText)
    {
        ThrowIfDisposed();
        _trayBackend.UpdateStatus(statusText);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _trayBackend.LeftClickRequested -= OnTrayBackendLeftClickRequested;
        _trayBackend.RightClickRequested -= OnTrayBackendRightClickRequested;
        _popupMenuHost.Dispose();
        _trayBackend.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnTrayBackendLeftClickRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        EnqueueOnUiThread(() => OpenRequested?.Invoke(this, EventArgs.Empty));
    }

    private void OnTrayBackendRightClickRequested(object? sender, NotifyIconRightClickEventArgs e)
    {
        _ = sender;
        EnqueueOnUiThread(() =>
        {
            var command = _popupMenuHost.ShowMenu(e.ScreenPosition);
            switch (command)
            {
                case TrayPopupMenuCommand.Open:
                    OpenRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case TrayPopupMenuCommand.PauseResume:
                    PauseResumeRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case TrayPopupMenuCommand.Settings:
                    SettingsRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case TrayPopupMenuCommand.Exit:
                    ExitRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        });
    }

    private void EnqueueOnUiThread(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        if (!_dispatcherQueue.TryEnqueue(() => action()))
        {
            throw new InvalidOperationException("Failed to marshal tray work to the WinUI dispatcher.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
