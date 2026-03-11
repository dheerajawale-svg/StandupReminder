using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace StandupReminder.WinUI;

public sealed partial class MainWindow : Window
{
    private const int DefaultWidth = 960;
    private const int DefaultHeight = 660;
    private bool _isExiting;

    public MainWindow(ShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ConfigureWindow();
    }

    public ShellViewModel ViewModel { get; }

    /// <summary>
    /// Signals that the next close is a real application exit, not a hide-to-tray.
    /// Must be called before <see cref="Window.Close"/> during application shutdown.
    /// </summary>
    public void PrepareForExit()
    {
        _isExiting = true;
    }

    /// <summary>
    /// Makes the window visible and brings it to the foreground after it was hidden to the tray.
    /// </summary>
    public void RestoreFromTray()
    {
        AppWindow.Show();
        Activate();
    }

    private void ConfigureWindow()
    {
        AppWindow.Title = "Standup Reminder";
        AppWindow.Resize(new SizeInt32(DefaultWidth, DefaultHeight));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.Closing += OnAppWindowClosing;
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        _ = sender;

        if (_isExiting)
        {
            return;
        }

        // Hide to tray instead of closing
        args.Cancel = true;
        AppWindow.Hide();
    }
}