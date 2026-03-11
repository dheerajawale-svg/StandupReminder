using System.Runtime.InteropServices;
using Microsoft.Win32;
using StandupReminder.Core.Services;

namespace StandupReminder.Windows;

public sealed class SystemEventsReminderSessionEventSource : IReminderSessionEventSource
{
    private bool _disposed;
    private bool _subscribed;

    public SystemEventsReminderSessionEventSource()
    {
        TrySubscribe();
    }

    public event Action<ReminderSessionEvent>? SessionEvent;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_subscribed)
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            _subscribed = false;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal static bool TryMapSessionEvent(SessionSwitchReason reason, out ReminderSessionEvent sessionEvent)
    {
        switch (reason)
        {
            case SessionSwitchReason.SessionLogon:
                sessionEvent = ReminderSessionEvent.Logon;
                return true;

            case SessionSwitchReason.SessionLock:
                sessionEvent = ReminderSessionEvent.Lock;
                return true;

            case SessionSwitchReason.SessionUnlock:
                sessionEvent = ReminderSessionEvent.Unlock;
                return true;

            default:
                sessionEvent = default;
                return false;
        }
    }

    private void TrySubscribe()
    {
        try
        {
            SystemEvents.SessionSwitch += OnSessionSwitch;
            _subscribed = true;
        }
        catch (InvalidOperationException)
        {
        }
        catch (ExternalException)
        {
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        _ = sender;

        if (TryMapSessionEvent(e.Reason, out var sessionEvent))
        {
            SessionEvent?.Invoke(sessionEvent);
        }
    }
}
