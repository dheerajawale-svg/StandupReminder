using System.Globalization;

namespace StandupReminder.Persistence;

public static class ArgbHexColor
{
    public static bool TryNormalize(string? value, out string normalizedArgbHex)
    {
        normalizedArgbHex = AppearanceSettings.DefaultWindowBackgroundArgbHex;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith('#'))
        {
            normalized = normalized[1..];
        }

        if (normalized.Length == 6)
        {
            normalized = $"FF{normalized}";
        }

        if (normalized.Length != 8
            || !uint.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        normalizedArgbHex = $"#{normalized.ToUpperInvariant()}";
        return true;
    }

    public static string NormalizeOrDefault(
        string? value,
        string fallbackArgbHex = AppearanceSettings.DefaultWindowBackgroundArgbHex)
    {
        if (TryNormalize(value, out var normalized))
        {
            return normalized;
        }

        return TryNormalize(fallbackArgbHex, out normalized)
            ? normalized
            : AppearanceSettings.DefaultWindowBackgroundArgbHex;
    }
}
