using System.Globalization;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using StandupReminder.Core.Models;
using StandupReminder.Persistence;
using Windows.Graphics;
using Windows.UI;

namespace StandupReminder.WinUI;

public sealed partial class SettingsWindow : Window
{
    private const int SettingsWidth = 560;
    private const int SettingsHeight = 820;
    private readonly Func<ReminderScheduleOptions, AppearanceSettings, string?> _saveHandler;
    private bool _isUpdatingBackgroundControls;

    public SettingsWindow(SettingsWindowViewModel viewModel, Func<ReminderScheduleOptions, AppearanceSettings, string?> saveHandler)
    {
        ViewModel = viewModel;
        _saveHandler = saveHandler;
        InitializeComponent();
        ConfigureWindow();
        ApplyBackgroundFromHex(ViewModel.WindowBackgroundArgbHex, updateHexTextBox: true);
    }

    public SettingsWindowViewModel ViewModel { get; }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        Close();
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (!ViewModel.TryBuildOptions(out var options))
        {
            return;
        }

        if (!ArgbHexColor.TryNormalize(BackgroundHexTextBox.Text, out var normalizedArgbHex))
        {
            ShowInvalidColor();
            return;
        }

        var appearanceSettings = new AppearanceSettings
        {
            WindowBackgroundArgbHex = normalizedArgbHex
        };

        ViewModel.WindowBackgroundArgbHex = normalizedArgbHex;

        var saveError = _saveHandler(options, appearanceSettings);
        if (!string.IsNullOrWhiteSpace(saveError))
        {
            ViewModel.ShowError(saveError);
            return;
        }

        Close();
    }

    private void OnBackgroundChannelChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isUpdatingBackgroundControls)
        {
            return;
        }

        var color = Color.FromArgb(
            ToByte(AlphaSlider.Value),
            ToByte(RedSlider.Value),
            ToByte(GreenSlider.Value),
            ToByte(BlueSlider.Value));

        _isUpdatingBackgroundControls = true;
        BackgroundPreviewBorder.Background = new SolidColorBrush(color);
        BackgroundColorPicker.Color = color;
        BackgroundHexTextBox.Text = ToArgbHex(color);
        ViewModel.WindowBackgroundArgbHex = BackgroundHexTextBox.Text;
        HideInvalidColor();
        UpdateChannelValueLabels();
        _isUpdatingBackgroundControls = false;
    }

    private void OnBackgroundHexTextChanged(object sender, TextChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isUpdatingBackgroundControls)
        {
            return;
        }

        ApplyBackgroundFromHex(BackgroundHexTextBox.Text, updateHexTextBox: false);
    }

    private void OnPickBackgroundColorClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        var currentColor = TryParseArgbColor(BackgroundHexTextBox.Text, out var parsedColor)
            ? parsedColor
            : ParseNormalizedArgbColor(AppearanceSettings.DefaultWindowBackgroundArgbHex);

        BackgroundColorPicker.Color = currentColor;
    }

    private void OnConfirmColorPickerClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyBackgroundFromHex(ToArgbHex(BackgroundColorPicker.Color), updateHexTextBox: true);
        BackgroundColorPickerButton.Flyout?.Hide();
    }

    private void OnCancelColorPickerClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        BackgroundColorPickerButton.Flyout?.Hide();
    }

    private void ConfigureWindow()
    {
        AppWindow.Title = "Reminder settings";
        var presenter = OverlappedPresenter.Create();
        presenter.IsAlwaysOnTop = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        presenter.PreferredMinimumWidth = SettingsWidth;
        presenter.PreferredMaximumWidth = SettingsWidth;
        presenter.PreferredMinimumHeight = SettingsHeight;
        presenter.PreferredMaximumHeight = SettingsHeight;
        AppWindow.SetPresenter(presenter);
        AppWindow.Resize(new SizeInt32(SettingsWidth, SettingsHeight));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        CenterOnDisplay();
    }

    private void CenterOnDisplay()
    {
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest)?.WorkArea;
        if (workArea is null)
        {
            return;
        }

        var centeredX = workArea.Value.X + Math.Max(0, (workArea.Value.Width - SettingsWidth) / 2);
        var centeredY = workArea.Value.Y + Math.Max(0, (workArea.Value.Height - SettingsHeight) / 2);
        AppWindow.Move(new PointInt32(centeredX, centeredY));
    }

    private void ApplyBackgroundFromHex(string? hex, bool updateHexTextBox)
    {
        _isUpdatingBackgroundControls = true;

        if (TryParseArgbColor(hex, out var color))
        {
            AlphaSlider.Value = color.A;
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;
            BackgroundPreviewBorder.Background = new SolidColorBrush(color);
            BackgroundColorPicker.Color = color;
            ViewModel.WindowBackgroundArgbHex = ToArgbHex(color);

            if (updateHexTextBox)
            {
                BackgroundHexTextBox.Text = ViewModel.WindowBackgroundArgbHex;
            }

            HideInvalidColor();
        }
        else
        {
            if (updateHexTextBox)
            {
                BackgroundHexTextBox.Text = hex ?? string.Empty;
            }

            ShowInvalidColor();
        }

        UpdateChannelValueLabels();
        _isUpdatingBackgroundControls = false;
    }

    private void UpdateChannelValueLabels()
    {
        AlphaValueTextBlock.Text = ((int)Math.Round(AlphaSlider.Value, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
        RedValueTextBlock.Text = ((int)Math.Round(RedSlider.Value, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
        GreenValueTextBlock.Text = ((int)Math.Round(GreenSlider.Value, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
        BlueValueTextBlock.Text = ((int)Math.Round(BlueSlider.Value, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
    }

    private void ShowInvalidColor()
    {
        ColorValidationInfoBar.Message = "Color must be in #AARRGGBB or #RRGGBB format.";
        ColorValidationInfoBar.IsOpen = true;
    }

    private void HideInvalidColor()
    {
        ColorValidationInfoBar.Message = string.Empty;
        ColorValidationInfoBar.IsOpen = false;
    }

    private static bool TryParseArgbColor(string? value, out Color color)
    {
        color = default;

        if (!ArgbHexColor.TryNormalize(value, out var normalizedArgbHex))
        {
            return false;
        }

        color = ParseNormalizedArgbColor(normalizedArgbHex);
        return true;
    }

    private static Color ParseNormalizedArgbColor(string normalizedArgbHex)
    {
        var hex = normalizedArgbHex.TrimStart('#');
        var value = uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Color.FromArgb(
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value);
    }

    private static string ToArgbHex(Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static byte ToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
    }
}