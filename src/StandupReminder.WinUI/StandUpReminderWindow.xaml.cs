using System.Globalization;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StandupReminder.Persistence;
using Windows.Graphics;
using Windows.UI;

namespace StandupReminder.WinUI;

public sealed partial class StandUpReminderWindow : Window
{
    private const int PromptWidth = 460;
    private const int PromptHeight = 300;
    private bool _allowClose;

    public StandUpReminderWindow(TimeSpan standDuration, string backgroundArgbHex)
    {
        InitializeComponent();
        ConfigureWindow();
        UpdateStandDuration(standDuration);
        UpdateBackground(backgroundArgbHex);
    }

    public event EventHandler? Confirmed;
    public event EventHandler? Snoozed;

    public void UpdateStandDuration(TimeSpan standDuration)
    {
        StandDurationTextBlock.Text = $"Stand up now, stay active for the next {FormatMinutes(standDuration)}, and then the app will notify you when it is time to sit again.";
    }

    public void UpdateBackground(string backgroundArgbHex)
    {
        ReminderBackgroundBorder.Background = new SolidColorBrush(ParseArgbColor(backgroundArgbHex));
    }

    public void DismissForLock()
    {
        CloseAllowed();
    }

    public void DismissForPause()
    {
        CloseAllowed();
    }

    public void DismissForShutdown()
    {
        CloseAllowed();
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

    private void ConfigureWindow()
    {
        AppWindow.Title = "Time to stand up";
        var presenter = OverlappedPresenter.Create();
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        presenter.PreferredMinimumWidth = PromptWidth;
        presenter.PreferredMaximumWidth = PromptWidth;
        presenter.PreferredMinimumHeight = PromptHeight;
        presenter.PreferredMaximumHeight = PromptHeight;
        AppWindow.SetPresenter(presenter);
        AppWindow.Resize(new SizeInt32(PromptWidth, PromptHeight));
        CenterOnDisplay();
        AppWindow.Closing += OnAppWindowClosing;
        Closed += OnClosed;
    }

    private void CenterOnDisplay()
    {
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest)?.WorkArea;
        if (workArea is null)
        {
            return;
        }

        var centeredX = workArea.Value.X + Math.Max(0, (workArea.Value.Width - PromptWidth) / 2);
        var centeredY = workArea.Value.Y + Math.Max(0, (workArea.Value.Height - PromptHeight) / 2);
        AppWindow.Move(new PointInt32(centeredX, centeredY));
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        _ = sender;
        if (!_allowClose)
        {
            args.Cancel = true;
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _ = sender;
        _ = args;
        AppWindow.Closing -= OnAppWindowClosing;
        Closed -= OnClosed;
    }

    private void CloseAllowed()
    {
        _allowClose = true;
        Close();
    }

    private static Color ParseArgbColor(string backgroundArgbHex)
    {
        var normalized = ArgbHexColor.NormalizeOrDefault(backgroundArgbHex);
        var hex = normalized.TrimStart('#');
        var value = uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Color.FromArgb(
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value);
    }

    private static string FormatMinutes(TimeSpan duration)
    {
        var wholeMinutes = Math.Max(1, (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero));
        return wholeMinutes == 1 ? "1 minute" : $"{wholeMinutes} minutes";
    }
}