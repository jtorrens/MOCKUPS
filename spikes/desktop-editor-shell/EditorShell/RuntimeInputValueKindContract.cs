using Mockups.DesktopEditorShell.Common;
using System;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class RuntimeInputValueKindContract
{
    public static string InputKind(ValueKind valueKind) => valueKind switch
    {
        ValueKind.Integer or ValueKind.Decimal or ValueKind.HueDegrees or ValueKind.Alpha => "number",
        ValueKind.IntegerPair => "integerPair",
        ValueKind.Boolean => "boolean",
        ValueKind.OptionToken => "option",
        ValueKind.RecordReference => "recordReference",
        ValueKind.ComponentVariant => "componentVariant",
        ValueKind.ThemeToken => "themeToken",
        ValueKind.IconToken => "icon",
        ValueKind.IconTokenList or ValueKind.IconSlots => "iconList",
        ValueKind.StringMultiline => "multilineText",
        ValueKind.MediaFilePath => "mediaFilePath",
        ValueKind.StructuredCollection => "collection",
        ValueKind.BehaviorTiming => "behaviorTiming",
        ValueKind.StringSingleLine
            or ValueKind.StringReadOnly
            or ValueKind.DirectoryPath
            or ValueKind.ImageFilePath
            or ValueKind.ThemeTokenPair
            or ValueKind.TypographyStyle
            or ValueKind.TypographySystemStyle
            or ValueKind.HexColor
            or ValueKind.PaletteColorToken
            or ValueKind.PaletteColorPair
            or ValueKind.PaletteColorAlphaPair
            or ValueKind.EmbeddedComponent
            or ValueKind.ComponentInputBindings
            or ValueKind.AlignmentPlacement
            or ValueKind.Motion
            or ValueKind.MotionTiming => "text",
        _ => throw new InvalidOperationException($"Runtime input kind is not declared for dictionary value kind '{valueKind}'."),
    };

    public static ValueKind RequireCompatible(string kind, string valueKind, string owner)
    {
        if (!Enum.TryParse<ValueKind>(valueKind, ignoreCase: false, out var parsed)
            || !parsed.ToString().Equals(valueKind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{owner} has unsupported or missing valueKind '{valueKind}'.");
        }

        var expectedKind = InputKind(parsed);
        if (!kind.Equals(expectedKind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{owner} kind '{kind}' does not match valueKind '{valueKind}' (expected '{expectedKind}').");
        }
        return parsed;
    }

    public static JsonNode CreateDefaultValue(JsonObject definition, string owner)
    {
        var kind = JsonPath.RequiredString(definition, "kind", owner);
        var valueKindName = JsonPath.RequiredString(definition, "valueKind", owner);
        var valueKind = RequireCompatible(kind, valueKindName, owner);
        var defaultValue = definition["defaultValue"] is JsonValue defaultNode
            && defaultNode.TryGetValue<string>(out var text)
                ? text
                : null;

        if (valueKind == ValueKind.StructuredCollection)
        {
            if (!string.IsNullOrWhiteSpace(defaultValue))
            {
                return JsonPath.ParseRequiredArray(defaultValue, $"{owner} defaultValue");
            }
            if (definition["collection"] is JsonObject)
            {
                return new JsonArray();
            }
            throw new InvalidOperationException(
                $"{owner} StructuredCollection requires an array defaultValue or a collection contract.");
        }

        if (defaultValue is null)
        {
            throw new InvalidOperationException($"{owner} requires a string defaultValue.");
        }

        return valueKind switch
        {
            ValueKind.Boolean => JsonValue.Create(BooleanText.ParseRequired(defaultValue, $"{owner} defaultValue"))!,
            ValueKind.Integer => JsonValue.Create(ParseInteger(defaultValue, owner))!,
            ValueKind.Decimal or ValueKind.HueDegrees or ValueKind.Alpha =>
                JsonValue.Create(ParseDecimal(defaultValue, owner))!,
            ValueKind.IconTokenList or ValueKind.IconSlots =>
                JsonPath.ParseRequiredArray(defaultValue, $"{owner} defaultValue"),
            ValueKind.BehaviorTiming => JsonPath.ParseRequiredObject(
                BehaviorTimingValue.Parse(defaultValue).ToJson(),
                $"{owner} defaultValue"),
            _ => JsonValue.Create(defaultValue)!,
        };
    }

    private static int ParseInteger(string value, string owner)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"{owner} defaultValue must be an integer.");
        }
        return parsed;
    }

    private static decimal ParseDecimal(string value, string owner)
    {
        if (!decimal.TryParse(
                value.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            throw new InvalidOperationException($"{owner} defaultValue must be a finite number.");
        }
        return parsed;
    }
}
