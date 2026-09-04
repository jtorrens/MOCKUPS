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
            "useAppWallpaper", "showStatusBar", "showHeader", "showBackButton", "showCallTitle",
            "showParticipantCount", "showDuration", "showAddParticipant", "showSelfView",
            "showParticipantNames", "showParticipantStatus", "showControls", "showNavigationBar",
            "showCameraControl", "showMicrophoneControl", "showSpeakerControl", "showMoreControl",
            "showEndCallControl", "screenPadding", "participantGapToken", "headerHeight", "controlsHeight",
            "selfViewSize", "selfViewPlacement", "backgroundColorToken", "participantSlot", "titleLabelSlot",
            "metaLabelSlot", "backButtonSlot", "addButtonSlot", "cameraButtonSlot", "microphoneButtonSlot",
            "speakerButtonSlot", "moreButtonSlot", "endCallButtonSlot", "statusBarSlot", "navigationBarSlot"
        ], $"{context}.videoCall");
        foreach (var key in new[] { "useAppWallpaper", "showStatusBar", "showHeader", "showBackButton", "showCallTitle", "showParticipantCount", "showDuration", "showAddParticipant", "showSelfView", "showParticipantNames", "showParticipantStatus", "showControls", "showNavigationBar", "showCameraControl", "showMicrophoneControl", "showSpeakerControl", "showMoreControl", "showEndCallControl" })
            JsonPath.RequiredBoolean(owner, key, context);
        _ = RuntimeInputValueKindContract.ParseValue(ValueKind.ThemeTokenPair, JsonPath.RequiredString(owner, "screenPadding", context), $"{context}.videoCall.screenPadding");
        JsonPath.RequiredString(owner, "participantGapToken", context);
        JsonPath.RequiredString(owner, "backgroundColorToken", context);
        if (JsonPath.RequiredNumber(owner, "headerHeight", context) <= 0 || JsonPath.RequiredNumber(owner, "controlsHeight", context) <= 0)
            throw new InvalidOperationException($"{context} video call heights must be positive.");
        _ = RuntimeInputValueKindContract.ParseValue(ValueKind.IntegerPair, JsonPath.RequiredString(owner, "selfViewSize", context), $"{context}.videoCall.selfViewSize");
        _ = AlignmentPlacementValue.Parse(JsonPath.RequiredObject(owner, "selfViewPlacement", context).ToJsonString());
        foreach (var key in new[] { "participantSlot", "titleLabelSlot", "metaLabelSlot", "backButtonSlot", "addButtonSlot", "cameraButtonSlot", "microphoneButtonSlot", "speakerButtonSlot", "moreButtonSlot", "endCallButtonSlot", "statusBarSlot", "navigationBarSlot" })
            ComponentVariantSlotDocumentContract.Validate(JsonPath.RequiredObject(owner, key, context), $"{context}.videoCall.{key}");
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
