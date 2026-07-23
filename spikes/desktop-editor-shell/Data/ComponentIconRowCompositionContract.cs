using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class ComponentIconRowCompositionContract
{
    private static readonly HashSet<string> RetiredTextBoxInputKeys = new(StringComparer.Ordinal)
    {
        "leftIcons",
        "rightIcons",
        "leftIconRowInputs",
        "rightIconRowInputs",
        "iconRowSize",
        "iconRowGap",
        "iconRowOrientation",
    };

    public static void ValidateConfig(
        string componentType,
        JsonObject config,
        string owner)
    {
        if (!componentType.Equals("textInputBar", StringComparison.Ordinal)
            && config.ContainsKey("textInput"))
        {
            throw new InvalidOperationException(
                $"{owner} contains retired Text Input Bar config outside its owning component.");
        }

        if (componentType.Equals("textInputBar", StringComparison.Ordinal))
        {
            var textInput = JsonPath.RequiredObject(config, "textInput", owner);
            ValidateTextBoxInputs(
                JsonPath.RequiredObject(textInput, "textBoxInputs", $"{owner}.textInput"),
                $"{owner}.textInput.textBoxInputs");
        }
        else if (componentType.Equals("bubble", StringComparison.Ordinal))
        {
            var bubble = JsonPath.RequiredObject(config, "bubble", owner);
            ValidateTextBoxInputs(
                JsonPath.RequiredObject(bubble, "textBoxInputs", $"{owner}.bubble"),
                $"{owner}.bubble.textBoxInputs");
        }
    }

    public static void ValidateDesignPreview(
        string componentType,
        JsonObject preview,
        string owner)
    {
        if (componentType.Equals("textInputBar", StringComparison.Ordinal))
        {
            foreach (var key in RetiredTextBoxInputKeys)
            {
                if (preview.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        $"{owner} contains retired Text Box Runtime Input '{key}'.");
                }
            }
            return;
        }
        if (!componentType.Equals("textBox", StringComparison.Ordinal)) return;

        var definitions = JsonPath.ObjectItems(
            JsonPath.RequiredArray(preview, "inputs", owner),
            $"{owner}.inputs");
        var byId = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            var id = JsonPath.RequiredString(definition, "id", $"{owner}.inputs");
            if (!byId.TryAdd(id, definition))
            {
                throw new InvalidOperationException(
                    $"{owner} contains duplicate Runtime Input id '{id}'.");
            }
        }
        foreach (var key in RetiredTextBoxInputKeys)
        {
            if (byId.ContainsKey(key) || preview.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"{owner} contains retired Text Box Runtime Input '{key}'.");
            }
        }

        foreach (var side in new[] { "left", "right" })
        {
            RequireInput(byId, $"{side}IconRowSlot", nameof(ValueKind.ComponentVariant), owner);
            RequireInput(byId, $"{side}IconRowItems", nameof(ValueKind.IconSlots), owner);
            RequireInput(byId, $"{side}IconRowGap", nameof(ValueKind.ThemeToken), owner);
            RequireInput(byId, $"{side}IconRowOrientation", nameof(ValueKind.OptionToken), owner);
        }
        RequireInput(byId, "iconGap", nameof(ValueKind.ThemeToken), owner);
    }

    private static void ValidateTextBoxInputs(JsonObject inputs, string owner)
    {
        foreach (var key in RetiredTextBoxInputKeys)
        {
            if (inputs.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"{owner} contains retired Text Box Runtime Input '{key}'.");
            }
        }

        foreach (var side in new[] { "left", "right" })
        {
            var slot = JsonPath.RequiredObject(inputs, $"{side}IconRowSlot", owner);
            _ = JsonPath.RequiredString(
                slot,
                "variantReference",
                $"{owner}.{side}IconRowSlot");
            _ = JsonPath.RequiredObject(
                slot,
                "overrides",
                $"{owner}.{side}IconRowSlot");
            IconSlotsDocumentContract.Validate(
                JsonPath.RequiredArray(inputs, $"{side}IconRowItems", owner),
                $"{owner}.{side}IconRowItems");
            _ = JsonPath.RequiredString(inputs, $"{side}IconRowGap", owner);
            var orientation = JsonPath.RequiredString(
                inputs,
                $"{side}IconRowOrientation",
                owner);
            if (orientation is not "horizontal" and not "vertical")
            {
                throw new InvalidOperationException(
                    $"{owner} has unsupported {side} Icon Row orientation '{orientation}'.");
            }
        }
        _ = JsonPath.RequiredString(inputs, "iconGap", owner);
    }

    private static void RequireInput(
        IReadOnlyDictionary<string, JsonObject> definitions,
        string id,
        string valueKind,
        string owner)
    {
        if (!definitions.TryGetValue(id, out var definition)
            || !JsonPath.RequiredString(definition, "jsonKey", $"{owner} Runtime Input '{id}'")
                .Equals(id, StringComparison.Ordinal)
            || !JsonPath.RequiredString(definition, "valueKind", $"{owner} Runtime Input '{id}'")
                .Equals(valueKind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{owner} requires exact Runtime Input '{id}' with ValueKind '{valueKind}'.");
        }
    }
}
