using StandupReminder.Core.Services;
using StandupReminder.Persistence;
using StandupReminder.Windows;

namespace StandupReminder.WinUI;

internal sealed class AppBootstrapper
{
    private readonly LocalAppDataReminderSettingsStore _settingsStore;
    private readonly IAppearanceSettingsStore _appearanceSettingsStore;
    private readonly Func<IReminderTrayHost> _trayHostFactory;
    private readonly Func<IReminderTickSource> _tickSourceFactory;
    private readonly Func<IReminderSessionEventSource> _sessionEventSourceFactory;

    public AppBootstrapper()
        : this(
            new LocalAppDataReminderSettingsStore(),
            new LocalAppDataAppearanceSettingsStore(),
            static () => new HNotifyIconReminderTrayHost(),
            static () => new SynchronizationContextReminderTickSource(),
            // Session scope is intentionally limited to Windows 11 logon/lock/unlock transitions.
            static () => new SystemEventsReminderSessionEventSource())
    {
    }

    internal AppBootstrapper(
        LocalAppDataReminderSettingsStore settingsStore,
        IAppearanceSettingsStore appearanceSettingsStore,
        Func<IReminderTrayHost> trayHostFactory,
        Func<IReminderTickSource> tickSourceFactory,
        Func<IReminderSessionEventSource> sessionEventSourceFactory)
    {
        _settingsStore = settingsStore;
        _appearanceSettingsStore = appearanceSettingsStore;
        _trayHostFactory = trayHostFactory;
        _tickSourceFactory = tickSourceFactory;
        _sessionEventSourceFactory = sessionEventSourceFactory;
    }

    public ReminderRuntimeComposition CreateRuntimeComposition()
    {
        var settingsLoadResult = _settingsStore.Load();
        var appearanceSettings = _appearanceSettingsStore.Load();
        var trayHost = _trayHostFactory();
        var runtime = new ReminderSchedulerRuntime(
            settingsLoadResult.Options,
            appearanceSettings.WindowBackgroundArgbHex,
            trayHost,
            new StandUpReminderPromptHost(),
            _tickSourceFactory());
        var sessionEventSource = _sessionEventSourceFactory();

        return new ReminderRuntimeComposition(
            settingsLoadResult,
            appearanceSettings,
            _settingsStore,
            _appearanceSettingsStore,
            _settingsStore.SettingsFilePath,
            runtime,
            trayHost,
            sessionEventSource);
    }
}
