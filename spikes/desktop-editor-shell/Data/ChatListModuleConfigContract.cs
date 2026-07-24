using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class ChatListModuleConfigContract
{
    public const string RecordClassId = "module.core.chatList";

    public static void Validate(JsonObject config, string context)
    {
        RequireExactKeys(config, ["appearanceMode", "chatList"], context);
        ModuleAppearanceModeContract.Read(config, context);
        var chatList = JsonPath.RequiredObject(config, "chatList", context);
        var owner = $"{context}.chatList";
        RequireExactKeys(
            chatList,
            [
                "wallpaperEnabled",
                "stackSlot",
                "topIconBarSlot",
                "topIconBarInputs",
                "listSlot",
                "bottomIconBarSlot",
                "bottomIconBarInputs",
                "statusBarSlot",
                "navigationBarSlot",
                "runtimeContract",
            ],
            owner);
        JsonPath.RequiredBoolean(chatList, "wallpaperEnabled", owner);
        foreach (var slotKey in new[]
        {
            "stackSlot",
            "topIconBarSlot",
            "listSlot",
            "bottomIconBarSlot",
            "statusBarSlot",
            "navigationBarSlot",
        })
        {
            ComponentVariantSlotDocumentContract.Validate(
                JsonPath.RequiredObject(chatList, slotKey, owner),
                $"{owner}.{slotKey}");
        }
        ValidateIconBarInputs(
            JsonPath.RequiredObject(chatList, "topIconBarInputs", owner),
            $"{owner}.topIconBarInputs");
        ValidateIconBarInputs(
            JsonPath.RequiredObject(chatList, "bottomIconBarInputs", owner),
            $"{owner}.bottomIconBarInputs");

        var runtime = JsonPath.RequiredObject(chatList, "runtimeContract", owner);
        var runtimeOwner = $"{owner}.runtimeContract";
        RequireExactKeys(
            runtime,
            ["mode", "componentType", "variantReference", "inputIds", "collectionIds"],
            runtimeOwner);
        RequireExactValue(
            JsonPath.RequiredString(runtime, "mode", runtimeOwner),
            "exact",
            $"{runtimeOwner}.mode");
        RequireExactValue(
            JsonPath.RequiredString(runtime, "componentType", runtimeOwner),
            "list",
            $"{runtimeOwner}.componentType");
        var sourceVariant = JsonPath.RequiredString(runtime, "variantReference", runtimeOwner);
        if (!VariantReferenceId.TryParse(sourceVariant, out _, out _))
        {
            throw new InvalidOperationException(
                $"{runtimeOwner}.variantReference must be a full Variant reference.");
        }
        RequireExactStringArray(
            JsonPath.RequiredArray(runtime, "inputIds", runtimeOwner),
            ["itemWidth", "itemHeight"],
            $"{runtimeOwner}.inputIds");
        RequireExactStringArray(
            JsonPath.RequiredArray(runtime, "collectionIds", runtimeOwner),
            ["items"],
            $"{runtimeOwner}.collectionIds");
    }

    private static void ValidateIconBarInputs(JsonObject inputs, string owner)
    {
        RequireExactKeys(inputs, ["state", "size"], owner);
        RequireOneOf(
            JsonPath.RequiredString(inputs, "state", owner),
            ["idle", "active"],
            $"{owner}.state");
        JsonPath.RequiredString(inputs, "size", owner);
    }

    private static void RequireExactKeys(
        JsonObject value,
        IReadOnlyList<string> expected,
        string owner)
    {
        var missing = expected.Where((key) => !value.ContainsKey(key)).ToList();
        var unknown = value.Select((pair) => pair.Key)
            .Where((key) => !expected.Contains(key, StringComparer.Ordinal))
            .ToList();
        if (missing.Count == 0 && unknown.Count == 0) return;
        throw new InvalidOperationException(
            $"{owner} has an invalid shape."
            + (missing.Count > 0 ? $" Missing: {string.Join(", ", missing)}." : "")
            + (unknown.Count > 0 ? $" Unknown: {string.Join(", ", unknown)}." : ""));
    }

    private static void RequireOneOf(
        string value,
        IReadOnlyList<string> options,
        string path)
    {
        if (!options.Contains(value, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"{path} has unsupported value '{value}'.");
        }
    }

    private static void RequireExactValue(string value, string expected, string path)
    {
        if (!string.Equals(value, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{path} must be '{expected}'.");
        }
    }

    private static void RequireExactStringArray(
        JsonArray values,
        IReadOnlyList<string> expected,
        string path)
    {
        var actual = values.Select((value) =>
        {
            if (value is JsonValue jsonValue
                && jsonValue.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
            throw new InvalidOperationException($"{path} must contain only strings.");
        }).ToList();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"{path} must be exactly {string.Join(", ", expected)}.");
        }
    }
}
