using System.Windows;
using StandupReminder.App.Models;
using StandupReminder.App.Services;
using StandupReminder.App.ViewModels;
using Wpf.Ui.Appearance;

namespace StandupReminder.App;

public partial class App : System.Windows.Application
{
    private ITrayService? _trayService;
    private IPostureReminderScheduler? _scheduler;
    private MainWindow? _mainWindow;
    private bool _isShuttingDown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplicationThemeManager.ApplySystemTheme();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var viewModel = new MainWindowViewModel();
        var trayService = new NotifyIconTrayService();
        var scheduler = new PostureReminderScheduler(new ReminderScheduleOptions(), trayService);
        var autoStartService = new RegistryAutoStartRegistrationService();

        _trayService = trayService;
        _scheduler = scheduler;

        viewModel.AttachScheduler(scheduler);

        try
        {
            autoStartService.EnsureRegistered();
            viewModel.LogSystemMessage("Configured the app to start automatically at Windows sign-in.");
        }
        catch (Exception ex)
        {
            viewModel.LogSystemMessage($"Failed to configure Windows sign-in startup. {ex.Message}");
        }

        _mainWindow = new MainWindow(viewModel, scheduler);
        _mainWindow.PrepareForBackgroundLaunch();
        MainWindow = _mainWindow;

        trayService.OpenRequested += (_, _) => _mainWindow.ShowFromTray();
        trayService.ExitRequested += (_, _) => PerformShutdown();
        scheduler.StateChanged += (_, _) => trayService.UpdateStatus(BuildTrayStatus(scheduler));

        trayService.Initialize();
        trayService.UpdateStatus(BuildTrayStatus(scheduler));

        _mainWindow.Show();
        scheduler.Start();
        viewModel.LogSystemMessage("Application started in tray mode.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _scheduler?.Dispose();
        _trayService?.Dispose();
        base.OnExit(e);
    }

    private void PerformShutdown()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;

        if (_mainWindow is not null)
        {
            _mainWindow.PrepareForExit();
            _mainWindow.Close();
        }

        Shutdown();
    }

    private static string BuildTrayStatus(IPostureReminderScheduler scheduler)
    {
        var phaseLabel = scheduler.Phase switch
        {
            ReminderPhase.SittingCountdown => "Sitting",
            ReminderPhase.StandingCountdown => "Standing",
            ReminderPhase.StandPromptPending => "Stand-up confirmation",
            ReminderPhase.PausedForLock => "Paused for lock",
            _ => "Starting"
        };

        var remainingLabel = scheduler.Phase == ReminderPhase.StandPromptPending
            ? "awaiting action"
            : $"{scheduler.RemainingTime:hh\\:mm\\:ss} remaining";

        return $"{phaseLabel} - {remainingLabel}";
    }
}
