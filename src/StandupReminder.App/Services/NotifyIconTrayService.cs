using System.Drawing;
using System.IO;
using Forms = System.Windows.Forms;

namespace StandupReminder.App.Services;

public sealed class NotifyIconTrayService : ITrayService
{
    private const int MaxTooltipLength = 63;
    private const int BalloonTipTimeoutMilliseconds = 5000;
    private const int PersistentBalloonIntervalMilliseconds = 10000;

    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _contextMenu;
    private readonly Forms.ToolStripMenuItem _pauseMenuItem;
    private readonly Forms.Timer _persistentBalloonTimer;
    private string? _persistentBalloonTitle;
    private string? _persistentBalloonMessage;

    public event EventHandler? OpenRequested;

    public event EventHandler? PauseResumeRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public NotifyIconTrayService()
    {
        _contextMenu = new Forms.ContextMenuStrip();
        _contextMenu.Items.Add("Open", null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
        _pauseMenuItem = new Forms.ToolStripMenuItem("Pause timer", null, (_, _) => PauseResumeRequested?.Invoke(this, EventArgs.Empty));
        _contextMenu.Items.Add(_pauseMenuItem);
        _contextMenu.Items.Add("Settings", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        _contextMenu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _contextMenu,
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

    public void Initialize()
    {
        _notifyIcon.Visible = true;
    }

    public void ShowBalloonTip(string title, string message)
    {
        _notifyIcon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(BalloonTipTimeoutMilliseconds);
    }

    public void ShowPersistentBalloonTip(string title, string message)
    {
        _persistentBalloonTitle = title;
        _persistentBalloonMessage = message;

        ShowBalloonTip(title, message);
        _persistentBalloonTimer.Stop();
        _persistentBalloonTimer.Start();
    }

    public void DismissPersistentBalloonTip()
    {
        _persistentBalloonTimer.Stop();
        _persistentBalloonTitle = null;
        _persistentBalloonMessage = null;
    }

    public void SetPauseMenuLabel(bool isPaused)
    {
        _pauseMenuItem.Text = isPaused ? "Resume timer" : "Pause timer";
    }

    public void UpdateStatus(string statusText)
    {
        var tooltip = $"Standup Reminder - {statusText}";
        _notifyIcon.Text = tooltip.Length <= MaxTooltipLength
            ? tooltip
            : tooltip[..MaxTooltipLength];
    }

    public void Dispose()
    {
        DismissPersistentBalloonTip();
        _persistentBalloonTimer.Tick -= OnPersistentBalloonTimerTick;
        _persistentBalloonTimer.Dispose();
        _notifyIcon.BalloonTipClicked -= OnBalloonTipClicked;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }

    private void OnNotifyIconMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        _ = sender;

        if (e.Button == Forms.MouseButtons.Left)
        {
            OpenRequested?.Invoke(this, EventArgs.Empty);
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
}
