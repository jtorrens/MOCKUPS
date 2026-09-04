using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class CallParticipantComponentConfigContract
{
    public const string ComponentType = "callParticipant";

    public static void Validate(JsonObject config, string context)
    {
        RequireExactKeys(config, ["boundaryMotion", "callParticipant"], context);
        _ = MotionVariantValue.Parse(JsonPath.RequiredObject(config, "boundaryMotion", context).ToJsonString());
        var owner = JsonPath.RequiredObject(config, "callParticipant", context);
        RequireExactKeys(owner, [
            "showBackground", "showMedia", "showAvatarWhenVideoAbsent", "showStatusWhenVideoAbsent",
            "showName", "showMicrophoneStatus", "showConnectionStatus", "showActiveSpeakerIndicator",
            "defaultStatusText", "tilePadding", "avatarSize", "namePlacement", "statusPlacement",
            "microphonePlacement", "connectionPlacement", "microphoneOnIconToken", "microphoneMutedIconToken",
            "connectionWeakIconToken", "connectionLostIconToken", "activeSpeakerColorToken",
            "activeSpeakerBorderWidth", "surfaceSlot", "mediaSlot", "avatarSlot", "statusLabelSlot", "nameLabelSlot"
        ], $"{context}.callParticipant");
        foreach (var key in new[] { "showBackground", "showMedia", "showAvatarWhenVideoAbsent", "showStatusWhenVideoAbsent", "showName", "showMicrophoneStatus", "showConnectionStatus", "showActiveSpeakerIndicator" })
            JsonPath.RequiredBoolean(owner, key, context);
        JsonPath.RequiredString(owner, "defaultStatusText", context);
        _ = RuntimeInputValueKindContract.ParseValue(ValueKind.ThemeTokenPair, JsonPath.RequiredString(owner, "tilePadding", context), $"{context}.callParticipant.tilePadding");
        if (JsonPath.RequiredNumber(owner, "avatarSize", context) <= 0) throw new InvalidOperationException($"{context}.callParticipant.avatarSize must be positive.");
        if (JsonPath.RequiredNumber(owner, "activeSpeakerBorderWidth", context) < 0) throw new InvalidOperationException($"{context}.callParticipant.activeSpeakerBorderWidth cannot be negative.");
        foreach (var key in new[] { "namePlacement", "statusPlacement", "microphonePlacement", "connectionPlacement" })
            _ = AlignmentPlacementValue.Parse(JsonPath.RequiredObject(owner, key, context).ToJsonString());
        foreach (var key in new[] { "microphoneOnIconToken", "microphoneMutedIconToken", "connectionWeakIconToken", "connectionLostIconToken", "activeSpeakerColorToken" })
            JsonPath.RequiredString(owner, key, context);
        foreach (var key in new[] { "surfaceSlot", "mediaSlot", "avatarSlot", "statusLabelSlot", "nameLabelSlot" })
            ComponentVariantSlotDocumentContract.Validate(JsonPath.RequiredObject(owner, key, context), $"{context}.callParticipant.{key}");
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
