using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

    public static IReadOnlyList<JsonObject> CollectionItems(
        JsonObject preview,
        RuntimeInputCollectionDefinition collection)
    {
        var source = preview["testValues"] is JsonObject testValues
            && testValues[collection.JsonKey] is JsonArray testItems
            ? testItems
            : preview[collection.JsonKey] as JsonArray;
        return source?.OfType<JsonObject>().Select((item) => item.DeepClone() as JsonObject ?? new JsonObject()).ToList()
            ?? [];
    }

    public static string CollectionValue(JsonObject item, ComponentInputDefinition input)
    {
        var value = item[input.JsonKey];
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

    public static void SetCollectionValue(
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        int itemIndex,
        ComponentInputDefinition input,
        string value)
    {
        var items = CollectionItems(preview, collection).Select((item) => item.DeepClone() as JsonObject ?? new JsonObject()).ToList();
        if (itemIndex < 0 || itemIndex >= items.Count)
        {
            return;
        }

        items[itemIndex][input.JsonKey] = ValueNode(input, value);
        var testValues = preview["testValues"] as JsonObject ?? new JsonObject();
        preview["testValues"] = testValues;
        testValues[collection.JsonKey] = new JsonArray(items.Select((item) => (JsonNode?)item).ToArray());
    }

    public static void PromoteToDefaults(
        JsonObject preview,
        IReadOnlyList<ComponentInputDefinition> inputs,
        IReadOnlyList<RuntimeInputCollectionDefinition>? collections = null)
    {
        var contract = preview["inputs"] as JsonArray;
        foreach (var input in inputs)
        {
            var value = Value(preview, input);
            preview[input.JsonKey] = ValueNode(input, value);
            if (contract is null)
            {
                continue;
            }

            var definition = contract
                .OfType<JsonObject>()
                .FirstOrDefault((candidate) =>
                    candidate["id"] is JsonValue id
                    && id.TryGetValue<string>(out var text)
                    && text.Equals(input.Id, StringComparison.Ordinal));
            if (definition is not null)
            {
                definition["defaultValue"] = value;
            }
        }

        foreach (var collection in collections ?? [])
        {
            if (preview["testValues"] is JsonObject values
                && values[collection.JsonKey] is JsonArray items)
            {
                preview[collection.JsonKey] = items.DeepClone();
            }
        }

        preview.Remove("testValues");
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
