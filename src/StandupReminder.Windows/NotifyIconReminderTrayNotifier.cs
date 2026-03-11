using System.Drawing;
using System.IO;
using StandupReminder.Core.Services;
using Forms = System.Windows.Forms;

namespace StandupReminder.Windows;

public sealed class NotifyIconReminderTrayNotifier : IReminderTrayNotifier, IDisposable
{
    private const int BalloonTipTimeoutMilliseconds = 5000;
    private const int PersistentBalloonIntervalMilliseconds = 10000;

    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.Timer _persistentBalloonTimer;
    private string? _persistentBalloonTitle;
    private string? _persistentBalloonMessage;
    private bool _disposed;

    public NotifyIconReminderTrayNotifier()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = LoadApplicationIcon(),
            Text = "Standup Reminder",
            Visible = true
        };

        _persistentBalloonTimer = new Forms.Timer
        {
            Interval = PersistentBalloonIntervalMilliseconds
        };

        _notifyIcon.BalloonTipClicked += OnBalloonTipClicked;
        _persistentBalloonTimer.Tick += OnPersistentBalloonTimerTick;
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DismissPersistentBalloonTip();
        _persistentBalloonTimer.Tick -= OnPersistentBalloonTimerTick;
        _persistentBalloonTimer.Dispose();
        _notifyIcon.BalloonTipClicked -= OnBalloonTipClicked;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
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