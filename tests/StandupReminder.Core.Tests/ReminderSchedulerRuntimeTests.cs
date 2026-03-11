using StandupReminder.Core.Models;
using StandupReminder.Core.Services;

namespace StandupReminder.Core.Tests;

public sealed class ReminderSchedulerRuntimeTests
{
    [Fact]
    public void Start_BeginsCountdownAndStartsTickSource()
    {
        var clock = new FakeClock();
        var tickSource = new FakeTickSource();

        using var runtime = CreateRuntime(clock, tickSource);

        runtime.Start();

        Assert.Equal(ReminderPhase.SittingCountdown, runtime.Phase);
        Assert.True(runtime.IsTimerRunning);
        Assert.True(tickSource.IsRunning);
        Assert.Equal(ReminderSchedulerRuntime.DefaultTickInterval, tickSource.LastInterval);
    }

    [Fact]
    public void TickSourceTick_WhenCountdownExpires_StopsTickSourceAtPromptBoundary()
    {
        var clock = new FakeClock();
        var tickSource = new FakeTickSource();
        var promptHost = new FakePromptHost();

        using var runtime = CreateRuntime(clock, tickSource, promptHost: promptHost);
        runtime.Start();

        clock.Advance(TimeSpan.FromMinutes(10));
        tickSource.RaiseTick();

        Assert.Equal(ReminderPhase.StandPromptPending, runtime.Phase);
        Assert.False(runtime.IsTimerRunning);
        Assert.False(tickSource.IsRunning);
        Assert.Equal(1, promptHost.ShowCount);
    }

    [Fact]
    public void HandleSessionEvent_Logon_StartsCountdownAndStartsTickSource()
    {
        var clock = new FakeClock();
        var tickSource = new FakeTickSource();

        using var runtime = CreateRuntime(clock, tickSource);

        runtime.HandleSessionEvent(ReminderSessionEvent.Logon);

        Assert.Equal(ReminderPhase.SittingCountdown, runtime.Phase);
        Assert.True(runtime.IsTimerRunning);
        Assert.True(tickSource.IsRunning);
        Assert.Equal(ReminderSchedulerRuntime.DefaultTickInterval, tickSource.LastInterval);
    }

    [Fact]
    public void HandleSessionEvent_LockAndUnlock_StopsAndRestartsTickSource()
    {
        var clock = new FakeClock();
        var tickSource = new FakeTickSource();

        using var runtime = CreateRuntime(clock, tickSource);
        runtime.Start();

        clock.Advance(TimeSpan.FromMinutes(4));
        tickSource.RaiseTick();
        runtime.HandleSessionEvent(ReminderSessionEvent.Lock);

        Assert.Equal(ReminderPhase.PausedForLock, runtime.Phase);
        Assert.False(runtime.IsTimerRunning);
        Assert.False(tickSource.IsRunning);

        runtime.HandleSessionEvent(ReminderSessionEvent.Unlock);

        Assert.Equal(ReminderPhase.SittingCountdown, runtime.Phase);
        Assert.True(runtime.IsTimerRunning);
        Assert.True(tickSource.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(6), runtime.RemainingTime);
    }

    private static ReminderSchedulerRuntime CreateRuntime(FakeClock clock, FakeTickSource tickSource, FakeTrayNotifier? tray = null, FakePromptHost? promptHost = null)
    {
        tray ??= new FakeTrayNotifier();
        promptHost ??= new FakePromptHost();

        return new ReminderSchedulerRuntime(
            new ReminderScheduleOptions
            {
                InitialSit = TimeSpan.FromMinutes(10),
                RecurringSit = TimeSpan.FromMinutes(8),
                Stand = TimeSpan.FromMinutes(4)
            },
            "#FF102030",
            tray,
            promptHost,
            tickSource,
            clock.GetNow);
    }

    private sealed class FakeClock
    {
        private DateTimeOffset _now = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

        public DateTimeOffset GetNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class FakeTickSource : IReminderTickSource
    {
        public event EventHandler? Tick;

        public bool IsRunning { get; private set; }

        public TimeSpan LastInterval { get; private set; }

        public void Start(TimeSpan interval)
        {
            LastInterval = interval;
            IsRunning = true;
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public void RaiseTick()
        {
            Tick?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            IsRunning = false;
        }
    }

    private sealed class FakeTrayNotifier : IReminderTrayNotifier
    {
        public void ShowBalloonTip(string title, string message)
        {
        }

        public void ShowPersistentBalloonTip(string title, string message)
        {
        }

        public void DismissPersistentBalloonTip()
        {
        }
    }

    private sealed class FakePromptHost : IReminderPromptHost
    {
        public event EventHandler? Confirmed
        {
            add { }
            remove { }
        }

        public event EventHandler? Snoozed
        {
            add { }
            remove { }
        }

        public bool IsVisible { get; private set; }

        public int ShowCount { get; private set; }

        public void Show(TimeSpan standDuration, string backgroundArgbHex)
        {
            _ = standDuration;
            _ = backgroundArgbHex;
            IsVisible = true;
            ShowCount++;
        }

        public void Activate()
        {
        }

        public void UpdateStandDuration(TimeSpan standDuration)
        {
            _ = standDuration;
        }

        public void UpdateBackground(string backgroundArgbHex)
        {
            _ = backgroundArgbHex;
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
    }
}