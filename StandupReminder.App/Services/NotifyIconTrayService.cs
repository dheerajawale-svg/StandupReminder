using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.Foundation.Collections;
using Windows.UI.Notifications;
using Forms = System.Windows.Forms;

namespace StandupReminder.App.Services;

public sealed class NotifyIconTrayService : ITrayService
{
    private const int MaxTooltipLength = 63;
    private const int BalloonTipTimeoutMilliseconds = 5000;
    private const string SitReminderHeroImageRelativePath = "Assets\\sit_down_img.jpg";
    private const string SitReminderTag = "sit-reminder";
    private const string SitReminderGroup = "posture-reminders";
    private const string SitReminderKindArgument = "notification";
    private const string SitReminderKindValue = "sitReminder";
    private const string SitReminderAcknowledgeAction = "ackSitReminder";
    private const string SitReminderExtendAction = "extendSitReminder";
    private const string SitReminderExtendMinutesInputId = "sitReminderExtendMinutes";
    private const int DefaultSitReminderExtensionMinutes = 5;
    private static readonly TimeSpan SitReminderReshowDelay = TimeSpan.FromSeconds(1);
    private static readonly int[] AllowedSitReminderExtensionMinutes = [5, 10, 20, 30];

    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _contextMenu;
    private readonly Forms.ToolStripMenuItem _pauseMenuItem;
    private readonly object _sitReminderLock = new();

    private string? _sitReminderTitle;
    private string? _sitReminderMessage;
    private CancellationTokenSource? _sitReminderReshowCancellation;
    private bool _keepSitReminderVisible;
    private bool _disposed;

    public event EventHandler? OpenRequested;

    public event EventHandler? PauseResumeRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler? SitReminderAcknowledged;

    public event EventHandler<SitReminderExtendedEventArgs>? SitReminderExtended;

    public event EventHandler? SitReminderBodyActivated;

    public event EventHandler? SitReminderDismissed;

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

        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
    }

    public void Initialize()
    {
        _notifyIcon.Visible = true;
    }

    public void ShowNotification(string title, string message)
    {
        _notifyIcon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(BalloonTipTimeoutMilliseconds);
    }

    public void ShowPersistentNotification(string title, string message)
    {
        _sitReminderTitle = title;
        _sitReminderMessage = message;

        lock (_sitReminderLock)
        {
            _keepSitReminderVisible = true;
        }

        CancelSitReminderReshow();
        ShowSitReminderToast();
    }

    public void HandlePersistentNotificationActivation(string arguments, ValueSet? userInput)
    {
        var toastArguments = ToastArguments.Parse(arguments);

        if (!IsSitReminderToast(toastArguments))
        {
            return;
        }

        if (toastArguments.TryGetValue("action", out var action))
        {
            if (IsSitReminderAcknowledgeAction(action))
            {
                lock (_sitReminderLock)
                {
                    _keepSitReminderVisible = false;
                }

                CancelSitReminderReshow();
                SitReminderAcknowledged?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (IsSitReminderExtendAction(action))
            {
                var extensionMinutes = GetSitReminderExtensionMinutes(userInput);

                lock (_sitReminderLock)
                {
                    _keepSitReminderVisible = false;
                }

                CancelSitReminderReshow();
                SitReminderExtended?.Invoke(this, new SitReminderExtendedEventArgs(extensionMinutes));
                return;
            }
        }

        SitReminderBodyActivated?.Invoke(this, EventArgs.Empty);
        ScheduleSitReminderReshow();
    }

    public void DismissPersistentNotification()
    {
        lock (_sitReminderLock)
        {
            _keepSitReminderVisible = false;
        }

        CancelSitReminderReshow();
        ToastNotificationManagerCompat.History.Remove(SitReminderTag, SitReminderGroup);
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DismissPersistentNotification();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }

    private void ShowSitReminderToast()
    {
        if (string.IsNullOrWhiteSpace(_sitReminderTitle) || string.IsNullOrWhiteSpace(_sitReminderMessage))
        {
            return;
        }

        ToastNotificationManagerCompat.History.Remove(SitReminderTag, SitReminderGroup);

        new ToastContentBuilder()
            .AddArgument(SitReminderKindArgument, SitReminderKindValue)
            .AddText(_sitReminderTitle)
            //.AddText(_sitReminderMessage)            
            .SetToastScenario(ToastScenario.Reminder)
            .AddComboBox(
                SitReminderExtendMinutesInputId,
                "Extend by",
                DefaultSitReminderExtensionMinutes.ToString(),
                AllowedSitReminderExtensionMinutes.Select(minutes => (minutes.ToString(), minutes.ToString())))
            .AddButton(new ToastButton()
                .SetContent("OK")
                .AddArgument("action", SitReminderAcknowledgeAction))
            .AddButton(new ToastButton()
                .SetContent("Extend")
                .AddArgument("action", SitReminderExtendAction))
            .AddInlineImage(GetSitReminderHeroImageUri())
            .Show(toast =>
            {
                toast.Tag = SitReminderTag;
                toast.Group = SitReminderGroup;
                toast.Dismissed += OnSitReminderToastDismissed;
                toast.Failed += OnSitReminderToastFailed;
            });
    }

    private void OnSitReminderToastDismissed(ToastNotification sender, ToastDismissedEventArgs args)
    {
        _ = sender;

        if (args.Reason is not ToastDismissalReason.UserCanceled and not ToastDismissalReason.TimedOut)
        {
            return;
        }

        SitReminderDismissed?.Invoke(this, EventArgs.Empty);
        ScheduleSitReminderReshow();
    }

    private void OnSitReminderToastFailed(ToastNotification sender, ToastFailedEventArgs args)
    {
        _ = sender;
        _ = args;

        ScheduleSitReminderReshow();
    }

    private void ScheduleSitReminderReshow()
    {
        lock (_sitReminderLock)
        {
            if (!_keepSitReminderVisible || _disposed)
            {
                return;
            }
        }

        CancelSitReminderReshow();

        var cancellationSource = new CancellationTokenSource();
        _sitReminderReshowCancellation = cancellationSource;
        _ = ReshowSitReminderAsync(cancellationSource.Token);
    }

    private async Task ReshowSitReminderAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SitReminderReshowDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_sitReminderLock)
        {
            if (!_keepSitReminderVisible || _disposed || cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }

        ShowSitReminderToast();
    }

    private void CancelSitReminderReshow()
    {
        var cancellationSource = _sitReminderReshowCancellation;
        _sitReminderReshowCancellation = null;

        if (cancellationSource is null)
        {
            return;
        }

        cancellationSource.Cancel();
        cancellationSource.Dispose();
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

    private static Uri? GetSitReminderHeroImageUri()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        var executableDirectory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(executableDirectory))
        {
            return null;
        }

        var imagePath = Path.Combine(executableDirectory, SitReminderHeroImageRelativePath);
        return File.Exists(imagePath) ? new Uri(imagePath) : null;
    }

    private static bool IsSitReminderToast(ToastArguments arguments)
    {
        return arguments.TryGetValue(SitReminderKindArgument, out var kind)
            && string.Equals(kind, SitReminderKindValue, StringComparison.Ordinal);
    }

    private static int GetSitReminderExtensionMinutes(ValueSet? userInput)
    {
        if (userInput is not null
            && userInput.TryGetValue(SitReminderExtendMinutesInputId, out var rawValue)
            && int.TryParse(rawValue?.ToString(), out var minutes)
            && AllowedSitReminderExtensionMinutes.Contains(minutes))
        {
            return minutes;
        }

        return DefaultSitReminderExtensionMinutes;
    }

    public static bool IsSitReminderAcknowledgeAction(string? action)
    {
        return string.Equals(action, SitReminderAcknowledgeAction, StringComparison.Ordinal);
    }

    public static bool IsSitReminderExtendAction(string? action)
    {
        return string.Equals(action, SitReminderExtendAction, StringComparison.Ordinal);
    }
}
