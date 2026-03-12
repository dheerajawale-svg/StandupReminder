using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace StandupReminder.WinUI;

public sealed partial class TrayMenuWindow : Window
{
    private const int WindowWidth = 256;
    private const int WindowHeight = 220;
    private const int WindowMargin = 8;
    private bool _isVisible;

    public TrayMenuWindow()
    {
        InitializeComponent();
        ConfigureWindow();
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? PauseResumeRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public string PauseResumeLabel
    {
        get => PauseResumeTextBlock.Text;
        set => PauseResumeTextBlock.Text = value;
    }

    public bool IsVisible => _isVisible;

    public void ShowAt(PointInt32 anchorPoint)
    {
        var workArea = DisplayArea.GetFromPoint(anchorPoint, DisplayAreaFallback.Nearest).WorkArea;
        var x = Math.Clamp(anchorPoint.X - WindowWidth, workArea.X, workArea.X + workArea.Width - WindowWidth);
        var y = anchorPoint.Y - WindowHeight - WindowMargin;

        if (y < workArea.Y)
        {
            y = Math.Clamp(anchorPoint.Y + WindowMargin, workArea.Y, workArea.Y + workArea.Height - WindowHeight);
        }

        AppWindow.Move(new PointInt32(x, y));
        AppWindow.Show();
        EnsureTopmost();
        Activate();
        _isVisible = true;
    }

    public void HideMenu()
    {
        if (!_isVisible)
        {
            return;
        }

        AppWindow.Hide();
        _isVisible = false;
    }

    private void ConfigureWindow()
    {
        AppWindow.Title = "Standup Reminder Tray Menu";
        AppWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));
        if (OverlappedPresenter.CreateForContextMenu() is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            AppWindow.SetPresenter(presenter);
        }
        Activated += OnWindowActivated;
    }

    private void EnsureTopmost()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        _ = SetWindowPos(
            hwnd,
            TopmostWindowHandle,
            0,
            0,
            0,
            0,
            SetWindowPosFlags.IgnoreMove
            | SetWindowPosFlags.IgnoreResize
            | SetWindowPosFlags.ShowWindow
            | SetWindowPosFlags.DoNotActivate);
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        _ = sender;

        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            HideMenu();
        }
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        HideMenu();
        OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        HideMenu();
        PauseResumeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        HideMenu();
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        HideMenu();
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private static readonly IntPtr TopmostWindowHandle = new(-1);

    [Flags]
    private enum SetWindowPosFlags : uint
    {
        IgnoreMove = 0x0002,
        IgnoreResize = 0x0001,
        DoNotActivate = 0x0010,
        ShowWindow = 0x0040
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        SetWindowPosFlags uFlags);
}
