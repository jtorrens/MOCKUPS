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

    public static string ApplyNeutralTint(string color, double hueDeg, double saturation)
    {
        if (!IsHexColor(color))
        {
            return color;
        }

        var clampedSaturation = Math.Clamp(saturation, 0, 1);
        if (clampedSaturation <= 0)
        {
            return color.ToUpperInvariant();
        }

        var parsed = ParseIrHex(color);
        var (_, _, lightness) = RgbToHsl(parsed.R / 255d, parsed.G / 255d, parsed.B / 255d);
        var (red, green, blue) = HslToRgb(NormalizeHue(hueDeg) / 360d, clampedSaturation, lightness);
        return parsed.A == 255
            ? $"#{ClampByte(red * 255):X2}{ClampByte(green * 255):X2}{ClampByte(blue * 255):X2}"
            : $"#{ClampByte(red * 255):X2}{ClampByte(green * 255):X2}{ClampByte(blue * 255):X2}{parsed.A:X2}";
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

    private static double NormalizeHue(double hueDeg)
    {
        var normalized = hueDeg % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static (double Hue, double Saturation, double Lightness) RgbToHsl(double red, double green, double blue)
    {
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var lightness = (max + min) / 2;
        if (Math.Abs(max - min) < 0.000001)
        {
            return (0, 0, lightness);
        }

        var delta = max - min;
        var saturation = lightness > 0.5 ? delta / (2 - max - min) : delta / (max + min);
        var hue = max == red
            ? (green - blue) / delta + (green < blue ? 6 : 0)
            : max == green
                ? (blue - red) / delta + 2
                : (red - green) / delta + 4;
        return (hue / 6, saturation, lightness);
    }

    private static (double Red, double Green, double Blue) HslToRgb(double hue, double saturation, double lightness)
    {
        if (saturation <= 0)
        {
            return (lightness, lightness, lightness);
        }

        var q = lightness < 0.5
            ? lightness * (1 + saturation)
            : lightness + saturation - lightness * saturation;
        var p = 2 * lightness - q;
        return (
            HueToRgb(p, q, hue + 1d / 3d),
            HueToRgb(p, q, hue),
            HueToRgb(p, q, hue - 1d / 3d));
    }

    private static double HueToRgb(double p, double q, double hue)
    {
        var next = hue;
        if (next < 0) next += 1;
        if (next > 1) next -= 1;
        if (next < 1d / 6d) return p + (q - p) * 6 * next;
        if (next < 1d / 2d) return q;
        if (next < 2d / 3d) return p + (q - p) * (2d / 3d - next) * 6;
        return p;
    }
}
