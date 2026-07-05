using System;
using System.Globalization;

namespace Mockups.DesktopEditorShell.Common;

internal sealed record PaletteAlphaValue(string ColorToken, double Alpha);

internal sealed record PaletteAlphaPair(PaletteAlphaValue First, PaletteAlphaValue Second)
{
    public static PaletteAlphaPair Split(string value)
    {
        var parts = value.Split("||", 2, StringSplitOptions.None);
        var colors = SplitPair(parts[0], "", "");
        var alphas = parts.Length == 2 ? SplitPair(parts[1], "1", "1") : (First: "1", Second: "1");
        return new PaletteAlphaPair(
            new PaletteAlphaValue(colors.First, ParseAlpha(alphas.First)),
            new PaletteAlphaValue(colors.Second, ParseAlpha(alphas.Second)));
    }

    public static string Join(PaletteAlphaValue first, PaletteAlphaValue second)
    {
        return $"{first.ColorToken}|{second.ColorToken}||{FormatAlpha(first.Alpha)}|{FormatAlpha(second.Alpha)}";
    }

    public static bool TryParseAlpha(string? text, out double value)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out invariant))
        {
            value = ClampAlpha(invariant);
            return true;
        }

        value = 1;
        return false;
    }

    public static double ClampAlpha(double value)
    {
        return Math.Clamp(value, 0, 1);
    }

    public static double SnapAlpha(double value)
    {
        return ClampAlpha(Math.Round(value / 0.05) * 0.05);
    }

    public static string FormatAlpha(double value)
    {
        return ClampAlpha(value).ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static double ParseAlpha(string value)
    {
        return TryParseAlpha(value, out var parsed) ? parsed : 1;
    }

    private static (string First, string Second) SplitPair(string value, string firstFallback, string secondFallback)
    {
        var parts = value.Split('|', 2, StringSplitOptions.None);
        return (
            parts.Length > 0 ? parts[0] : firstFallback,
            parts.Length > 1 ? parts[1] : secondFallback);
    }
}
