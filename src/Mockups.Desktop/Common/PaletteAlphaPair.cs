using System;
using System.Globalization;

namespace Mockups.DesktopEditorShell.Common;

internal sealed record PaletteAlphaValue(string ColorToken, double Alpha);

internal sealed record PaletteAlphaPair(PaletteAlphaValue First, PaletteAlphaValue Second)
{
    public static PaletteAlphaPair Split(string value)
    {
        return ParseRequired(value, "Palette alpha pair");
    }

    public static PaletteAlphaPair ParseRequired(string value, string context)
    {
        var parts = value.Split("||", StringSplitOptions.None);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException(
                $"{context} must contain exactly one color pair and one alpha pair separated by '||'.");
        }

        var colors = SplitRequiredPair(parts[0], context, "color");
        var alphas = SplitRequiredPair(parts[1], context, "alpha");
        return new PaletteAlphaPair(
            new PaletteAlphaValue(colors.First, ParseAlphaRequired(alphas.First, context)),
            new PaletteAlphaValue(colors.Second, ParseAlphaRequired(alphas.Second, context)));
    }

    public static string Join(PaletteAlphaValue first, PaletteAlphaValue second)
    {
        return $"{first.ColorToken}|{second.ColorToken}||{FormatAlpha(first.Alpha)}|{FormatAlpha(second.Alpha)}";
    }

    public static string NormalizeRequired(string value, string context)
    {
        var pair = ParseRequired(value, context);
        return Join(pair.First, pair.Second);
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

    public static double ParseAlphaRequired(string value, string context)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed)
            || parsed is < 0 or > 1)
        {
            throw new InvalidOperationException($"{context} alpha values must be finite numbers from 0 to 1.");
        }

        return parsed;
    }

    private static (string First, string Second) SplitRequiredPair(
        string value,
        string context,
        string member)
    {
        var parts = value.Split('|', StringSplitOptions.None);
        if (parts.Length != 2
            || string.IsNullOrWhiteSpace(parts[0])
            || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new InvalidOperationException(
                $"{context} {member} pair must contain exactly two non-empty values.");
        }

        return (parts[0], parts[1]);
    }
}
