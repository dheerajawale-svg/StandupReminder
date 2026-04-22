using System.Media;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using StandupReminder.App.Models;
using System.Globalization;

namespace StandupReminder.App;

public partial class StandUpReminderWindow : Window
{
    private bool _allowClose;
    private string _defaultSnoozeMinutesText = "5";

    public event EventHandler? Confirmed;
    public event EventHandler<StandReminderSnoozedEventArgs>? Snoozed;

    public StandUpReminderWindow(TimeSpan standDuration, TimeSpan snoozeDuration, string backgroundArgbHex)
    {
        InitializeComponent();
        UpdateStandDuration(standDuration);
        UpdateSnoozeDuration(snoozeDuration);
        UpdateBackground(backgroundArgbHex);
        Closing += OnClosing;
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public void UpdateStandDuration(TimeSpan standDuration)
    {
        //StandDurationTextBlock.Text = $"Stand up now, stay active for the next {FormatMinutes(standDuration)}, and then the app will notify you when it is time to sit again.";
    }

    public void UpdateSnoozeDuration(TimeSpan snoozeDuration)
    {
        _defaultSnoozeMinutesText = ToWholeMinutes(snoozeDuration).ToString(CultureInfo.InvariantCulture);
        SnoozeMinutesTextBox.Text = _defaultSnoozeMinutesText;
        UpdateSnoozeButtonContent();
        ClearSnoozeValidation();
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

        if (!TryGetSnoozeDuration(out var duration))
        {
            return;
        }

        _allowClose = true;
        Snoozed?.Invoke(this, new StandReminderSnoozedEventArgs(duration));
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
        var wholeMinutes = ToWholeMinutes(duration);
        return wholeMinutes == 1 ? "1 minute" : $"{wholeMinutes} minutes";
    }

    private static int ToWholeMinutes(TimeSpan duration)
    {
        return Math.Max(1, (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero));
    }

    private void OnSnoozeMinutesTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        UpdateSnoozeButtonContent();
        ClearSnoozeValidation();
    }

    private void UpdateSnoozeButtonContent()
    {
        var labelMinutes = int.TryParse(SnoozeMinutesTextBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes > 0
            ? minutes.ToString(CultureInfo.InvariantCulture)
            : _defaultSnoozeMinutesText;
        SnoozeButton.Content = $"Snooze {labelMinutes} min";
    }

    private bool TryGetSnoozeDuration(out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        var rawValue = SnoozeMinutesTextBox.Text.Trim();

        if (!int.TryParse(rawValue, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes <= 0)
        {
            SnoozeValidationTextBlock.Text = "Snooze interval must be a whole number greater than 0.";
            SnoozeValidationTextBlock.Visibility = Visibility.Visible;
            return false;
        }

        duration = TimeSpan.FromMinutes(minutes);
        ClearSnoozeValidation();
        return true;
    }

    private void ClearSnoozeValidation()
    {
        SnoozeValidationTextBlock.Text = string.Empty;
        SnoozeValidationTextBlock.Visibility = Visibility.Collapsed;
    }
}
