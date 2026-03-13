using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace StandupReminder.Windows;

public sealed class NativeTrayPopupMenuHost : IDisposable
{
    private readonly PopupMenuNativeWindow _ownerWindow;
    private bool _disposed;

    public NativeTrayPopupMenuHost()
    {
        _ownerWindow = new PopupMenuNativeWindow();
    }

    public string PauseResumeLabel { get; set; } = "Pause timer";

    public TrayPopupMenuCommand ShowMenu(System.Drawing.Point screenPosition)
    {
        ThrowIfDisposed();

        var menuHandle = CreatePopupMenu();
        if (menuHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create the tray popup menu.");
        }

        try
        {
            AppendMenuString(menuHandle, TrayPopupMenuCommand.Open, "Open");
            AppendMenuString(menuHandle, TrayPopupMenuCommand.PauseResume, PauseResumeLabel);
            AppendMenuString(menuHandle, TrayPopupMenuCommand.Settings, "Settings");
            AppendMenuSeparator(menuHandle);
            AppendMenuString(menuHandle, TrayPopupMenuCommand.Exit, "Exit");

            SetForegroundWindow(_ownerWindow.Handle);

            var alignmentFlag = GetSystemMetrics(SmMenuDropAlignment) == 0
                ? TpmLeftAlign
                : TpmRightAlign;

            var selectedCommand = TrackPopupMenu(
                menuHandle,
                alignmentFlag | TpmBottomAlign | TpmRightButton | TpmNonotify | TpmReturnCommand,
                screenPosition.X,
                screenPosition.Y,
                0,
                _ownerWindow.Handle,
                IntPtr.Zero);

            PostMessage(_ownerWindow.Handle, WmNull, IntPtr.Zero, IntPtr.Zero);

            return Enum.IsDefined(typeof(TrayPopupMenuCommand), (int)selectedCommand)
                ? (TrayPopupMenuCommand)selectedCommand
                : TrayPopupMenuCommand.None;
        }
        finally
        {
            DestroyMenu(menuHandle);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _ownerWindow.DestroyHandle();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static void AppendMenuString(IntPtr menuHandle, TrayPopupMenuCommand command, string text)
    {
        if (!AppendMenu(menuHandle, MfString, (nuint)command, text))
        {
            throw new InvalidOperationException("Failed to add a tray popup menu item.");
        }
    }

    private static void AppendMenuSeparator(IntPtr menuHandle)
    {
        if (!AppendMenu(menuHandle, MfSeparator, 0, string.Empty))
        {
            throw new InvalidOperationException("Failed to add a tray popup menu separator.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class PopupMenuNativeWindow : Forms.NativeWindow
    {
        public PopupMenuNativeWindow()
        {
            CreateHandle(new Forms.CreateParams
            {
                Caption = "StandupReminderTrayPopupMenuHost"
            });
        }
    }

    private const uint MfString = 0x0000;
    private const uint MfSeparator = 0x0800;
    private const uint TpmLeftAlign = 0x0000;
    private const uint TpmRightAlign = 0x0008;
    private const uint TpmBottomAlign = 0x0020;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmNonotify = 0x0080;
    private const uint TpmReturnCommand = 0x0100;
    private const int SmMenuDropAlignment = 40;
    private const uint WmNull = 0x0000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, nuint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(
        IntPtr hMenu,
        uint uFlags,
        int x,
        int y,
        int nReserved,
        IntPtr hWnd,
        IntPtr prcRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}

public enum TrayPopupMenuCommand
{
    None = 0,
    Open = 1,
    PauseResume = 2,
    Settings = 3,
    Exit = 4
}
