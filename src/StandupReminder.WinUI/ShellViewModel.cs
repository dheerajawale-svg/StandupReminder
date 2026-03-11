using System.ComponentModel;
using System.Runtime.CompilerServices;
using StandupReminder.Core.Models;
using StandupReminder.Core.Services;

namespace StandupReminder.WinUI;

public sealed class ShellViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ReminderSchedulerRuntime _runtime;
    private string _initialSitDisplay = string.Empty;
    private string _recurringSitDisplay = string.Empty;
    private string _standDisplay = string.Empty;
    private string _runtimePhaseDisplay = string.Empty;
    private string _runtimeStatusMessage = string.Empty;
    private string _remainingTimeDisplay = string.Empty;
    private string _timerAdapterDisplay = string.Empty;

    public ShellViewModel(ReminderScheduleOptions options, string? warningMessage, string settingsFilePath, ReminderSchedulerRuntime runtime)
    {
        _runtime = runtime;
        StatusTitle = string.IsNullOrWhiteSpace(warningMessage)
            ? "Reminder settings loaded and runtime composed"
            : "Reminder settings fallback applied";
        StatusMessage = string.IsNullOrWhiteSpace(warningMessage)
            ? "The WinUI shell is now creating a live reminder runtime through an explicit composition seam."
            : warningMessage;
        SettingsFilePath = settingsFilePath;
        ApplyOptions(options);
        _runtime.StateChanged += OnRuntimeStateChanged;
        ApplyRuntimeState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StatusTitle { get; }

    public string StatusMessage { get; }

    public string SettingsFilePath { get; }

    public string InitialSitDisplay
    {
        get => _initialSitDisplay;
        private set => SetProperty(ref _initialSitDisplay, value);
    }

    public string RecurringSitDisplay
    {
        get => _recurringSitDisplay;
        private set => SetProperty(ref _recurringSitDisplay, value);
    }

    public string StandDisplay
    {
        get => _standDisplay;
        private set => SetProperty(ref _standDisplay, value);
    }

    public string RuntimePhaseDisplay
    {
        get => _runtimePhaseDisplay;
        private set => SetProperty(ref _runtimePhaseDisplay, value);
    }

    public string RuntimeStatusMessage
    {
        get => _runtimeStatusMessage;
        private set => SetProperty(ref _runtimeStatusMessage, value);
    }

    public string RemainingTimeDisplay
    {
        get => _remainingTimeDisplay;
        private set => SetProperty(ref _remainingTimeDisplay, value);
    }

    public string TimerAdapterDisplay
    {
        get => _timerAdapterDisplay;
        private set => SetProperty(ref _timerAdapterDisplay, value);
    }

    public string RemainingMigrationNote => "Tray, session, stand-up prompt, and settings adapters are now wired into the WinUI runtime composition. WPF remains available only as the fallback/reference implementation while parity is validated.";

    public void UpdateSettings(ReminderScheduleOptions options)
    {
        ApplyOptions(options);
    }

    public void Dispose()
    {
        _runtime.StateChanged -= OnRuntimeStateChanged;
    }

    private void OnRuntimeStateChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyRuntimeState();
    }

    private void ApplyRuntimeState()
    {
        RuntimePhaseDisplay = $"Runtime phase: {FormatPhase(_runtime.Phase)}";
        RuntimeStatusMessage = _runtime.Phase switch
        {
            ReminderPhase.SittingCountdown => "Sitting countdown is active. The scheduler is running inside the WinUI composition seam.",
            ReminderPhase.StandingCountdown => "Standing interval is active.",
            ReminderPhase.StandPromptPending => "Time to stand up! The stand-up prompt window should now be visible.",
            ReminderPhase.SnoozedCountdown => "Snooze countdown is active.",
            ReminderPhase.PausedManually => "Paused manually. Use the tray menu to resume.",
            ReminderPhase.PausedForLock => "Paused for session lock. Will resume on unlock.",
            _ => "The runtime is waiting for the first scheduled phase."
        };
        RemainingTimeDisplay = _runtime.Phase == ReminderPhase.StandPromptPending
            ? "Remaining time: awaiting prompt action"
            : $"Remaining time: {_runtime.RemainingTime:hh\\:mm\\:ss}";
        TimerAdapterDisplay = _runtime.IsTimerRunning
            ? "Timer adapter: StandupReminder.Windows is driving runtime ticks."
            : "Timer adapter: idle until the runtime re-enters a timed phase.";
    }

    private void ApplyOptions(ReminderScheduleOptions options)
    {
        InitialSitDisplay = $"Initial sit interval: {FormatDuration(options.InitialSit)}";
        RecurringSitDisplay = $"Recurring sit interval: {FormatDuration(options.RecurringSit)}";
        StandDisplay = $"Stand duration: {FormatDuration(options.Stand)}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var wholeMinutes = Math.Max(1, (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero));
        return $"{wholeMinutes} minute{(wholeMinutes == 1 ? string.Empty : "s")}";
    }

    private static string FormatPhase(ReminderPhase phase)
    {
        return phase switch
        {
            ReminderPhase.SittingCountdown => "Sitting countdown",
            ReminderPhase.StandingCountdown => "Standing countdown",
            ReminderPhase.StandPromptPending => "Stand prompt pending",
            ReminderPhase.SnoozedCountdown => "Snoozed countdown",
            ReminderPhase.PausedManually => "Paused manually",
            ReminderPhase.PausedForLock => "Paused for lock",
            _ => "Idle"
        };
    }

    private void SetProperty(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
