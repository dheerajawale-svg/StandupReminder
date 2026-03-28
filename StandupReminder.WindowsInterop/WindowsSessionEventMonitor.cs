using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace StandupReminder.WindowsInterop;

public sealed class WindowsSessionEventMonitor : IDisposable
{
    private const int WM_WTSSESSION_CHANGE = 0x02B1;
    private const int WTS_SESSION_LOGON = 0x0005;
    private const int WTS_SESSION_LOCK = 0x0007;
    private const int WTS_SESSION_UNLOCK = 0x0008;
    private const uint NOTIFY_FOR_THIS_SESSION = 0;

    private readonly Action<SessionChangeEvent> _onSessionChanged;
    private readonly Action<SessionMonitorLifecycleEvent>? _onLifecycleChanged;

    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private bool _sessionNotificationRegistered;
    private bool _disposed;
    private Window? _window;

    private WindowsSessionEventMonitor(
        Action<SessionChangeEvent> onSessionChanged,
        Action<SessionMonitorLifecycleEvent>? onLifecycleChanged
    )
    {
        _onSessionChanged = onSessionChanged;
        _onLifecycleChanged = onLifecycleChanged;
    }

    public static WindowsSessionEventMonitor Attach(
        Window window,
        Action<SessionChangeEvent> onSessionChanged,
        Action<SessionMonitorLifecycleEvent>? onLifecycleChanged = null
    )
    {
        var monitor = new WindowsSessionEventMonitor(onSessionChanged, onLifecycleChanged);
        monitor.AttachToWindow(window);
        return monitor;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_window is not null)
        {
            _window.SourceInitialized -= OnWindowSourceInitialized;
            _window = null;
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

    private void AttachToWindow(Window window)
    {
        _window = window;

        if (window.IsInitialized)
        {
            TryStart(window);
            return;
        }

        window.SourceInitialized += OnWindowSourceInitialized;
    }

    private void OnWindowSourceInitialized(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (sender is not Window window)
        {
            return;
        }

        window.SourceInitialized -= OnWindowSourceInitialized;
        TryStart(window);
    }

    private void TryStart(Window window)
    {
        _windowHandle = new WindowInteropHelper(window).EnsureHandle();
        if (_windowHandle == IntPtr.Zero)
        {
            _onLifecycleChanged?.Invoke(new SessionMonitorLifecycleEvent(SessionMonitorLifecycleEventType.WindowHandleUnavailable));
            return;
        }

        var source = PresentationSource.FromVisual(window) as HwndSource;
        source ??= HwndSource.FromHwnd(_windowHandle);
        if (source is null)
        {
            _onLifecycleChanged?.Invoke(new SessionMonitorLifecycleEvent(SessionMonitorLifecycleEventType.HwndSourceUnavailable));
            return;
        }

        _hwndSource = source;
        _hwndSource.AddHook(WindowMessageHook);

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
            _onSessionChanged(MapSessionChangeEvent(wParam.ToInt32(), lParam.ToInt32()));
        }

        return IntPtr.Zero;
    }

    private static SessionChangeEvent MapSessionChangeEvent(int rawEventCode, int sessionId)
    {
        var type = rawEventCode switch
        {
            WTS_SESSION_LOCK => SessionChangeType.Lock,
            WTS_SESSION_UNLOCK => SessionChangeType.Unlock,
            WTS_SESSION_LOGON => SessionChangeType.Logon,
            _ => SessionChangeType.Unknown
        };

        return new SessionChangeEvent(type, sessionId, rawEventCode);
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, uint dwFlags);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);
}
