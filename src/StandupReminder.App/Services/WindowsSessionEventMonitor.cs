using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace StandupReminder.App.Services;

public enum SessionMonitorLifecycleEventType
{
    HwndSourceUnavailable,
    WindowHandleUnavailable,
    RegistrationFailed,
    ListeningStarted
}

public readonly record struct SessionMonitorLifecycleEvent(SessionMonitorLifecycleEventType Type, int? Win32Error = null);

public sealed class WindowsSessionEventMonitor : IDisposable
{
    private const int WM_WTSSESSION_CHANGE = 0x02B1;
    private const uint NOTIFY_FOR_THIS_SESSION = 0;

    private readonly Action<int, int> _onSessionChanged;
    private readonly Action<SessionMonitorLifecycleEvent>? _onLifecycleChanged;

    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private bool _sessionNotificationRegistered;
    private bool _disposed;

    private WindowsSessionEventMonitor(
        Action<int, int> onSessionChanged,
        Action<SessionMonitorLifecycleEvent>? onLifecycleChanged
    )
    {
        _onSessionChanged = onSessionChanged;
        _onLifecycleChanged = onLifecycleChanged;
    }

    public static WindowsSessionEventMonitor Attach(
        Window window,
        Action<int, int> onSessionChanged,
        Action<SessionMonitorLifecycleEvent>? onLifecycleChanged = null
    )
    {
        var monitor = new WindowsSessionEventMonitor(onSessionChanged, onLifecycleChanged);
        monitor.TryStart(window);
        return monitor;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WindowMessageHook);
            _hwndSource = null;
        }

        if (_sessionNotificationRegistered && _windowHandle != IntPtr.Zero)
        {
            WTSUnRegisterSessionNotification(_windowHandle);
            _sessionNotificationRegistered = false;
        }

        _windowHandle = IntPtr.Zero;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void TryStart(Window window)
    {
        if (PresentationSource.FromVisual(window) is not HwndSource source)
        {
            _onLifecycleChanged?.Invoke(new SessionMonitorLifecycleEvent(SessionMonitorLifecycleEventType.HwndSourceUnavailable));
            return;
        }

        _hwndSource = source;
        _hwndSource.AddHook(WindowMessageHook);

        _windowHandle = new WindowInteropHelper(window).Handle;
        if (_windowHandle == IntPtr.Zero)
        {
            _onLifecycleChanged?.Invoke(new SessionMonitorLifecycleEvent(SessionMonitorLifecycleEventType.WindowHandleUnavailable));
            return;
        }

        if (!WTSRegisterSessionNotification(_windowHandle, NOTIFY_FOR_THIS_SESSION))
        {
            var win32Error = Marshal.GetLastWin32Error();
            _onLifecycleChanged?.Invoke(new SessionMonitorLifecycleEvent(SessionMonitorLifecycleEventType.RegistrationFailed, win32Error));
            return;
        }

        _sessionNotificationRegistered = true;
        _onLifecycleChanged?.Invoke(new SessionMonitorLifecycleEvent(SessionMonitorLifecycleEventType.ListeningStarted));
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        _ = hwnd;
        _ = handled;

        if (msg == WM_WTSSESSION_CHANGE)
        {
            _onSessionChanged(wParam.ToInt32(), lParam.ToInt32());
        }

        return IntPtr.Zero;
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, uint dwFlags);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);
}
