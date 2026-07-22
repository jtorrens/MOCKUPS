using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class ThemeNumericTokenValue
{
    public static double Require(JsonObject themeTokens, string token, string context)
    {
        if (!ThemeNumericTokenCatalog.TryGet(token, out var definition))
        {
            throw new InvalidOperationException(
                $"{context} uses undeclared numeric Theme token '{token}'.");
        }

        var node = JsonPath.Get(themeTokens, definition.Path);
        if (node is not JsonValue value || !TryFiniteNumber(value, out var number))
        {
            throw new InvalidOperationException(
                $"{context} Theme token '{token}' must resolve to a finite JSON number.");
        }
        return number;
    }

    public static double RequireNonNegative(
        JsonObject themeTokens,
        string token,
        string context)
    {
        var value = Require(themeTokens, token, context);
        if (value < 0)
        {
            throw new InvalidOperationException(
                $"{context} Theme token '{token}' must not be negative.");
        }
        return value;
    }

    public static double RequirePositive(
        JsonObject themeTokens,
        string token,
        string context)
    {
        var value = Require(themeTokens, token, context);
        if (value <= 0)
        {
            throw new InvalidOperationException(
                $"{context} Theme token '{token}' must be positive.");
        }
        return value;
    }

    private static bool TryFiniteNumber(JsonValue value, out double number)
    {
        if (value.TryGetValue<double>(out number) && double.IsFinite(number))
        {
            return true;
        }
        if (value.TryGetValue<decimal>(out var decimalValue))
        {
            number = (double)decimalValue;
            return double.IsFinite(number);
        }
        if (value.TryGetValue<long>(out var longValue))
        {
            number = longValue;
            return true;
        }
        if (value.TryGetValue<int>(out var integerValue))
        {
            number = integerValue;
            return true;
        }
        number = 0;
        return false;
    }
}
