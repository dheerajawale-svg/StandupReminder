using System.IO;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
using StandupReminder.App.Models;
using StandupReminder.App.Services;
using StandupReminder.App.ViewModels;
using Wpf.Ui.Appearance;

namespace StandupReminder.App;

public partial class App : System.Windows.Application
{
    private const string CleanupToastArgument = "--cleanup-toast";

    private ITrayService? _trayService;
    private IReminderSettingsStore? _settingsStore;
    private IAppearanceSettingsStore? _appearanceSettingsStore;
    private IPostureReminderScheduler? _scheduler;
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _mainWindowViewModel;
    private SettingsWindow? _settingsWindow;
    private ReminderScheduleOptions _currentReminderOptions = new();
    private AppearanceSettings _currentAppearanceSettings = new();
    private bool _isShuttingDown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(argument => string.Equals(argument, CleanupToastArgument, StringComparison.OrdinalIgnoreCase)))
        {
            CleanupToastArtifacts();
            Shutdown();
            return;
        }

        ToastNotificationManagerCompat.OnActivated += OnToastActivated;

        ApplicationThemeManager.ApplySystemTheme();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var startupExperienceService = new StartupExperienceService();
        var shouldShowFirstRunSplash = startupExperienceService.ShouldShowFirstRunSplash();
        var firstRunSplashPersistFailed = false;

        if (shouldShowFirstRunSplash)
        {
            new FirstRunSplashWindow().ShowDialog();
            firstRunSplashPersistFailed = !startupExperienceService.TryMarkFirstRunSplashShown();
        }

        var settingsStore = new LocalAppDataReminderSettingsStore();
        var settingsLoadResult = settingsStore.Load();
        var appearanceSettingsStore = new LocalAppDataAppearanceSettingsStore();
        var appearanceSettings = appearanceSettingsStore.Load();
        var sessionEventLogStore = new LocalAppDataSessionEventLogStore();
        var viewModel = new MainWindowViewModel(sessionEventLogStore);
        var trayService = new NotifyIconTrayService();
        var scheduler = new PostureReminderScheduler(settingsLoadResult.Options, appearanceSettings, trayService);

        _settingsStore = settingsStore;
        _appearanceSettingsStore = appearanceSettingsStore;
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
        trayService.SettingsRequested += (_, _) => ShowSettingsWindow();
        trayService.ExitRequested += (_, _) => PerformShutdown();
        trayService.SitReminderAcknowledged += (_, _) => _mainWindowViewModel?.LogSystemMessage("Sit reminder acknowledged from Windows notification.");
        trayService.SitReminderBodyActivated += (_, _) => _mainWindowViewModel?.LogSystemMessage("Sit reminder body clicked. Waiting for OK acknowledgement.");
        trayService.SitReminderDismissed += (_, _) => _mainWindowViewModel?.LogSystemMessage("Sit reminder dismissed from Windows notification. Re-showing reminder.");
        scheduler.StateChanged += (_, _) => UpdateTrayState(trayService, scheduler);

        trayService.Initialize();
        UpdateTrayState(trayService, scheduler);

        _mainWindow.Show();
        scheduler.Start();
        viewModel.LogSystemMessage("Application started in tray mode.");

        if (!string.IsNullOrWhiteSpace(settingsLoadResult.WarningMessage))
        {
            viewModel.LogSystemMessage(settingsLoadResult.WarningMessage);
        }

        if (shouldShowFirstRunSplash && firstRunSplashPersistFailed)
        {
            viewModel.LogSystemMessage("Could not persist first-run startup state. The splash may appear again next launch.");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _scheduler?.Dispose();
        _trayService?.Dispose();
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

    private static string BuildTrayStatus(IPostureReminderScheduler scheduler)
    {
        var phaseLabel = scheduler.Phase switch
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

        var remainingLabel = scheduler.Phase switch
        {
            ReminderPhase.StandPromptPending => "awaiting action",
            ReminderPhase.SitPromptPending => "awaiting OK",
            ReminderPhase.PausedManually when scheduler.RemainingTime == TimeSpan.Zero => "paused",
            _ => $"{scheduler.RemainingTime:hh\\:mm\\:ss} remaining"
        };

        return $"{phaseLabel} - {remainingLabel}";
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

    private static void UpdateTrayState(ITrayService trayService, IPostureReminderScheduler scheduler)
    {
        trayService.SetPauseMenuLabel(scheduler.IsManuallyPaused);
        trayService.UpdateStatus(BuildTrayStatus(scheduler));
    }

    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        Dispatcher.Invoke(() => _trayService?.HandlePersistentNotificationActivation(e.Argument));
    }

    private static void CleanupToastArtifacts()
    {
        ToastNotificationManagerCompat.Uninstall();
    }
}
