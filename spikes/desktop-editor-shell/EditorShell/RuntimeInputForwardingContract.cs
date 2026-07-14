using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class RuntimeInputForwardingContract
{
    public const string StorageKey = "$forwardedInputs";

    public static JsonObject EffectivePreview(JsonObject preview, JsonObject config)
    {
        var effective = preview.DeepClone() as JsonObject ?? new JsonObject();
        var inputs = effective["inputs"] as JsonArray ?? new JsonArray();
        effective["inputs"] = inputs;
        var existingIds = inputs.OfType<JsonObject>()
            .Select((input) => Text(input["id"]))
            .Where((id) => id.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        Visit(config, (container, targetKey, definition) =>
        {
            var id = Text(definition["id"]);
            var jsonKey = Text(definition["jsonKey"]);
            if (id.Length == 0 || jsonKey.Length == 0)
            {
                throw new InvalidOperationException("Forwarded runtime inputs require stable id and jsonKey values.");
            }
            var next = definition.DeepClone() as JsonObject ?? new JsonObject();
            next["source"] = "runtime";
            next["defaultValue"] = StorageText(container[targetKey]);
            if (existingIds.Add(id)) inputs.Add(next);
            if (effective[jsonKey] is null)
            {
                effective[jsonKey] = container[targetKey]?.DeepClone()
                    ?? throw new InvalidOperationException($"Forwarded runtime input '{id}' has no variant value.");
            }
        });
        return effective;
    }

    public static JsonObject Definition(
        FieldDefinition owner,
        ComponentInputBindingDefinition input,
        string runtimeLabel,
        string defaultValue)
    {
        var id = $"forwarded.{owner.Id}.{input.Id}";
        var jsonKey = string.Join("_", id.Select((character) => char.IsLetterOrDigit(character) ? character : '_'));
        return new JsonObject
        {
            ["id"] = id,
            ["label"] = runtimeLabel,
            ["jsonKey"] = jsonKey,
            ["kind"] = InputKind(input.ValueKind),
            ["valueKind"] = input.ValueKind.ToString(),
            ["defaultValue"] = defaultValue,
            ["source"] = "runtime",
            ["componentType"] = input.ComponentType,
            ["minimum"] = input.Number?.Minimum,
            ["maximum"] = input.Number?.Maximum,
            ["increment"] = input.Number?.Increment,
            ["options"] = input.Options is null
                ? new JsonArray()
                : new JsonArray(input.Options.Select((option) => (JsonNode?)new JsonObject
                {
                    ["value"] = option.Value,
                    ["label"] = option.Label,
                }).ToArray()),
        };
    }

    public static void Visit(
        JsonNode? node,
        Action<JsonObject, string, JsonObject> visitor)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (var child in array) Visit(child, visitor);
                break;
            case JsonObject obj:
                if (obj[StorageKey] is JsonObject forwards)
                {
                    foreach (var (targetKey, definitionNode) in forwards)
                    {
                        if (definitionNode is JsonObject definition) visitor(obj, targetKey, definition);
                    }
                }
                foreach (var (key, child) in obj.ToList())
                {
                    if (!key.Equals(StorageKey, StringComparison.Ordinal)) Visit(child, visitor);
                }
                break;
        }
    }

    private static string InputKind(ValueKind valueKind) => valueKind switch
    {
        ValueKind.Integer or ValueKind.Decimal or ValueKind.Alpha => "number",
        ValueKind.IntegerPair => "integerPair",
        ValueKind.Boolean => "boolean",
        ValueKind.OptionToken => "option",
        ValueKind.RecordReference => "recordReference",
        ValueKind.ComponentPreset => "componentPreset",
        ValueKind.ThemeToken => "themeToken",
        ValueKind.IconToken => "icon",
        ValueKind.IconTokenList => "iconList",
        ValueKind.StringMultiline => "multilineText",
        _ => "text",
    };

    private static string StorageText(JsonNode? node) => node switch
    {
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        JsonValue value when value.TryGetValue<bool>(out var boolean) => boolean ? "true" : "false",
        JsonValue value when value.TryGetValue<decimal>(out var number) => number.ToString(CultureInfo.InvariantCulture),
        JsonArray or JsonObject => node.ToJsonString(),
        _ => "",
    };

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
}
