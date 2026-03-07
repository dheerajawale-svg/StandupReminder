using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using StandupReminder.App.ViewModels;

namespace StandupReminder.App;

public partial class MainWindow : Window
{
    private const int WM_WTSSESSION_CHANGE = 0x02B1;

    private const int WTS_SESSION_LOGON = 0x0005;
    private const int WTS_SESSION_LOCK = 0x0007;
    private const int WTS_SESSION_UNLOCK = 0x0008;

    private const uint NOTIFY_FOR_THIS_SESSION = 0;

    private readonly MainWindowViewModel _viewModel;

    private HwndSource? _hwndSource;
    private bool _sessionNotificationRegistered;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;

        SourceInitialized += OnSourceInitialized;
        Closing += OnWindowClosing;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is not HwndSource source)
        {
            _viewModel.LogHwndSourceInitializationFailed();
            return;
        }

        _hwndSource = source;
        _hwndSource.AddHook(WindowMessageHook);

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            _viewModel.LogWindowHandleUnavailable();
            return;
        }

        if (!WTSRegisterSessionNotification(hwnd, NOTIFY_FOR_THIS_SESSION))
        {
            var errorCode = Marshal.GetLastWin32Error();
            _viewModel.LogWtsRegistrationFailed(errorCode);
            return;
        }

        _sessionNotificationRegistered = true;
        _viewModel.LogSessionNotificationListening();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WindowMessageHook);
            _hwndSource = null;
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        if (_sessionNotificationRegistered && hwnd != IntPtr.Zero)
        {
            WTSUnRegisterSessionNotification(hwnd);
            _sessionNotificationRegistered = false;
        }
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        _ = hwnd;
        _ = handled;

        if (msg == WM_WTSSESSION_CHANGE)
        {
            HandleSessionChange(wParam.ToInt32(), lParam.ToInt32());
        }

        return IntPtr.Zero;
    }

    private void HandleSessionChange(int sessionEvent, int sessionId)
    {
        switch (sessionEvent)
        {
            case WTS_SESSION_LOCK:
                _viewModel.LogSessionLock(sessionId);
                break;

            case WTS_SESSION_UNLOCK:
                _viewModel.LogSessionUnlock(sessionId);
                break;

            case WTS_SESSION_LOGON:
                _viewModel.LogSessionLogon(sessionId);
                break;

            default:
                _viewModel.LogUnknownSessionEvent(sessionEvent, sessionId);
                break;
        }
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, uint dwFlags);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);
}
