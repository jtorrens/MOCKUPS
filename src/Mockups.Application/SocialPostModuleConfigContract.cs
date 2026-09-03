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
                "footerHeight",
                "footerSurfaceSlot",
                "footerRowGapToken",
                "footerRows",
                "mediaSlot",
                "mediaPadding",
                "showMedia",
                "mediaHeightMode",
                "mediaHeight",
                "mediaInputs",
                "showMediaSeparator",
                "gallerySlot",
                "messageMinHeight",
                "showMessage",
                "messageBubbleSlot",
                "messageTextInputBarSlot",
                "messageKeyboardSlot",
                "messagePadding",
                "messageBubbleInputs",
                "showMessageSeparator",
            ],
            owner);

        foreach (var key in new[]
        {
            "useAppWallpaper",
            "showHeader",
            "showStatusBar",
            "showNavigationBar",
            "showMedia",
            "showMediaSeparator",
            "showMessage",
            "showMessageSeparator",
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
        var footerHeight = JsonPath.RequiredNumber(socialPost, "footerHeight", owner);
        if (footerHeight < 0)
        {
            throw new InvalidOperationException(
                $"{owner}.footerHeight must be non-negative.");
        }
        ValidateSlot(socialPost, "footerSurfaceSlot", owner);
        JsonPath.RequiredString(socialPost, "footerRowGapToken", owner);
        ValidateSlot(socialPost, "mediaSlot", owner);
        JsonPath.RequiredString(socialPost, "mediaPadding", owner);
        var mediaHeightMode = JsonPath.RequiredString(socialPost, "mediaHeightMode", owner);
        if (mediaHeightMode is not ("fixed" or "fill"))
        {
            throw new InvalidOperationException(
                $"{owner}.mediaHeightMode must be 'fixed' or 'fill'.");
        }
        if (JsonPath.RequiredNumber(socialPost, "mediaHeight", owner) <= 0)
        {
            throw new InvalidOperationException(
                $"{owner}.mediaHeight must be positive.");
        }
        ValidateMediaInputs(
            JsonPath.RequiredObject(socialPost, "mediaInputs", owner),
            $"{owner}.mediaInputs");
        ValidateSlot(socialPost, "gallerySlot", owner);
        var messageMinHeight = JsonPath.RequiredNumber(
            socialPost,
            "messageMinHeight",
            owner);
        if (messageMinHeight <= 0)
        {
            throw new InvalidOperationException(
                $"{owner}.messageMinHeight must be positive.");
        }
        ValidateSlot(socialPost, "messageBubbleSlot", owner);
        ValidateSlot(socialPost, "messageTextInputBarSlot", owner);
        ValidateSlot(socialPost, "messageKeyboardSlot", owner);
        JsonPath.RequiredString(socialPost, "messagePadding", owner);
        ValidateMessageBubbleInputs(
            JsonPath.RequiredObject(socialPost, "messageBubbleInputs", owner),
            $"{owner}.messageBubbleInputs");

        ValidateRows(socialPost, "rows", owner);
        ValidateRows(socialPost, "footerRows", owner);
    }

    private static void ValidateRows(JsonObject socialPost, string key, string owner)
    {
        var rows = JsonPath.RequiredArray(socialPost, key, owner);
        var idPrefix = key.Equals("footerRows", StringComparison.Ordinal)
            ? "footerRow"
            : "row";
        if (rows.Count != 2)
        {
            throw new InvalidOperationException(
                $"{owner}.{key} must contain exactly {idPrefix}1 and {idPrefix}2.");
        }
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var rowId = $"{idPrefix}{rowIndex + 1}";
            var row = rows[rowIndex] as JsonObject
                ?? throw new InvalidOperationException(
                    $"{owner}.{key}[{rowIndex}] must be an object.");
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
                rowKeys.Add($"slot{slot}IconSizeToken");
                rowKeys.Add($"slot{slot}LabelSlot");
            }
            RequireExactKeys(row, rowKeys, $"{owner}.{key}.{rowId}");
            if (!JsonPath.RequiredString(row, "id", owner)
                    .Equals(rowId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{owner}.{key}[{rowIndex}] must have stable id '{rowId}'.");
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
                JsonPath.RequiredString(row, $"{prefix}IconSizeToken", owner);
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

    private static void ValidateMessageBubbleInputs(JsonObject inputs, string owner)
    {
        RequireExactKeys(
            inputs,
            [
                "state",
                "actorId",
                "actorName",
                "actor",
                "actorIdentityVisible",
                "mediaType",
                "mediaSource",
                "viewportSize",
                "mediaScale",
                "mediaOffset",
                "isPlaying",
                "currentTimeSeconds",
                "durationSeconds",
                "playbackMode",
                "isFullScreen",
                "fullScreenTransition",
                "fullframeOrientation",
                "controlsElapsedMs",
                "motionElapsedMs",
                "statusState",
                "statusText",
                "typingIndicator",
            ],
            owner);
        RequireOneOf(JsonPath.RequiredString(inputs, "state", owner),
            ["incoming", "system", "outgoing"], $"{owner}.state");
        JsonPath.RequiredString(inputs, "actorId", owner, allowEmpty: true);
        JsonPath.RequiredString(inputs, "actorName", owner, allowEmpty: true);
        JsonPath.RequiredObject(inputs, "actor", owner);
        JsonPath.RequiredBoolean(inputs, "actorIdentityVisible", owner);
        RequireOneOf(JsonPath.RequiredString(inputs, "mediaType", owner),
            ["none", "image", "video", "audio"], $"{owner}.mediaType");
        JsonPath.RequiredString(inputs, "mediaSource", owner, allowEmpty: true);
        JsonPath.RequiredString(inputs, "viewportSize", owner);
        JsonPath.RequiredNumber(inputs, "mediaScale", owner);
        JsonPath.RequiredString(inputs, "mediaOffset", owner);
        JsonPath.RequiredBoolean(inputs, "isPlaying", owner);
        JsonPath.RequiredNumber(inputs, "currentTimeSeconds", owner);
        JsonPath.RequiredNumber(inputs, "durationSeconds", owner);
        RequireOneOf(JsonPath.RequiredString(inputs, "playbackMode", owner),
            ["once", "loop"], $"{owner}.playbackMode");
        JsonPath.RequiredBoolean(inputs, "isFullScreen", owner);
        JsonPath.RequiredBoolean(inputs, "fullScreenTransition", owner);
        JsonPath.RequiredString(inputs, "fullframeOrientation", owner);
        JsonPath.RequiredNumber(inputs, "controlsElapsedMs", owner);
        JsonPath.RequiredNumber(inputs, "motionElapsedMs", owner);
        RequireOneOf(JsonPath.RequiredString(inputs, "statusState", owner),
            ["none", "sent", "delivered", "read"], $"{owner}.statusState");
        JsonPath.RequiredString(inputs, "statusText", owner, allowEmpty: true);
        JsonPath.RequiredBoolean(inputs, "typingIndicator", owner);
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
