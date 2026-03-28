using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;
using StandupReminder.App.Models;
using Windows.Foundation.Collections;
using Windows.UI.Notifications;
using Forms = System.Windows.Forms;

namespace StandupReminder.App.Services;

public sealed class NotifyIconTrayService : ITrayService
{
    private const int MaxTooltipLength = 63;
    private const int BalloonTipTimeoutMilliseconds = 5000;
    private static readonly TimeSpan LiveTooltipDuration = TimeSpan.FromSeconds(5);
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
    private readonly Forms.Timer _liveTooltipTimer;
    private readonly object _sitReminderLock = new();

    private string? _sitReminderTitle;
    private string? _sitReminderMessage;
    private CancellationTokenSource? _sitReminderReshowCancellation;
    private DateTimeOffset _liveTooltipUntil;
    private DateTimeOffset _statusCapturedAt;
    private ReminderPhase _currentPhase = ReminderPhase.Idle;
    private TimeSpan _currentRemainingTime = TimeSpan.Zero;
    private bool _keepSitReminderVisible;
    private bool _hasStatusSnapshot;
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

        _liveTooltipTimer = new Forms.Timer
        {
            Interval = (int)TimeSpan.FromSeconds(1).TotalMilliseconds
        };

        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
        _notifyIcon.MouseMove += OnNotifyIconMouseMove;
        _liveTooltipTimer.Tick += OnLiveTooltipTimerTick;
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

    public void UpdateStatus(ReminderPhase phase, TimeSpan remainingTime)
    {
        var now = DateTimeOffset.Now;
        var phaseChanged = !_hasStatusSnapshot || _currentPhase != phase;

        _currentPhase = phase;
        _currentRemainingTime = remainingTime > TimeSpan.Zero ? remainingTime : TimeSpan.Zero;
        _statusCapturedAt = now;
        _hasStatusSnapshot = true;

        if (phaseChanged || IsLiveTooltipActive(now))
        {
            ApplyTooltip(now);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DismissPersistentNotification();
        _liveTooltipTimer.Stop();
        _liveTooltipTimer.Tick -= OnLiveTooltipTimerTick;
        _notifyIcon.MouseMove -= OnNotifyIconMouseMove;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _liveTooltipTimer.Dispose();
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

    private void OnNotifyIconMouseMove(object? sender, Forms.MouseEventArgs e)
    {
        _ = sender;
        _ = e;

        var now = DateTimeOffset.Now;
        _liveTooltipUntil = now + LiveTooltipDuration;
        ApplyTooltip(now);

        if (!_liveTooltipTimer.Enabled)
        {
            _liveTooltipTimer.Start();
        }
    }

    private void OnLiveTooltipTimerTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        var now = DateTimeOffset.Now;
        if (!IsLiveTooltipActive(now))
        {
            _liveTooltipTimer.Stop();
        }

        ApplyTooltip(now);
    }

    private void ApplyTooltip(DateTimeOffset now)
    {
        var tooltip = BuildTooltipText(now);
        _notifyIcon.Text = tooltip.Length <= MaxTooltipLength
            ? tooltip
            : tooltip[..MaxTooltipLength];
    }

    private string BuildTooltipText(DateTimeOffset now)
    {
        if (!_hasStatusSnapshot)
        {
            return "Standup Reminder";
        }

        if (!IsLiveTooltipActive(now))
        {
            return $"Standup Reminder - {GetPhaseLabel(_currentPhase)}";
        }

        return $"Standup Reminder - {GetPhaseLabel(_currentPhase)} - {GetRemainingLabel(now)}";
    }

    private string GetRemainingLabel(DateTimeOffset now)
    {
        return _currentPhase switch
        {
            ReminderPhase.StandPromptPending => "awaiting action",
            ReminderPhase.SitPromptPending => "awaiting OK or Extend",
            ReminderPhase.PausedManually when GetDisplayRemainingTime(now) == TimeSpan.Zero => "paused",
            _ => $"{GetDisplayRemainingTime(now):hh\\:mm\\:ss} remaining"
        };
    }

    private TimeSpan GetDisplayRemainingTime(DateTimeOffset now)
    {
        if (!ShouldAgeRemainingTime(_currentPhase))
        {
            return _currentRemainingTime;
        }

        var remaining = _currentRemainingTime - (now - _statusCapturedAt);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private bool IsLiveTooltipActive(DateTimeOffset now)
    {
        return _liveTooltipUntil > now;
    }

    private static bool ShouldAgeRemainingTime(ReminderPhase phase)
    {
        return phase is ReminderPhase.SittingCountdown
            or ReminderPhase.SnoozedCountdown
            or ReminderPhase.StandingCountdown;
    }

    private static string GetPhaseLabel(ReminderPhase phase)
    {
        return phase switch
        {
            ReminderPhase.SittingCountdown => "Sitting",
            ReminderPhase.StandingCountdown => "Standing",
            ReminderPhase.StandPromptPending => "Stand-up confirmation",
            ReminderPhase.SitPromptPending => "Sit confirmation",
            ReminderPhase.SnoozedCountdown => "Snoozed",
            ReminderPhase.PausedManually => "Paused manually",
            ReminderPhase.PausedForLock => "Paused for lock",
            _ => "Starting"
        };
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
