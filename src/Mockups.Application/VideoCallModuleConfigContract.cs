using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class VideoCallModuleConfigContract
{
    public const string RecordClassId = "module.core.videoCall";

    public static void Validate(JsonObject config, string context)
    {
        RequireExactKeys(config, ["appearanceMode", "videoCall"], context);
        JsonPath.RequiredString(config, "appearanceMode", context);
        var owner = JsonPath.RequiredObject(config, "videoCall", context);
        RequireExactKeys(owner, [
            "useAppWallpaper", "backgroundColorToken",
            "showHeader", "headerLayoutMode", "headerFloatHorizontalPaddingToken", "headerFloatOffsetY", "headerHeight", "headerSurfaceSlot", "headerRowGapToken", "headerRows",
            "showFooter", "footerLayoutMode", "footerFloatHorizontalPaddingToken", "footerFloatOffsetY", "footerHeight", "footerSurfaceSlot", "footerRowGapToken", "footerRows",
            "showMainVideo", "mainParticipantSlot", "mainPadding",
            "showPip", "pipParticipantSlot", "pipSize", "pipPlacement", "pipPadding",
            "showGridParticipants", "gridParticipantSlot", "gridPadding", "gridGapToken", "gridHeightMode", "gridHeight", "gridRows",
            "showParticipantNames", "showParticipantStatus", "showStatusBar", "showNavigationBar",
            "statusBarSlot", "navigationBarSlot"
        ], $"{context}.videoCall");
        foreach (var key in new[] { "useAppWallpaper", "showHeader", "showFooter", "showMainVideo", "showPip", "showGridParticipants", "showParticipantNames", "showParticipantStatus", "showStatusBar", "showNavigationBar" })
            JsonPath.RequiredBoolean(owner, key, context);
        RequireOneOf(JsonPath.RequiredString(owner, "headerLayoutMode", context), ["stack", "float"], $"{context}.videoCall.headerLayoutMode");
        RequireOneOf(JsonPath.RequiredString(owner, "footerLayoutMode", context), ["stack", "float"], $"{context}.videoCall.footerLayoutMode");
        RequireOneOf(JsonPath.RequiredString(owner, "gridHeightMode", context), ["fixed", "fill"], $"{context}.videoCall.gridHeightMode");
        JsonPath.RequiredString(owner, "backgroundColorToken", context);
        foreach (var key in new[] { "headerFloatHorizontalPaddingToken", "footerFloatHorizontalPaddingToken", "headerRowGapToken", "footerRowGapToken", "gridGapToken" }) JsonPath.RequiredString(owner, key, context);
        foreach (var key in new[] { "headerFloatOffsetY", "footerFloatOffsetY" })
            if (JsonPath.RequiredNumber(owner, key, context) < 0)
                throw new InvalidOperationException($"{context}.videoCall.{key} must be non-negative.");
        foreach (var key in new[] { "mainPadding", "pipPadding", "gridPadding" })
            _ = RuntimeInputValueKindContract.ParseValue(ValueKind.ThemeTokenPair, JsonPath.RequiredString(owner, key, context), $"{context}.videoCall.{key}");
        _ = RuntimeInputValueKindContract.ParseValue(ValueKind.IntegerPair, JsonPath.RequiredString(owner, "pipSize", context), $"{context}.videoCall.pipSize");
        _ = AlignmentPlacementValue.Parse(JsonPath.RequiredObject(owner, "pipPlacement", context).ToJsonString());
        if (JsonPath.RequiredNumber(owner, "headerHeight", context) < 0 || JsonPath.RequiredNumber(owner, "footerHeight", context) < 0)
            throw new InvalidOperationException($"{context} video call section heights must be non-negative.");
        foreach (var key in new[] { "gridHeight", "gridRows" })
            if (JsonPath.RequiredNumber(owner, key, context) < 1)
                throw new InvalidOperationException($"{context}.videoCall.{key} must be positive.");
        foreach (var key in new[] { "headerSurfaceSlot", "footerSurfaceSlot", "mainParticipantSlot", "pipParticipantSlot", "gridParticipantSlot", "statusBarSlot", "navigationBarSlot" })
            ComponentVariantSlotDocumentContract.Validate(JsonPath.RequiredObject(owner, key, context), $"{context}.videoCall.{key}");
        SocialPostModuleConfigContract.ValidateRows(owner, "headerRows", $"{context}.videoCall");
        SocialPostModuleConfigContract.ValidateRows(owner, "footerRows", $"{context}.videoCall");
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

    private static void RequireOneOf(string value, IReadOnlyList<string> options, string owner)
    {
        if (!options.Contains(value, StringComparer.Ordinal))
            throw new InvalidOperationException($"{owner} must be one of: {string.Join(", ", options)}.");
    }
}
