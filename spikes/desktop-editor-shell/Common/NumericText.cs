using System;
using System.Globalization;

namespace Mockups.DesktopEditorShell.Common;

internal static class NumericText
{
    public static decimal Decimal(string value, decimal fallback)
    {
        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant)
            ? invariant
            : decimal.TryParse(value, out var local) ? local : fallback;
    }

    public static decimal Integer(string value, decimal fallback)
    {
        return Math.Round(Decimal(value, fallback));
    }

    public static double Double(string value, double fallback)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant)
            ? invariant
            : double.TryParse(value, out var local) ? local : fallback;
    }

    public static double ClampedDouble(string value, double fallback, double min, double max)
    {
        return Math.Clamp(Double(value, fallback), min, max);
    }

    public static string IntegerString(decimal value)
    {
        return Math.Round(value).ToString("0", CultureInfo.InvariantCulture);
    }
}
