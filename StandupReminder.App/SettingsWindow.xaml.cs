using System.Windows;
using StandupReminder.App.Models;
using StandupReminder.App.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace StandupReminder.App;

public partial class SettingsWindow : FluentWindow
{
    private readonly SettingsWindowViewModel _viewModel;
    private readonly Func<ReminderScheduleOptions, string?> _saveHandler;

    public SettingsWindow(SettingsWindowViewModel viewModel, Func<ReminderScheduleOptions, string?> saveHandler)
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        _viewModel = viewModel;
        _saveHandler = saveHandler;
        DataContext = _viewModel;
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

        var saveError = _saveHandler(options);
        if (!string.IsNullOrWhiteSpace(saveError))
        {
            _viewModel.ShowError(saveError);
            return;
        }

        Close();
    }
}
