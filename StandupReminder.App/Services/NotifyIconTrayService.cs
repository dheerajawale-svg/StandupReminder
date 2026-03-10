using System.Drawing;
using System.IO;
using Forms = System.Windows.Forms;

namespace StandupReminder.App.Services;

public sealed class NotifyIconTrayService : ITrayService
{
    private const int MaxTooltipLength = 63;

    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _contextMenu;

    public event EventHandler? OpenRequested;

    public event EventHandler? ExitRequested;

    public NotifyIconTrayService()
    {
        _contextMenu = new Forms.ContextMenuStrip();
        _contextMenu.Items.Add("Open", null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
        _contextMenu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _contextMenu,
            Icon = LoadApplicationIcon(),
            Text = "Standup Reminder",
            Visible = false
        };

        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
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
        _notifyIcon.ShowBalloonTip(5000);
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
