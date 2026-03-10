using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace StandupReminder.App;

public partial class StandUpReminderWindow : Window
{
    private bool _allowClose;

    public event EventHandler? Confirmed;
    public event EventHandler? Snoozed;

    public StandUpReminderWindow(TimeSpan standDuration)
    {
        InitializeComponent();
        UpdateStandDuration(standDuration);
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public void UpdateStandDuration(TimeSpan standDuration)
    {
        StandDurationTextBlock.Text = $"Stand up now, stay active for the next {FormatMinutes(standDuration)}, and then the app will notify you when it is time to sit again.";
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

    private void OnSnoozeClicked(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        _allowClose = true;
        Snoozed?.Invoke(this, EventArgs.Empty);
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

    private static string FormatMinutes(TimeSpan duration)
    {
        var wholeMinutes = Math.Max(1, (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero));
        return wholeMinutes == 1 ? "1 minute" : $"{wholeMinutes} minutes";
    }
}
