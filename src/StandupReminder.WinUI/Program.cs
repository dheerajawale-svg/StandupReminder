using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace StandupReminder.WinUI;

public static class Program
{
    private const string SingleInstanceKey = "StandupReminder.WinUI";
    private static DispatcherQueue? _dispatcherQueue;
    private static IntPtr _redirectEventHandle = IntPtr.Zero;

    [STAThread]
    private static int Main(string[] args)
    {
        _ = args;

        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (DecideRedirection())
        {
            return 0;
        }

        Application.Start(_ =>
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            var context = new DispatcherQueueSynchronizationContext(_dispatcherQueue);
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });

        return 0;
    }

    private static bool DecideRedirection()
    {
        var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        var keyInstance = AppInstance.FindOrRegisterForKey(SingleInstanceKey);

        if (keyInstance.IsCurrent)
        {
            keyInstance.Activated += OnActivated;
            return false;
        }

        RedirectActivationTo(activationArgs, keyInstance);
        return true;
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        _ = sender;

        if (_dispatcherQueue is null || Application.Current is not App app)
        {
            return;
        }

        _ = _dispatcherQueue.TryEnqueue(() => app.HandleActivated(args));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string? lpName);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetEvent(IntPtr hEvent);

    [DllImport("ole32.dll")]
    private static extern uint CoWaitForMultipleObjects(uint dwFlags, uint dwMilliseconds, ulong nHandles, IntPtr[] pHandles, out uint dwIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static void RedirectActivationTo(AppActivationArguments activationArgs, AppInstance keyInstance)
    {
        _redirectEventHandle = CreateEvent(IntPtr.Zero, true, false, null);

        Task.Run(async () =>
        {
            try
            {
                await keyInstance.RedirectActivationToAsync(activationArgs).AsTask().ConfigureAwait(false);
            }
            finally
            {
                _ = SetEvent(_redirectEventHandle);
            }
        });

        const uint cWmoDefault = 0;
        const uint infinite = 0xFFFFFFFF;
        _ = CoWaitForMultipleObjects(cWmoDefault, infinite, 1, [_redirectEventHandle], out _);

        var process = Process.GetProcessById((int)keyInstance.ProcessId);
        if (process.MainWindowHandle != IntPtr.Zero)
        {
            _ = SetForegroundWindow(process.MainWindowHandle);
        }
    }
}