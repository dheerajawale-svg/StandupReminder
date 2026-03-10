using Microsoft.Win32;

namespace StandupReminder.App.Services;

public sealed class RegistryAutoStartRegistrationService : IAutoStartRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "StandupReminder";

    public void EnsureRegistered()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("The application executable path could not be resolved.");
        }

        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, true)
            ?? throw new InvalidOperationException("The Windows Run registry key could not be opened.");

        var command = $"\"{processPath}\"";
        var existingValue = runKey.GetValue(ValueName) as string;

        if (string.Equals(existingValue, command, StringComparison.Ordinal))
        {
            return;
        }

        runKey.SetValue(ValueName, command, RegistryValueKind.String);
    }
}
