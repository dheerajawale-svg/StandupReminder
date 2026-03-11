using StandupReminder.Core.Models;
using StandupReminder.Core.Services;

namespace StandupReminder.Core.Tests;

public sealed class ReminderSchedulerEngineTests
{
    [Fact]
    public void Start_BeginsInitialSittingCountdown()
    {
        var clock = new FakeClock();
        var tray = new FakeTrayNotifier();
        var prompt = new FakePromptHost();

        using var engine = CreateEngine(clock, tray, prompt);

        engine.Start();

        Assert.Equal(ReminderPhase.SittingCountdown, engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(10), engine.RemainingTime);
        Assert.True(engine.IsTimerRunning);
        Assert.False(engine.IsPaused);
    }

    [Fact]
    public void Tick_WhenInitialCountdownExpires_ShowsStandPrompt()
    {
        var clock = new FakeClock();
        var tray = new FakeTrayNotifier();
        var prompt = new FakePromptHost();

        using var engine = CreateEngine(clock, tray, prompt);
        engine.Start();

        clock.Advance(TimeSpan.FromMinutes(10));
        engine.Tick();

        Assert.Equal(ReminderPhase.StandPromptPending, engine.Phase);
        Assert.Equal(TimeSpan.Zero, engine.RemainingTime);
        Assert.False(engine.IsTimerRunning);
        Assert.True(prompt.IsVisible);
        Assert.Equal(1, prompt.ShowCount);
        Assert.Equal(1, prompt.ActivateCount);
        Assert.Equal(TimeSpan.FromMinutes(4), prompt.LastShownDuration);
        Assert.Equal("#FF102030", prompt.LastBackground);
        Assert.Equal(1, tray.DismissPersistentBalloonTipCount);
    }

    [Fact]
    public void ConfirmingPrompt_BeginsStandingCountdown()
    {
        var clock = new FakeClock();
        var tray = new FakeTrayNotifier();
        var prompt = new FakePromptHost();

        using var engine = CreateEngine(clock, tray, prompt);
        engine.Start();
        clock.Advance(TimeSpan.FromMinutes(10));
        engine.Tick();

        prompt.RaiseConfirmed();

        Assert.Equal(ReminderPhase.StandingCountdown, engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(4), engine.RemainingTime);
        Assert.True(engine.IsTimerRunning);
        Assert.False(prompt.IsVisible);
    }

    [Fact]
    public void SnoozingPrompt_BeginsSnoozedCountdown()
    {
        var clock = new FakeClock();
        var tray = new FakeTrayNotifier();
        var prompt = new FakePromptHost();

        using var engine = CreateEngine(clock, tray, prompt);
        engine.Start();
        clock.Advance(TimeSpan.FromMinutes(10));
        engine.Tick();

        prompt.RaiseSnoozed();

        Assert.Equal(ReminderPhase.SnoozedCountdown, engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(5), engine.RemainingTime);
        Assert.True(engine.IsTimerRunning);
        Assert.False(prompt.IsVisible);
    }

    [Fact]
    public void PauseAndResume_PreservesRemainingTime()
    {
        var clock = new FakeClock();

        using var engine = CreateEngine(clock, new FakeTrayNotifier(), new FakePromptHost());
        engine.Start();

        clock.Advance(TimeSpan.FromMinutes(3));
        engine.Tick();
        engine.PauseTimer();

        Assert.Equal(ReminderPhase.PausedManually, engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(7), engine.RemainingTime);
        Assert.True(engine.IsPaused);

        clock.Advance(TimeSpan.FromMinutes(20));
        engine.ResumeTimer();
        clock.Advance(TimeSpan.FromMinutes(2));
        engine.Tick();

        Assert.Equal(ReminderPhase.SittingCountdown, engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(5), engine.RemainingTime);
        Assert.False(engine.IsPaused);
    }

    [Fact]
    public void LockAndUnlock_PreservesRemainingTime()
    {
        var clock = new FakeClock();

        using var engine = CreateEngine(clock, new FakeTrayNotifier(), new FakePromptHost());
        engine.Start();

        clock.Advance(TimeSpan.FromMinutes(4));
        engine.Tick();
        engine.HandleSessionEvent(ReminderSessionEvent.Lock);

        Assert.Equal(ReminderPhase.PausedForLock, engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(6), engine.RemainingTime);
        Assert.True(engine.IsPaused);

        clock.Advance(TimeSpan.FromMinutes(30));
        engine.HandleSessionEvent(ReminderSessionEvent.Unlock);
        clock.Advance(TimeSpan.FromMinutes(1));
        engine.Tick();

        Assert.Equal(ReminderPhase.SittingCountdown, engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(5), engine.RemainingTime);
        Assert.False(engine.IsPaused);
    }

    [Fact]
    public void UpdateOptions_WhilePromptIsVisible_UpdatesPromptDuration()
    {
        var clock = new FakeClock();
        var prompt = new FakePromptHost();

        using var engine = CreateEngine(clock, new FakeTrayNotifier(), prompt);
        engine.Start();
        clock.Advance(TimeSpan.FromMinutes(10));
        engine.Tick();

        engine.UpdateOptions(new ReminderScheduleOptions
        {
            InitialSit = TimeSpan.FromMinutes(12),
            RecurringSit = TimeSpan.FromMinutes(9),
            Stand = TimeSpan.FromMinutes(6)
        });

        Assert.Equal(TimeSpan.FromMinutes(6), prompt.LastUpdatedDuration);
    }

    private static ReminderSchedulerEngine CreateEngine(FakeClock clock, FakeTrayNotifier tray, FakePromptHost prompt)
    {
        return new ReminderSchedulerEngine(
            new ReminderScheduleOptions
            {
                InitialSit = TimeSpan.FromMinutes(10),
                RecurringSit = TimeSpan.FromMinutes(8),
                Stand = TimeSpan.FromMinutes(4)
            },
            "#FF102030",
            tray,
            prompt,
            clock.GetNow);
    }

    private sealed class FakeClock
    {
        private DateTimeOffset _now = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

        public DateTimeOffset GetNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class FakeTrayNotifier : IReminderTrayNotifier
    {
        public int DismissPersistentBalloonTipCount { get; private set; }

        public void ShowBalloonTip(string title, string message)
        {
        }

        public void ShowPersistentBalloonTip(string title, string message)
        {
        }

        public void DismissPersistentBalloonTip()
        {
            DismissPersistentBalloonTipCount++;
        }
    }

    private sealed class FakePromptHost : IReminderPromptHost
    {
        public event EventHandler? Confirmed;
        public event EventHandler? Snoozed;

        public bool IsVisible { get; private set; }
        public int ShowCount { get; private set; }
        public int ActivateCount { get; private set; }
        public TimeSpan LastShownDuration { get; private set; }
        public TimeSpan LastUpdatedDuration { get; private set; }
        public string LastBackground { get; private set; } = string.Empty;

        public void Show(TimeSpan standDuration, string backgroundArgbHex)
        {
            IsVisible = true;
            ShowCount++;
            LastShownDuration = standDuration;
            LastBackground = backgroundArgbHex;
        }

        public void Activate()
        {
            ActivateCount++;
        }

        public void UpdateStandDuration(TimeSpan standDuration)
        {
            LastUpdatedDuration = standDuration;
        }

        public void UpdateBackground(string backgroundArgbHex)
        {
            LastBackground = backgroundArgbHex;
        }

        public void DismissForLock()
        {
            IsVisible = false;
        }

        public void DismissForPause()
        {
            IsVisible = false;
        }

        public void DismissForShutdown()
        {
            IsVisible = false;
        }

        public void RaiseConfirmed()
        {
            IsVisible = false;
            Confirmed?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseSnoozed()
        {
            IsVisible = false;
            Snoozed?.Invoke(this, EventArgs.Empty);
        }
    }
}