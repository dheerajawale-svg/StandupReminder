using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace StandupReminder.App;

public partial class StandUpReminderWindow : FluentWindow
{
    private bool _allowClose;

    public event EventHandler? Confirmed;

    public StandUpReminderWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public void DismissForLock()
    {
        _allowClose = true;
        Close();
    }

    public void DismissForShutdown()
    {
        _allowClose = true;
        Close();
    }

    private void OnConfirmClicked(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        _allowClose = true;
        Confirmed?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _ = sender;

        if (!_allowClose)
        {
            e.Cancel = true;
        }
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        _ = sender;

        if (e.Key == Key.Escape || (e.Key == Key.F4 && Keyboard.Modifiers == ModifierKeys.Alt))
        {
            e.Handled = true;
        }
    }
}
