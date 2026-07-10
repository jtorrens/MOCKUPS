using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static bool NormalizeComponentSpacingTokens(JsonObject config)
    {
        var changed = false;
        changed |= NormalizeSpacingPair(config, ["textInput", "barPadding"]);
        changed |= NormalizeSpacingPair(config, ["textInput", "textPadding"]);
        changed |= NormalizeSpacingToken(config, ["textInput", "iconGap"]);
        changed |= NormalizeSpacingToken(config, ["textInput", "textBoxInputs", "iconGap"]);
        changed |= NormalizeSpacingToken(config, ["textInput", "textBoxInputs", "iconRowGap"]);
        changed |= NormalizeSpacingToken(config, ["keyboard", "iconEdgePadding"]);
        changed |= NormalizeSpacingToken(config, ["iconRow", "gap"]);
        changed |= NormalizeSpacingToken(config, ["iconBar", "edgePadding"]);
        changed |= NormalizeSpacingToken(config, ["iconBar", "idleLeftIconRowInputs", "gap"]);
        changed |= NormalizeSpacingToken(config, ["iconBar", "idleCenterIconRowInputs", "gap"]);
        changed |= NormalizeSpacingToken(config, ["iconBar", "idleRightIconRowInputs", "gap"]);
        changed |= NormalizeSpacingToken(config, ["iconBar", "activeLeftIconRowInputs", "gap"]);
        changed |= NormalizeSpacingToken(config, ["iconBar", "activeCenterIconRowInputs", "gap"]);
        changed |= NormalizeSpacingToken(config, ["iconBar", "activeRightIconRowInputs", "gap"]);
        changed |= NormalizeSpacingToken(config, ["keyboard", "keyPadding"]);
        changed |= NormalizeSpacingToken(config, ["buttonIcon", "iconPadding"]);
        changed |= NormalizeSpacingPair(config, ["label", "padding"]);
        changed |= NormalizeSpacingPair(config, ["audio", "padding"]);
        changed |= NormalizeSpacingToken(config, ["audio", "playIconPadding"]);
        changed |= NormalizeSpacingToken(config, ["audio", "waveformGap"]);
        changed |= NormalizeSpacingToken(config, ["layout", "gap"]);
        changed |= NormalizeSpacingToken(config, ["layout", "sidePadding"]);

        foreach (var child in config.Select((pair) => pair.Value).OfType<JsonObject>())
        {
            changed |= NormalizeComponentSpacingTokens(child);
        }

        foreach (var child in config.Select((pair) => pair.Value).OfType<JsonArray>())
        {
            foreach (var item in child.OfType<JsonObject>())
            {
                changed |= NormalizeComponentSpacingTokens(item);
            }
        }

        return changed;
    }

    private static bool NormalizeComponentIconSizeTokens(JsonObject config)
    {
        var changed = false;
        changed |= NormalizeIconSizeToken(config, ["textInput", "textBoxInputs", "iconRowSize"]);
        changed |= NormalizeIconSizeToken(config, ["iconRow", "size"]);
        changed |= NormalizeIconSizeToken(config, ["iconBar", "idleLeftIconRowInputs", "size"]);
        changed |= NormalizeIconSizeToken(config, ["iconBar", "idleCenterIconRowInputs", "size"]);
        changed |= NormalizeIconSizeToken(config, ["iconBar", "idleRightIconRowInputs", "size"]);
        changed |= NormalizeIconSizeToken(config, ["iconBar", "activeLeftIconRowInputs", "size"]);
        changed |= NormalizeIconSizeToken(config, ["iconBar", "activeCenterIconRowInputs", "size"]);
        changed |= NormalizeIconSizeToken(config, ["iconBar", "activeRightIconRowInputs", "size"]);
        changed |= NormalizeIconSizeToken(config, ["bubble", "status", "sizeToken"]);
        changed |= NormalizeIconSizeToken(config, ["bubble", "status", "textSizeToken"]);
        if (JsonPath.Get(config, ["iconRow", "buttonIconSlot", "overrides", "buttonIcon"]) is JsonObject buttonIconOverrides
            && buttonIconOverrides.Remove("size"))
        {
            changed = true;
        }

        foreach (var child in config.Select((pair) => pair.Value).OfType<JsonObject>())
        {
            changed |= NormalizeComponentIconSizeTokens(child);
        }

        foreach (var child in config.Select((pair) => pair.Value).OfType<JsonArray>())
        {
            foreach (var item in child.OfType<JsonObject>())
            {
                changed |= NormalizeComponentIconSizeTokens(item);
            }
        }

        return changed;
    }

    private static bool NormalizeIconSizeToken(JsonObject config, string[] path)
    {
        var node = JsonPath.Get(config, path);
        if (node is null)
        {
            return false;
        }

        if (node is not JsonValue value)
        {
            return false;
        }

        if (value.TryGetValue<string>(out var text))
        {
            if (IsIconSizeToken(text))
            {
                return false;
            }

            if (!decimal.TryParse(
                    text.Replace(",", "."),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var textNumber))
            {
                return false;
            }

            JsonPath.Set(config, path, JsonValue.Create(ClosestIconSizeToken(textNumber))!);
            return true;
        }

        if (!value.TryGetValue<decimal>(out var number))
        {
            return false;
        }

        JsonPath.Set(config, path, JsonValue.Create(ClosestIconSizeToken(number))!);
        return true;
    }

    private static bool IsIconSizeToken(string value)
    {
        return value.StartsWith("theme.iconSizes.", StringComparison.Ordinal);
    }

    private static string ClosestIconSizeToken(decimal value)
    {
        (string Token, decimal Value)[] candidates =
        [
            ("theme.iconSizes.xs", 12m),
            ("theme.iconSizes.s", 16m),
            ("theme.iconSizes.m", 20m),
            ("theme.iconSizes.l", 24m),
            ("theme.iconSizes.xl", 32m),
        ];

        return candidates
            .OrderBy((candidate) => Math.Abs(candidate.Value - value))
            .First()
            .Token;
    }

    private static bool NormalizeComponentTypographyStyles(JsonObject config)
    {
        var changed = NormalizeLabelTypography(config);

        foreach (var child in config.Select((pair) => pair.Value).OfType<JsonObject>())
        {
            changed |= NormalizeComponentTypographyStyles(child);
        }

        foreach (var child in config.Select((pair) => pair.Value).OfType<JsonArray>())
        {
            foreach (var item in child.OfType<JsonObject>())
            {
                changed |= NormalizeComponentTypographyStyles(item);
            }
        }

        return changed;
    }

    private static bool NormalizeLabelTypography(JsonObject config)
    {
        if (JsonPath.Get(config, ["label"]) is not JsonObject label)
        {
            return false;
        }

        var changed = false;
        changed |= NormalizeTypographyObject(
            label,
            "textTypography",
            "textSizeToken",
            "textStyle");
        changed |= NormalizeTypographyObject(
            label,
            "subtextTypography",
            "subtextSizeToken",
            "subtextStyle");
        return changed;
    }

    private static bool NormalizeTypographyObject(
        JsonObject owner,
        string typographyKey,
        string legacySizeKey,
        string legacyStyleKey)
    {
        if (owner[typographyKey] is JsonObject existingTypography)
        {
            return NormalizeTypographyLineHeight(existingTypography);
        }

        var typography = new JsonObject();
        if (owner[legacySizeKey] is JsonValue sizeValue && sizeValue.TryGetValue<string>(out var sizeToken))
        {
            typography[TypographyStyleValue.SizeToken] = sizeToken;
        }

        if (owner[legacyStyleKey] is JsonValue styleValue && styleValue.TryGetValue<string>(out var style))
        {
            typography[TypographyStyleValue.Style] = style;
        }

        if (typography.Count == 0)
        {
            return false;
        }

        owner[typographyKey] = typography;
        return true;
    }

    private static bool NormalizeTypographyLineHeight(JsonObject typography)
    {
        if (typography[TypographyStyleValue.LineHeight] is not JsonValue lineHeightValue)
        {
            return false;
        }

        decimal? numericValue = null;
        if (lineHeightValue.TryGetValue<decimal>(out var number))
        {
            numericValue = number;
        }
        else if (lineHeightValue.TryGetValue<string>(out var text))
        {
            if (text.StartsWith("theme.typography.lineHeights.", StringComparison.Ordinal))
            {
                return false;
            }

            if (decimal.TryParse(text.Replace(",", ".", StringComparison.Ordinal), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                numericValue = parsed;
            }
        }

        if (numericValue is null)
        {
            return false;
        }

        typography[TypographyStyleValue.LineHeight] = ClosestLineHeightToken(numericValue.Value);
        return true;
    }

    private static string ClosestLineHeightToken(decimal value)
    {
        (string Token, decimal Value)[] candidates =
        [
            ("theme.typography.lineHeights.tight", 1m),
            ("theme.typography.lineHeights.compact", 1.1m),
            ("theme.typography.lineHeights.normal", 1.2m),
            ("theme.typography.lineHeights.relaxed", 1.35m),
            ("theme.typography.lineHeights.loose", 1.5m),
        ];

        return candidates
            .OrderBy((candidate) => Math.Abs(candidate.Value - value))
            .First()
            .Token;
    }

    private static bool NormalizeSpacingToken(JsonObject config, string[] path)
    {
        var node = JsonPath.Get(config, path);
        if (node is null)
        {
            return false;
        }

        if (!TrySpacingToken(node, out var token) || SpacingNodeIsToken(node, token))
        {
            return false;
        }

        JsonPath.Set(config, path, JsonValue.Create(token)!);
        return true;
    }

    private static bool NormalizeSpacingPair(JsonObject config, string[] path)
    {
        var node = JsonPath.Get(config, path);
        if (node is null)
        {
            return false;
        }

        if (node is not JsonValue value || !value.TryGetValue<string>(out var pairText))
        {
            return false;
        }

        var parts = pairText.Split('|', 2);
        var first = NormalizeSpacingPart(parts.ElementAtOrDefault(0) ?? "");
        var second = NormalizeSpacingPart(parts.ElementAtOrDefault(1) ?? "");
        var next = DictionaryFieldPairText.Join(first, second);
        if (next.Equals(pairText, StringComparison.Ordinal))
        {
            return false;
        }

        JsonPath.Set(config, path, JsonValue.Create(next)!);
        return true;
    }

    private static string NormalizeSpacingPart(string value)
    {
        return value.StartsWith("theme.spacing.", StringComparison.Ordinal)
            ? value
            : double.TryParse(value.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? ClosestSpacingToken(number)
                : value;
    }

    private static bool TrySpacingToken(JsonNode node, out string token)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
            {
                if (text.StartsWith("theme.spacing.", StringComparison.Ordinal))
                {
                    token = text;
                    return true;
                }

                if (double.TryParse(text.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    token = ClosestSpacingToken(parsed);
                    return true;
                }
            }

            if (value.TryGetValue<double>(out var number))
            {
                token = ClosestSpacingToken(number);
                return true;
            }
        }

        token = "";
        return false;
    }

    private static bool SpacingNodeIsToken(JsonNode node, string token)
    {
        return node is JsonValue value
            && value.TryGetValue<string>(out var text)
            && text.Equals(token, StringComparison.Ordinal);
    }

    private static string ClosestSpacingToken(double value)
    {
        var tokens = new (string Token, double Value)[]
        {
            ("theme.spacing.none", 0),
            ("theme.spacing.xs", 2),
            ("theme.spacing.s", 4),
            ("theme.spacing.m", 8),
            ("theme.spacing.l", 12),
            ("theme.spacing.xl", 16),
            ("theme.spacing.xxl", 24),
        };
        return tokens
            .OrderBy((token) => Math.Abs(token.Value - value))
            .ThenByDescending((token) => token.Value)
            .First()
            .Token;
    }

    private static bool NormalizeAvatarLabelPlacement(string componentType, JsonObject config)
    {
        if (componentType != "avatar")
        {
            return false;
        }

        var labelSlot = JsonPath.Get(config, ["avatar", "labelSlot"]) as JsonObject;
        if (labelSlot is null || labelSlot["placement"] is not null)
        {
            return false;
        }

        var position = JsonPath.String(labelSlot, "position", "bottom");
        var gap = JsonPath.Number(labelSlot, "gap", 4);
        labelSlot["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromDirectionalEdge(position, gap).ToJsonString());
        if (labelSlot["presetId"] is null)
        {
            labelSlot["presetId"] = DefaultComponentPresetId;
        }

        return true;
    }

    private static bool NormalizeButtonIconLabelSlot(string componentType, JsonObject config)
    {
        if (componentType != "buttonIcon")
        {
            return false;
        }

        var buttonIcon = JsonPath.Get(config, ["buttonIcon"]) as JsonObject;
        if (buttonIcon is null || buttonIcon["labelSlot"] is not null)
        {
            return false;
        }

        var labelEnabled = JsonBool(buttonIcon, ["labelEnabled"]);
        var labelPosition = JsonPath.String(buttonIcon, "labelPosition", "bottom");
        var labelPadding = JsonPath.Number(buttonIcon, "labelPadding", 3);
        buttonIcon["labelSlot"] = new JsonObject
        {
            ["showLabel"] = labelEnabled,
            ["showSubtext"] = false,
            ["presetId"] = DefaultComponentPresetId,
                ["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromDirectionalEdge(labelPosition, labelPadding).ToJsonString()),
            ["overrides"] = new JsonObject(),
        };
        return true;
    }

    private static bool NormalizeButtonIconSizing(string componentType, JsonObject config)
    {
        if (componentType != "buttonIcon")
        {
            return false;
        }

        var buttonIcon = JsonPath.Get(config, ["buttonIcon"]) as JsonObject;
        if (buttonIcon is null)
        {
            return false;
        }

        var changed = false;
        if (string.IsNullOrWhiteSpace(JsonPath.String(buttonIcon, "sizeMode", "")))
        {
            buttonIcon["sizeMode"] = "fixed";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(JsonPath.String(buttonIcon, "iconSizeToken", "")))
        {
            buttonIcon["iconSizeToken"] = "theme.iconSizes.xl";
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeAudioEmbeddedSlots(string componentType, JsonObject config)
    {
        if (componentType != "audio")
        {
            return false;
        }

        var audio = JsonPath.Get(config, ["audio"]) as JsonObject;
        if (audio is null)
        {
            return false;
        }

        var changed = false;
        if (audio["padding"] is null)
        {
            audio["padding"] = "theme.spacing.l|theme.spacing.m";
            changed = true;
        }

        if (audio["playIconPadding"] is null)
        {
            audio["playIconPadding"] = "theme.spacing.m";
            changed = true;
        }

        changed |= NormalizeNumber(audio, "playCircleSize", 32, minimum: 8);
        changed |= NormalizeNumber(audio, "textSize", 11, minimum: 6);
        changed |= NormalizeNumber(audio, "waveformBarCount", 28, minimum: 4);
        if (audio["waveformGap"] is null)
        {
            audio["waveformGap"] = "theme.spacing.xs";
            changed = true;
        }
        changed |= NormalizeNumber(audio, "waveformMinHeight", 4, minimum: 1);
        changed |= NormalizeNumber(audio, "waveformMaxHeight", 22, minimum: 2);
        changed |= NormalizeNumber(audio, "progressKnobSize", 9, minimum: 4);

        if (audio["waveformBarWidth"] is null)
        {
            audio["waveformBarWidth"] = 3;
            changed = true;
        }
        else
        {
            changed |= NormalizeNumber(audio, "waveformBarWidth", 3, minimum: 1);
        }

        if (audio["progressKnobSize"] is null && audio["knobSize"] is not null)
        {
            audio["progressKnobSize"] = audio["knobSize"]?.DeepClone();
            changed = true;
        }

        if (audio["avatarSlot"] is null)
        {
            var avatarPosition = JsonPath.String(audio, "avatarPosition", "right");
            var avatarSize = JsonPath.Get(audio, ["avatarSize"]);
            var avatarOverrides = new JsonObject();
            if (avatarSize is not null)
            {
                JsonPath.Set(avatarOverrides, ["avatar", "defaultSize"], avatarSize.DeepClone());
            }

            audio["avatarSlot"] = new JsonObject
            {
                ["showAvatar"] = true,
                ["presetId"] = DefaultComponentPresetId,
                ["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromDirectionalEdge(avatarPosition, 4).ToJsonString()),
                ["overrides"] = avatarOverrides,
            };
            changed = true;
        }

        var badgeSlot = audio["badgeSlot"] as JsonObject;
        if (badgeSlot is null)
        {
            badgeSlot = new JsonObject
            {
                ["showBadge"] = false,
                ["iconToken"] = "media_mic",
                ["backgroundColor"] = "blue",
                ["iconColor"] = "gray_100",
                ["presetId"] = DefaultComponentPresetId,
                ["placement"] = JsonNode.Parse("""{"mode":"center","alignX":1,"alignY":1,"offsetX":0,"offsetY":0}"""),
                ["overrides"] = new JsonObject
                {
                    ["buttonIcon"] = new JsonObject
                    {
                        ["size"] = 16,
                        ["iconPadding"] = "theme.spacing.s",
                    },
                },
            };
            audio["badgeSlot"] = badgeSlot;
            changed = true;
        }

        if (badgeSlot["backgroundColor"] is null)
        {
            badgeSlot["backgroundColor"] = "blue";
            changed = true;
        }

        if (badgeSlot["iconToken"] is null)
        {
            badgeSlot["iconToken"] = "media_mic";
            changed = true;
        }

        if (badgeSlot["iconColor"] is null)
        {
            badgeSlot["iconColor"] = "gray_100";
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeEmbeddedSlotPresetIds(SqliteConnection connection, string projectId, JsonObject config)
    {
        var changed = false;
        foreach (var slot in EmbeddedComponentSlotCatalog.All())
        {
            if (JsonPath.Get(config, slot.SlotPath) is not JsonObject slotNode)
            {
                continue;
            }

            var currentValue = JsonPath.String(slotNode, "presetId", "");
            var normalizedValue = NormalizeComponentPresetReference(
                connection,
                projectId,
                slot.EmbeddedComponentType,
                currentValue);
            if (currentValue.Equals(normalizedValue, StringComparison.Ordinal))
            {
                continue;
            }

            slotNode["presetId"] = normalizedValue;
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeComponentSlots(string componentType, JsonObject config)
    {
        var (ownerPath, preferredPresetName) = componentType switch
        {
            "label" => (new[] { "label" }, "Label"),
            "buttonIcon" => (new[] { "buttonIcon" }, "IconButton"),
            "textBox" => (new[] { "textBox" }, "InputBox"),
            "audio" => (new[] { "audio" }, DefaultComponentPresetId),
            "media" => (new[] { "media" }, DefaultComponentPresetId),
            _ => (Array.Empty<string>(), ""),
        };
        if (ownerPath.Length == 0 || JsonPath.Get(config, ownerPath) is not JsonObject owner)
        {
            return false;
        }

        var changed = false;
        if (owner["surfaceSlot"] is not JsonObject surfaceSlot)
        {
            owner["surfaceSlot"] = ComponentSurfaceSlot(preferredPresetName);
            return true;
        }

        if (string.IsNullOrWhiteSpace(JsonPath.String(surfaceSlot, "presetId", "")))
        {
            surfaceSlot["presetId"] = preferredPresetName;
            changed = true;
        }

        if (surfaceSlot["overrides"] is not JsonObject)
        {
            surfaceSlot["overrides"] = new JsonObject();
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeTextInputBarSlots(
        SqliteConnection connection,
        string projectId,
        string componentType,
        JsonObject config)
    {
        if (componentType != "textInputBar"
            || JsonPath.Get(config, ["textInput"]) is not JsonObject textInput)
        {
            return false;
        }

        var changed = false;
        changed |= NormalizeComponentSlot(textInput, "barSurfaceSlot", DefaultComponentPresetId);
        changed |= NormalizeComponentSlot(textInput, "textBoxSlot", DefaultComponentPresetId);
        changed |= NormalizeComponentSlot(textInput, "iconBarSlot", DefaultComponentPresetId);
        changed |= RemoveJsonProperties(
            textInput,
            "iconButtonPresetId",
            "leftIconRowSlot",
            "leftIconRowInputs",
            "rightIconRowSlot",
            "rightIconRowInputs",
            "idleLeftIconRowSlot",
            "idleLeftIconRowInputs",
            "idleRightIconRowSlot",
            "idleRightIconRowInputs",
            "typingLeftIconRowSlot",
            "typingLeftIconRowInputs",
            "typingRightIconRowSlot",
            "typingRightIconRowInputs");
        if (textInput["textBoxInputs"] is not JsonObject)
        {
            textInput["textBoxInputs"] = TextBoxInputBindings();
            changed = true;
        }
        else if (textInput["textBoxInputs"] is JsonObject textBoxInputs)
        {
            changed |= JsonPath.MergeMissing(textBoxInputs, TextBoxInputBindings());
            if (string.IsNullOrWhiteSpace(JsonPath.String(textInput, ["textBoxInputs", "placeholder"])))
            {
                SetJsonValue(textInput, ["textBoxInputs", "placeholder"], JsonValue.Create("Message")!);
                changed = true;
            }

            if (JsonPath.Get(textInput, ["textBoxInputs", "maxLines"]) is null)
            {
                SetJsonValue(textInput, ["textBoxInputs", "maxLines"], JsonValue.Create(4)!);
                changed = true;
            }
        }
        changed |= NormalizeComponentSlot(textInput, "surfaceSlot", "InputBox");
        changed |= NormalizeComponentPresetString(connection, projectId, textInput, ["iconBarSlot", "presetId"], "iconBar");
        return changed;
    }

    private static bool NormalizeKeyboardSlots(
        SqliteConnection connection,
        string projectId,
        string componentType,
        JsonObject config)
    {
        if (componentType != "keyboard"
            || JsonPath.Get(config, ["keyboard"]) is not JsonObject keyboard)
        {
            return false;
        }

        var changed = false;
        if (config.Remove("textInput"))
        {
            changed = true;
        }

        changed |= NormalizeComponentSlot(keyboard, "iconBarSlot", DefaultComponentPresetId);
        if (JsonPath.Get(keyboard, ["iconEdgePadding"]) is null)
        {
            keyboard["iconEdgePadding"] = "theme.spacing.none";
            changed = true;
        }
        changed |= RemoveJsonProperties(
            keyboard,
            "iconRowsEdgePadding",
            "iconButtonSlot",
            "iconButtonPresetId",
            "leftIconRowSlot",
            "leftIconRowInputs",
            "centerIconRowSlot",
            "centerIconRowInputs",
            "rightIconRowSlot",
            "rightIconRowInputs",
            "bottomIconSlots",
            "bottomIconColorToken");
        if (keyboard["motion"] is not JsonObject)
        {
            keyboard["motion"] = JsonNode.Parse(MotionVariantValue.Default.ToJsonString());
            changed = true;
        }
        else if (keyboard["motion"] is JsonObject motion)
        {
            if (motion["translate"] is null)
            {
                motion["translate"] = true;
                changed = true;
            }
            if (motion["scale"] is null)
            {
                motion["scale"] = false;
                changed = true;
            }
        }

        changed |= NormalizeComponentPresetString(connection, projectId, keyboard, ["iconBarSlot", "presetId"], "iconBar");

        if (keyboard.Remove("keyCornerRadius"))
        {
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeMediaSlots(
        SqliteConnection connection,
        string projectId,
        string componentType,
        JsonObject config)
    {
        if (componentType != "media"
            || JsonPath.Get(config, ["media"]) is not JsonObject media)
        {
            return false;
        }

        var changed = false;
        changed |= NormalizeComponentSlot(media, "surfaceSlot", DefaultComponentPresetId);
        if (media["iconColorTokenOverride"] is null)
        {
            media["iconColorTokenOverride"] = "theme.icons.alternate";
            changed = true;
        }
        foreach (var key in MediaIconBarSlotKeys)
        {
            changed |= NormalizeComponentSlot(media, key, DefaultComponentPresetId);
            changed |= NormalizeComponentPresetString(connection, projectId, media, [key, "presetId"], "iconBar");
        }
        if (media["motion"] is not JsonObject)
        {
            media["motion"] = JsonNode.Parse(MediaMotionDefault().ToJsonString());
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeBubbleSlots(
        SqliteConnection connection,
        string projectId,
        string componentType,
        JsonObject config)
    {
        if (componentType != "bubble"
            || JsonPath.Get(config, ["bubble"]) is not JsonObject bubble)
        {
            return false;
        }

        var changed = false;
        changed |= NormalizeComponentSlot(bubble, "surfaceSlot", DefaultComponentPresetId);
        changed |= NormalizeComponentSlot(bubble, "textBoxSlot", DefaultComponentPresetId);
        changed |= NormalizeComponentSlot(bubble, "imageMediaSlot", DefaultComponentPresetId);
        changed |= NormalizeComponentSlot(bubble, "videoMediaSlot", DefaultComponentPresetId);
        changed |= NormalizeComponentSlot(bubble, "audioSlot", DefaultComponentPresetId);
        changed |= NormalizeComponentSlot(bubble, "actorLabelSlot", DefaultComponentPresetId);
        changed |= NormalizeComponentSlot(bubble, "avatarSlot", DefaultComponentPresetId);
        changed |= NormalizeComponentPresetString(connection, projectId, bubble, ["surfaceSlot", "presetId"], "surface");
        changed |= NormalizeComponentPresetString(connection, projectId, bubble, ["textBoxSlot", "presetId"], "textBox");
        changed |= NormalizeComponentPresetString(connection, projectId, bubble, ["imageMediaSlot", "presetId"], "media");
        changed |= NormalizeComponentPresetString(connection, projectId, bubble, ["videoMediaSlot", "presetId"], "media");
        changed |= NormalizeComponentPresetString(connection, projectId, bubble, ["audioSlot", "presetId"], "audio");
        changed |= NormalizeComponentPresetString(connection, projectId, bubble, ["actorLabelSlot", "presetId"], "label");
        changed |= NormalizeComponentPresetString(connection, projectId, bubble, ["avatarSlot", "presetId"], "avatar");
        changed |= EnsureStringValue(bubble, ["mediaType"], "none");
        changed |= EnsureStringValue(bubble, ["mediaPosition"], "bottom");
        if (JsonPath.Get(bubble, ["actorLabelSlot", "showLabel"]) is null)
        {
            SetJsonValue(bubble, ["actorLabelSlot", "showLabel"], JsonValue.Create(false)!);
            changed = true;
        }
        if (JsonPath.Get(bubble, ["actorLabelSlot", "useActorColor"]) is null)
        {
            SetJsonValue(bubble, ["actorLabelSlot", "useActorColor"], JsonValue.Create(false)!);
            changed = true;
        }
        if (JsonPath.Get(bubble, ["actorLabelSlot", "placement"]) is null)
        {
            SetJsonValue(
                bubble,
                ["actorLabelSlot", "placement"],
                JsonNode.Parse(AlignmentPlacementValue.FromDirectionalEdge("top", -4).ToJsonString())!);
            changed = true;
        }
        if (JsonPath.Get(bubble, ["avatarSlot", "showAvatar"]) is null)
        {
            SetJsonValue(bubble, ["avatarSlot", "showAvatar"], JsonValue.Create(false)!);
            changed = true;
        }
        if (JsonPath.Get(bubble, ["avatarSlot", "placement"]) is null)
        {
            SetJsonValue(
                bubble,
                ["avatarSlot", "placement"],
                JsonNode.Parse(AlignmentPlacementValue.FromDirectionalEdge("left", 8).ToJsonString())!);
            changed = true;
        }
        changed |= EnsureStringValue(bubble, ["status", "sizeToken"], "theme.iconSizes.s");
        changed |= EnsureStringValue(bubble, ["status", "textSizeToken"], "theme.iconSizes.s");
        if (JsonPath.Get(bubble, ["status", "sent", "iconToken"]) is null)
        {
            SetJsonValue(bubble, ["status", "sent", "iconToken"], JsonValue.Create("system_check")!);
            changed = true;
        }
        changed |= EnsureStringValue(bubble, ["status", "sent", "colorToken"], "theme.icons.secondary");
        if (JsonPath.Get(bubble, ["status", "delivered", "iconToken"]) is null)
        {
            SetJsonValue(bubble, ["status", "delivered", "iconToken"], JsonValue.Create("system_check")!);
            changed = true;
        }
        changed |= EnsureStringValue(bubble, ["status", "delivered", "colorToken"], "theme.icons.secondary");
        if (JsonPath.Get(bubble, ["status", "read", "iconToken"]) is null)
        {
            SetJsonValue(bubble, ["status", "read", "iconToken"], JsonValue.Create("system_check")!);
            changed = true;
        }
        changed |= EnsureStringValue(bubble, ["status", "read", "colorToken"], "theme.icons.accent");
        changed |= EnsureStringValue(bubble, ["incomingBackground"], "gray_080|gray_020");
        changed |= EnsureStringValue(bubble, ["incomingText"], "gray_010|gray_100");
        changed |= EnsureStringValue(bubble, ["systemBackground"], "gray_080|gray_020");
        changed |= EnsureStringValue(bubble, ["systemText"], "gray_010|gray_100");
        changed |= EnsureStringValue(bubble, ["outgoingBackground"], "aqua_green|aqua_green");
        changed |= EnsureStringValue(bubble, ["outgoingText"], "gray_100|gray_100");
        changed |= EnsureStringValue(bubble, ["padding"], "theme.spacing.l|theme.spacing.m");

        return changed;
    }

    private static readonly string[] MediaIconBarSlotKeys =
    [
        "inlineTopIconBarSlot",
        "inlineCenterIconBarSlot",
        "inlineBottomIconBarSlot",
        "fullScreenTopIconBarSlot",
        "fullScreenCenterIconBarSlot",
        "fullScreenBottomIconBarSlot",
    ];

    private static bool NormalizeComponentSlot(JsonObject owner, string key, string preferredPresetName)
    {
        var changed = false;
        if (owner[key] is not JsonObject slot)
        {
            owner[key] = ComponentSurfaceSlot(preferredPresetName);
            return true;
        }

        if (string.IsNullOrWhiteSpace(JsonPath.String(slot, "presetId", "")))
        {
            slot["presetId"] = preferredPresetName;
            changed = true;
        }

        if (slot["overrides"] is not JsonObject)
        {
            slot["overrides"] = new JsonObject();
            changed = true;
        }

        return changed;
    }

    private static bool RemoveJsonProperties(JsonObject owner, params string[] keys)
    {
        var changed = false;
        foreach (var key in keys)
        {
            if (owner.Remove(key))
            {
                changed = true;
            }
        }

        return changed;
    }

    private static bool NormalizeComponentPresetString(
        SqliteConnection connection,
        string projectId,
        JsonObject owner,
        string[] path,
        string componentType)
    {
        var currentValue = JsonPath.String(owner, path);
        var normalizedValue = NormalizeComponentPresetReference(connection, projectId, componentType, currentValue);
        if (string.IsNullOrWhiteSpace(normalizedValue) || normalizedValue.Equals(currentValue, StringComparison.Ordinal))
        {
            return false;
        }

        SetJsonValue(owner, path, JsonValue.Create(normalizedValue)!);
        return true;
    }

    private static bool EnsureStringValue(JsonObject owner, string[] path, string replacement)
    {
        if (JsonPath.String(owner, path) is { Length: > 0 })
        {
            return false;
        }

        SetJsonValue(owner, path, JsonValue.Create(replacement)!);
        return true;
    }

    private static bool NormalizeNumber(
        JsonObject owner,
        string property,
        double replacement,
        double minimum)
    {
        var value = owner[property];
        if (value is not JsonValue jsonValue
            || !jsonValue.TryGetValue<double>(out var number)
            || !double.IsFinite(number)
            || number < minimum)
        {
            owner[property] = replacement;
            return true;
        }

        return false;
    }

    private static bool NormalizeReliefIntensity(JsonObject config, string key)
    {
        var style = JsonPath.Get(config, ["style"]) as JsonObject;
        if (style is null)
        {
            return false;
        }

        var value = JsonPath.Number(style, key, 0);
        if (Math.Abs(value) <= 1)
        {
            return false;
        }

        style[key] = JsonValue.Create(Math.Clamp(value / 100, -1, 1));
        return true;
    }

    private static bool EnsureComponentDesignPreviewText(string componentType, JsonObject designPreview)
    {
        if (componentType != "avatar")
        {
            return false;
        }

        if (JsonPath.String(designPreview, "sampleSubtext", "").Trim().Length > 0)
        {
            return false;
        }

        designPreview["sampleSubtext"] = "Subtitle";
        return true;
    }

    private static bool EnsureComponentInputs(
        string componentType,
        JsonObject designPreview,
        JsonObject designPreviewDefaults)
    {
        if (designPreviewDefaults["inputs"] is not JsonArray defaultInputs)
        {
            return false;
        }

        if (componentType is "textBox" or "textInputBar")
        {
            var defaultJson = defaultInputs.ToJsonString();
            if (designPreview["inputs"]?.ToJsonString() != defaultJson)
            {
                designPreview["inputs"] = JsonNode.Parse(defaultJson);
                return true;
            }

            return false;
        }

        if (designPreview["inputs"] is not JsonArray inputs)
        {
            designPreview["inputs"] = JsonNode.Parse(defaultInputs.ToJsonString());
            return true;
        }

        var existingIds = inputs
            .OfType<JsonObject>()
            .Select((input) => JsonPath.String(input, "id", ""))
            .Where((id) => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var changed = false;
        foreach (var defaultInput in defaultInputs.OfType<JsonObject>())
        {
            var id = JsonPath.String(defaultInput, "id", "");
            if (string.IsNullOrWhiteSpace(id) || existingIds.Contains(id))
            {
                continue;
            }

            inputs.Add(JsonNode.Parse(defaultInput.ToJsonString()));
            existingIds.Add(id);
            changed = true;
        }

        changed |= NormalizeComponentInputDefinitions(componentType, inputs, defaultInputs);
        if (componentType is "keyboard" or "media" or "bubble")
        {
            changed |= RemoveUnknownComponentInputs(inputs, defaultInputs);
        }
        return changed;
    }

    private static bool NormalizeBubbleWriteOnFrameInputs(string componentType, JsonObject designPreview)
    {
        if (componentType != "bubble")
        {
            return false;
        }

        var changed = false;
        if (designPreview["writeOnDurationFrames"] is null)
        {
            var seconds = JsonPath.Number(designPreview, "writeOnDurationSeconds", 0);
            designPreview["writeOnDurationFrames"] = seconds > 0
                ? Math.Max(1, (int)Math.Round(seconds * 25, MidpointRounding.AwayFromZero))
                : 30;
            changed = true;
        }

        if (designPreview["writeOnFrame"] is null)
        {
            var seconds = JsonPath.Number(designPreview, "writeOnTimeSeconds", 0);
            designPreview["writeOnFrame"] = seconds > 0
                ? Math.Max(0, (int)Math.Round(seconds * 25, MidpointRounding.AwayFromZero))
                : 0;
            changed = true;
        }

        if (designPreview.Remove("writeOnDurationSeconds"))
        {
            changed = true;
        }

        if (designPreview.Remove("writeOnTimeSeconds"))
        {
            changed = true;
        }

        return changed;
    }

    private static bool RemoveUnknownComponentInputs(JsonArray inputs, JsonArray defaultInputs)
    {
        var defaultIds = defaultInputs
            .OfType<JsonObject>()
            .Select((input) => JsonPath.String(input, "id", ""))
            .Where((id) => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var changed = false;
        for (var index = inputs.Count - 1; index >= 0; index--)
        {
            if (inputs[index] is not JsonObject input)
            {
                continue;
            }

            var id = JsonPath.String(input, "id", "");
            if (string.IsNullOrWhiteSpace(id) || defaultIds.Contains(id))
            {
                continue;
            }

            inputs.RemoveAt(index);
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeComponentInputDefinitions(
        string componentType,
        JsonArray inputs,
        JsonArray defaultInputs)
    {
        var changed = false;
        var defaultsById = defaultInputs
            .OfType<JsonObject>()
            .Select((input) => (Id: JsonPath.String(input, "id", ""), Input: input))
            .Where((input) => !string.IsNullOrWhiteSpace(input.Id))
            .ToDictionary((input) => input.Id, (input) => input.Input, StringComparer.Ordinal);

        foreach (var input in inputs.OfType<JsonObject>())
        {
            var id = JsonPath.String(input, "id", "");
            if (string.IsNullOrWhiteSpace(id)
                || !defaultsById.TryGetValue(id, out var defaultInput))
            {
                continue;
            }

            changed |= SyncComponentInputDefinition(input, defaultInput, "label");
            changed |= SyncComponentInputDefinition(input, defaultInput, "jsonKey");
            changed |= SyncComponentInputDefinition(input, defaultInput, "kind");
            changed |= SyncComponentInputDefinition(input, defaultInput, "minimum");
            changed |= SyncComponentInputDefinition(input, defaultInput, "maximum");
            changed |= SyncComponentInputDefinition(input, defaultInput, "increment");
            changed |= SyncComponentInputDefinition(input, defaultInput, "tableId");
            changed |= SyncComponentInputDefinition(input, defaultInput, "resolvedJsonKey");
            changed |= SyncComponentInputDefinition(input, defaultInput, "componentType");
            changed |= SyncComponentInputDefinition(input, defaultInput, "pairFirstLabel");
            changed |= SyncComponentInputDefinition(input, defaultInput, "pairSecondLabel");
            changed |= SyncComponentInputDefinition(input, defaultInput, "visibleWhenPath");
            changed |= SyncComponentInputDefinition(input, defaultInput, "visibleWhenValue");
            changed |= SyncComponentInputDefinition(input, defaultInput, "source");
            changed |= SyncComponentInputDefinition(input, defaultInput, "uiOrigin");
            changed |= SyncComponentInputDefinition(input, defaultInput, "uiGroupId");
            changed |= SyncComponentInputDefinition(input, defaultInput, "uiGroupLabel");
            changed |= SyncComponentInputDefinition(input, defaultInput, "uiParentGroupId");
            changed |= SyncComponentInputDefinition(input, defaultInput, "options");
        }

        return changed;
    }

    private static bool SyncComponentInputDefinition(JsonObject input, JsonObject defaultInput, string key)
    {
        var next = defaultInput[key];
        var current = input[key];
        if (next is null)
        {
            if (current is null)
            {
                return false;
            }

            input.Remove(key);
            return true;
        }

        var nextJson = next.ToJsonString();
        if (current?.ToJsonString() == nextJson)
        {
            return false;
        }

        input[key] = JsonNode.Parse(nextJson);
        return true;
    }

    private static bool EnsureButtonIconPreviewSize(string componentType, JsonObject designPreview)
    {
        if (componentType != "buttonIcon")
        {
            return false;
        }

        var sampleSize = JsonPath.Number(designPreview, "sampleSize", 0);
        if (sampleSize > 0 && sampleSize <= 96)
        {
            return false;
        }

        designPreview["sampleSize"] = 48;
        return true;
    }

    private static bool EnsureComponentPreviewActions(
        string componentType,
        JsonObject designPreview,
        JsonObject designPreviewDefaults)
    {
        if (componentType is not ("audio" or "keyboard" or "media" or "bubble")
            || designPreviewDefaults["actions"] is not JsonArray defaultActions)
        {
            return false;
        }

        var changed = false;
        var defaultActionsJson = defaultActions.ToJsonString();
        if (designPreview["actions"]?.ToJsonString() != defaultActionsJson)
        {
            designPreview["actions"] = JsonNode.Parse(defaultActionsJson);
            changed = true;
        }

        if (componentType == "media" && designPreview["fullScreenTransition"] is null)
        {
            designPreview["fullScreenTransition"] = false;
            changed = true;
        }

        return changed;
    }

}
