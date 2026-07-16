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
        var collections = effective["collections"] as JsonArray ?? new JsonArray();
        effective["collections"] = collections;
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
            RebaseTransitionForParent(next, container);
            next["source"] = "runtime";
            next["defaultValue"] = StorageText(container[targetKey]);
            if (next["collection"] is JsonObject collection)
            {
                var projected = ProjectCollectionValue(container[targetKey], next["projection"] as JsonObject);
                effective[jsonKey] = projected;
                var runtimeCollection = collection.DeepClone() as JsonObject ?? new JsonObject();
                runtimeCollection["jsonKey"] = jsonKey;
                runtimeCollection["sourceCollectionJsonKey"] = jsonKey;
                if (!collections.OfType<JsonObject>().Any((candidate) => Text(candidate["id"]) == Text(runtimeCollection["id"])))
                    collections.Add(runtimeCollection);
                return;
            }
            if (existingIds.Add(id)) inputs.Add(next);
            if (effective[jsonKey] is null)
            {
                effective[jsonKey] = container[targetKey]?.DeepClone()
                    ?? throw new InvalidOperationException($"Forwarded runtime input '{id}' has no variant value.");
            }
            var resolvedJsonKey = Text(definition["resolvedJsonKey"]);
            var targetResolvedJsonKey = Text(definition["targetResolvedJsonKey"]);
            if (resolvedJsonKey.Length > 0
                && targetResolvedJsonKey.Length > 0
                && effective[resolvedJsonKey] is null
                && container[targetResolvedJsonKey] is { } resolvedValue)
            {
                effective[resolvedJsonKey] = resolvedValue.DeepClone();
            }
        });
        return effective;
    }

    private static JsonArray ProjectCollectionValue(JsonNode? source, JsonObject? projection)
    {
        var result = source?.DeepClone() as JsonArray
            ?? throw new InvalidOperationException("Forwarded runtime collection has no Variant collection value.");
        if (projection is null) return result;
        var alternativesKey = Text(projection["optionsSourceCollectionJsonKey"]);
        var stateKey = Text(projection["stateJsonKey"]);
        var transitionKey = Text(projection["transitionJsonKey"]);
        var elapsedKey = Text(projection["elapsedJsonKey"]);
        var fromKey = Text(projection["fromJsonKey"]);
        foreach (var item in result.OfType<JsonObject>())
        {
            var alternatives = item[alternativesKey] as JsonArray
                ?? throw new InvalidOperationException($"Forwarded runtime collection item is missing '{alternativesKey}'.");
            var firstId = alternatives.OfType<JsonObject>().FirstOrDefault()?["id"]?.GetValue<string>() ?? "";
            item[stateKey] = firstId;
            item[transitionKey] = false;
            item[elapsedKey] = 0;
            item[fromKey] = firstId;
        }
        return result;
    }

    private static void RebaseTransitionForParent(JsonObject definition, JsonObject container)
    {
        if (definition["transition"] is not JsonObject transition) return;
        var targetSourceInputId = Text(transition["targetInputId"]);
        var sibling = (container[StorageKey] as JsonObject)?
            .Select((entry) => entry.Value as JsonObject)
            .FirstOrDefault((candidate) => candidate is not null
                && Text(candidate["sourceInputId"]).Equals(targetSourceInputId, StringComparison.Ordinal));
        if (sibling is null)
        {
            definition.Remove("transition");
            return;
        }
        transition["targetInputId"] = Text(sibling["id"]);
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
            ["sourceInputId"] = input.Id,
            ["label"] = runtimeLabel,
            ["jsonKey"] = jsonKey,
            ["kind"] = InputKind(input.ValueKind),
            ["valueKind"] = input.ValueKind.ToString(),
            ["defaultValue"] = defaultValue,
            ["source"] = "runtime",
            ["componentType"] = input.ComponentType,
            ["tableId"] = input.TableId,
            ["resolvedJsonKey"] = string.IsNullOrWhiteSpace(input.ResolvedJsonKey)
                ? ""
                : $"{jsonKey}_resolved",
            ["targetResolvedJsonKey"] = input.ResolvedJsonKey,
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
            ["transition"] = TransitionNode(input.Transition),
        };
    }

    private static JsonObject? TransitionNode(ComponentInputTransitionDefinition? transition) =>
        transition is null
            ? null
            : new JsonObject
            {
                ["targetInputId"] = transition.TargetInputId,
                ["triggerValues"] = new JsonArray(
                    transition.TriggerValues.Select((value) => (JsonNode?)value).ToArray()),
                ["replacementValue"] = transition.ReplacementValue,
                ["targetValuePattern"] = transition.TargetValuePattern,
                ["forwardedTargetOnly"] = transition.ForwardedTargetOnly,
            };

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

    public static void RebaseIds(JsonNode node, string oldOwnerSegment, string newOwnerSegment)
    {
        Visit(node, (_, _, definition) =>
        {
            var id = Text(definition["id"]);
            if (id.Length == 0) return;
            var nextId = id.Replace(oldOwnerSegment, newOwnerSegment, StringComparison.Ordinal);
            var nextJsonKey = string.Join("_", nextId.Select((character) =>
                char.IsLetterOrDigit(character) ? character : '_'));
            definition["id"] = nextId;
            definition["jsonKey"] = nextJsonKey;
            if (Text(definition["targetResolvedJsonKey"]).Length > 0)
            {
                definition["resolvedJsonKey"] = $"{nextJsonKey}_resolved";
            }
        });
    }

    public static IReadOnlyList<string> Labels(JsonNode? node)
    {
        var labels = new List<string>();
        Visit(node, (_, _, definition) =>
        {
            var label = Text(definition["label"]);
            if (label.Length == 0) label = Text(definition["id"]);
            if (label.Length > 0 && !labels.Contains(label, StringComparer.Ordinal))
            {
                labels.Add(label);
            }
        });
        return labels;
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
        ValueKind.IconTokenList or ValueKind.IconSlots => "iconList",
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
