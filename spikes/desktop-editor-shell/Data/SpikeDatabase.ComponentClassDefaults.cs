using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static string ComponentTypeLabel(string componentType)
    {
        return componentType switch
        {
            "avatar" => "Avatar component",
            "status_bar" => "Status bar component",
            "navigation_bar" => "Navigation bar component",
            "surface" => "Surface component",
            "cursor" => "Cursor component",
            "textBox" => "Text box component",
            "iconRow" => "Icon row component",
            "iconBar" => "Icon bar component",
            "textInputBar" => "Text input bar component",
            "keyboard" => "Keyboard component",
            "button" => "Button component",
            "label" => "Label component",
            "audio" => "Audio component",
            "media" => "Media component",
            "bubble" => "Bubble component",
            _ => componentType,
        };
    }

    private static ComponentSeedRow NewComponentSeed(string componentType, string recordClassId, string name)
    {
        var configJson = DefaultComponentClassConfigJson(componentType);
        return new ComponentSeedRow(
            componentType,
            recordClassId,
            name,
            configJson,
            DefaultComponentDesignPreviewJson(componentType),
            componentType == "button" ? DefaultButtonMetadataJson(configJson) : DefaultComponentMetadataJson(configJson));
    }

    private static string DefaultButtonMetadataJson(string configJson)
    {
        var iconConfig = JsonNode.Parse(configJson)!.AsObject().DeepClone().AsObject();
        iconConfig["button"]!["dimensionMode"] = "fixed";
        iconConfig["button"]!["size"] = "44|44";
        return new JsonObject
        {
            ["note"] = "Seeded reusable component class.",
            ["presets"] = new JsonArray
            {
                new JsonObject { ["id"] = DefaultComponentPresetId, ["name"] = "Default", ["protected"] = true, ["locked"] = true, ["config"] = JsonNode.Parse(configJson) },
                new JsonObject { ["id"] = "icon_only", ["name"] = "Icon only", ["protected"] = true, ["locked"] = true, ["config"] = iconConfig },
            },
        }.ToJsonString();
    }

    private static string DefaultComponentMetadataJson(string configJson)
    {
        return new JsonObject
        {
            ["note"] = "Seeded reusable component class.",
            ["presets"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = DefaultComponentPresetId,
                    ["name"] = "Default",
                    ["protected"] = true,
                    ["locked"] = true,
                    ["config"] = JsonNode.Parse(configJson),
                },
            },
        }.ToJsonString();
    }

    private static JsonObject ComponentSurfaceSlot(string presetName)
    {
        return new JsonObject
        {
            ["presetId"] = presetName,
            ["overrides"] = new JsonObject(),
        };
    }

    private static JsonObject ButtonStateStyle(string iconColorToken)
    {
        return new JsonObject
        {
            ["surfaceSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
            ["labelSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
            ["iconColorToken"] = iconColorToken,
        };
    }

    private static JsonObject MediaTextOverlayDefault(
        bool enabled,
        string mode,
        string text,
        string placement)
    {
        return new JsonObject
        {
            ["enabled"] = enabled,
            ["mode"] = mode,
            ["text"] = text,
            ["targetSeconds"] = 0,
            ["textColorToken"] = "theme.colors.textPrimary",
            ["typography"] = JsonNode.Parse(TypographyStyleValue.CreateDefault("theme.typography.sizes.s")),
            ["placement"] = JsonNode.Parse(placement),
            ["textAlign"] = "center",
        };
    }

    private static string DefaultComponentClassConfigJson(string componentType)
    {
        var config = new JsonObject
        {
            ["style"] = new JsonObject
            {
                ["shadowEnabled"] = false,
                ["reliefEnabled"] = false,
                ["borderWidth"] = 0,
                ["borderColorToken"] = "theme.borders.primary",
                ["cornerRadiusToken"] = componentType == "avatar" ? "theme.radii.full" : "theme.radii.xl",
                ["reliefAngle"] = -45,
                ["reliefExtent"] = 1,
                ["reliefSpread"] = 0,
                ["reliefTopIntensity"] = 0.12,
                ["reliefBottomIntensity"] = -0.1,
            },
        };

        switch (componentType)
        {
            case "surface":
                config["surface"] = new JsonObject
                {
                    ["backgroundColorToken"] = "theme.colors.surface",
                    ["backgroundAlpha"] = 1,
                    ["borderAlpha"] = 1,
                    ["tail"] = new JsonObject
                    {
                        ["enabled"] = false,
                        ["style"] = "rounded_wedge",
                        ["side"] = "left",
                        ["vertical"] = "bottom",
                        ["size"] = "18|14",
                        ["outerCornerRadius"] = 0,
                    },
                };
                break;
            case "cursor":
                config["cursor"] = new JsonObject
                {
                    ["colorToken"] = "theme.cursor.color",
                    ["width"] = 2,
                    ["minimumFade"] = 0.15,
                    ["fadeDurationMs"] = 480,
                };
                break;
            case "textBox":
                config["textBox"] = new JsonObject
                {
                    ["dimensionMode"] = "fixed",
                    ["maxLines"] = 4,
                    ["padding"] = "theme.spacing.m|theme.spacing.s",
                    ["surfaceSlot"] = ComponentSurfaceSlot("InputBox"),
                    ["placeholder"] = "Message",
                    ["textColorToken"] = "theme.colors.textPrimary",
                    ["placeholderColorToken"] = "theme.colors.textSecondary",
                    ["typography"] = JsonNode.Parse(TypographyStyleValue.CreateDefault("theme.typography.sizes.s")),
                    ["textAlign"] = "left",
                    ["overflowMode"] = "clip",
                    ["cursorSlot"] = new JsonObject
                    {
                        ["showCursor"] = true,
                        ["presetId"] = DefaultComponentPresetId,
                        ["overrides"] = new JsonObject(),
                    },
                };
                break;
            case "avatar":
                config["avatar"] = new JsonObject
                {
                    ["defaultSize"] = 48,
                    ["cornerRadiusToken"] = "theme.radii.full",
                    ["labelSlot"] = new JsonObject
                    {
                        ["showLabel"] = false,
                        ["showSubtext"] = false,
                        ["presetId"] = DefaultComponentPresetId,
                        ["placement"] = JsonNode.Parse(AlignmentPlacementValue.Default.ToJsonString()),
                        ["overrides"] = new JsonObject(),
                    },
                };
                break;
            case "iconRow":
                config["iconRow"] = new JsonObject
                {
                    ["orientation"] = "horizontal",
                    ["gap"] = "theme.spacing.s",
                    ["sizeSource"] = "shared",
                    ["iconSizeToken"] = "theme.iconSizes.m",
                    ["textSizeToken"] = "theme.typography.sizes.s",
                };
                break;
            case "iconBar":
                config["iconBar"] = new JsonObject
                {
                    ["edgePadding"] = "theme.spacing.m",
                    ["sizeSource"] = "shared",
                    ["iconSizeToken"] = "theme.iconSizes.m",
                    ["textSizeToken"] = "theme.typography.sizes.s",
                    ["idleLeftIconRowSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["idleLeftIconRowInputs"] = IconRowInputBindings(new JsonArray("media_camera")),
                    ["idleCenterIconRowSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["idleCenterIconRowInputs"] = IconRowInputBindings(new JsonArray("media_play")),
                    ["idleRightIconRowSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["idleRightIconRowInputs"] = IconRowInputBindings(new JsonArray("nav_more_horizontal")),
                    ["activeLeftIconRowSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["activeLeftIconRowInputs"] = IconRowInputBindings(new JsonArray("media_camera")),
                    ["activeCenterIconRowSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["activeCenterIconRowInputs"] = IconRowInputBindings(new JsonArray("media_pause")),
                    ["activeRightIconRowSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["activeRightIconRowInputs"] = IconRowInputBindings(new JsonArray("nav_more_horizontal")),
                };
                break;
            case "textInputBar":
                config["textInput"] = new JsonObject
                {
                    ["height"] = 44,
                    ["barPadding"] = "theme.spacing.l|theme.spacing.m",
                    ["barSurfaceSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["textBoxSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["iconBarSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["textBoxInputs"] = TextBoxInputBindings(),
                    ["textPadding"] = "theme.spacing.xl|theme.spacing.none",
                    ["iconGap"] = "theme.spacing.m",
                    ["placeholder"] = "Message",
                    ["surfaceSlot"] = ComponentSurfaceSlot("InputBox"),
                    ["idleTextColorToken"] = "theme.colors.textSecondary",
                    ["textSizeToken"] = "theme.typography.sizes.s",
                    ["cursorColorToken"] = "theme.cursor.color",
                    ["cursorWidth"] = 2,
                    ["cursorBlinkDurationMs"] = 720,
                };
                break;
            case "keyboard":
                config.Remove("style");
                config["keyboard"] = new JsonObject
                {
                    ["language"] = "es",
                    ["heightToken"] = "theme.keyboard.height",
                    ["keyGapToken"] = "theme.keyboard.keyGap",
                    ["rowGapToken"] = "theme.keyboard.rowGap",
                    ["keyPadding"] = "theme.spacing.s",
                    ["keyCornerRadiusToken"] = "theme.radii.m",
                    ["keyBorderWidth"] = 0,
                    ["keyShadowEnabled"] = false,
                    ["pressedEffect"] = "popup",
                    ["emojiScale"] = "1.2",
                    ["typography"] = JsonNode.Parse(TypographyStyleValue.CreateDefault("theme.typography.sizes.s", "theme.system")),
                    ["iconRowPlacement"] = "bottom",
                    ["iconRowsHeight"] = 52,
                    ["iconEdgePadding"] = "theme.spacing.none",
                    ["iconBarSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["motion"] = JsonNode.Parse(MotionVariantValue.Default.ToJsonString()),
                };
                break;
            case "button":
                config["button"] = new JsonObject
                {
                    ["dimensionMode"] = "content",
                    ["size"] = "120|44",
                    ["padding"] = "theme.spacing.l|theme.spacing.m",
                    ["contentGapToken"] = "theme.spacing.s",
                    ["iconToken"] = "media_play_fill",
                    ["pushedDurationToken"] = "theme.motion.buttonPushedDurationMs",
                    ["states"] = new JsonObject
                    {
                        ["normal"] = ButtonStateStyle("theme.colors.icon"),
                        ["active"] = ButtonStateStyle("theme.colors.accent"),
                        ["pushed"] = ButtonStateStyle("theme.colors.accent"),
                        ["disabled"] = ButtonStateStyle("theme.colors.icon"),
                    },
                };
                break;
            case "label":
                config["label"] = new JsonObject
                {
                    ["dimensionMode"] = "content",
                    ["size"] = "120|32",
                    ["padding"] = "theme.spacing.m|theme.spacing.s",
                    ["surfaceSlot"] = ComponentSurfaceSlot("Label"),
                    ["textColorToken"] = "theme.colors.textPrimary",
                    ["textTypography"] = JsonNode.Parse(TypographyStyleValue.CreateDefault("theme.typography.sizes.s")),
                    ["textAlign"] = "center",
                    ["textGap"] = 2,
                    ["subtextColorToken"] = "theme.colors.textSecondary",
                    ["subtextTypography"] = JsonNode.Parse(TypographyStyleValue.CreateDefault("theme.typography.sizes.xs")),
                };
                break;
            case "audio":
                config["audio"] = new JsonObject
                {
                    ["padding"] = "theme.spacing.l|theme.spacing.m",
                    ["surfaceSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["textSize"] = 11,
                    ["textColorToken"] = "theme.icons.secondary",
                    ["playCircleSize"] = 32,
                    ["playIconPadding"] = "theme.spacing.m",
                    ["playColorToken"] = "theme.icons.accent",
                    ["playIconColorToken"] = "theme.icons.secondary",
                    ["waveformColorToken"] = "theme.icons.primary",
                    ["waveformPlayedColorToken"] = "theme.icons.accent",
                    ["waveformBarCount"] = 28,
                    ["waveformGap"] = "theme.spacing.xs",
                    ["waveformMinHeight"] = 4,
                    ["waveformMaxHeight"] = 22,
                    ["progressKnobSize"] = 9,
                    ["avatarSlot"] = new JsonObject
                    {
                        ["showAvatar"] = true,
                        ["presetId"] = DefaultComponentPresetId,
                        ["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromDirectionalEdge("left", 8).ToJsonString()),
                        ["overrides"] = new JsonObject
                        {
                            ["avatar"] = new JsonObject
                            {
                                ["defaultSize"] = 38,
                            },
                        },
                    },
                    ["badgeSlot"] = new JsonObject
                    {
                        ["showBadge"] = false,
                        ["size"] = 16,
                        ["iconToken"] = "media_mic",
                        ["presetId"] = DefaultComponentPresetId,
                        ["placement"] = JsonNode.Parse("""{"mode":"center","alignX":1,"alignY":1,"offsetX":0,"offsetY":0}"""),
                        ["overrides"] = new JsonObject(),
                    },
                };
                break;
            case "status_bar":
                return DefaultStatusBarConfigJson();
            case "navigation_bar":
                return DefaultNavigationBarConfigJson();
            case "media":
                config["media"] = new JsonObject
                {
                    ["surfaceSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["controlBarHeight"] = 56,
                    ["iconBarPadding"] = "theme.spacing.xl|theme.spacing.xl",
                    ["iconColorTokenOverride"] = "theme.icons.alternate",
                    ["inlineTopIconBarSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["inlineCenterIconBarSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["inlineBottomIconBarSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["fullScreenTopIconBarSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["fullScreenCenterIconBarSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["fullScreenBottomIconBarSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["idleText"] = MediaTextOverlayDefault(
                        false,
                        "free",
                        "",
                        """{"mode":"center","alignX":0.5,"alignY":0.5,"offsetX":0,"offsetY":0}"""),
                    ["playText"] = MediaTextOverlayDefault(
                        true,
                        "countDown",
                        "",
                        """{"mode":"center","alignX":0.5,"alignY":1,"offsetX":0,"offsetY":-18}"""),
                    ["controlsFadeDelayMs"] = 900,
                    ["controlsFadeDurationMs"] = 180,
                    ["motion"] = JsonNode.Parse(MediaMotionDefault().ToJsonString()),
                };
                break;
            case "bubble":
                config["bubble"] = new JsonObject
                {
                    ["surfaceSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["textBoxSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["mediaType"] = "none",
                    ["mediaPosition"] = "bottom",
                    ["imageMediaSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["videoMediaSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["audioSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["actorLabelSlot"] = new JsonObject
                    {
                        ["showLabel"] = false,
                        ["useActorColor"] = false,
                        ["presetId"] = DefaultComponentPresetId,
                        ["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromDirectionalEdge("top", -4).ToJsonString()),
                        ["overrides"] = new JsonObject(),
                    },
                    ["avatarSlot"] = new JsonObject
                    {
                        ["showAvatar"] = false,
                        ["presetId"] = DefaultComponentPresetId,
                        ["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromDirectionalEdge("left", 8).ToJsonString()),
                        ["overrides"] = new JsonObject(),
                    },
                    ["status"] = new JsonObject
                    {
                        ["sizeToken"] = "theme.iconSizes.s",
                        ["textSizeToken"] = "theme.iconSizes.s",
                        ["sent"] = new JsonObject
                        {
                            ["iconToken"] = "system_check",
                            ["colorToken"] = "theme.icons.secondary",
                        },
                        ["delivered"] = new JsonObject
                        {
                            ["iconToken"] = "system_check",
                            ["colorToken"] = "theme.icons.secondary",
                        },
                        ["read"] = new JsonObject
                        {
                            ["iconToken"] = "system_check",
                            ["colorToken"] = "theme.icons.accent",
                        },
                    },
                    ["incomingBackground"] = "gray_080|gray_020",
                    ["incomingText"] = "gray_010|gray_100",
                    ["systemBackground"] = "gray_080|gray_020",
                    ["systemText"] = "gray_010|gray_100",
                    ["outgoingBackground"] = "aqua_green|aqua_green",
                    ["outgoingText"] = "gray_100|gray_100",
                    ["padding"] = "theme.spacing.l|theme.spacing.m",
                };
                break;
        }

        return config.ToJsonString();
    }

    private static string DefaultComponentDesignPreviewJson(string componentType)
    {
        var preview = new JsonObject
        {
            ["componentType"] = componentType,
            ["sampleText"] = componentType switch
            {
                "label" => "Alex",
                "textBox" => "Message",
                "textInputBar" => "Message",
                "audio" => "0:05",
                "media" => "",
                "bubble" => "Message",
                _ => "Sample",
            },
            ["sampleSubtext"] = componentType is "label" or "avatar" ? "Subtitle" : "",
            ["sampleSize"] = 256,
            ["inputs"] = ComponentInputsForComponent(componentType),
        };
        if (componentType == "surface")
        {
            preview["size"] = "180|104";
        }
        if (componentType == "textBox")
        {
            preview["size"] = "220|44";
            preview["maxWidth"] = 220;
            preview["maxLines"] = 4;
        }
        if (componentType == "cursor")
        {
            preview["height"] = 32;
        }
        if (componentType == "iconBar")
        {
            preview["size"] = "360|56";
            preview["state"] = "idle";
        }
        if (componentType == "iconRow")
        {
            preview["gap"] = "theme.spacing.s";
            preview["orientation"] = "horizontal";
            preview["items"] = IconRowItems(new JsonArray("media_mic", "chat_send"));
            preview["collections"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "items",
                    ["label"] = "Buttons",
                    ["jsonKey"] = "items",
                    ["itemLabel"] = "Button",
                    ["sourceCollectionJsonKey"] = "items",
                    ["fields"] = IconRowButtonFields(),
                    ["itemActions"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "push",
                            ["label"] = "Push",
                            ["playInputId"] = "pushTrigger",
                            ["durationThemeToken"] = "theme.motion.buttonPushedDurationMs",
                            ["timeJsonKey"] = "pushElapsedMs",
                            ["timeUnit"] = "milliseconds",
                            ["prewarmFrames"] = false,
                        },
                    },
                },
            };
        }
        if (componentType == "textInputBar")
        {
            preview.Remove("leftIcons");
            preview.Remove("rightIcons");
        }
        if (componentType == "bubble")
        {
            preview["state"] = "incoming";
            preview["maxWidth"] = 66;
            preview["actorName"] = "Alex Q";
            preview["statusText"] = "9:41";
            preview["statusState"] = "read";
            preview.Remove("writeOnDurationSeconds");
            preview.Remove("writeOnTimeSeconds");
            preview["writeOnDurationFrames"] = 30;
            preview["writeOnTrigger"] = false;
            preview["writeOnFrame"] = 0;
            preview["mediaSource"] = "";
            preview["viewportSize"] = "240|160";
            preview["mediaScale"] = 1;
            preview["mediaOffset"] = "0|0";
            preview["isPlaying"] = false;
            preview["currentTimeSeconds"] = 0;
            preview["durationSeconds"] = 12;
            preview["isFullScreen"] = false;
            preview["fullScreenTransition"] = false;
            preview["fullframeOrientation"] = "portrait";
            preview["controlsElapsedMs"] = 0;
            preview["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "writeOn",
                    ["label"] = "Write-on",
                    ["playInputId"] = "writeOnTrigger",
                    ["durationInputId"] = "writeOnDurationFrames",
                    ["timeJsonKey"] = "writeOnFrame",
                    ["timeUnit"] = "frames",
                    ["prewarmFrames"] = false,
                },
                new JsonObject
                {
                    ["id"] = "play",
                    ["label"] = "Play",
                    ["playInputId"] = "isPlaying",
                    ["durationInputId"] = "durationSeconds",
                    ["timeJsonKey"] = "currentTimeSeconds",
                    ["prewarmFrames"] = true,
                    ["prewarmWhenConfigPath"] = "bubble.mediaType",
                    ["prewarmWhenValue"] = "video",
                },
                new JsonObject
                {
                    ["id"] = "fullScreen",
                    ["label"] = "Full screen",
                    ["playInputId"] = "fullScreenTransition",
                    ["activateInputIds"] = new JsonArray { "isFullScreen" },
                    ["durationSeconds"] = 0.3,
                    ["durationMotionConfigPath"] = "media.motion",
                    ["timeJsonKey"] = "motionElapsedMs",
                    ["timeUnit"] = "milliseconds",
                    ["prewarmFrames"] = false,
                },
            };
        }

        if (componentType == "audio")
        {
            preview["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "play",
                    ["label"] = "Play",
                    ["playInputId"] = "isPlaying",
                    ["durationInputId"] = "durationSeconds",
                    ["timeJsonKey"] = "currentTimeSeconds",
                    ["prewarmFrames"] = false,
                },
            };
        }
        if (componentType == "button")
        {
            preview["contentMode"] = "text";
            preview["iconSizeToken"] = "theme.iconSizes.m";
            preview["textSizeToken"] = "theme.typography.sizes.s";
            preview["pushTrigger"] = false;
            preview["pushElapsedMs"] = 0;
            preview["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "push",
                    ["label"] = "Push",
                    ["playInputId"] = "pushTrigger",
                    ["durationThemeToken"] = "theme.motion.buttonPushedDurationMs",
                    ["timeJsonKey"] = "pushElapsedMs",
                    ["timeUnit"] = "milliseconds",
                    ["prewarmFrames"] = false,
                },
            };
        }
        if (componentType == "keyboard")
        {
            preview["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "in",
                    ["label"] = "In",
                    ["playInputId"] = "trigger",
                    ["durationMotionConfigPath"] = "keyboard.motion",
                    ["timeJsonKey"] = "motionElapsedMs",
                    ["timeUnit"] = "milliseconds",
                },
            };
        }
        if (componentType == "media")
        {
            preview["mediaSource"] = "";
            preview["mediaType"] = "image";
            preview["viewportSize"] = "320|180";
            preview["mediaScale"] = 1;
            preview["mediaOffset"] = "0|0";
            preview["isPlaying"] = false;
            preview["currentTimeSeconds"] = 0;
            preview["durationSeconds"] = 12;
            preview["isFullScreen"] = false;
            preview["fullScreenTransition"] = false;
            preview["fullframeOrientation"] = "portrait";
            preview["controlsElapsedMs"] = 0;
            preview["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "play",
                    ["label"] = "Play",
                    ["playInputId"] = "isPlaying",
                    ["durationInputId"] = "durationSeconds",
                    ["timeJsonKey"] = "currentTimeSeconds",
                    ["prewarmFrames"] = true,
                    ["prewarmWhenJsonKey"] = "mediaType",
                    ["prewarmWhenValue"] = "video",
                },
                new JsonObject
                {
                    ["id"] = "fullScreen",
                    ["label"] = "Full screen",
                    ["playInputId"] = "fullScreenTransition",
                    ["activateInputIds"] = new JsonArray { "isFullScreen" },
                    ["durationSeconds"] = 0.3,
                    ["durationMotionConfigPath"] = "media.motion",
                    ["timeJsonKey"] = "motionElapsedMs",
                    ["timeUnit"] = "milliseconds",
                    ["prewarmFrames"] = false,
                },
            };
        }

        return preview.ToJsonString();
    }

    private static JsonArray ComponentInputsForComponent(string componentType)
    {
        JsonArray inputs = componentType switch
        {
            "label" =>
            [
                ComponentInput("sampleText", "Text", "sampleText", "text", "Sample"),
                ComponentInput("sampleSubtext", "Subtext", "sampleSubtext", "text", "Subtitle"),
            ],
            "surface" =>
            [
                ComponentInput("size", "Size", "size", "integerPair", "180|104"),
            ],
            "cursor" =>
            [
                ComponentInput("height", "Height", "height", "number", "32", minimum: 1, maximum: 9999, increment: 1),
            ],
            "textBox" =>
            [
                ComponentInput("sampleText", "Text", "sampleText", "multilineText", "Message"),
                ComponentInput("placeholder", "Placeholder", "placeholder", "text", "Message"),
                ComponentInput("maxLines", "Max lines", "maxLines", ValueKind.Integer, "4", minimum: 1, maximum: 64, increment: 1),
                ComponentInput("leftIconRowSlot", "Variant", "leftIconRowSlot", "componentPreset", "iconRow::preset::default", componentType: "iconRow", uiOrigin: "embedded", uiGroupId: "leftIconRow", uiGroupLabel: "Left icon row"),
                ComponentInput("leftIcons", "Icons", "leftIcons", "iconList", "[]", uiOrigin: "embedded", uiGroupId: "leftIconRow", uiGroupLabel: "Left icon row"),
                ComponentInput("rightIconRowSlot", "Variant", "rightIconRowSlot", "componentPreset", "iconRow::preset::default", componentType: "iconRow", uiOrigin: "embedded", uiGroupId: "rightIconRow", uiGroupLabel: "Right icon row"),
                ComponentInput("rightIcons", "Icons", "rightIcons", "iconList", "[]", uiOrigin: "embedded", uiGroupId: "rightIconRow", uiGroupLabel: "Right icon row"),
                ComponentInput("iconGap", "Gap to text", "iconGap", "themeToken", "theme.spacing.m", options: ComponentClassFieldCatalog.SpacingTokenOptions, uiOrigin: "embedded", uiGroupId: "iconRowShared", uiGroupLabel: "Icon row shared"),
                ComponentInput("iconRowSize", "Icon size", "iconRowSize", ValueKind.ThemeToken, "theme.iconSizes.xl", options: ComponentClassFieldCatalog.IconSizeTokenOptions, uiOrigin: "embedded", uiGroupId: "iconRowShared", uiGroupLabel: "Icon row shared"),
                ComponentInput("iconRowGap", "Gap", "iconRowGap", "themeToken", "theme.spacing.s", options: ComponentClassFieldCatalog.SpacingTokenOptions, uiOrigin: "embedded", uiGroupId: "iconRowShared", uiGroupLabel: "Icon row shared"),
                ComponentInput(
                    "iconRowOrientation",
                    "Orientation",
                    "iconRowOrientation",
                    "option",
                    "horizontal",
                    options:
                    [
                        new FieldOption("horizontal", "Horizontal"),
                        new FieldOption("vertical", "Vertical"),
                    ],
                    uiOrigin: "embedded",
                    uiGroupId: "iconRowShared",
                    uiGroupLabel: "Icon row shared"),
                ComponentInput(
                    "fixedSize",
                    "Size",
                    "size",
                    "integerPair",
                    "220|44",
                    pairFirstLabel: "W",
                    pairSecondLabel: "H",
                    visibleWhenPath: "textBox.dimensionMode",
                    visibleWhenValue: "fixed"),
                ComponentInput(
                    "contentMaxWidth",
                    "Max width",
                    "maxWidth",
                    "number",
                    "220",
                    minimum: 1,
                    maximum: 9999,
                    increment: 1,
                    visibleWhenPath: "textBox.dimensionMode",
                    visibleWhenValue: "content"),
                ComponentInput(
                    "growSize",
                    "Size",
                    "size",
                    "integerPair",
                    "220|44",
                    pairFirstLabel: "W",
                    pairSecondLabel: "Min H",
                    visibleWhenPath: "textBox.dimensionMode",
                    visibleWhenValue: "growVertical"),
            ],
            "iconRow" =>
            [
                ComponentInput(
                    "gap",
                    "Gap",
                    "gap",
                    "themeToken",
                    "theme.spacing.s",
                    options: ComponentClassFieldCatalog.SpacingTokenOptions),
                ComponentInput(
                    "orientation",
                    "Orientation",
                    "orientation",
                    "option",
                    "horizontal",
                    options:
                    [
                        new FieldOption("horizontal", "Horizontal"),
                        new FieldOption("vertical", "Vertical"),
                    ]),
            ],
            "iconBar" =>
            [
                ComponentInput(
                    "state",
                    "State",
                    "state",
                    "option",
                    "idle",
                    options:
                    [
                        new FieldOption("idle", "Idle"),
                        new FieldOption("active", "Active"),
                    ]),
                ComponentInput(
                    "size",
                    "Size",
                    "size",
                    "integerPair",
                    "360|56",
                    pairFirstLabel: "W",
                    pairSecondLabel: "H"),
            ],
            "avatar" =>
            [
                ComponentInput(
                    "actorId",
                    "Actor",
                    "actorId",
                    "recordReference",
                    "",
                    tableId: "actors",
                    resolvedJsonKey: "actor"),
                ComponentInput("sampleSubtext", "Subtitle", "sampleSubtext", "text", "Subtitle"),
            ],
            "button" =>
            [
                ComponentInput("contentMode", "Content", "contentMode", "option", "text", options: [new("icon", "Icon"), new("text", "Text"), new("iconText", "Icon + text")]),
                ComponentInput("state", "State", "state", "option", "normal", options: [new("normal", "Normal"), new("active", "Active"), new("pushed", "Pushed"), new("disabled", "Disabled")]),
                ComponentInput("sampleText", "Text", "sampleText", "text", "Action"),
                ComponentInput("iconToken", "Icon", "iconToken", "icon", "media_play_fill"),
                ComponentInput("iconSizeToken", "Icon size", "iconSizeToken", ValueKind.ThemeToken, "theme.iconSizes.m", options: ComponentClassFieldCatalog.IconSizeTokenOptions),
                ComponentInput("textSizeToken", "Text size", "textSizeToken", ValueKind.ThemeToken, "theme.typography.sizes.s", options: ComponentClassFieldCatalog.TypographySizeOptions),
                ComponentInput("pushTrigger", "Push", "pushTrigger", "boolean", "false", source: "calculated"),
                ComponentInput("pushElapsedMs", "Push elapsed", "pushElapsedMs", ValueKind.Decimal, "0", minimum: 0, maximum: 60000, increment: 10, source: "calculated", unit: "ms"),
            ],
            "textInputBar" =>
            [
                ComponentInput("availableWidth", "Available width", "availableWidth", "number", "360", minimum: 1, maximum: 10000, increment: 1),
                ComponentInput("sampleText", "Text", "sampleText", ValueKind.StringMultiline, "Message", uiOrigin: "embedded", uiGroupId: "textBox", uiGroupLabel: "Text box"),
            ],
            "keyboard" =>
            [
                ComponentInput("text", "Text", "text", "multilineText", "Hola 😀😃😄"),
                ComponentInput("currentCharacter", "Current character", "currentCharacter", "number", "1", minimum: 1, maximum: 9999, increment: 1),
                ComponentInput("trigger", "Trigger", "trigger", "boolean", "false"),
            ],
            "audio" =>
            [
                ComponentInput("availableWidth", "Available width", "availableWidth", "number", "240", minimum: 1, maximum: 10000, increment: 1),
                ComponentInput("isPlaying", "Playing", "isPlaying", "boolean", "false"),
                ComponentInput("currentTimeSeconds", "Current time", "currentTimeSeconds", "number", "0", minimum: 0, maximum: 86400, increment: 0.1m, unit: "s"),
                ComponentInput("durationSeconds", "Duration", "durationSeconds", "number", "65", minimum: 1, maximum: 86400, increment: 1, unit: "s"),
                ComponentInput(
                    "actorId",
                    "Actor",
                    "actorId",
                    "recordReference",
                    "",
                    tableId: "actors",
                    resolvedJsonKey: "actor"),
            ],
            "media" =>
            [
                ComponentInput("mediaSource", "Source", "mediaSource", ValueKind.MediaFilePath, ""),
                ComponentInput(
                    "mediaType",
                    "Type",
                    "mediaType",
                    "option",
                    "image",
                    options:
                    [
                        new FieldOption("image", "Image"),
                        new FieldOption("video", "Video"),
                    ]),
                ComponentInput(
                    "viewportSize",
                    "Viewport",
                    "viewportSize",
                    "integerPair",
                    "320|180",
                    pairFirstLabel: "W",
                    pairSecondLabel: "H"),
                ComponentInput("mediaScale", "Scale", "mediaScale", ValueKind.Decimal, "1", minimum: 0.05m, maximum: 16, increment: 0.05m),
                ComponentInput(
                    "mediaOffset",
                    "Offset",
                    "mediaOffset",
                    "integerPair",
                    "0|0",
                    pairFirstLabel: "X",
                    pairSecondLabel: "Y"),
                ComponentInput("isPlaying", "Play/Pause", "isPlaying", ValueKind.Boolean, "false"),
                ComponentInput("currentTimeSeconds", "Current time", "currentTimeSeconds", ValueKind.Decimal, "0", minimum: 0, maximum: 86400, increment: 0.1m, unit: "s"),
                ComponentInput("durationSeconds", "Duration", "durationSeconds", ValueKind.Decimal, "12", minimum: 0, maximum: 86400, increment: 0.1m, unit: "s"),
                ComponentInput("isFullScreen", "Full screen", "isFullScreen", ValueKind.Boolean, "false"),
                ComponentInput("fullScreenTransition", "Full-screen transition", "fullScreenTransition", ValueKind.Boolean, "false"),
                ComponentInput(
                    "fullframeOrientation",
                    "Fullframe orientation",
                    "fullframeOrientation",
                    "option",
                    "portrait",
                    options:
                    [
                        new FieldOption("portrait", "Portrait"),
                        new FieldOption("landscape", "Landscape"),
                    ]),
                ComponentInput("controlsElapsedMs", "Controls elapsed", "controlsElapsedMs", ValueKind.Integer, "0", minimum: 0, maximum: 60000, increment: 10, unit: "ms"),
                ComponentInput("motionElapsedMs", "Motion elapsed", "motionElapsedMs", ValueKind.Decimal, "0", minimum: 0, maximum: 86400000, increment: 10, source: "calculated", unit: "ms"),
            ],
            "bubble" =>
            [
                ComponentInput(
                    "state",
                    "State",
                    "state",
                    "option",
                    "incoming",
                    options:
                    [
                        new FieldOption("incoming", "Incoming"),
                        new FieldOption("system", "System"),
                        new FieldOption("outgoing", "Outgoing"),
                    ]),
                ComponentInput("sampleText", "Text", "sampleText", ValueKind.StringMultiline, "Message", uiOrigin: "embedded", uiGroupId: "textBox", uiGroupLabel: "Text box"),
                ComponentInput("maxWidth", "Max width %", "maxWidth", ValueKind.Integer, "66", minimum: 1, maximum: 100, increment: 1),
                ComponentInput("writeOnDurationFrames", "Write-on duration", "writeOnDurationFrames", ValueKind.Integer, "30", minimum: 1, maximum: 10000, increment: 1, unit: "frames"),
                ComponentInput("writeOnTrigger", "Write-on trigger", "writeOnTrigger", ValueKind.Boolean, "false", source: "calculated"),
                ComponentInput("writeOnFrame", "Write-on position", "writeOnFrame", ValueKind.Integer, "0", minimum: 0, maximum: 10000, increment: 1, source: "calculated", unit: "frames"),
                ComponentInput(
                    "actorId",
                    "Actor",
                    "actorId",
                    "recordReference",
                    "",
                    uiOrigin: "embedded",
                    uiGroupId: "avatar",
                    uiGroupLabel: "Avatar",
                    tableId: "actors",
                    resolvedJsonKey: "actor"),
                ComponentInput("actorName", "Actor name", "actorName", "text", "Alex Q", uiOrigin: "embedded", uiGroupId: "actorLabel", uiGroupLabel: "Actor label"),
                ComponentInput("statusText", "Status text", "statusText", "text", "9:41"),
                ComponentInput(
                    "statusState",
                    "Status",
                    "statusState",
                    "option",
                    "read",
                    options: ComponentClassFieldCatalog.BubbleStatusStateOptions),
                ComponentInput(
                    "mediaType",
                    "Media type",
                    "mediaType",
                    "option",
                    "image",
                    options:
                    [
                        new FieldOption("none", "None"),
                        new FieldOption("image", "Image"),
                        new FieldOption("video", "Video"),
                        new FieldOption("audio", "Audio"),
                    ],
                    uiOrigin: "embedded",
                    uiGroupId: "media",
                    uiGroupLabel: "Media"),
                ComponentInput("mediaSource", "Media source", "mediaSource", ValueKind.MediaFilePath, "", uiOrigin: "embedded", uiGroupId: "media", uiGroupLabel: "Media"),
                ComponentInput(
                    "viewportSize",
                    "Media viewport",
                    "viewportSize",
                    "integerPair",
                    "240|160",
                    pairFirstLabel: "W",
                    pairSecondLabel: "H",
                    uiOrigin: "embedded",
                    uiGroupId: "media",
                    uiGroupLabel: "Media"),
                ComponentInput("mediaScale", "Media scale", "mediaScale", ValueKind.Decimal, "1", minimum: 0.05m, maximum: 16, increment: 0.05m, uiOrigin: "embedded", uiGroupId: "media", uiGroupLabel: "Media"),
                ComponentInput(
                    "mediaOffset",
                    "Media offset",
                    "mediaOffset",
                    "integerPair",
                    "0|0",
                    pairFirstLabel: "X",
                    pairSecondLabel: "Y",
                    uiOrigin: "embedded",
                    uiGroupId: "media",
                    uiGroupLabel: "Media"),
                ComponentInput("isPlaying", "Play/Pause", "isPlaying", ValueKind.Boolean, "false", uiOrigin: "embedded", uiGroupId: "media", uiGroupLabel: "Media"),
                ComponentInput("currentTimeSeconds", "Current time", "currentTimeSeconds", ValueKind.Decimal, "0", minimum: 0, maximum: 86400, increment: 0.1m, uiOrigin: "embedded", uiGroupId: "media", uiGroupLabel: "Media", unit: "s"),
                ComponentInput("durationSeconds", "Duration", "durationSeconds", ValueKind.Decimal, "12", minimum: 0, maximum: 86400, increment: 0.1m, uiOrigin: "embedded", uiGroupId: "media", uiGroupLabel: "Media", unit: "s"),
                ComponentInput("isFullScreen", "Full screen", "isFullScreen", ValueKind.Boolean, "false", uiOrigin: "embedded", uiGroupId: "media", uiGroupLabel: "Media"),
                ComponentInput("fullScreenTransition", "Full-screen transition", "fullScreenTransition", ValueKind.Boolean, "false", uiOrigin: "embedded", uiGroupId: "media", uiGroupLabel: "Media"),
                ComponentInput(
                    "fullframeOrientation",
                    "Fullframe orientation",
                    "fullframeOrientation",
                    "option",
                    "portrait",
                    options:
                    [
                        new FieldOption("portrait", "Portrait"),
                        new FieldOption("landscape", "Landscape"),
                    ],
                    uiOrigin: "embedded",
                    uiGroupId: "media",
                    uiGroupLabel: "Media"),
                ComponentInput("controlsElapsedMs", "Controls elapsed", "controlsElapsedMs", ValueKind.Integer, "0", minimum: 0, maximum: 60000, increment: 10, uiOrigin: "embedded", uiGroupId: "media", uiGroupLabel: "Media", unit: "ms"),
                ComponentInput("motionElapsedMs", "Motion elapsed", "motionElapsedMs", ValueKind.Decimal, "0", minimum: 0, maximum: 86400000, increment: 10, source: "calculated", uiOrigin: "embedded", uiGroupId: "media", uiGroupLabel: "Media", unit: "ms"),
            ],
            _ => [],
        };
        ApplyComponentInputLayout(componentType, inputs);
        return inputs;
    }

    private static void ApplyComponentInputLayout(string componentType, JsonArray inputs)
    {
        switch (componentType)
        {
            case "textBox":
                SetComponentInputGroup(inputs, ["sampleText", "placeholder", "maxLines"], "text", "Text", 10);
                SetComponentInputGroup(inputs, ["fixedSize", "contentMaxWidth", "growSize"], "layout", "Layout", 20);
                break;
            case "iconRow":
                SetComponentInputGroup(inputs, ["gap", "orientation"], "layout", "Layout", 10);
                break;
            case "iconBar":
                SetComponentInputGroup(inputs, ["state"], "state", "State", 10);
                SetComponentInputGroup(inputs, ["size"], "layout", "Layout", 20);
                break;
            case "avatar":
                SetComponentInputGroup(inputs, ["actorId", "sampleSubtext"], "identity", "Identity", 10);
                break;
            case "button":
                SetComponentInputGroup(inputs, ["state"], "state", "State", 10);
                SetComponentInputGroup(inputs, ["contentMode", "sampleText", "iconToken", "iconSizeToken", "textSizeToken"], "content", "Content", 20);
                SetComponentInputGroup(inputs, ["pushTrigger", "pushElapsedMs"], "action", "Action", 30);
                break;
            case "textInputBar":
                SetComponentInputGroup(inputs, ["availableWidth"], "layout", "Layout", 10);
                SetComponentInputGroup(inputs, ["sampleText"], "textBox", "Text box", 20);
                break;
            case "keyboard":
                SetComponentInputGroup(inputs, ["text", "currentCharacter"], "input", "Input", 10);
                SetComponentInputGroup(inputs, ["trigger"], "motion", "Motion", 20);
                break;
            case "audio":
                SetComponentInputGroup(inputs, ["availableWidth"], "layout", "Layout", 10);
                SetComponentInputGroup(inputs, ["isPlaying", "currentTimeSeconds", "durationSeconds"], "playback", "Playback", 20);
                SetComponentInputGroup(inputs, ["actorId"], "identity", "Actor", 30);
                break;
            case "media":
                SetComponentInputGroup(inputs, ["mediaSource", "mediaType"], "source", "Source", 10);
                SetComponentInputGroup(inputs, ["viewportSize", "mediaScale", "mediaOffset"], "frame", "Frame", 20);
                SetComponentInputGroup(inputs, ["isPlaying", "currentTimeSeconds", "durationSeconds", "controlsElapsedMs"], "playback", "Playback", 30);
                SetComponentInputGroup(inputs, ["isFullScreen", "fullScreenTransition", "fullframeOrientation"], "fullScreen", "Full screen", 40);
                break;
            case "bubble":
                SetComponentInputGroup(inputs, ["state", "sampleText", "maxWidth"], "message", "Message", 10);
                SetComponentInputGroup(inputs, ["writeOnDurationFrames", "writeOnTrigger", "writeOnFrame"], "timing", "Timing", 20);
                SetComponentInputGroup(inputs, ["statusText", "statusState"], "delivery", "Delivery", 30);
                SetComponentInputGroup(inputs, ["actorId"], "avatar", "Avatar", 40);
                SetComponentInputGroup(inputs, ["actorName"], "actorLabel", "Actor label", 50);
                SetComponentInputGroup(inputs, ["mediaType", "mediaSource", "viewportSize", "mediaScale", "mediaOffset", "isPlaying", "currentTimeSeconds", "durationSeconds", "isFullScreen", "fullScreenTransition", "fullframeOrientation", "controlsElapsedMs", "motionElapsedMs"], "media", "Media", 60);
                break;
        }
    }

    private static void SetComponentInputGroup(
        JsonArray inputs,
        string[] ids,
        string groupId,
        string groupLabel,
        int groupOrder)
    {
        foreach (var id in ids)
        {
            var input = inputs.OfType<JsonObject>().FirstOrDefault((candidate) => candidate["id"]?.GetValue<string>() == id);
            if (input is null) continue;
            input["uiOrigin"] = "embedded";
            input["uiGroupId"] = groupId;
            input["uiGroupLabel"] = groupLabel;
            input["uiParentGroupId"] = "";
            input["uiOrder"] = groupOrder + Array.IndexOf(ids, id);
        }
    }

    private static JsonObject ComponentInput(
        string id,
        string label,
        string jsonKey,
        ValueKind valueKind,
        string defaultValue,
        decimal minimum = 0,
        decimal maximum = 9999,
        decimal increment = 1,
        string tableId = "",
        string resolvedJsonKey = "",
        string componentType = "",
        IReadOnlyList<FieldOption>? options = null,
        string pairFirstLabel = "W",
        string pairSecondLabel = "H",
        string visibleWhenPath = "",
        string visibleWhenValue = "",
        string source = "runtime",
        string uiOrigin = "",
        string uiGroupId = "",
        string uiGroupLabel = "",
        string uiParentGroupId = "",
        string unit = "")
    {
        return ComponentInput(
            id,
            label,
            jsonKey,
            InputKindForValueKind(valueKind),
            defaultValue,
            minimum,
            maximum,
            increment,
            tableId,
            resolvedJsonKey,
            componentType,
            options,
            pairFirstLabel,
            pairSecondLabel,
            visibleWhenPath,
            visibleWhenValue,
            source,
            valueKind.ToString(),
            uiOrigin,
            uiGroupId,
            uiGroupLabel,
            uiParentGroupId,
            unit);
    }

    private static JsonObject ComponentInput(
        string id,
        string label,
        string jsonKey,
        string kind,
        string defaultValue,
        decimal minimum = 0,
        decimal maximum = 9999,
        decimal increment = 1,
        string tableId = "",
        string resolvedJsonKey = "",
        string componentType = "",
        IReadOnlyList<FieldOption>? options = null,
        string pairFirstLabel = "W",
        string pairSecondLabel = "H",
        string visibleWhenPath = "",
        string visibleWhenValue = "",
        string source = "runtime",
        string valueKind = "",
        string uiOrigin = "",
        string uiGroupId = "",
        string uiGroupLabel = "",
        string uiParentGroupId = "",
        string unit = "")
    {
        return new JsonObject
        {
            ["id"] = id,
            ["label"] = label,
            ["jsonKey"] = jsonKey,
            ["kind"] = kind,
            ["valueKind"] = string.IsNullOrWhiteSpace(valueKind) ? ValueKindForInputKind(kind) : valueKind,
            ["defaultValue"] = defaultValue,
            ["minimum"] = minimum,
            ["maximum"] = maximum,
            ["increment"] = increment,
            ["tableId"] = tableId,
            ["resolvedJsonKey"] = resolvedJsonKey,
            ["componentType"] = componentType,
            ["options"] = OptionsJson(options),
            ["pairFirstLabel"] = pairFirstLabel,
            ["pairSecondLabel"] = pairSecondLabel,
            ["visibleWhenPath"] = visibleWhenPath,
            ["visibleWhenValue"] = visibleWhenValue,
            ["source"] = source,
            ["uiOrigin"] = uiOrigin,
            ["uiGroupId"] = uiGroupId,
            ["uiGroupLabel"] = uiGroupLabel,
            ["uiParentGroupId"] = uiParentGroupId,
            ["unit"] = unit,
        };
    }

    private static JsonObject ProjectRuntimeInput(
        string componentType,
        string inputId,
        string id,
        string label,
        string jsonKey,
        string defaultValue)
    {
        var binding = ComponentClassFieldCatalog.RuntimeInputBindingsForComponent(componentType)
            .FirstOrDefault((candidate) => candidate.Id == inputId)
            ?? throw new InvalidOperationException($"Component '{componentType}' does not expose runtime input '{inputId}'.");
        return ComponentInput(
            id,
            label,
            jsonKey,
            InputKindForValueKind(binding.ValueKind),
            defaultValue,
            minimum: binding.Number?.Minimum ?? 0,
            maximum: binding.Number?.Maximum ?? 9999,
            increment: binding.Number?.Increment ?? 1,
            componentType: binding.ComponentType,
            options: binding.Options,
            source: "runtime",
            valueKind: binding.ValueKind.ToString());
    }

    private static string InputKindForValueKind(ValueKind valueKind)
    {
        return valueKind switch
        {
            ValueKind.Decimal or ValueKind.Integer or ValueKind.Alpha => "number",
            ValueKind.IntegerPair => "integerPair",
            ValueKind.MediaFilePath => "mediaFilePath",
            ValueKind.Boolean => "boolean",
            ValueKind.OptionToken => "option",
            ValueKind.RecordReference => "recordReference",
            ValueKind.ComponentPreset => "componentPreset",
            ValueKind.ThemeToken => "themeToken",
            ValueKind.IconToken => "icon",
            ValueKind.IconTokenList => "iconList",
            ValueKind.StringMultiline => "multilineText",
            _ => "text",
        };
    }

    private static string ValueKindForInputKind(string kind)
    {
        return kind.Trim().ToLowerInvariant() switch
        {
            "number" => ValueKind.Decimal.ToString(),
            "integerpair" or "integer_pair" or "size" => ValueKind.IntegerPair.ToString(),
            "mediafilepath" or "media_file_path" => ValueKind.MediaFilePath.ToString(),
            "boolean" => ValueKind.Boolean.ToString(),
            "option" => ValueKind.OptionToken.ToString(),
            "recordreference" or "record_reference" => ValueKind.RecordReference.ToString(),
            "componentpreset" or "component_preset" => ValueKind.ComponentPreset.ToString(),
            "themetoken" or "theme_token" => ValueKind.ThemeToken.ToString(),
            "icon" => ValueKind.IconToken.ToString(),
            "iconlist" or "icon_list" or "icons" => ValueKind.IconTokenList.ToString(),
            "multilinetext" or "multiline_text" or "textmultiline" or "text_multiline" => ValueKind.StringMultiline.ToString(),
            _ => ValueKind.StringSingleLine.ToString(),
        };
    }

    private static JsonObject IconRowInputBindings(JsonArray? icons = null)
    {
        return new JsonObject
        {
            ["gap"] = "theme.spacing.m",
            ["orientation"] = "horizontal",
            ["items"] = IconRowItems(icons ?? []),
        };
    }

    private static JsonArray IconRowItems(JsonArray icons)
    {
        var items = new JsonArray();
        for (var index = 0; index < icons.Count; index++)
        {
            var icon = icons[index]?.GetValue<string>() ?? "";
            items.Add(new JsonObject
            {
                ["id"] = $"button_{index + 1:000}",
                ["buttonPresetId"] = "button::preset::default",
                ["contentMode"] = "icon",
                ["state"] = "normal",
                ["iconToken"] = icon,
                ["iconSizeToken"] = "theme.iconSizes.m",
                ["textSizeToken"] = "theme.typography.sizes.s",
                ["text"] = "",
                ["pushTrigger"] = false,
                ["pushElapsedMs"] = 0,
                ["buttonOverrides"] = new JsonObject(),
            });
        }
        return items;
    }

    private static JsonArray IconRowButtonFields() =>
    [
        ComponentInput("buttonPresetId", "Variant", "buttonPresetId", "componentPreset", "button::preset::default", componentType: "button"),
        ComponentInput("contentMode", "Content", "contentMode", "option", "icon", options:
        [
            new FieldOption("icon", "Icon"),
            new FieldOption("text", "Text"),
            new FieldOption("iconText", "Icon + text"),
        ]),
        ComponentInput("state", "State", "state", "option", "normal", options:
        [
            new FieldOption("normal", "Normal"),
            new FieldOption("active", "Active"),
            new FieldOption("pushed", "Pushed"),
            new FieldOption("disabled", "Disabled"),
        ]),
        ComponentInput("iconToken", "Icon", "iconToken", "icon", "media_mic"),
        ComponentInput("text", "Text", "text", "text", ""),
        ComponentInput("iconSizeToken", "Icon size", "iconSizeToken", ValueKind.ThemeToken, "theme.iconSizes.m", options: ComponentClassFieldCatalog.IconSizeTokenOptions),
        ComponentInput("textSizeToken", "Text size", "textSizeToken", ValueKind.ThemeToken, "theme.typography.sizes.s", options: ComponentClassFieldCatalog.TypographySizeOptions),
        ComponentInput("pushTrigger", "Push trigger", "pushTrigger", "boolean", "false", source: "calculated"),
        ComponentInput("pushElapsedMs", "Push elapsed", "pushElapsedMs", ValueKind.Integer, "0", minimum: 0, maximum: 60000, increment: 10, source: "calculated", unit: "ms"),
    ];

    private static JsonObject TextBoxInputBindings()
    {
        return new JsonObject
        {
            ["placeholder"] = "Message",
            ["maxLines"] = 4,
            ["leftIconRowSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
            ["leftIcons"] = new JsonArray(),
            ["rightIconRowSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
            ["rightIcons"] = new JsonArray(),
            ["iconGap"] = "theme.spacing.m",
            ["iconRowSize"] = "theme.iconSizes.xl",
            ["iconRowGap"] = "theme.spacing.s",
            ["iconRowOrientation"] = "horizontal",
        };
    }

    private static JsonArray OptionsJson(IReadOnlyList<FieldOption>? options)
    {
        var array = new JsonArray();
        if (options is null) return array;

        foreach (var option in options)
        {
            array.Add(new JsonObject
            {
                ["value"] = option.Value,
                ["label"] = option.Label,
            });
        }

        return array;
    }

    private static readonly ComponentSeedRow[] ComponentSeedRows =
    [
        NewComponentSeed("avatar", "component.avatar", "Default Avatar"),
        NewComponentSeed("surface", "component.surface", "Default Surface"),
        NewComponentSeed("cursor", "component.cursor", "Default Cursor"),
        NewComponentSeed("textBox", "component.textBox", "Default Text Box"),
        NewComponentSeed("iconRow", "component.iconRow", "Default Icon Row"),
        NewComponentSeed("iconBar", "component.iconBar", "Default Icon Bar"),
        NewComponentSeed("status_bar", "component.status_bar", "Default Status Bar"),
        NewComponentSeed("navigation_bar", "component.navigation_bar", "Default Navigation Bar"),
        NewComponentSeed("textInputBar", "component.textInputBar", "Default Text Input Bar"),
        NewComponentSeed("keyboard", "component.keyboard", "Default Keyboard"),
        NewComponentSeed("button", "component.button", "Default Button"),
        NewComponentSeed("label", "component.label", "Default Label"),
        NewComponentSeed("audio", "component.audio", "Default Audio"),
        NewComponentSeed("media", "component.media", "Default Media"),
        NewComponentSeed("bubble", "component.bubble", "Default Bubble"),
    ];

    private static MotionVariantValue MediaMotionDefault()
    {
        return MotionVariantValue.Default with { Scale = true };
    }
}
