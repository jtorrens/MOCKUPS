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
        RequireExactKeys(owner, ["padding", "verticalAlignment", "showSeparator", "slots"], $"{context}.contentRow");
        _ = RuntimeInputValueKindContract.ParseValue(ValueKind.ThemeTokenPair, JsonPath.RequiredString(owner, "padding", context), $"{context}.contentRow.padding");
        var alignment = JsonPath.RequiredString(owner, "verticalAlignment", context);
        if (alignment is not ("top" or "center" or "bottom")) throw new InvalidOperationException($"{context}.contentRow.verticalAlignment is invalid.");
        JsonPath.RequiredBoolean(owner, "showSeparator", context);
        var slots = JsonPath.ObjectItems(JsonPath.RequiredArray(owner, "slots", context), $"{context}.contentRow.slots").ToList();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];
            var slotContext = $"{context}.contentRow.slots[{index}]";
            RequireExactKeys(slot, ["id", "name", "kind", "avatarSlot", "iconSlot", "iconSizeToken", "labelSlot"], slotContext);
            var id = JsonPath.RequiredString(slot, "id", slotContext);
            if (!ids.Add(id)) throw new InvalidOperationException($"{context}.contentRow.slots contains duplicate id '{id}'.");
            JsonPath.RequiredString(slot, "name", slotContext);
            var kind = JsonPath.RequiredString(slot, "kind", slotContext);
            if (kind is not ("none" or "avatar" or "icon" or "label")) throw new InvalidOperationException($"{slotContext}.kind is invalid.");
            ComponentVariantSlotDocumentContract.Validate(JsonPath.RequiredObject(slot, "avatarSlot", slotContext), $"{slotContext}.avatarSlot");
            ComponentVariantSlotDocumentContract.Validate(JsonPath.RequiredObject(slot, "iconSlot", slotContext), $"{slotContext}.iconSlot");
            ComponentVariantSlotDocumentContract.Validate(JsonPath.RequiredObject(slot, "labelSlot", slotContext), $"{slotContext}.labelSlot");
            JsonPath.RequiredString(slot, "iconSizeToken", slotContext);
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
