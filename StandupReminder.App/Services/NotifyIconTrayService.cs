using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;
using StandupReminder.App.Models;
using Windows.Foundation.Collections;
using Windows.UI.Notifications;
using Forms = System.Windows.Forms;
using Application = System.Windows.Application;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

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
    private readonly Forms.Timer _liveTooltipTimer;
    private readonly object _sitReminderLock = new();

    private string? _sitReminderTitle;
    private string? _sitReminderMessage;
    private string _pauseMenuLabel = "Pause timer";
    private CancellationTokenSource? _sitReminderReshowCancellation;
    private TrayMenuWindow? _trayMenuWindow;
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
        _notifyIcon = new Forms.NotifyIcon
        {
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
        _pauseMenuLabel = isPaused ? "Resume timer" : "Pause timer";

        if (_trayMenuWindow is not null)
        {
            _trayMenuWindow.SetPauseResumeLabel(_pauseMenuLabel);
        }
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
        CloseTrayMenu();
        _liveTooltipTimer.Stop();
        _liveTooltipTimer.Tick -= OnLiveTooltipTimerTick;
        _notifyIcon.MouseClick -= OnNotifyIconMouseClick;
        _notifyIcon.MouseMove -= OnNotifyIconMouseMove;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _liveTooltipTimer.Dispose();
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
            CloseTrayMenu();
            OpenRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (e.Button == Forms.MouseButtons.Right)
        {
            ToggleTrayMenu();
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

    private void ToggleTrayMenu()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.Invoke(() =>
        {
            if (_trayMenuWindow is not null)
            {
                CloseTrayMenuCore();
                return;
            }

            var trayMenuWindow = CreateTrayMenuWindow();
            _trayMenuWindow = trayMenuWindow;
            trayMenuWindow.Closed += OnTrayMenuWindowClosed;
            trayMenuWindow.Left = -10000;
            trayMenuWindow.Top = -10000;
            trayMenuWindow.Opacity = 0;
            trayMenuWindow.Show();
            PositionTrayMenuWindow(trayMenuWindow);
            trayMenuWindow.Opacity = 1;
            trayMenuWindow.Activate();
        });
    }

    private TrayMenuWindow CreateTrayMenuWindow()
    {
        var trayMenuWindow = new TrayMenuWindow();
        trayMenuWindow.SetPauseResumeLabel(_pauseMenuLabel);
        trayMenuWindow.OpenSelected += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        trayMenuWindow.PauseResumeSelected += (_, _) => PauseResumeRequested?.Invoke(this, EventArgs.Empty);
        trayMenuWindow.SettingsSelected += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        trayMenuWindow.ExitSelected += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        return trayMenuWindow;
    }

    private void PositionTrayMenuWindow(TrayMenuWindow trayMenuWindow)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(trayMenuWindow).Handle;
        var dpi = GetDpiForWindow(handle);
        var dpiScaleX = dpi / 96d;
        var dpiScaleY = dpi / 96d;
        var cursor = Forms.Control.MousePosition;
        var workingArea = Forms.Screen.FromPoint(cursor).WorkingArea;

        var left = CalculateHorizontalPosition(cursor, workingArea, trayMenuWindow.Width * dpiScaleX) / dpiScaleX;
        var top = CalculateVerticalPosition(cursor, workingArea, trayMenuWindow.Height * dpiScaleY) / dpiScaleY;

        trayMenuWindow.Left = left;
        trayMenuWindow.Top = top;
    }

    private void CloseTrayMenu()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.Invoke(CloseTrayMenuCore);
    }

    private void CloseTrayMenuCore()
    {
        var trayMenuWindow = _trayMenuWindow;
        if (trayMenuWindow is null)
        {
            return;
        }

        _trayMenuWindow = null;
        trayMenuWindow.Closed -= OnTrayMenuWindowClosed;
        trayMenuWindow.Close();
    }

    private void OnTrayMenuWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not TrayMenuWindow trayMenuWindow)
        {
            return;
        }

        trayMenuWindow.Closed -= OnTrayMenuWindowClosed;

        if (ReferenceEquals(_trayMenuWindow, trayMenuWindow))
        {
            _trayMenuWindow = null;
        }
    }

    private static double CalculateHorizontalPosition(Point cursor, Rectangle workingArea, double menuWidth)
    {
        var desiredLeft = cursor.X >= workingArea.Left + (workingArea.Width / 2)
            ? cursor.X - menuWidth - 8
            : cursor.X + 8;
        var maxLeft = workingArea.Right - menuWidth - 8;

        return Math.Clamp(desiredLeft, workingArea.Left + 8, maxLeft);
    }

    private static double CalculateVerticalPosition(Point cursor, Rectangle workingArea, double menuHeight)
    {
        var desiredTop = cursor.Y >= workingArea.Top + (workingArea.Height / 2)
            ? cursor.Y - menuHeight - 8
            : cursor.Y + 8;
        var maxTop = workingArea.Bottom - menuHeight - 8;

        return Math.Clamp(desiredTop, workingArea.Top + 8, maxTop);
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);
}
