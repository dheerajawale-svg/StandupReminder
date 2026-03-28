using System.Windows;
using System.Windows.Input;

namespace StandupReminder.App;

public partial class TrayMenuWindow : Window
{
    public event EventHandler? OpenSelected;
    public event EventHandler? PauseResumeSelected;
    public event EventHandler? SettingsSelected;
    public event EventHandler? ExitSelected;

    public TrayMenuWindow()
    {
        InitializeComponent();
        Deactivated += OnDeactivated;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public void SetPauseResumeLabel(string label)
    {
        PauseResumeButton.Content = label;
    }

    private void OnOpenClicked(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        OpenSelected?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void OnPauseResumeClicked(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        PauseResumeSelected?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        SettingsSelected?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        ExitSelected?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        Close();
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        _ = sender;

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }
}
