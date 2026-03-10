using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using StandupReminder.App.Models;

namespace StandupReminder.App.ViewModels;

public sealed class SettingsWindowViewModel : INotifyPropertyChanged
{
    private string _initialSitMinutes;
    private string _recurringSitMinutes;
    private string _standMinutes;
    private string _windowBackgroundArgbHex;
    private string _errorMessage = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsWindowViewModel(ReminderScheduleOptions options, AppearanceSettings appearanceSettings)
    {
        _initialSitMinutes = ToWholeMinutes(options.InitialSit).ToString(CultureInfo.InvariantCulture);
        _recurringSitMinutes = ToWholeMinutes(options.RecurringSit).ToString(CultureInfo.InvariantCulture);
        _standMinutes = ToWholeMinutes(options.Stand).ToString(CultureInfo.InvariantCulture);
        _windowBackgroundArgbHex = appearanceSettings.WindowBackgroundArgbHex;
    }

    public string InitialSitMinutes
    {
        get => _initialSitMinutes;
        set => SetProperty(ref _initialSitMinutes, value);
    }

    public string RecurringSitMinutes
    {
        get => _recurringSitMinutes;
        set => SetProperty(ref _recurringSitMinutes, value);
    }

    public string StandMinutes
    {
        get => _standMinutes;
        set => SetProperty(ref _standMinutes, value);
    }

    public string WindowBackgroundArgbHex
    {
        get => _windowBackgroundArgbHex;
        set => SetProperty(ref _windowBackgroundArgbHex, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool TryBuildOptions(out ReminderScheduleOptions options)
    {
        options = new ReminderScheduleOptions();
        ErrorMessage = string.Empty;

        if (!TryParseMinutes(InitialSitMinutes, "Initial sit interval", out var initialSitMinutes))
        {
            return false;
        }

        if (!TryParseMinutes(RecurringSitMinutes, "Recurring sit interval", out var recurringSitMinutes))
        {
            return false;
        }

        if (!TryParseMinutes(StandMinutes, "Stand interval", out var standMinutes))
        {
            return false;
        }

        options = new ReminderScheduleOptions
        {
            InitialSit = TimeSpan.FromMinutes(initialSitMinutes),
            RecurringSit = TimeSpan.FromMinutes(recurringSitMinutes),
            Stand = TimeSpan.FromMinutes(standMinutes)
        };

        return true;
    }

    public void ShowError(string message)
    {
        ErrorMessage = message;
    }

    private bool TryParseMinutes(string rawValue, string label, out int minutes)
    {
        minutes = 0;

        if (!int.TryParse(rawValue.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out minutes) || minutes <= 0)
        {
            ErrorMessage = $"{label} must be a whole number greater than 0.";
            return false;
        }

        return true;
    }

    private void SetProperty(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static int ToWholeMinutes(TimeSpan duration)
    {
        return Math.Max(1, (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero));
    }
}
