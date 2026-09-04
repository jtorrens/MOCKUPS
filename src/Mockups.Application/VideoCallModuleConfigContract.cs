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
            "useAppWallpaper", "conversationType", "backgroundColorToken",
            "showHeader", "headerHeight", "headerSurfaceSlot", "headerRowGapToken", "headerRows",
            "showFooter", "footerHeight", "footerSurfaceSlot", "footerRowGapToken", "footerRows",
            "showMainVideo", "mainParticipantSlot", "mainSizeMode", "mainSize", "mainPlacement", "mainPadding",
            "showPip", "pipParticipantSlot", "pipSize", "pipPlacement", "pipPadding",
            "showGridParticipants", "gridParticipantSlot", "gridPadding", "gridGapToken", "gridColumns",
            "showParticipantNames", "showParticipantStatus", "showStatusBar", "showNavigationBar",
            "statusBarSlot", "navigationBarSlot"
        ], $"{context}.videoCall");
        foreach (var key in new[] { "useAppWallpaper", "showHeader", "showFooter", "showMainVideo", "showPip", "showGridParticipants", "showParticipantNames", "showParticipantStatus", "showStatusBar", "showNavigationBar" })
            JsonPath.RequiredBoolean(owner, key, context);
        RequireOneOf(JsonPath.RequiredString(owner, "conversationType", context), ["individual", "group"], $"{context}.videoCall.conversationType");
        RequireOneOf(JsonPath.RequiredString(owner, "mainSizeMode", context), ["fill", "fixed"], $"{context}.videoCall.mainSizeMode");
        JsonPath.RequiredString(owner, "backgroundColorToken", context);
        foreach (var key in new[] { "headerRowGapToken", "footerRowGapToken", "gridGapToken" }) JsonPath.RequiredString(owner, key, context);
        foreach (var key in new[] { "mainPadding", "pipPadding", "gridPadding" })
            _ = RuntimeInputValueKindContract.ParseValue(ValueKind.ThemeTokenPair, JsonPath.RequiredString(owner, key, context), $"{context}.videoCall.{key}");
        foreach (var key in new[] { "mainSize", "pipSize" })
            _ = RuntimeInputValueKindContract.ParseValue(ValueKind.IntegerPair, JsonPath.RequiredString(owner, key, context), $"{context}.videoCall.{key}");
        foreach (var key in new[] { "mainPlacement", "pipPlacement" })
            _ = AlignmentPlacementValue.Parse(JsonPath.RequiredObject(owner, key, context).ToJsonString());
        if (JsonPath.RequiredNumber(owner, "headerHeight", context) < 0 || JsonPath.RequiredNumber(owner, "footerHeight", context) < 0)
            throw new InvalidOperationException($"{context} video call section heights must be non-negative.");
        if (JsonPath.RequiredNumber(owner, "gridColumns", context) < 1)
            throw new InvalidOperationException($"{context}.videoCall.gridColumns must be positive.");
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
