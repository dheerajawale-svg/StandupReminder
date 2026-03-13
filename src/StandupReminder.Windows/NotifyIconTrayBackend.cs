using System.Drawing;
using System.IO;
using Forms = System.Windows.Forms;

namespace StandupReminder.Windows;

public sealed class NotifyIconTrayBackend : IDisposable
{
    private const int MaxTooltipLength = 63;
    private const int BalloonTipTimeoutMilliseconds = 5000;
    private const int PersistentBalloonIntervalMilliseconds = 10000;

    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.Timer _persistentBalloonTimer;
    private string? _persistentBalloonTitle;
    private string? _persistentBalloonMessage;
    private bool _disposed;

    public NotifyIconTrayBackend()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = LoadApplicationIcon(),
            Text = "Standup Reminder",
            Visible = false
        };

        _persistentBalloonTimer = new Forms.Timer
        {
            Interval = PersistentBalloonIntervalMilliseconds
        };

        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
        _notifyIcon.BalloonTipClicked += OnBalloonTipClicked;
        _persistentBalloonTimer.Tick += OnPersistentBalloonTimerTick;
    }

    public event EventHandler? LeftClickRequested;

    public event EventHandler<NotifyIconRightClickEventArgs>? RightClickRequested;

    public void Initialize()
    {
        ThrowIfDisposed();
        _notifyIcon.Visible = true;
    }

    public void ShowBalloonTip(string title, string message)
    {
        ThrowIfDisposed();
        _notifyIcon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(BalloonTipTimeoutMilliseconds);
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
    }

    public void UpdateStatus(string statusText)
    {
        ThrowIfDisposed();
        var tooltip = $"Standup Reminder - {statusText}";
        _notifyIcon.Text = tooltip.Length <= MaxTooltipLength
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
        _persistentBalloonTimer.Tick -= OnPersistentBalloonTimerTick;
        _persistentBalloonTimer.Dispose();
        _notifyIcon.MouseClick -= OnNotifyIconMouseClick;
        _notifyIcon.BalloonTipClicked -= OnBalloonTipClicked;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnNotifyIconMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        _ = sender;

        if (e.Button == Forms.MouseButtons.Left)
        {
            LeftClickRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (e.Button == Forms.MouseButtons.Right)
        {
            RightClickRequested?.Invoke(this, new NotifyIconRightClickEventArgs(Forms.Control.MousePosition));
        }
    }

    private void OnBalloonTipClicked(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        DismissPersistentBalloonTip();
    }

    private void OnPersistentBalloonTimerTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

public sealed class NotifyIconRightClickEventArgs : EventArgs
{
    public NotifyIconRightClickEventArgs(Point screenPosition)
    {
        ScreenPosition = screenPosition;
    }

    public Point ScreenPosition { get; }
}
