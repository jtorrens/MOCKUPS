using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class ComponentIconRowCompositionContract
{
    private static readonly HashSet<string> TextBoxVariantOwnedInputKeys = new(StringComparer.Ordinal)
    {
        "placeholder",
        "maxLines",
        "leftIconRowSlot",
        "leftIconRowItems",
        "leftIconRowGap",
        "leftIconRowOrientation",
        "rightIconRowSlot",
        "rightIconRowItems",
        "rightIconRowGap",
        "rightIconRowOrientation",
        "iconGap",
    };

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

        if (componentType.Equals("iconRow", StringComparison.Ordinal))
        {
            ValidateIconRow(
                JsonPath.RequiredObject(config, "iconRow", owner),
                $"{owner}.iconRow");
            return;
        }

        if (componentType.Equals("textBox", StringComparison.Ordinal))
        {
            ValidateTextBox(
                JsonPath.RequiredObject(config, "textBox", owner),
                $"{owner}.textBox");
            return;
        }

        if (componentType.Equals("textInputBar", StringComparison.Ordinal))
        {
            var textInput = JsonPath.RequiredObject(config, "textInput", owner);
            ValidateTextBoxRuntimeForwarding(
                JsonPath.RequiredObject(textInput, "textBoxInputs", $"{owner}.textInput"),
                $"{owner}.textInput.textBoxInputs");
            return;
        }

        if (componentType.Equals("bubble", StringComparison.Ordinal)
            && JsonPath.RequiredObject(config, "bubble", owner).ContainsKey("textBoxInputs"))
        {
            throw new InvalidOperationException(
                $"{owner} contains retired duplicated Text Box Variant inputs.");
        }
    }

    public static void ValidateDesignPreview(
        string componentType,
        JsonObject preview,
        string owner)
    {
        if (!componentType.Equals("textBox", StringComparison.Ordinal)
            && !componentType.Equals("iconRow", StringComparison.Ordinal))
        {
            return;
        }

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

        if (componentType.Equals("textBox", StringComparison.Ordinal))
        {
            _ = RequireInput(byId, "sampleText", nameof(ValueKind.StringMultiline), owner);
            foreach (var key in TextBoxVariantOwnedInputKeys)
            {
                if (byId.ContainsKey(key) || preview.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        $"{owner} exposes Variant-owned Text Box value '{key}' as Runtime.");
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
            return;
        }

        foreach (var key in new[] { "items", "gap", "orientation" })
        {
            if (byId.ContainsKey(key) || preview.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"{owner} exposes Variant-owned Icon Row value '{key}' as Runtime.");
            }
        }
        if (preview.ContainsKey("collections"))
        {
            throw new InvalidOperationException(
                $"{owner} exposes Variant-owned Icon Row Buttons as a Runtime collection.");
        }
    }

    private static void ValidateTextBox(JsonObject textBox, string owner)
    {
        foreach (var side in new[] { "left", "right" })
        {
            ComponentVariantSlotDocumentContract.Validate(
                JsonPath.RequiredObject(textBox, $"{side}IconRowSlot", owner),
                $"{owner}.{side}IconRowSlot");
        }
        _ = JsonPath.RequiredString(textBox, "placeholder", owner);
        _ = JsonPath.RequiredInteger(textBox, "maxLines", owner);
        _ = JsonPath.RequiredString(textBox, "iconGap", owner);
    }

    private static void ValidateIconRow(JsonObject iconRow, string owner)
    {
        IconSlotsDocumentContract.Validate(
            JsonPath.RequiredArray(iconRow, "items", owner),
            $"{owner}.items");
        _ = JsonPath.RequiredString(iconRow, "gap", owner);
        var orientation = JsonPath.RequiredString(iconRow, "orientation", owner);
        if (orientation is not "horizontal" and not "vertical")
        {
            throw new InvalidOperationException(
                $"{owner} has unsupported orientation '{orientation}'.");
        }
    }

    private static void ValidateTextBoxRuntimeForwarding(JsonObject inputs, string owner)
    {
        foreach (var key in TextBoxVariantOwnedInputKeys)
        {
            if (inputs.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"{owner} duplicates Variant-owned Text Box value '{key}'.");
            }
        }
        foreach (var key in RetiredTextBoxInputKeys)
        {
            if (inputs.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"{owner} contains retired Text Box input '{key}'.");
            }
        }
        _ = JsonPath.RequiredString(inputs, "sampleText", owner);
    }

    private static JsonObject RequireInput(
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
        return definition;
    }
}
