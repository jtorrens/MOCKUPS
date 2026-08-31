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
        var expected = new List<string>
        {
            "useAppWallpaper",
            "showHeader",
            "showStatusBar",
            "showNavigationBar",
            "headerSurfaceSlot",
            "rowGapToken",
        };
        for (var row = 1; row <= 2; row++)
        {
            expected.Add($"row{row}Padding");
            expected.Add($"row{row}VerticalAlignment");
            expected.Add($"row{row}ShowSeparator");
            for (var slot = 1; slot <= 5; slot++)
            {
                var prefix = $"row{row}Slot{slot}";
                expected.Add($"{prefix}Kind");
                expected.Add($"{prefix}AvatarSlot");
                expected.Add($"{prefix}IconSlot");
                expected.Add($"{prefix}LabelSlot");
            }
        }
        RequireExactKeys(socialPost, expected, owner);

        foreach (var key in new[]
        {
            "useAppWallpaper",
            "showHeader",
            "showStatusBar",
            "showNavigationBar",
        })
        {
            JsonPath.RequiredBoolean(socialPost, key, owner);
        }
        ValidateSlot(socialPost, "headerSurfaceSlot", owner);
        JsonPath.RequiredString(socialPost, "rowGapToken", owner);

        for (var row = 1; row <= 2; row++)
        {
            JsonPath.RequiredString(socialPost, $"row{row}Padding", owner);
            RequireOneOf(
                JsonPath.RequiredString(socialPost, $"row{row}VerticalAlignment", owner),
                ["top", "center", "bottom"],
                $"{owner}.row{row}VerticalAlignment");
            JsonPath.RequiredBoolean(socialPost, $"row{row}ShowSeparator", owner);
            for (var slot = 1; slot <= 5; slot++)
            {
                var prefix = $"row{row}Slot{slot}";
                RequireOneOf(
                    JsonPath.RequiredString(socialPost, $"{prefix}Kind", owner),
                    ["none", "avatar", "icon", "label"],
                    $"{owner}.{prefix}Kind");
                ValidateSlot(socialPost, $"{prefix}AvatarSlot", owner);
                ValidateSlot(socialPost, $"{prefix}IconSlot", owner);
                ValidateSlot(socialPost, $"{prefix}LabelSlot", owner);
            }
        }
    }

    private static void ValidateSlot(JsonObject owner, string key, string context) =>
        ComponentVariantSlotDocumentContract.Validate(
            JsonPath.RequiredObject(owner, key, context),
            $"{context}.{key}");

    private static void RequireOneOf(
        string value,
        IReadOnlyList<string> supported,
        string owner)
    {
        if (supported.Contains(value, StringComparer.Ordinal)) return;
        throw new InvalidOperationException(
            $"{owner} must be one of: {string.Join(", ", supported)}.");
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
