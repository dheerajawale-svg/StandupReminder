using System.Windows.Threading;
using StandupReminder.Core.Models;
using StandupReminder.Core.Services;
using StandupReminder.Persistence;

namespace StandupReminder.App.Services;

public sealed class PostureReminderScheduler : IPostureReminderScheduler
{
    private readonly DispatcherTimer _timer;
    private readonly ReminderSchedulerEngine _engine;
    private readonly StandUpReminderPromptHost _promptHost;
    private bool _disposed;

    public event EventHandler? StateChanged;

    public ReminderPhase Phase => _engine.Phase;

    public TimeSpan RemainingTime => _engine.RemainingTime;

    public bool IsPaused => _engine.IsPaused;

    public bool IsManuallyPaused => _engine.IsManuallyPaused;

    public PostureReminderScheduler(ReminderScheduleOptions options, AppearanceSettings appearanceSettings, ITrayService trayService)
    {
        _promptHost = new StandUpReminderPromptHost();
        _engine = new ReminderSchedulerEngine(
            options,
            NormalizeAppearanceSettings(appearanceSettings).WindowBackgroundArgbHex,
            trayService,
            _promptHost);
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;
        _engine.StateChanged += OnEngineStateChanged;
    }

    public void Start()
    {
        _engine.Start();
    }

    public void PauseTimer()
    {
        _engine.PauseTimer();
    }

    public void ResumeTimer()
    {
        _engine.ResumeTimer();
    }

    public void UpdateOptions(ReminderScheduleOptions options)
    {
        _engine.UpdateOptions(options);
    }

    public void UpdateAppearanceSettings(AppearanceSettings settings)
    {
        _engine.UpdatePromptBackground(NormalizeAppearanceSettings(settings).WindowBackgroundArgbHex);
    }

    public void HandleSessionLogon()
    {
        _engine.HandleSessionEvent(ReminderSessionEvent.Logon);
    }

    public void HandleSessionLock()
    {
        _engine.HandleSessionEvent(ReminderSessionEvent.Lock);
    }

    public void HandleSessionUnlock()
    {
        _engine.HandleSessionEvent(ReminderSessionEvent.Unlock);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _engine.StateChanged -= OnEngineStateChanged;
        _engine.Dispose();
        _promptHost.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        _engine.Tick();
    }

    private void OnEngineStateChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_engine.IsTimerRunning)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static AppearanceSettings NormalizeAppearanceSettings(AppearanceSettings settings)
    {
        return new AppearanceSettings
        {
            WindowBackgroundArgbHex = ArgbHexColor.NormalizeOrDefault(settings.WindowBackgroundArgbHex)
        };
    }
}
