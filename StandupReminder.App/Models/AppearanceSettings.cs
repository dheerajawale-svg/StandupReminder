namespace StandupReminder.App.Models;

public sealed class AppearanceSettings
{
    public const string DefaultWindowBackgroundArgbHex = "#B81E1E1E";

    public string WindowBackgroundArgbHex { get; init; } = DefaultWindowBackgroundArgbHex;
}