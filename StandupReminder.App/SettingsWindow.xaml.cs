using System.Windows;
using StandupReminder.App.ViewModels;
using StandupReminder.Core.Models;
using StandupReminder.Persistence;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using MediaColor = System.Windows.Media.Color;
using MediaColors = System.Windows.Media.Colors;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace StandupReminder.App;

public partial class SettingsWindow : FluentWindow
{
    private readonly SettingsWindowViewModel _viewModel;
    private readonly Func<ReminderScheduleOptions, AppearanceSettings, string?> _saveHandler;
    private bool _isUpdatingBackgroundControls;

    public SettingsWindow(SettingsWindowViewModel viewModel, Func<ReminderScheduleOptions, AppearanceSettings, string?> saveHandler)
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        _viewModel = viewModel;
        _saveHandler = saveHandler;
        DataContext = _viewModel;
        ApplyBackgroundFromHex(_viewModel.WindowBackgroundArgbHex, updateHexTextBox: true);
    }

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

        if (!_viewModel.TryBuildOptions(out var options))
        {
            return;
        }

        if (!ColorUtil.TryParseArgbHex(BackgroundHexTextBox.Text, out var backgroundColor))
        {
            ColorValidationInfoBar.Message = "Color must be in #AARRGGBB or #RRGGBB format.";
            ColorValidationInfoBar.IsOpen = true;
            return;
        }

        var appearanceSettings = new AppearanceSettings
        {
            WindowBackgroundArgbHex = ColorUtil.ToArgbHex(backgroundColor)
        };

        _viewModel.WindowBackgroundArgbHex = appearanceSettings.WindowBackgroundArgbHex;

        var saveError = _saveHandler(options, appearanceSettings);
        if (!string.IsNullOrWhiteSpace(saveError))
        {
            _viewModel.ShowError(saveError);
            return;
        }

        Close();
    }

    private void ApplyBackgroundFromHex(string? hex, bool updateHexTextBox)
    {
        _isUpdatingBackgroundControls = true;

        if (ColorUtil.TryParseArgbHex(hex, out var color))
        {
            AlphaSlider.Value = color.A;
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;

            var normalizedHex = ColorUtil.ToArgbHex(color);
            BackgroundPreviewBorder.Background = new SolidColorBrush(color);
            _viewModel.WindowBackgroundArgbHex = normalizedHex;

            if (updateHexTextBox)
            {
                BackgroundHexTextBox.Text = normalizedHex;
            }

            ColorValidationInfoBar.IsOpen = false;
            ColorValidationInfoBar.Message = string.Empty;
        }
        else
        {
            if (updateHexTextBox)
            {
                BackgroundHexTextBox.Text = hex ?? string.Empty;
            }

            ColorValidationInfoBar.Message = "Color must be in #AARRGGBB or #RRGGBB format.";
            ColorValidationInfoBar.IsOpen = true;
        }

        _isUpdatingBackgroundControls = false;
    }

    private void OnBackgroundChannelChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _ = sender;
        _ = e;

        if (_isUpdatingBackgroundControls)
        {
            return;
        }

        var color = MediaColor.FromArgb(
            (byte)AlphaSlider.Value,
            (byte)RedSlider.Value,
            (byte)GreenSlider.Value,
            (byte)BlueSlider.Value);

        _isUpdatingBackgroundControls = true;
        BackgroundPreviewBorder.Background = new SolidColorBrush(color);
        BackgroundHexTextBox.Text = ColorUtil.ToArgbHex(color);
        _viewModel.WindowBackgroundArgbHex = BackgroundHexTextBox.Text;
        ColorValidationInfoBar.IsOpen = false;
        ColorValidationInfoBar.Message = string.Empty;
        _isUpdatingBackgroundControls = false;
    }

    private void OnBackgroundHexTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
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

        var initial = MediaColors.Black;
        if (ColorUtil.TryParseArgbHex(BackgroundHexTextBox.Text, out var parsed))
        {
            initial = parsed;
        }

        var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(initial.A, initial.R, initial.G, initial.B)
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var color = dialog.Color;
            var selected = MediaColor.FromArgb(color.A, color.R, color.G, color.B);
            ApplyBackgroundFromHex(ColorUtil.ToArgbHex(selected), updateHexTextBox: true);
        }
    }
}
