using System;
using System.Globalization;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DesignPreviewTestValues
{
    public static JsonObject Parse(string json)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    public static string RuntimeJson(string previewJson)
    {
        var preview = Parse(previewJson);
        if (preview["testValues"] is not JsonObject testValues)
        {
            return previewJson;
        }

        foreach (var (key, value) in testValues)
        {
            preview[key] = value?.DeepClone();
        }

        return preview.ToJsonString();
    }

    public static string Value(JsonObject preview, ComponentInputDefinition input)
    {
        var testValues = preview["testValues"] as JsonObject;
        var value = testValues?[input.JsonKey] ?? preview[input.JsonKey];
        return value switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => text,
            JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var boolean) => boolean ? "true" : "false",
            JsonValue jsonValue when jsonValue.TryGetValue<double>(out var number) => number.ToString(CultureInfo.InvariantCulture),
            JsonValue jsonValue when jsonValue.TryGetValue<int>(out var integer) => integer.ToString(CultureInfo.InvariantCulture),
            JsonArray array => array.ToJsonString(),
            _ => input.DefaultValue,
        };
    }

    public static void SetValue(JsonObject preview, ComponentInputDefinition input, string value)
    {
        var testValues = preview["testValues"] as JsonObject ?? new JsonObject();
        preview["testValues"] = testValues;
        testValues[input.JsonKey] = ValueNode(input, value);
    }

    private static JsonNode? ValueNode(ComponentInputDefinition input, string value)
    {
        return input.Kind switch
        {
            ComponentInputKind.Number when double.TryParse(value.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => JsonValue.Create(number),
            ComponentInputKind.Boolean => JsonValue.Create(BooleanText.Parse(value)),
            ComponentInputKind.IconList => JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value) ?? new JsonArray(),
            _ => JsonValue.Create(value),
        };
    }
}
