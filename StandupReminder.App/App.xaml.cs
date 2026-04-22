using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using StandupReminder.App.Models;
using StandupReminder.App.Services;
using StandupReminder.App.ViewModels;
using Wpf.Ui.Appearance;
using Forms = System.Windows.Forms;

namespace StandupReminder.App;

public partial class App : System.Windows.Application
{
    private const string CleanupToastArgument = "--cleanup-toast";
    private const string StartupDelayArgumentPrefix = "--startup-delay=";
    private static readonly TimeSpan ActiveCountdownPersistenceInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PersistenceFailureLogInterval = TimeSpan.FromMinutes(5);

    private ITrayService? _trayService;
    private IReminderSettingsStore? _settingsStore;
    private IAppearanceSettingsStore? _appearanceSettingsStore;
    private IReminderRuntimeStateStore? _runtimeStateStore;
    private ILogger<App>? _logger;
    private IPostureReminderScheduler? _scheduler;
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _mainWindowViewModel;
    private SettingsWindow? _settingsWindow;
    private ReminderRuntimeState? _lastPersistedRuntimeState;
    private DateTimeOffset? _lastRuntimeStatePersistedAt;
    private string? _lastRuntimeStatePersistenceErrorMessage;
    private DateTimeOffset? _lastRuntimeStatePersistenceErrorAt;
    private ReminderScheduleOptions _currentReminderOptions = new();
    private AppearanceSettings _currentAppearanceSettings = new();
    private bool _isShuttingDown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize application logger first
        ApplicationLogger.Initialize();
        _logger = ApplicationLogger.CreateLogger<App>();

        if (e.Args.Any(argument => string.Equals(argument, CleanupToastArgument, StringComparison.OrdinalIgnoreCase)))
        {
            CleanupToastArtifacts();
            Shutdown();
            return;
        }

        ApplyStartupDelay(e.Args);

        Forms.Application.SetColorMode(Forms.SystemColorMode.Dark);
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;

        ApplicationThemeManager.ApplySystemTheme();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        new FirstRunSplashWindow().ShowDialog();

        var settingsStore = new LocalAppDataReminderSettingsStore();
        var settingsLoadResult = settingsStore.Load();
        var appearanceSettingsStore = new LocalAppDataAppearanceSettingsStore();
        var appearanceSettings = appearanceSettingsStore.Load();
        var runtimeStateStore = new LocalAppDataReminderRuntimeStateStore();
        var runtimeStateLoadResult = runtimeStateStore.Load();
        var viewModel = new MainWindowViewModel(null!);
        var trayService = new NotifyIconTrayService();
        var scheduler = new PostureReminderScheduler(settingsLoadResult.Options, appearanceSettings, trayService);

        _settingsStore = settingsStore;
        _appearanceSettingsStore = appearanceSettingsStore;
        _runtimeStateStore = runtimeStateStore;
        _trayService = trayService;
        _scheduler = scheduler;
        _mainWindowViewModel = viewModel;
        _currentReminderOptions = settingsLoadResult.Options;
        _currentAppearanceSettings = appearanceSettings;

        viewModel.AttachScheduler(scheduler);

        _mainWindow = new MainWindow(viewModel, scheduler);
        _mainWindow.PrepareForBackgroundLaunch();
        MainWindow = _mainWindow;

        trayService.OpenRequested += (_, _) => _mainWindow.ShowFromTray();
        trayService.PauseResumeRequested += (_, _) => ToggleManualPause();
        trayService.SwitchModeRequested += (_, _) => SwitchReminderMode();
        trayService.SettingsRequested += (_, _) => ShowSettingsWindow();
        trayService.ExitRequested += (_, _) => PerformShutdown();
        trayService.SitReminderAcknowledged += (_, _) => _mainWindowViewModel?.LogSystemMessage("Sit reminder acknowledged from Windows notification.");
        trayService.SitReminderExtended += (_, args) => _mainWindowViewModel?.LogSystemMessage($"Sit reminder extended by {args.Minutes} minutes from Windows notification. The next sitting timer will also gain {args.Minutes} minutes.");
        trayService.SitReminderBodyActivated += (_, _) => _mainWindowViewModel?.LogSystemMessage("Sit reminder body clicked. Waiting for OK or Extend.");
        trayService.SitReminderDismissed += (_, _) => _mainWindowViewModel?.LogSystemMessage("Sit reminder dismissed from Windows notification. Re-showing reminder.");
        scheduler.StateChanged += (_, _) => OnSchedulerStateChanged(trayService, scheduler);

        trayService.Initialize();
        UpdateTrayState(trayService, scheduler);

        RestoreOrStartScheduler(runtimeStateLoadResult);
        viewModel.LogSystemMessage("Application started in tray mode.");

        if (!string.IsNullOrWhiteSpace(settingsLoadResult.WarningMessage))
        {
            viewModel.LogSystemMessage(settingsLoadResult.WarningMessage);
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        PersistRuntimeState(force: true);
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        PersistRuntimeState(force: true);
        _scheduler?.Dispose();
        _trayService?.Dispose();
        ApplicationLogger.Shutdown();
        ToastNotificationManagerCompat.OnActivated -= OnToastActivated;
        base.OnExit(e);
    }

    private void PerformShutdown()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;
        _trayService?.DismissPersistentNotification();

        if (_mainWindow is not null)
        {
            _mainWindow.PrepareForExit();
            _mainWindow.Close();
        }

        Shutdown();
    }

    private void ShowSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            if (!_settingsWindow.IsVisible)
            {
                _settingsWindow.Show();
            }

            _settingsWindow.Activate();
            _settingsWindow.Focus();
            return;
        }

        var viewModel = new SettingsWindowViewModel(_currentReminderOptions, _currentAppearanceSettings);
        var settingsWindow = new SettingsWindow(viewModel, SaveSettings)
        {
            Owner = _mainWindow
        };

        settingsWindow.Closed += OnSettingsWindowClosed;

        _settingsWindow = settingsWindow;
        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private void OnSettingsWindowClosed(object? sender, EventArgs e)
    {
        if (sender is SettingsWindow settingsWindow)
        {
            settingsWindow.Closed -= OnSettingsWindowClosed;

            if (ReferenceEquals(_settingsWindow, settingsWindow))
            {
                _settingsWindow = null;
            }
        }
    }

    private string? SaveSettings(ReminderScheduleOptions options, AppearanceSettings appearanceSettings)
    {
        if (_settingsStore is null || _appearanceSettingsStore is null || _scheduler is null)
        {
            return "Reminder settings are not available right now.";
        }

        try
        {
            _settingsStore.Save(options);
            _appearanceSettingsStore.Save(appearanceSettings);
            _currentReminderOptions = options;
            _currentAppearanceSettings = appearanceSettings;
            _scheduler.UpdateOptions(options);
            _scheduler.UpdateAppearanceSettings(appearanceSettings);
            _mainWindowViewModel?.LogSystemMessage("Reminder settings saved.");
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Could not save reminder settings. {ex.Message}";
        }
    }

    private void ToggleManualPause()
    {
        if (_scheduler is null || _trayService is null)
        {
            return;
        }

        if (_scheduler.IsManuallyPaused)
        {
            _scheduler.ResumeTimer();
            _mainWindowViewModel?.LogSystemMessage("Reminder timer resumed from the tray.");
        }
        else
        {
            _scheduler.PauseTimer();
            _mainWindowViewModel?.LogSystemMessage("Reminder timer paused from the tray.");
        }
    }

    private void SwitchReminderMode()
    {
        if (_scheduler is null)
        {
            return;
        }

        var previousPhase = _scheduler.Phase;
        if (!_scheduler.CanSwitchMode)
        {
            return;
        }

        _scheduler.SwitchMode();
        _mainWindowViewModel?.LogSystemMessage(GetSwitchLogMessage(previousPhase));
    }

    private void RestoreOrStartScheduler(ReminderRuntimeStateLoadResult runtimeStateLoadResult)
    {
        if (_scheduler is null)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);

        if (!string.IsNullOrWhiteSpace(runtimeStateLoadResult.WarningMessage))
        {
            _runtimeStateStore?.Delete();
            _mainWindowViewModel?.LogSystemMessage("Ignored invalid or corrupt runtime state. Starting a fresh reminder cycle.");
            _scheduler.Start();
            return;
        }

        var runtimeState = runtimeStateLoadResult.State;
        if (runtimeState is not null
            && runtimeState.Phase != ReminderPhase.Idle
            && runtimeState.LocalDate == today)
        {
            _scheduler.RestoreRuntimeState(runtimeState);
            _mainWindowViewModel?.LogSystemMessage("Restored current-day reminder state from the previous app session.");
            return;
        }

        if (runtimeState is not null && runtimeState.LocalDate != today)
        {
            _runtimeStateStore?.Delete();
            _mainWindowViewModel?.LogSystemMessage("Discarded stale runtime state because the local date changed. Starting a fresh reminder cycle.");
        }

        _scheduler.Start();
    }

    private void OnSchedulerStateChanged(ITrayService trayService, IPostureReminderScheduler scheduler)
    {
        UpdateTrayState(trayService, scheduler);
        PersistRuntimeState(force: false);
    }

    private void PersistRuntimeState(bool force)
    {
        if (_runtimeStateStore is null || _scheduler is null)
        {
            return;
        }

        var snapshot = _scheduler.CaptureRuntimeState();
        if (!force && !ShouldPersistRuntimeState(snapshot))
        {
            return;
        }

        try
        {
            _runtimeStateStore.Save(snapshot);
            _lastPersistedRuntimeState = snapshot;
            _lastRuntimeStatePersistedAt = DateTimeOffset.Now;
            _lastRuntimeStatePersistenceErrorMessage = null;
            _lastRuntimeStatePersistenceErrorAt = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            LogRuntimeStatePersistenceFailure(ex);
        }
    }

    private bool ShouldPersistRuntimeState(ReminderRuntimeState snapshot)
    {
        if (_lastPersistedRuntimeState is null || _lastRuntimeStatePersistedAt is null)
        {
            return true;
        }

        if (snapshot.LocalDate != _lastPersistedRuntimeState.LocalDate
            || snapshot.Phase != _lastPersistedRuntimeState.Phase
            || snapshot.PhaseBeforePause != _lastPersistedRuntimeState.PhaseBeforePause
            || snapshot.PhaseBeforeManualPause != _lastPersistedRuntimeState.PhaseBeforeManualPause
            || snapshot.LastStartedSittingDuration != _lastPersistedRuntimeState.LastStartedSittingDuration
            || snapshot.PendingRecurringSitExtension != _lastPersistedRuntimeState.PendingRecurringSitExtension
            || snapshot.SnoozedDuration != _lastPersistedRuntimeState.SnoozedDuration)
        {
            return true;
        }

        if (!IsActiveCountdownPhase(snapshot.Phase))
        {
            return false;
        }

        return DateTimeOffset.Now - _lastRuntimeStatePersistedAt.Value >= ActiveCountdownPersistenceInterval;
    }

    private void LogRuntimeStatePersistenceFailure(Exception ex)
    {
        var now = DateTimeOffset.Now;
        var message = $"Runtime state persistence failed: {ex.Message}";

        if (_lastRuntimeStatePersistenceErrorMessage == message
            && _lastRuntimeStatePersistenceErrorAt is not null
            && now - _lastRuntimeStatePersistenceErrorAt.Value < PersistenceFailureLogInterval)
        {
            return;
        }

        _lastRuntimeStatePersistenceErrorMessage = message;
        _lastRuntimeStatePersistenceErrorAt = now;
        _logger?.LogWarning(ex, "Runtime state persistence failed.");
        _mainWindowViewModel?.LogSystemMessage(message);
    }

    private static bool IsActiveCountdownPhase(ReminderPhase phase)
    {
        return phase is ReminderPhase.SittingCountdown
            or ReminderPhase.SnoozedCountdown
            or ReminderPhase.StandingCountdown;
    }

    private static void UpdateTrayState(ITrayService trayService, IPostureReminderScheduler scheduler)
    {
        trayService.SetPauseMenuLabel(scheduler.IsManuallyPaused);
        trayService.SetSwitchModeMenuState(GetSwitchModeMenuLabel(scheduler.Phase), scheduler.CanSwitchMode);
        trayService.UpdateStatus(scheduler.Phase, scheduler.RemainingTime);
    }

    private static string GetSwitchModeMenuLabel(ReminderPhase phase)
    {
        return phase switch
        {
            ReminderPhase.SittingCountdown or ReminderPhase.SnoozedCountdown => "Switch to Standing",
            ReminderPhase.StandingCountdown => "Switch to Sitting",
            _ => "Switch mode"
        };
    }

    private static string GetSwitchLogMessage(ReminderPhase previousPhase)
    {
        return previousPhase switch
        {
            ReminderPhase.SittingCountdown => "Reminder mode switched from sitting to standing from the tray.",
            ReminderPhase.SnoozedCountdown => "Reminder mode switched from snoozed sitting to standing from the tray.",
            ReminderPhase.StandingCountdown => "Reminder mode switched from standing to sitting from the tray.",
            _ => "Reminder mode switch requested from the tray."
        };
    }


    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        Dispatcher.Invoke(() => _trayService?.HandlePersistentNotificationActivation(e.Argument, e.UserInput));
    }


    private static void CleanupToastArtifacts()
    {
        ToastNotificationManagerCompat.Uninstall();
    }

    private static void ApplyStartupDelay(IReadOnlyList<string> arguments)
    {
        foreach (var argument in arguments)
        {
            if (!argument.StartsWith(StartupDelayArgumentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var secondsText = argument[StartupDelayArgumentPrefix.Length..];
            if (!int.TryParse(secondsText, out var seconds) || seconds <= 0)
            {
                return;
            }

            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            return;
        }
    }
}
