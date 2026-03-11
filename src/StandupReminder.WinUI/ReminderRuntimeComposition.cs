using System.IO;
using Microsoft.UI.Dispatching;
using StandupReminder.Core.Models;
using StandupReminder.Core.Services;
using StandupReminder.Persistence;

namespace StandupReminder.WinUI;

internal sealed class ReminderRuntimeComposition : IDisposable
{
    private readonly ShellViewModel _shellViewModel;
    private readonly IReminderSettingsStore _settingsStore;
    private readonly IAppearanceSettingsStore _appearanceSettingsStore;
    private readonly ReminderSchedulerRuntime _runtime;
    private readonly IReminderTrayHost _trayHost;
    private readonly IReminderSessionEventSource _sessionEventSource;
    private readonly DispatcherQueue _dispatcherQueue;
    private ReminderScheduleOptions _currentReminderOptions;
    private AppearanceSettings _currentAppearanceSettings;

    public ReminderRuntimeComposition(
        ReminderSettingsLoadResult settingsLoadResult,
        AppearanceSettings appearanceSettings,
        IReminderSettingsStore settingsStore,
        IAppearanceSettingsStore appearanceSettingsStore,
        string settingsFilePath,
        ReminderSchedulerRuntime runtime,
        IReminderTrayHost trayHost,
        IReminderSessionEventSource sessionEventSource)
    {
        _settingsStore = settingsStore;
        _appearanceSettingsStore = appearanceSettingsStore;
        _runtime = runtime;
        _trayHost = trayHost;
        _sessionEventSource = sessionEventSource;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _currentReminderOptions = CloneOptions(settingsLoadResult.Options);
        _currentAppearanceSettings = CloneAppearanceSettings(appearanceSettings);

        _sessionEventSource.SessionEvent += OnSessionEvent;
        _trayHost.OpenRequested += OnTrayOpenRequested;
        _trayHost.PauseResumeRequested += OnTrayPauseResumeRequested;
        _trayHost.SettingsRequested += OnTraySettingsRequested;
        _trayHost.ExitRequested += OnTrayExitRequested;
        _runtime.StateChanged += OnRuntimeStateChanged;

        _shellViewModel = new ShellViewModel(_currentReminderOptions, settingsLoadResult.WarningMessage, settingsFilePath, runtime);

        _trayHost.Initialize();
        UpdateTrayState();
        _runtime.Start();

        _shellViewModel.LogEvent("Reminder runtime started.");
        if (!string.IsNullOrWhiteSpace(settingsLoadResult.WarningMessage))
        {
            _shellViewModel.LogEvent($"Settings warning: {settingsLoadResult.WarningMessage}");
        }
    }

    public event EventHandler? WindowActivationRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ShutdownRequested;

    public MainWindow CreateMainWindow()
    {
        return new MainWindow(_shellViewModel);
    }

    public SettingsWindowViewModel CreateSettingsViewModel()
    {
        return new SettingsWindowViewModel(_currentReminderOptions, _currentAppearanceSettings);
    }

    public string? SaveSettings(ReminderScheduleOptions options, AppearanceSettings appearanceSettings)
    {
        try
        {
            _settingsStore.Save(options);
            _appearanceSettingsStore.Save(appearanceSettings);
            _currentReminderOptions = CloneOptions(options);
            _currentAppearanceSettings = CloneAppearanceSettings(appearanceSettings);
            _runtime.UpdateOptions(_currentReminderOptions);
            _runtime.UpdatePromptBackground(_currentAppearanceSettings.WindowBackgroundArgbHex);
            _shellViewModel.UpdateSettings(_currentReminderOptions);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Could not save reminder settings. {ex.Message}";
        }
    }

    public void Dispose()
    {
        _trayHost.OpenRequested -= OnTrayOpenRequested;
        _trayHost.PauseResumeRequested -= OnTrayPauseResumeRequested;
        _trayHost.SettingsRequested -= OnTraySettingsRequested;
        _trayHost.ExitRequested -= OnTrayExitRequested;
        _runtime.StateChanged -= OnRuntimeStateChanged;
        _sessionEventSource.SessionEvent -= OnSessionEvent;

        _sessionEventSource.Dispose();
        _shellViewModel.Dispose();
        _runtime.Dispose();
        _trayHost.Dispose();
    }

    private void OnSessionEvent(ReminderSessionEvent sessionEvent)
    {
        _runtime.HandleSessionEvent(sessionEvent);

        var message = sessionEvent switch
        {
            ReminderSessionEvent.Logon => "Session logon detected.",
            ReminderSessionEvent.Lock => "Session locked — timer paused.",
            ReminderSessionEvent.Unlock => "Session unlocked — timer resumed.",
            _ => $"Session event: {sessionEvent}."
        };

        _dispatcherQueue.TryEnqueue(() => _shellViewModel.LogEvent(message));
    }

    private void OnTrayOpenRequested(object? sender, EventArgs e)
    {
        WindowActivationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnTrayPauseResumeRequested(object? sender, EventArgs e)
    {
        if (_runtime.IsManuallyPaused)
        {
            _runtime.ResumeTimer();
        }
        else
        {
            _runtime.PauseTimer();
        }
    }

    private void OnTraySettingsRequested(object? sender, EventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnTrayExitRequested(object? sender, EventArgs e)
    {
        ShutdownRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRuntimeStateChanged(object? sender, EventArgs e)
    {
        UpdateTrayState();
    }

    private void UpdateTrayState()
    {
        _trayHost.SetPauseMenuLabel(_runtime.IsManuallyPaused);
        _trayHost.UpdateStatus(BuildTrayStatus());
    }

    private string BuildTrayStatus()
    {
        var phaseLabel = _runtime.Phase switch
        {
            ReminderPhase.SittingCountdown => "Sitting",
            ReminderPhase.StandingCountdown => "Standing",
            ReminderPhase.StandPromptPending => "Stand-up confirmation",
            ReminderPhase.SnoozedCountdown => "Snoozed",
            ReminderPhase.PausedManually => "Paused manually",
            ReminderPhase.PausedForLock => "Paused for lock",
            _ => "Starting"
        };

        var remainingLabel = _runtime.Phase switch
        {
            ReminderPhase.StandPromptPending => "awaiting action",
            ReminderPhase.PausedManually when _runtime.RemainingTime == TimeSpan.Zero => "paused",
            _ => $"{_runtime.RemainingTime:hh\\:mm\\:ss} remaining"
        };

        return $"{phaseLabel} - {remainingLabel}";
    }

    private static ReminderScheduleOptions CloneOptions(ReminderScheduleOptions options)
    {
        return new ReminderScheduleOptions
        {
            InitialSit = options.InitialSit,
            RecurringSit = options.RecurringSit,
            Stand = options.Stand
        };
    }

    private static AppearanceSettings CloneAppearanceSettings(AppearanceSettings appearanceSettings)
    {
        return new AppearanceSettings
        {
            WindowBackgroundArgbHex = ArgbHexColor.NormalizeOrDefault(appearanceSettings.WindowBackgroundArgbHex)
        };
    }
}
