using Avalonia.Media;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Common;

internal static class ColorValue
{
    private static readonly Regex HexColorRegex = new("^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$", RegexOptions.Compiled);

    public static string NormalizeHex(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 6 && !trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            trimmed = $"#{trimmed}";
        }

        return trimmed;
    }

    public static bool IsHexColor(string value)
    {
        return HexColorRegex.IsMatch(value);
    }

    public static Color Parse(string? value, string fallback = "#808080")
    {
        try
        {
            return ParseIrHex(string.IsNullOrWhiteSpace(value) ? fallback : NormalizeHex(value));
        }
        catch (FormatException)
        {
            return ParseIrHex(fallback);
        }
    }

    public static IBrush SafeBrush(string? value, string fallback)
    {
        return new SolidColorBrush(Parse(value, fallback));
    }

    public static IBrush ContrastBrush(string? value)
    {
        var color = Parse(value, "#808080");
        var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
        return new SolidColorBrush(luminance > 0.58 ? Color.Parse("#111827") : Color.Parse("#FFFFFF"));
    }

    public static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static string WithAlpha(string color, double alpha)
    {
        if (!IsHexColor(color))
        {
            return color;
        }

        var rgb = color.Length == 9 ? color[..7] : color;
        var alphaByte = (int)Math.Round(Math.Clamp(alpha, 0, 1) * 255);
        return $"{rgb}{alphaByte:X2}";
    }

    public static string AdjustBrightness(string color, double multiplier)
    {
        if (!IsHexColor(color))
        {
            return color;
        }

        var parsed = ParseIrHex(color);
        var factor = Math.Max(0, 1 + multiplier);
        var red = ClampByte(parsed.R * factor);
        var green = ClampByte(parsed.G * factor);
        var blue = ClampByte(parsed.B * factor);
        return parsed.A == 255
            ? $"#{red:X2}{green:X2}{blue:X2}"
            : $"#{red:X2}{green:X2}{blue:X2}{parsed.A:X2}";
    }

    public static string CssColor(string value)
    {
        var color = ParseIrHex(value);
        if (color.A == 255)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        var alpha = (color.A / 255d).ToString("0.###", CultureInfo.InvariantCulture);
        return $"rgba({color.R}, {color.G}, {color.B}, {alpha})";
    }

    public static Color ParseIrHex(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "#ff00ff" : NormalizeHex(value);
        if (normalized.Length == 9
            && normalized[0] == '#'
            && byte.TryParse(normalized.AsSpan(1, 2), NumberStyles.HexNumber, null, out var red)
            && byte.TryParse(normalized.AsSpan(3, 2), NumberStyles.HexNumber, null, out var green)
            && byte.TryParse(normalized.AsSpan(5, 2), NumberStyles.HexNumber, null, out var blue)
            && byte.TryParse(normalized.AsSpan(7, 2), NumberStyles.HexNumber, null, out var alpha))
        {
            return Color.FromArgb(alpha, red, green, blue);
        }

        return Color.Parse(normalized);
    }

    private static byte ClampByte(double value)
    {
        return (byte)Math.Clamp(Math.Round(value), 0, 255);
    }
}
