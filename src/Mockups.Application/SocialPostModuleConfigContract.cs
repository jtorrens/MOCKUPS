using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.Data;

internal static class SocialPostModuleConfigContract
{
    public const string RecordClassId = "module.core.socialPost";

    public static void Validate(JsonObject config, string context)
    {
        RequireExactKeys(config, ["appearanceMode", "socialPost"], context);
        ModuleAppearanceModeContract.Read(config, context);
        var socialPost = JsonPath.RequiredObject(config, "socialPost", context);
        var owner = $"{context}.socialPost";
        RequireExactKeys(
            socialPost,
            [
                "useAppWallpaper",
                "screenGutter",
                "zoneGap",
                "showHeader",
                "showStatusBar",
                "showNavigationBar",
                "showTextInputBar",
                "showKeyboard",
                "stackSlot",
                "headerStackSlot",
                "headerPrimarySlot",
                "headerPrimaryInputs",
                "headerSecondaryIconRowSlot",
                "headerSecondaryIconRowInputs",
                "mediaSlot",
                "bubbleSlot",
                "footerIconBarSlot",
                "textInputBarSlot",
                "keyboardSlot",
            ],
            owner);
        foreach (var key in new[]
        {
            "useAppWallpaper",
            "showHeader",
            "showStatusBar",
            "showNavigationBar",
            "showTextInputBar",
            "showKeyboard",
        })
        {
            JsonPath.RequiredBoolean(socialPost, key, owner);
        }
        JsonPath.RequiredString(socialPost, "screenGutter", owner);
        JsonPath.RequiredString(socialPost, "zoneGap", owner);
        foreach (var key in new[]
        {
            "stackSlot",
            "headerStackSlot",
            "headerPrimarySlot",
            "headerSecondaryIconRowSlot",
            "mediaSlot",
            "bubbleSlot",
            "footerIconBarSlot",
            "textInputBarSlot",
            "keyboardSlot",
        })
        {
            ComponentVariantSlotDocumentContract.Validate(
                JsonPath.RequiredObject(socialPost, key, owner),
                $"{owner}.{key}");
        }
        JsonPath.RequiredObject(socialPost, "headerPrimaryInputs", owner);
        JsonPath.RequiredObject(socialPost, "headerSecondaryIconRowInputs", owner);
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
}
