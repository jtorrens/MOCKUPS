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
                "showHeader",
                "headerHeight",
                "showStatusBar",
                "showNavigationBar",
                "headerSurfaceSlot",
                "rowGapToken",
                "rows",
                "mediaSlot",
                "mediaPadding",
                "mediaInputs",
                "showMediaSeparator",
            ],
            owner);

        foreach (var key in new[]
        {
            "useAppWallpaper",
            "showHeader",
            "showStatusBar",
            "showNavigationBar",
            "showMediaSeparator",
        })
        {
            JsonPath.RequiredBoolean(socialPost, key, owner);
        }
        var headerHeight = JsonPath.RequiredNumber(socialPost, "headerHeight", owner);
        if (headerHeight < 0)
        {
            throw new InvalidOperationException(
                $"{owner}.headerHeight must be non-negative.");
        }
        ValidateSlot(socialPost, "headerSurfaceSlot", owner);
        JsonPath.RequiredString(socialPost, "rowGapToken", owner);
        ValidateSlot(socialPost, "mediaSlot", owner);
        JsonPath.RequiredString(socialPost, "mediaPadding", owner);
        ValidateMediaInputs(
            JsonPath.RequiredObject(socialPost, "mediaInputs", owner),
            $"{owner}.mediaInputs");

        var rows = JsonPath.RequiredArray(socialPost, "rows", owner);
        if (rows.Count != 2)
        {
            throw new InvalidOperationException(
                $"{owner}.rows must contain exactly row1 and row2.");
        }
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var rowId = $"row{rowIndex + 1}";
            var row = rows[rowIndex] as JsonObject
                ?? throw new InvalidOperationException(
                    $"{owner}.rows[{rowIndex}] must be an object.");
            var rowKeys = new List<string>
            {
                "id",
                "label",
                "padding",
                "verticalAlignment",
                "showSeparator",
            };
            for (var slot = 1; slot <= 5; slot++)
            {
                rowKeys.Add($"slot{slot}Kind");
                rowKeys.Add($"slot{slot}AvatarSlot");
                rowKeys.Add($"slot{slot}IconSlot");
                rowKeys.Add($"slot{slot}LabelSlot");
            }
            RequireExactKeys(row, rowKeys, $"{owner}.{rowId}");
            if (!JsonPath.RequiredString(row, "id", owner)
                    .Equals(rowId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{owner}.rows[{rowIndex}] must have stable id '{rowId}'.");
            }
            JsonPath.RequiredString(row, "label", owner);
            JsonPath.RequiredString(row, "padding", owner);
            RequireOneOf(
                JsonPath.RequiredString(row, "verticalAlignment", owner),
                ["top", "center", "bottom"],
                $"{owner}.{rowId}.verticalAlignment");
            JsonPath.RequiredBoolean(row, "showSeparator", owner);
            for (var slot = 1; slot <= 5; slot++)
            {
                var prefix = $"slot{slot}";
                RequireOneOf(
                    JsonPath.RequiredString(row, $"{prefix}Kind", owner),
                    ["none", "avatar", "icon", "label"],
                    $"{owner}.{rowId}.{prefix}Kind");
                ValidateSlot(row, $"{prefix}AvatarSlot", owner);
                ValidateSlot(row, $"{prefix}IconSlot", owner);
                ValidateSlot(row, $"{prefix}LabelSlot", owner);
            }
        }
    }

    private static void ValidateMediaInputs(JsonObject inputs, string owner)
    {
        RequireExactKeys(
            inputs,
            [
                "mediaType",
                "mediaScale",
                "mediaOffset",
                "isPlaying",
                "currentTimeSeconds",
                "durationSeconds",
                "isFullScreen",
                "fullScreenTransition",
                "fullframeOrientation",
                "controlsElapsedMs",
                "motionElapsedMs",
            ],
            owner);
        RequireOneOf(
            JsonPath.RequiredString(inputs, "mediaType", owner),
            ["image", "video"],
            $"{owner}.mediaType");
        JsonPath.RequiredNumber(inputs, "mediaScale", owner);
        JsonPath.RequiredString(inputs, "mediaOffset", owner);
        JsonPath.RequiredBoolean(inputs, "isPlaying", owner);
        JsonPath.RequiredNumber(inputs, "currentTimeSeconds", owner);
        JsonPath.RequiredNumber(inputs, "durationSeconds", owner);
        JsonPath.RequiredBoolean(inputs, "isFullScreen", owner);
        JsonPath.RequiredBoolean(inputs, "fullScreenTransition", owner);
        RequireOneOf(
            JsonPath.RequiredString(inputs, "fullframeOrientation", owner),
            ["portrait", "landscape"],
            $"{owner}.fullframeOrientation");
        JsonPath.RequiredNumber(inputs, "controlsElapsedMs", owner);
        JsonPath.RequiredNumber(inputs, "motionElapsedMs", owner);
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
