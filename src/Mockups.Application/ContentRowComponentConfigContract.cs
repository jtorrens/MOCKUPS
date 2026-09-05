using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class ContentRowComponentConfigContract
{
    public const string ComponentType = "contentRow";

    public static void Validate(JsonObject config, string context)
    {
        RequireExactKeys(config, ["boundaryMotion", "contentRow"], context);
        _ = MotionVariantValue.Parse(JsonPath.RequiredObject(config, "boundaryMotion", context).ToJsonString());
        var owner = JsonPath.RequiredObject(config, "contentRow", context);
        var keys = new List<string> { "padding", "verticalAlignment", "showSeparator" };
        for (var index = 1; index <= 5; index++)
        {
            keys.AddRange([
                $"slot{index}Kind", $"slot{index}AvatarSlot", $"slot{index}IconSlot",
                $"slot{index}IconSizeToken", $"slot{index}LabelSlot"
            ]);
        }
        RequireExactKeys(owner, keys, $"{context}.contentRow");
        _ = RuntimeInputValueKindContract.ParseValue(ValueKind.ThemeTokenPair, JsonPath.RequiredString(owner, "padding", context), $"{context}.contentRow.padding");
        var alignment = JsonPath.RequiredString(owner, "verticalAlignment", context);
        if (alignment is not ("top" or "center" or "bottom")) throw new InvalidOperationException($"{context}.contentRow.verticalAlignment is invalid.");
        JsonPath.RequiredBoolean(owner, "showSeparator", context);
        for (var index = 1; index <= 5; index++)
        {
            var kind = JsonPath.RequiredString(owner, $"slot{index}Kind", context);
            if (kind is not ("none" or "avatar" or "icon" or "label")) throw new InvalidOperationException($"{context}.contentRow.slot{index}Kind is invalid.");
            ComponentVariantSlotDocumentContract.Validate(JsonPath.RequiredObject(owner, $"slot{index}AvatarSlot", context), $"{context}.contentRow.slot{index}AvatarSlot");
            ComponentVariantSlotDocumentContract.Validate(JsonPath.RequiredObject(owner, $"slot{index}IconSlot", context), $"{context}.contentRow.slot{index}IconSlot");
            ComponentVariantSlotDocumentContract.Validate(JsonPath.RequiredObject(owner, $"slot{index}LabelSlot", context), $"{context}.contentRow.slot{index}LabelSlot");
            JsonPath.RequiredString(owner, $"slot{index}IconSizeToken", context);
        }
    }

    private static void RequireExactKeys(JsonObject value, IReadOnlyList<string> expected, string owner)
    {
        var missing = expected.Where(key => !value.ContainsKey(key)).ToList();
        var unknown = value.Select(pair => pair.Key).Where(key => !expected.Contains(key, StringComparer.Ordinal)).ToList();
        if (missing.Count == 0 && unknown.Count == 0) return;
        throw new InvalidOperationException($"{owner} has an invalid shape."
            + (missing.Count > 0 ? $" Missing: {string.Join(", ", missing)}." : "")
            + (unknown.Count > 0 ? $" Unknown: {string.Join(", ", unknown)}." : ""));
    }
}
