using System.Media;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using StandupReminder.App.Models;

namespace StandupReminder.App;

public partial class StandUpReminderWindow : Window
{
    private bool _allowClose;

    public event EventHandler? Confirmed;
    public event EventHandler? Snoozed;

    public StandUpReminderWindow(TimeSpan standDuration, string backgroundArgbHex)
    {
        InitializeComponent();
        UpdateStandDuration(standDuration);
        UpdateBackground(backgroundArgbHex);
        Closing += OnClosing;
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public void UpdateStandDuration(TimeSpan standDuration)
    {
        //StandDurationTextBlock.Text = $"Stand up now, stay active for the next {FormatMinutes(standDuration)}, and then the app will notify you when it is time to sit again.";
    }

    public void DismissForLock()
    {
        _allowClose = true;
        Close();
    }

    public void DismissForPause()
    {
        _allowClose = true;
        Close();
    }

    public void DismissForShutdown()
    {
        _allowClose = true;
        Close();
    }

    public void UpdateBackground(string backgroundArgbHex)
    {
        if (!ColorUtil.TryParseArgbHex(backgroundArgbHex, out var color)
            && !ColorUtil.TryParseArgbHex(AppearanceSettings.DefaultWindowBackgroundArgbHex, out color))
        {
            return;
        }

        ReminderBackgroundBorder.Background = ColorUtil.ToBrush(color);
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        SystemSounds.Exclamation.Play();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _ = sender;

        if (!IsDescendantOfButton(e.OriginalSource as DependencyObject))
        {
            DragMove();
        }
    }

    private static bool IsDescendantOfButton(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is System.Windows.Controls.Button)
                return true;
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
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
