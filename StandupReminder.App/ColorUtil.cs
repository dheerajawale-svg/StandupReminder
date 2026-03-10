using System.Globalization;
using MediaColor = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace StandupReminder.App;

internal static class ColorUtil
{
    public static bool TryParseArgbHex(string? value, out MediaColor color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var s = value.Trim();
        if (s.StartsWith('#'))
        {
            s = s[1..];
        }

        if (s.Length == 6)
        {
            s = $"FF{s}";
        }

        if (s.Length != 8)
        {
            return false;
        }

        if (!uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            return false;
        }

        var a = (byte)((argb >> 24) & 0xFF);
        var r = (byte)((argb >> 16) & 0xFF);
        var g = (byte)((argb >> 8) & 0xFF);
        var b = (byte)(argb & 0xFF);

        color = MediaColor.FromArgb(a, r, g, b);
        return true;
    }

    public static string ToArgbHex(MediaColor color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static SolidColorBrush ToBrush(MediaColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}