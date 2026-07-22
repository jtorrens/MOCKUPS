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

        return ParseValue(valueKind, defaultValue, $"{owner} defaultValue");
    }

    public static JsonNode ParseValue(ValueKind valueKind, string value, string owner) => valueKind switch
    {
        ValueKind.Boolean => JsonValue.Create(BooleanText.ParseRequired(value, owner))!,
        ValueKind.Integer => JsonValue.Create(ParseInteger(value, owner))!,
        ValueKind.Decimal or ValueKind.HueDegrees or ValueKind.Alpha =>
            JsonValue.Create(ParseDecimal(value, owner))!,
        ValueKind.IconTokenList =>
            JsonPath.ParseRequiredArray(value, owner),
        ValueKind.IconSlots or ValueKind.StructuredCollection =>
            ParseCollection(value, owner),
        ValueKind.AlignmentPlacement => JsonPath.ParseRequiredObject(
            AlignmentPlacementValue.Parse(value).ToJsonString(),
            owner),
        ValueKind.Motion => JsonPath.ParseRequiredObject(
            MotionVariantValue.Parse(value).ToJsonString(),
            owner),
        ValueKind.MotionTiming => JsonPath.ParseRequiredObject(
            MotionTimingValue.Parse(value).ToJsonString(),
            owner),
        ValueKind.TypographyStyle or ValueKind.TypographySystemStyle =>
            TypographyStyleValue.Parse(value),
        ValueKind.ComponentInputBindings => ParseComponentInputBindings(value, owner),
        ValueKind.BehaviorTiming => JsonPath.ParseRequiredObject(
            BehaviorTimingValue.Parse(value).ToJson(),
            owner),
        _ => JsonValue.Create(value)!,
    };

    public static void ValidateRuntimeValue(JsonObject definition, JsonNode? value, string owner)
    {
        var kind = JsonPath.RequiredString(definition, "kind", owner);
        var valueKindName = JsonPath.RequiredString(definition, "valueKind", owner);
        var valueKind = RequireCompatible(kind, valueKindName, owner);
        if (value is null)
        {
            throw new InvalidOperationException($"{owner} value cannot be null.");
        }

        switch (valueKind)
        {
            case ValueKind.Boolean:
                RequireBoolean(value, owner);
                return;
            case ValueKind.Integer:
                RequireInteger(value, owner);
                return;
            case ValueKind.Decimal:
            case ValueKind.HueDegrees:
            case ValueKind.Alpha:
                RequireNumber(value, owner);
                return;
            case ValueKind.IconTokenList:
                RequireStringArray(value, owner);
                return;
            case ValueKind.IconSlots:
                RuntimeCollectionDocumentContract.Validate(RequireArray(value, owner), owner);
                return;
            case ValueKind.StructuredCollection:
                RuntimeCollectionDocumentContract.Validate(RequireArray(value, owner), owner);
                return;
            case ValueKind.AlignmentPlacement:
                _ = AlignmentPlacementValue.Parse(RequireObject(value, owner).ToJsonString());
                return;
            case ValueKind.Motion:
                _ = MotionVariantValue.Parse(RequireObject(value, owner).ToJsonString());
                return;
            case ValueKind.MotionTiming:
                _ = MotionTimingValue.Parse(RequireObject(value, owner).ToJsonString());
                return;
            case ValueKind.TypographyStyle:
            case ValueKind.TypographySystemStyle:
                _ = TypographyStyleValue.Parse(RequireObject(value, owner));
                return;
            case ValueKind.ComponentInputBindings:
                _ = RuntimeInputForwardingContract.Labels(RequireObject(value, owner));
                return;
            case ValueKind.BehaviorTiming:
                _ = BehaviorTimingValue.Parse(RequireObject(value, owner).ToJsonString());
                return;
            default:
                RequireString(value, owner);
                return;
        }
    }

    private static int ParseInteger(string value, string owner)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"{owner} defaultValue must be an integer.");
        }
        return parsed;
    }

    private static JsonArray ParseCollection(string value, string owner)
    {
        var items = JsonPath.ParseRequiredArray(value, owner);
        RuntimeCollectionDocumentContract.Validate(items, owner);
        return items;
    }

    private static JsonObject ParseComponentInputBindings(string value, string owner)
    {
        var bindings = JsonPath.ParseRequiredObject(value, owner);
        _ = RuntimeInputForwardingContract.Labels(bindings);
        return bindings;
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

    private static void RequireBoolean(JsonNode value, string owner)
    {
        if (value is not JsonValue scalar || !scalar.TryGetValue<bool>(out _))
        {
            throw new InvalidOperationException($"{owner} value must be a boolean.");
        }
    }

    private static void RequireInteger(JsonNode value, string owner)
    {
        var number = RequireNumber(value, owner);
        if (number != Math.Truncate(number) || number < int.MinValue || number > int.MaxValue)
        {
            throw new InvalidOperationException($"{owner} value must be an integer.");
        }
    }

    private static double RequireNumber(JsonNode value, string owner)
    {
        if (value is not JsonValue scalar
            || !scalar.TryGetValue<double>(out var number)
            || double.IsNaN(number)
            || double.IsInfinity(number))
        {
            throw new InvalidOperationException($"{owner} value must be a finite number.");
        }
        return number;
    }

    private static void RequireString(JsonNode value, string owner)
    {
        if (value is not JsonValue scalar || !scalar.TryGetValue<string>(out _))
        {
            throw new InvalidOperationException($"{owner} value must be a string.");
        }
    }

    private static void RequireStringArray(JsonNode value, string owner)
    {
        var array = RequireArray(value, owner);
        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonValue scalar
                || !scalar.TryGetValue<string>(out var text)
                || string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException(
                    $"{owner} value at index {index} must be a non-empty string.");
            }
        }
    }

    private static JsonArray RequireArray(JsonNode value, string owner) =>
        value as JsonArray
        ?? throw new InvalidOperationException($"{owner} value must be an array.");

    private static JsonObject RequireObject(JsonNode value, string owner) =>
        value as JsonObject
        ?? throw new InvalidOperationException($"{owner} value must be an object.");
}
