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
                "wallpaperEnabled",
                "showStatusBar",
                "showNavigationBar",
                "showTextInputBar",
                "showKeyboard",
                "stackSlot",
                "headerStackSlot",
                "headerStackInputs",
                "mediaSlot",
                "mediaInputs",
                "bubbleSlot",
                "footerIconBarSlot",
                "footerIconBarInputs",
                "textInputBarSlot",
                "textInputBarInputs",
                "keyboardSlot",
                "keyboardInputs",
                "statusBarSlot",
                "navigationBarSlot",
                "forwarding",
                "runtimeContract",
            ],
            owner);
        foreach (var key in new[]
        {
            "wallpaperEnabled",
            "showStatusBar",
            "showNavigationBar",
            "showTextInputBar",
            "showKeyboard",
        })
        {
            JsonPath.RequiredBoolean(socialPost, key, owner);
        }
        foreach (var key in new[]
        {
            "stackSlot",
            "headerStackSlot",
            "mediaSlot",
            "bubbleSlot",
            "footerIconBarSlot",
            "textInputBarSlot",
            "keyboardSlot",
            "statusBarSlot",
            "navigationBarSlot",
        })
        {
            ComponentVariantSlotDocumentContract.Validate(
                JsonPath.RequiredObject(socialPost, key, owner),
                $"{owner}.{key}");
        }
        var headerInputs = JsonPath.RequiredObject(
            socialPost,
            "headerStackInputs",
            owner);
        JsonPath.RequiredArray(headerInputs, "items", $"{owner}.headerStackInputs");
        foreach (var key in new[]
        {
            "mediaInputs",
            "footerIconBarInputs",
            "textInputBarInputs",
            "keyboardInputs",
        })
        {
            JsonPath.RequiredObject(socialPost, key, owner);
        }
        var forwarding = JsonPath.RequiredObject(socialPost, "forwarding", owner);
        RequireExactKeys(forwarding, ["headerActor"], $"{owner}.forwarding");
        var headerActor = JsonPath.RequiredObject(
            forwarding,
            "headerActor",
            $"{owner}.forwarding");
        var forwardingOwner = $"{owner}.forwarding.headerActor";
        RequireExactKeys(
            headerActor,
            [
                "sourceInputId",
                "sourceResolvedJsonKey",
                "targetItemId",
                "targetContentSetId",
                "targetContentId",
                "targetInputJsonKey",
                "targetResolvedJsonKey",
            ],
            forwardingOwner);
        foreach (var expected in new Dictionary<string, string>
        {
            ["sourceInputId"] = "actorId",
            ["sourceResolvedJsonKey"] = "actor",
            ["targetItemId"] = "social_header_primary",
            ["targetContentSetId"] = "set_a",
            ["targetContentId"] = "set_a_avatar",
            ["targetInputJsonKey"] = "actorId",
            ["targetResolvedJsonKey"] = "actor",
        })
        {
            RequireExactValue(
                JsonPath.RequiredString(headerActor, expected.Key, forwardingOwner),
                expected.Value,
                $"{forwardingOwner}.{expected.Key}");
        }
        var runtime = JsonPath.RequiredObject(socialPost, "runtimeContract", owner);
        var runtimeOwner = $"{owner}.runtimeContract";
        RequireExactKeys(
            runtime,
            ["mode", "componentType", "variantReference", "inputIds", "collectionIds"],
            runtimeOwner);
        RequireExactValue(
            JsonPath.RequiredString(runtime, "mode", runtimeOwner),
            "exact",
            $"{runtimeOwner}.mode");
        RequireExactValue(
            JsonPath.RequiredString(runtime, "componentType", runtimeOwner),
            "bubble",
            $"{runtimeOwner}.componentType");
        var runtimeVariant = JsonPath.RequiredString(runtime, "variantReference", runtimeOwner);
        var bubbleVariant = ComponentVariantSlotDocumentContract.VariantReference(
            JsonPath.RequiredObject(socialPost, "bubbleSlot", owner),
            $"{owner}.bubbleSlot");
        RequireExactValue(runtimeVariant, bubbleVariant, $"{runtimeOwner}.variantReference");
        RequireExactStringArray(
            JsonPath.RequiredArray(runtime, "inputIds", runtimeOwner),
            [
                "state",
                "sampleText",
                "maxWidth",
                "writeOnDurationFrames",
                "writeOnTrigger",
                "writeOnFrame",
                "actorId",
                "actorName",
                "statusText",
                "statusState",
                "mediaType",
                "mediaSource",
                "viewportSize",
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
            $"{runtimeOwner}.inputIds");
        RequireExactStringArray(
            JsonPath.RequiredArray(runtime, "collectionIds", runtimeOwner),
            [],
            $"{runtimeOwner}.collectionIds");
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

    private static void RequireExactValue(string value, string expected, string path)
    {
        if (!string.Equals(value, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{path} must be '{expected}'.");
        }
    }

    private static void RequireExactStringArray(
        JsonArray values,
        IReadOnlyList<string> expected,
        string path)
    {
        var actual = values.Select((value) =>
        {
            if (value is JsonValue jsonValue
                && jsonValue.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
            throw new InvalidOperationException($"{path} must contain only strings.");
        }).ToList();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"{path} must be exactly {string.Join(", ", expected)}.");
        }
    }
}
