using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class IncomingCallNotificationComponentConfigContract
{
    public const string ComponentType = "incomingCallNotification";

    public static void Validate(JsonObject config, string context)
    {
        RequireExactKeys(config, ["boundaryMotion", "incomingCallNotification"], context);
        _ = MotionVariantValue.Parse(
            JsonPath.RequiredObject(config, "boundaryMotion", context).ToJsonString());

        var owner = JsonPath.RequiredObject(
            config,
            "incomingCallNotification",
            context);
        RequireExactKeys(
            owner,
            [
                "layout",
                "size",
                "padding",
                "contentGapToken",
                "sectionGapToken",
                "childCount",
                "avatarSize",
                "surfaceSlot",
                "avatarSlot",
                "labelSlot",
                "iconRowSlot",
            ],
            $"{context}.incomingCallNotification");
        RequireOneOf(
            JsonPath.RequiredString(owner, "layout", context),
            ["compact", "stackedActions"],
            $"{context}.incomingCallNotification.layout");
        var size = RuntimeInputValueKindContract.ParseValue(
            ValueKind.IntegerPair,
            JsonPath.RequiredString(owner, "size", context),
            $"{context}.incomingCallNotification.size");
        _ = size;
        _ = RuntimeInputValueKindContract.ParseValue(
            ValueKind.ThemeTokenPair,
            JsonPath.RequiredString(owner, "padding", context),
            $"{context}.incomingCallNotification.padding");
        _ = JsonPath.RequiredString(owner, "contentGapToken", context);
        _ = JsonPath.RequiredString(owner, "sectionGapToken", context);
        var childCount = JsonPath.RequiredNumber(owner, "childCount", context);
        if (childCount != 1)
        {
            throw new InvalidOperationException(
                $"{context}.incomingCallNotification.childCount must be 1.");
        }
        if (JsonPath.RequiredNumber(owner, "avatarSize", context) <= 0)
        {
            throw new InvalidOperationException(
                $"{context}.incomingCallNotification.avatarSize must be positive.");
        }
        foreach (var key in new[] { "surfaceSlot", "avatarSlot", "labelSlot", "iconRowSlot" })
        {
            ComponentVariantSlotDocumentContract.Validate(
                JsonPath.RequiredObject(owner, key, context),
                $"{context}.incomingCallNotification.{key}");
        }
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
}
