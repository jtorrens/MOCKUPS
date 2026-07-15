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
            "componentStack" => "Component stack atom",
            "collectionStack" => "Collection stack atom",
            "badge" => "Badge atom",
            "notification" => "Notification component",
            "notifications" => "Notifications component",
            "textInputBar" => "Text input bar component",
            "keyboard" => "Keyboard component",
            "keypad" => "Keypad component",
            "fingerprint" => "Fingerprint component",
            "faceRecognition" => "Face recognition component",
            "drawPassword" => "Draw password component",
            "codeIndicator" => "Code indicator atom",
            "password" => "Password component",
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

    private static JsonObject KeypadLabelSlot(string textColorToken)
    {
        return new JsonObject
        {
            ["presetId"] = DefaultComponentPresetId,
            ["overrides"] = new JsonObject
            {
                ["label"] = new JsonObject
                {
                    ["dimensionMode"] = "fixed",
                    ["padding"] = "theme.spacing.none|theme.spacing.none",
                    ["textColorToken"] = textColorToken,
                    ["textAlign"] = "center",
                    ["reserveSubtextSpace"] = true,
                },
            },
        };
    }

    private static JsonObject KeypadStateStyle(string textColorToken) => new()
    {
        ["backgroundColorToken"] = "theme.colors.surface",
        ["textColorToken"] = textColorToken,
        ["backgroundAlpha"] = 1,
        ["borderAlpha"] = 1,
    };

    private static JsonObject CodeIndicatorStateSlots(string emptyColorToken, string filledColorToken) => new()
    {
        ["emptySurfaceSlot"] = CodeIndicatorSurfaceSlot(emptyColorToken, 0, 1),
        ["filledSurfaceSlot"] = CodeIndicatorSurfaceSlot(filledColorToken, 1, 1),
    };

    private static JsonObject RecognitionStateStyle(string colorToken) => new()
    {
        ["colorToken"] = colorToken,
    };

    private static JsonObject DrawPasswordStateStyle(string colorToken) => new()
    {
        ["nodeColorToken"] = colorToken,
        ["lineColorToken"] = colorToken,
    };

    private static JsonObject CodeIndicatorSurfaceSlot(
        string backgroundColorToken,
        double backgroundAlpha,
        double borderAlpha) => new()
    {
        ["presetId"] = DefaultComponentPresetId,
        ["overrides"] = new JsonObject
        {
            ["surface"] = new JsonObject
            {
                ["backgroundColorToken"] = backgroundColorToken,
                ["backgroundAlpha"] = backgroundAlpha,
                ["borderAlpha"] = borderAlpha,
            },
            ["style"] = new JsonObject
            {
                ["borderWidth"] = 1,
                ["cornerRadiusToken"] = "theme.radii.full",
                ["shadowEnabled"] = false,
                ["reliefEnabled"] = false,
            },
        },
    };

    private static JsonArray DefaultKeypadKeys() =>
    [
        KeypadKey("key_1", "1", "1", ""),
        KeypadKey("key_2", "2", "2", "ABC"),
        KeypadKey("key_3", "3", "3", "DEF"),
        KeypadKey("key_4", "4", "4", "GHI"),
        KeypadKey("key_5", "5", "5", "JKL"),
        KeypadKey("key_6", "6", "6", "MNO"),
        KeypadKey("key_7", "7", "7", "PQRS"),
        KeypadKey("key_8", "8", "8", "TUV"),
        KeypadKey("key_9", "9", "9", "WXYZ"),
        KeypadKey("key_star", "*", "*", ""),
        KeypadKey("key_0", "0", "0", "+"),
        KeypadKey("key_hash", "#", "#", ""),
    ];

    private static JsonObject KeypadKey(string id, string value, string text, string subtext) => new()
    {
        ["id"] = id,
        ["kind"] = "text",
        ["value"] = value,
        ["text"] = text,
        ["subtext"] = subtext,
        ["iconToken"] = "app_clock",
        ["disabled"] = false,
    };

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
            ["labelSlot"] = CalculatedLabelSlot("theme.colors.textPrimary", "theme.typography.sizes.s", "center"),
            ["placement"] = JsonNode.Parse(placement),
        };
    }

    private static JsonObject CalculatedLabelSlot(string colorToken, string sizeToken, string textAlign)
    {
        return new JsonObject
        {
            ["presetId"] = DefaultComponentPresetId,
            ["overrides"] = new JsonObject
            {
                ["label"] = new JsonObject
                {
                    ["dimensionMode"] = "content",
                    ["padding"] = "theme.spacing.none|theme.spacing.none",
                    ["textColorToken"] = colorToken,
                    ["textTypography"] = JsonNode.Parse(TypographyStyleValue.CreateDefault(sizeToken)),
                    ["textAlign"] = textAlign,
                    ["surfaceSlot"] = new JsonObject
                    {
                        ["presetId"] = DefaultComponentPresetId,
                        ["overrides"] = new JsonObject
                        {
                            ["surface"] = new JsonObject
                            {
                                ["backgroundAlpha"] = 0,
                                ["borderAlpha"] = 0,
                            },
                            ["style"] = new JsonObject
                            {
                                ["borderWidth"] = 0,
                                ["shadowEnabled"] = false,
                                ["reliefEnabled"] = false,
                            },
                        },
                    },
                },
            },
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
            case "componentStack":
                config.Remove("style");
                config["componentStack"] = new JsonObject();
                break;
            case "collectionStack":
                config.Remove("style");
                config["collectionStack"] = new JsonObject();
                break;
            case "badge":
                config.Remove("style");
                config["badge"] = new JsonObject
                {
                    ["textTypography"] = JsonNode.Parse(TypographyStyleValue.CreateDefault("theme.typography.sizes.xs")),
                    ["paddingToken"] = "theme.spacing.xs",
                    ["placement"] = JsonNode.Parse("""{"mode":"center","alignX":1,"alignY":0,"offsetX":0,"offsetY":0}"""),
                };
                break;
            case "notification":
                config.Remove("style");
                config["notification"] = new JsonObject
                {
                    ["dimensionMode"] = "content",
                    ["size"] = "320|88",
                    ["padding"] = "theme.spacing.m|theme.spacing.m",
                    ["gapToken"] = "theme.spacing.m",
                    ["avatarPlacement"] = JsonNode.Parse("""{"mode":"center","alignX":0.25,"alignY":0.5,"offsetX":0,"offsetY":0}"""),
                    ["labelPlacement"] = JsonNode.Parse("""{"mode":"center","alignX":0.75,"alignY":0.5,"offsetX":0,"offsetY":0}"""),
                    ["avatarInputs"] = DefaultAvatarBadgeInputs(),
                    ["surfaceSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["avatarSlot"] = new JsonObject
                    {
                        ["presetId"] = DefaultComponentPresetId,
                        ["overrides"] = new JsonObject
                        {
                            ["avatar"] = new JsonObject
                            {
                                ["labelSlot"] = new JsonObject
                                {
                                    ["showLabel"] = false,
                                    ["showSubtext"] = false,
                                },
                            },
                        },
                    },
                    ["summaryLabelSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["detailLabelSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                };
                break;
            case "notifications":
                config.Remove("style");
                config["notifications"] = new JsonObject
                {
                    ["collectionStackSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["notificationSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["notificationInputs"] = new JsonObject { ["maxWidth"] = 90 },
                    ["closedItemLimit"] = 3,
                    ["distributionMode"] = "stacked",
                    ["sizingMode"] = "content",
                    ["startGapToken"] = "theme.spacing.none",
                    ["endGapToken"] = "theme.spacing.none",
                    ["stackDirection"] = "down",
                    ["stackOffsetToken"] = "theme.spacing.m",
                    ["itemSizingMode"] = "largest",
                    ["scaleRatio"] = 1,
                    ["opacityRatio"] = 1,
                    ["itemAlignment"] = "center",
                    ["itemGapBeforeMode"] = "fixed",
                    ["itemGapBeforeToken"] = "theme.spacing.m",
                    ["itemGapBeforeWeight"] = 1,
                    ["itemPresenceMotion"] = JsonNode.Parse(MotionVariantValue.Default.ToJsonString()),
                    ["showBadge"] = true,
                    ["distributionMotion"] = JsonNode.Parse(MotionVariantValue.Default.ToJsonString()),
                    ["badgeSlot"] = new JsonObject
                    {
                        ["presetId"] = DefaultComponentPresetId,
                        ["overrides"] = new JsonObject(),
                    },
                    ["badgeInputs"] = new JsonObject
                    {
                        ["size"] = 20,
                        ["backgroundPaletteColor"] = "blue",
                        ["contentPaletteColor"] = "gray_100",
                    },
                };
                break;
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
                    ["badgeSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
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
            case "keypad":
                config.Remove("style");
                config["keypad"] = new JsonObject
                {
                    ["sizingMode"] = "content",
                    ["columns"] = 3,
                    ["keySize"] = "72|72",
                    ["padding"] = "theme.spacing.none|theme.spacing.none",
                    ["columnGapToken"] = "theme.spacing.l",
                    ["rowGapToken"] = "theme.spacing.l",
                    ["iconSizeToken"] = "theme.iconSizes.m",
                    ["keys"] = DefaultKeypadKeys(),
                    ["labelSlot"] = KeypadLabelSlot("theme.colors.textPrimary"),
                    ["states"] = new JsonObject
                    {
                        ["normal"] = KeypadStateStyle("theme.colors.textPrimary"),
                        ["active"] = KeypadStateStyle("theme.colors.accent"),
                        ["pushed"] = KeypadStateStyle("theme.colors.accent"),
                        ["disabled"] = KeypadStateStyle("theme.colors.textSecondary"),
                    },
                };
                break;
            case "codeIndicator":
                config.Remove("style");
                config["codeIndicator"] = new JsonObject
                {
                    ["displayMode"] = "visible",
                    ["glyphSize"] = "16|16",
                    ["gapToken"] = "theme.spacing.m",
                    ["states"] = new JsonObject
                    {
                        ["initial"] = CodeIndicatorStateSlots("theme.colors.surface", "theme.colors.textPrimary"),
                        ["correct"] = CodeIndicatorStateSlots("theme.colors.surface", "theme.colors.accent"),
                        ["incorrect"] = CodeIndicatorStateSlots("theme.colors.surface", "theme.colors.textSecondary"),
                    },
                };
                break;
            case "fingerprint":
                config.Remove("style");
                config["fingerprint"] = new JsonObject
                {
                    ["size"] = "120|120",
                    ["iconToken"] = "print",
                    ["iconSizeToken"] = "theme.iconSizes.xl",
                    ["iconSizeMultiplier"] = 1,
                    ["scanLineThickness"] = 3,
                    ["states"] = new JsonObject
                    {
                        ["initial"] = RecognitionStateStyle("theme.colors.textSecondary"),
                        ["active"] = RecognitionStateStyle("theme.colors.accent"),
                        ["correct"] = RecognitionStateStyle("theme.colors.accent"),
                        ["incorrect"] = RecognitionStateStyle("theme.colors.textSecondary"),
                    },
                };
                break;
            case "faceRecognition":
                config.Remove("style");
                config["faceRecognition"] = new JsonObject
                {
                    ["size"] = "140|140",
                    ["iconToken"] = "face",
                    ["iconSizeToken"] = "theme.iconSizes.xl",
                    ["iconSizeMultiplier"] = 1,
                    ["strokeWidth"] = 3,
                    ["states"] = new JsonObject
                    {
                        ["initial"] = RecognitionStateStyle("theme.colors.textSecondary"),
                        ["active"] = RecognitionStateStyle("theme.colors.accent"),
                        ["correct"] = RecognitionStateStyle("theme.colors.accent"),
                        ["incorrect"] = RecognitionStateStyle("theme.colors.textSecondary"),
                    },
                };
                break;
            case "drawPassword":
                config.Remove("style");
                config["drawPassword"] = new JsonObject
                {
                    ["grid"] = "3|3",
                    ["nodeSize"] = 18,
                    ["columnGapToken"] = "theme.spacing.xl",
                    ["rowGapToken"] = "theme.spacing.xl",
                    ["lineWidth"] = 3,
                    ["states"] = new JsonObject
                    {
                        ["initial"] = DrawPasswordStateStyle("theme.colors.textSecondary"),
                        ["active"] = DrawPasswordStateStyle("theme.colors.accent"),
                        ["correct"] = DrawPasswordStateStyle("theme.colors.accent"),
                        ["incorrect"] = DrawPasswordStateStyle("theme.colors.textSecondary"),
                    },
                };
                break;
            case "password":
                config.Remove("style");
                config["password"] = new JsonObject
                {
                    ["mode"] = "pin",
                    ["initialText"] = "Enter password",
                    ["correctText"] = "Password correct",
                    ["incorrectText"] = "Password incorrect",
                    ["upperAnchor"] = "container",
                    ["lowerAnchor"] = "container",
                    ["labelIndicatorGapToken"] = "theme.spacing.l",
                    ["startGapToken"] = "theme.spacing.xl",
                    ["upperGapToken"] = "theme.spacing.xl",
                    ["lowerGapToken"] = "theme.spacing.l",
                    ["endGapToken"] = "theme.spacing.l",
                    ["iconBarHeight"] = 52,
                    ["initialLabelSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["correctLabelSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["incorrectLabelSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["indicatorSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["keypadSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["fingerprintSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["faceRecognitionSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["drawPasswordSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["iconBarSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
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
                    ["badgeSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
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
                    ["textGapToken"] = "theme.spacing.xs",
                    ["reserveSubtextSpace"] = false,
                    ["subtextVerticalPosition"] = "bottom",
                    ["subtextHorizontalAlign"] = "center",
                    ["subtextColorToken"] = "theme.colors.textSecondary",
                    ["subtextTypography"] = JsonNode.Parse(TypographyStyleValue.CreateDefault("theme.typography.sizes.xs")),
                };
                break;
            case "audio":
                config["audio"] = new JsonObject
                {
                    ["padding"] = "theme.spacing.l|theme.spacing.m",
                    ["surfaceSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["durationLabelSlot"] = CalculatedLabelSlot("theme.icons.secondary", "theme.typography.sizes.xs", "right"),
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
                        ["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromDirectionalOutsideEdge("left", 8).ToJsonString()),
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
                        ["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromDirectionalOutsideEdge("top", -4).ToJsonString()),
                        ["overrides"] = new JsonObject(),
                    },
                    ["avatarSlot"] = new JsonObject
                    {
                        ["showAvatar"] = false,
                        ["presetId"] = DefaultComponentPresetId,
                        ["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromDirectionalOutsideEdge("left", 8).ToJsonString()),
                        ["overrides"] = new JsonObject(),
                    },
                    ["status"] = new JsonObject
                    {
                        ["sizeToken"] = "theme.iconSizes.s",
                        ["textSizeToken"] = "theme.iconSizes.s",
                        ["gapToken"] = "theme.spacing.xs",
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
        if (componentType == "componentStack")
        {
            return ComponentStackDesignPreview().ToJsonString();
        }
        if (componentType == "collectionStack")
        {
            return CollectionStackDesignPreview().ToJsonString();
        }
        if (componentType == "notifications")
        {
            return NotificationsDesignPreview().ToJsonString();
        }

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
                "notification" => "New notification",
                _ => "Sample",
            },
            ["sampleSubtext"] = componentType is "label" or "avatar" ? "Subtitle" : componentType == "notification" ? "Now" : "",
            ["sampleSize"] = 256,
            ["inputs"] = ComponentInputsForComponent(componentType),
        };
        if (componentType == "surface")
        {
            preview["size"] = "180|104";
        }
        if (componentType == "label")
        {
            preview["textMode"] = "literal";
            preview["textSizeMultiplier"] = 1;
            preview["subtextMode"] = "literal";
            preview["subtextSizeMultiplier"] = 1;
        }
        if (componentType == "notification")
        {
            preview["maxWidth"] = 90;
            preview["actorId"] = "actor_alex";
            preview["displayModeTransition"] = false;
            preview["displayModeElapsedMs"] = 0;
            preview["displayModeFrom"] = "summary";
            preview["actions"] = new JsonArray
            {
                ReflowTargetAction("changeDisplayMode", "Display mode", "displayMode", "displayModeTransition", "displayModeElapsedMs", "displayModeFrom"),
            };
        }
        if (componentType == "badge")
        {
            preview["contentMode"] = "icon";
            preview["iconToken"] = "system_check";
            preview["text"] = "3";
            preview["size"] = 20;
            preview["backgroundPaletteColor"] = "blue";
            preview["contentPaletteColor"] = "gray_100";
        }
        if (componentType is "avatar" or "button")
        {
            preview["showBadge"] = false;
            preview["badgeContentMode"] = "icon";
            preview["badgeIconToken"] = "system_check";
            preview["badgeText"] = "1";
            preview["badgeSize"] = 20;
            preview["badgeBackgroundPaletteColor"] = "blue";
            preview["badgeContentPaletteColor"] = "gray_100";
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
            preview.Remove("sampleText");
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
                    ["targetInputId"] = "isFullScreen",
                    ["targetMode"] = "toggle",
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
        if (componentType == "keypad")
        {
            preview["availableWidth"] = 280;
            preview["activeKey"] = "";
            preview["pushedKey"] = "";
            preview["enabled"] = true;
            preview["pushTrigger"] = false;
            preview["pushElapsedMs"] = 0;
            preview["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "pushKey",
                    ["label"] = "Push key",
                    ["playInputId"] = "pushTrigger",
                    ["targetInputId"] = "pushedKey",
                    ["targetMode"] = "option",
                    ["targetOptions"] = new JsonArray(DefaultKeypadKeys().OfType<JsonObject>().Select((key) =>
                        (JsonNode?)new JsonObject
                        {
                            ["value"] = key["value"]?.DeepClone(),
                            ["label"] = key["text"]?.DeepClone(),
                        }).ToArray()),
                    ["durationThemeToken"] = "theme.motion.buttonPushedDurationMs",
                    ["timeJsonKey"] = "pushElapsedMs",
                    ["timeUnit"] = "milliseconds",
                    ["prewarmFrames"] = false,
                    ["completionBehavior"] = "holdFinal",
                },
            };
        }
        if (componentType == "codeIndicator")
        {
            preview["count"] = 4;
            preview["filledCount"] = 2;
            preview["state"] = "initial";
        }
        if (componentType == "fingerprint" || componentType == "faceRecognition")
        {
            preview["state"] = "active";
            preview["progress"] = 0.5;
        }
        if (componentType == "drawPassword")
        {
            preview["state"] = "active";
            preview["pattern"] = "1258";
            preview["visibleCount"] = 3;
        }
        if (componentType == "password")
        {
            preview["expectedPassword"] = "2345";
            preview["attemptPassword"] = "2345";
            preview["enabled"] = true;
            preview["entryTiming"] = new JsonObject
            {
                ["mode"] = "fixed",
                ["fixedFrames"] = 16,
                ["paceToken"] = "theme.motion.naturalPace.normal",
            };
            preview["entryTrigger"] = false;
            preview["entryFrame"] = 0;
            preview["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "enterPassword",
                    ["label"] = "Enter password",
                    ["playInputId"] = "entryTrigger",
                    ["durationBehaviorTimingInputId"] = "entryTiming",
                    ["timeJsonKey"] = "entryFrame",
                    ["timeUnit"] = "frames",
                    ["prewarmFrames"] = false,
                    ["completionBehavior"] = "holdFinal",
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
                    ["targetInputId"] = "isFullScreen",
                    ["targetMode"] = "toggle",
                    ["durationSeconds"] = 0.3,
                    ["durationMotionConfigPath"] = "media.motion",
                    ["timeJsonKey"] = "motionElapsedMs",
                    ["timeUnit"] = "milliseconds",
                    ["prewarmFrames"] = false,
                },
            };
        }

        NormalizePreviewActionCompletion(preview);
        return preview.ToJsonString();
    }

    private static JsonObject ComponentStackDesignPreview(
        JsonArray? items = null,
        string sizingMode = "fill",
        string startGapToken = "theme.spacing.none",
        string endGapToken = "theme.spacing.none") => new()
    {
        ["componentType"] = "componentStack",
        ["sizingMode"] = sizingMode,
        ["startGapToken"] = startGapToken,
        ["endGapToken"] = endGapToken,
        ["inputs"] = ComponentStackRuntimeInputs(),
        ["items"] = items ?? new JsonArray(),
        ["collections"] = new JsonArray { ComponentStackSlotCollectionDefinition() },
    };

    private static JsonObject CollectionStackDesignPreview(
        JsonArray? items = null,
        string distributionMode = "stacked",
        string sizingMode = "content",
        string startGapToken = "theme.spacing.none",
        string endGapToken = "theme.spacing.none",
        string stackDirection = "down",
        string stackOffsetToken = "theme.spacing.m",
        string itemSizingMode = "intrinsic",
        decimal scaleRatio = 1,
        decimal opacityRatio = 1) => new()
    {
        ["componentType"] = "collectionStack",
        ["distributionMode"] = distributionMode,
        ["sizingMode"] = sizingMode,
        ["startGapToken"] = startGapToken,
        ["endGapToken"] = endGapToken,
        ["stackDirection"] = stackDirection,
        ["stackOffsetToken"] = stackOffsetToken,
        ["itemSizingMode"] = itemSizingMode,
        ["scaleRatio"] = scaleRatio,
        ["opacityRatio"] = opacityRatio,
        ["inputs"] = CollectionStackRuntimeInputs(),
        ["items"] = items ?? new JsonArray(),
        ["collections"] = new JsonArray { ComponentCollectionDefinition("*,-collectionStack") },
        ["distributionTransition"] = false,
        ["distributionElapsedMs"] = 0,
        ["distributionFrom"] = distributionMode,
        ["actions"] = new JsonArray
        {
            ReflowTargetAction("changeDistribution", "Distribution", "distributionMode", "distributionTransition", "distributionElapsedMs", "distributionFrom"),
        },
    };

    private static JsonObject NotificationsDesignPreview(
        JsonArray? items = null,
        string distributionMode = "stacked") => new()
    {
        ["componentType"] = "notifications",
        ["distributionMode"] = distributionMode,
        ["inputs"] = NotificationsRuntimeInputs(),
        ["items"] = items ?? new JsonArray(),
        ["collections"] = new JsonArray { NotificationsCollectionDefinition() },
        ["distributionTransition"] = false,
        ["distributionElapsedMs"] = 0,
        ["distributionFrom"] = distributionMode,
        ["actions"] = new JsonArray
        {
            ReflowTargetAction("changeDistribution", "Distribution", "distributionMode", "distributionTransition", "distributionElapsedMs", "distributionFrom"),
        },
    };

    private static JsonObject NotificationsCollectionDefinition()
    {
        var definition = ComponentCollectionDefinition("notification");
        definition["label"] = "Notifications";
        definition["itemLabel"] = "Notification";
        definition.Remove("componentItems");
        var fields = definition["fields"]?.AsArray()
            ?? throw new InvalidOperationException("Missing component collection fields.");
        var variantOwnedFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "presetId", "presenceMotion", "alignment", "gapBeforeMode", "gapBeforeToken", "gapBeforeWeight",
        };
        for (var index = fields.Count - 1; index >= 0; index--)
        {
            if (fields[index] is JsonObject field
                && field["id"] is JsonValue idValue
                && idValue.TryGetValue<string>(out var id)
                && variantOwnedFields.Contains(id))
            {
                fields.RemoveAt(index);
            }
        }
        fields.Insert(0, ComponentInput("actorId", "Actor", "actorId", "recordReference", "actor_alex", tableId: "actors", resolvedJsonKey: "actor"));
        fields.Insert(1, AnimatableComponentInput(ComponentInput(
            "displayMode",
            "Display mode",
            "displayMode",
            "option",
            "summary",
            options: [new("summary", "Summary"), new("detail", "Detail")])));
        fields.Insert(2, ComponentInput("summaryText", "Summary text", "summaryText", "text", "New notification"));
        fields.Insert(3, ComponentInput("summarySubtext", "Summary subtext", "summarySubtext", "text", "Now"));
        fields.Insert(4, ComponentInput("detailText", "Detail text", "detailText", "text", "New notification"));
        fields.Insert(5, ComponentInput("detailSubtext", "Detail subtext", "detailSubtext", "text", "Notification detail"));
        var actions = definition["itemActions"]?.AsArray()
            ?? throw new InvalidOperationException("Missing component collection item actions.");
        actions.Add(ReflowTargetAction(
            "changeDisplayMode",
            "Display mode",
            "displayMode",
            "displayModeTransition",
            "displayModeElapsedMs",
            "displayModeFrom"));
        definition["itemPresentation"] = new JsonObject
        {
            ["subtitleFieldIds"] = new JsonArray("summaryText", "actorId", "present"),
            ["subtitleMaxCharacters"] = 72,
            ["fallbackIcon"] = "component",
        };
        return definition;
    }

    private static JsonObject ReflowTargetAction(
        string id,
        string label,
        string targetInputId,
        string playInputId,
        string timeJsonKey,
        string targetFromJsonKey) => new()
    {
        ["id"] = id,
        ["label"] = label,
        ["playInputId"] = playInputId,
        ["targetInputId"] = targetInputId,
        ["targetMode"] = "option",
        ["targetFromJsonKey"] = targetFromJsonKey,
        ["durationThemeToken"] = "theme.motion.reflowDurationMs",
        ["timeJsonKey"] = timeJsonKey,
        ["timeUnit"] = "milliseconds",
        ["prewarmFrames"] = false,
        ["completionBehavior"] = "reset",
    };

    private static JsonObject ComponentCollectionDefinition(string componentTypeFilter) => new()
    {
        ["id"] = "items",
        ["label"] = "Components",
        ["jsonKey"] = "items",
        ["itemLabel"] = "Component",
        ["componentItems"] = new JsonObject
        {
            ["presetJsonKey"] = "presetId",
            ["overridesJsonKey"] = "overrides",
            ["inputsJsonKey"] = "inputs",
        },
        ["fields"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "presetId", ["label"] = "Component", ["jsonKey"] = "presetId",
                ["kind"] = "componentPreset", ["defaultValue"] = "", ["componentType"] = componentTypeFilter,
            },
            new JsonObject
            {
                ["id"] = "present", ["label"] = "Present", ["jsonKey"] = "present",
                ["kind"] = "boolean", ["defaultValue"] = "true",
                ["animatable"] = true, ["animationInterpolations"] = new JsonArray("hold"),
            },
            new JsonObject
            {
                ["id"] = "presenceMotion", ["label"] = "Presence transition", ["jsonKey"] = "presenceMotion",
                ["kind"] = "text", ["valueKind"] = "Motion",
                ["defaultValue"] = MotionVariantValue.Default.ToJsonString(),
            },
            new JsonObject
            {
                ["id"] = "alignment", ["label"] = "Alignment", ["jsonKey"] = "alignment",
                ["kind"] = "option", ["defaultValue"] = "center",
                ["options"] = new JsonArray
                {
                    new JsonObject { ["value"] = "start", ["label"] = "Left" },
                    new JsonObject { ["value"] = "center", ["label"] = "Center" },
                    new JsonObject { ["value"] = "end", ["label"] = "Right" },
                },
            },
            new JsonObject
            {
                ["id"] = "gapBeforeMode", ["label"] = "Gap before", ["jsonKey"] = "gapBeforeMode",
                ["kind"] = "option", ["defaultValue"] = "fixed", ["minimumItemIndex"] = 1,
                ["options"] = new JsonArray
                {
                    new JsonObject { ["value"] = "fixed", ["label"] = "Fixed" },
                    new JsonObject { ["value"] = "reflow", ["label"] = "Reflow" },
                },
            },
            new JsonObject
            {
                ["id"] = "gapBeforeToken", ["label"] = "Fixed gap before", ["jsonKey"] = "gapBeforeToken",
                ["kind"] = "themeToken", ["defaultValue"] = "theme.spacing.m", ["minimumItemIndex"] = 1,
                ["options"] = new JsonArray(ComponentClassFieldCatalog.SpacingTokenOptions
                    .Select((option) => (JsonNode?)new JsonObject { ["value"] = option.Value, ["label"] = option.Label }).ToArray()),
                ["enabledWhenItemJsonKey"] = "gapBeforeMode",
                ["enabledWhenItemValues"] = new JsonArray("fixed"),
            },
            new JsonObject
            {
                ["id"] = "gapBeforeWeight", ["label"] = "Reflow gap before weight", ["jsonKey"] = "gapBeforeWeight",
                ["kind"] = "number", ["valueKind"] = "decimal", ["defaultValue"] = "1", ["minimumItemIndex"] = 1,
                ["minimum"] = 0.01, ["maximum"] = 100, ["increment"] = 0.1,
                ["enabledWhenItemJsonKey"] = "gapBeforeMode",
                ["enabledWhenItemValues"] = new JsonArray("reflow"),
            },
        },
        ["itemActions"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "togglePresent",
                ["label"] = "Presence",
                ["playInputId"] = "presenceTransition",
                ["targetInputId"] = "present",
                ["targetMode"] = "toggle",
                ["durationSeconds"] = 0.3,
                ["timeJsonKey"] = "presenceElapsedMs",
                ["timeUnit"] = "milliseconds",
                ["prewarmFrames"] = false,
                ["completionBehavior"] = "reset",
            },
        },
        ["itemPresentation"] = new JsonObject
        {
            ["subtitleFieldIds"] = new JsonArray("presetId", "present", "alignment"),
            ["subtitleMaxCharacters"] = 72,
            ["fallbackIcon"] = "component",
        },
    };

    private static JsonObject ComponentStackSlotCollectionDefinition() => new()
    {
        ["id"] = "items",
        ["label"] = "Slots",
        ["jsonKey"] = "items",
        ["itemLabel"] = "Slot",
        ["fields"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "alternatives", ["label"] = "States", ["jsonKey"] = "alternatives",
                ["kind"] = "text", ["valueKind"] = "StructuredCollection", ["defaultValue"] = "[]",
                ["structuredCollection"] = ComponentStackAlternativeCollectionDefinition(),
            },
            new JsonObject
            {
                ["id"] = "alignment", ["label"] = "Alignment", ["jsonKey"] = "alignment",
                ["kind"] = "option", ["defaultValue"] = "center",
                ["options"] = new JsonArray
                {
                    new JsonObject { ["value"] = "start", ["label"] = "Left" },
                    new JsonObject { ["value"] = "center", ["label"] = "Center" },
                    new JsonObject { ["value"] = "end", ["label"] = "Right" },
                },
            },
            new JsonObject
            {
                ["id"] = "gapBeforeMode", ["label"] = "Gap before", ["jsonKey"] = "gapBeforeMode",
                ["kind"] = "option", ["defaultValue"] = "fixed", ["minimumItemIndex"] = 1,
                ["options"] = new JsonArray
                {
                    new JsonObject { ["value"] = "fixed", ["label"] = "Fixed" },
                    new JsonObject { ["value"] = "reflow", ["label"] = "Reflow" },
                },
            },
            new JsonObject
            {
                ["id"] = "gapBeforeToken", ["label"] = "Fixed gap before", ["jsonKey"] = "gapBeforeToken",
                ["kind"] = "themeToken", ["defaultValue"] = "theme.spacing.m", ["minimumItemIndex"] = 1,
                ["options"] = new JsonArray(ComponentClassFieldCatalog.SpacingTokenOptions
                    .Select((option) => (JsonNode?)new JsonObject { ["value"] = option.Value, ["label"] = option.Label }).ToArray()),
                ["enabledWhenItemJsonKey"] = "gapBeforeMode",
                ["enabledWhenItemValues"] = new JsonArray("fixed"),
            },
            new JsonObject
            {
                ["id"] = "gapBeforeWeight", ["label"] = "Reflow gap before weight", ["jsonKey"] = "gapBeforeWeight",
                ["kind"] = "number", ["valueKind"] = "decimal", ["defaultValue"] = "1", ["minimumItemIndex"] = 1,
                ["minimum"] = 0.01, ["maximum"] = 100, ["increment"] = 0.1,
                ["enabledWhenItemJsonKey"] = "gapBeforeMode",
                ["enabledWhenItemValues"] = new JsonArray("reflow"),
            },
        },
        ["itemPresentation"] = new JsonObject
        {
            ["subtitleFieldIds"] = new JsonArray("alignment"),
            ["subtitleMaxCharacters"] = 72,
            ["fallbackIcon"] = "component",
        },
    };

    private static JsonObject ComponentStackAlternativeCollectionDefinition() => new()
    {
        ["id"] = "alternatives",
        ["label"] = "States",
        ["jsonKey"] = "alternatives",
        ["itemLabel"] = "State",
        ["componentItems"] = new JsonObject
        {
            ["presetJsonKey"] = "presetId",
            ["overridesJsonKey"] = "overrides",
            ["inputsJsonKey"] = "inputs",
        },
        ["fields"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "presetId", ["label"] = "Component", ["jsonKey"] = "presetId",
                ["kind"] = "componentPreset", ["defaultValue"] = "", ["componentType"] = "*,-componentStack",
                ["allowEmpty"] = true,
            },
            new JsonObject
            {
                ["id"] = "active", ["label"] = "Active", ["jsonKey"] = "active",
                ["kind"] = "boolean", ["defaultValue"] = "false", ["minimumItemIndex"] = 1,
                ["animatable"] = true, ["animationInterpolations"] = new JsonArray("hold"),
            },
            new JsonObject
            {
                ["id"] = "behavior", ["label"] = "Behavior", ["jsonKey"] = "behavior",
                ["kind"] = "option", ["defaultValue"] = "replace", ["minimumItemIndex"] = 1,
                ["options"] = new JsonArray
                {
                    new JsonObject { ["value"] = "replace", ["label"] = "Replace" },
                    new JsonObject { ["value"] = "overlay", ["label"] = "Overlay" },
                },
            },
            new JsonObject
            {
                ["id"] = "enterMotion", ["label"] = "Enter transition", ["jsonKey"] = "enterMotion",
                ["kind"] = "text", ["valueKind"] = "Motion",
                ["defaultValue"] = MotionVariantValue.Default.ToJsonString(),
            },
            new JsonObject
            {
                ["id"] = "exitMotion", ["label"] = "Exit transition", ["jsonKey"] = "exitMotion",
                ["kind"] = "text", ["valueKind"] = "Motion",
                ["defaultValue"] = MotionVariantValue.Default.ToJsonString(),
            },
        },
        ["itemPresentation"] = new JsonObject
        {
            ["subtitleFieldIds"] = new JsonArray("presetId", "behavior"),
            ["subtitleMaxCharacters"] = 72,
            ["fallbackIcon"] = "component",
        },
    };

    private static JsonArray ComponentInputsForComponent(string componentType)
    {
        JsonArray inputs = componentType switch
        {
            "componentStack" => ComponentStackRuntimeInputs(),
            "collectionStack" => CollectionStackRuntimeInputs(),
            "notifications" => NotificationsRuntimeInputs(),
            "badge" =>
            [
                ComponentInput("contentMode", "Content", "contentMode", "option", "icon", options: [new("icon", "Icon"), new("text", "Text")]),
                ComponentInput("iconToken", "Icon", "iconToken", "icon", "system_check"),
                ComponentInput("text", "Text", "text", "text", "3"),
                ComponentInput("size", "Size", "size", ValueKind.Integer, "20", minimum: 1, maximum: 512, increment: 1),
                ComponentInput("backgroundPaletteColor", "Background", "backgroundPaletteColor", ValueKind.PaletteColorToken, "blue"),
                ComponentInput("contentPaletteColor", "Icon / text color", "contentPaletteColor", ValueKind.PaletteColorToken, "gray_100"),
            ],
            "notification" =>
            [
                ComponentInput("maxWidth", "Max width %", "maxWidth", ValueKind.Integer, "90", minimum: 1, maximum: 100, increment: 1, unit: "%"),
                ComponentInput("actorId", "Actor", "actorId", "recordReference", "actor_alex", tableId: "actors", resolvedJsonKey: "actor"),
                AnimatableComponentInput(ComponentInput("displayMode", "Display mode", "displayMode", "option", "summary", options: [new("summary", "Summary"), new("detail", "Detail")])),
                ComponentInput("summaryText", "Summary text", "summaryText", "text", "New notification"),
                ComponentInput("summarySubtext", "Summary subtext", "summarySubtext", "text", "Now"),
                ComponentInput("detailText", "Detail text", "detailText", "text", "New notification"),
                ComponentInput("detailSubtext", "Detail subtext", "detailSubtext", "text", "Notification detail"),
            ],
            "label" =>
            [
                ComponentInput("sampleText", "Text", "sampleText", "text", "Sample"),
                ComponentInput(
                    "textMode",
                    "Content source",
                    "textMode",
                    "option",
                    "literal",
                    options:
                    [
                        new FieldOption("literal", "Text"),
                        new FieldOption("countUp", "Count up"),
                        new FieldOption("countDown", "Count down"),
                    ],
                    transition: CalculatedTextTransition("sampleText")),
                ComponentInput("textSizeMultiplier", "Size multiplier", "textSizeMultiplier", ValueKind.Decimal, "1", minimum: 0.1m, maximum: 20, increment: 0.1m),
                ComponentInput("sampleSubtext", "Subtext", "sampleSubtext", "text", "Subtitle"),
                ComponentInput(
                    "subtextMode",
                    "Content source",
                    "subtextMode",
                    "option",
                    "literal",
                    options:
                    [
                        new FieldOption("literal", "Text"),
                        new FieldOption("countUp", "Count up"),
                        new FieldOption("countDown", "Count down"),
                    ],
                    transition: CalculatedTextTransition("sampleSubtext")),
                ComponentInput("subtextSizeMultiplier", "Size multiplier", "subtextSizeMultiplier", ValueKind.Decimal, "1", minimum: 0.1m, maximum: 20, increment: 0.1m),
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
                ComponentInput("leftIconRowSlot", "Variant", "leftIconRowSlot", "componentPreset", SeededComponentPresetReference("project_foqn_s2", "iconRow"), componentType: "iconRow", uiOrigin: "embedded", uiGroupId: "leftIconRow", uiGroupLabel: "Left icon row"),
                ComponentInput("leftIcons", "Icons", "leftIcons", "iconList", "[]", uiOrigin: "embedded", uiGroupId: "leftIconRow", uiGroupLabel: "Left icon row"),
                ComponentInput("rightIconRowSlot", "Variant", "rightIconRowSlot", "componentPreset", SeededComponentPresetReference("project_foqn_s2", "iconRow"), componentType: "iconRow", uiOrigin: "embedded", uiGroupId: "rightIconRow", uiGroupLabel: "Right icon row"),
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
                ComponentInput("showBadge", "Show badge", "showBadge", "boolean", "false"),
                ComponentInput("badgeContentMode", "Badge content", "badgeContentMode", "option", "icon", options: [new("icon", "Icon"), new("text", "Text")]),
                ComponentInput("badgeIconToken", "Badge icon", "badgeIconToken", "icon", "system_check"),
                ComponentInput("badgeText", "Badge text", "badgeText", "text", "1"),
                ComponentInput("badgeSize", "Badge size", "badgeSize", ValueKind.Integer, "20", minimum: 1, maximum: 512, increment: 1),
                ComponentInput("badgeBackgroundPaletteColor", "Badge background", "badgeBackgroundPaletteColor", ValueKind.PaletteColorToken, "blue"),
                ComponentInput("badgeContentPaletteColor", "Badge icon / text color", "badgeContentPaletteColor", ValueKind.PaletteColorToken, "gray_100"),
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
                ComponentInput("showBadge", "Show badge", "showBadge", "boolean", "false"),
                ComponentInput("badgeContentMode", "Badge content", "badgeContentMode", "option", "icon", options: [new("icon", "Icon"), new("text", "Text")]),
                ComponentInput("badgeIconToken", "Badge icon", "badgeIconToken", "icon", "system_check"),
                ComponentInput("badgeText", "Badge text", "badgeText", "text", "1"),
                ComponentInput("badgeSize", "Badge size", "badgeSize", ValueKind.Integer, "20", minimum: 1, maximum: 512, increment: 1),
                ComponentInput("badgeBackgroundPaletteColor", "Badge background", "badgeBackgroundPaletteColor", ValueKind.PaletteColorToken, "blue"),
                ComponentInput("badgeContentPaletteColor", "Badge icon / text color", "badgeContentPaletteColor", ValueKind.PaletteColorToken, "gray_100"),
            ],
            "textInputBar" =>
            [
                ComponentInput("availableWidth", "Available width", "availableWidth", "number", "360", minimum: 1, maximum: 10000, increment: 1),
            ],
            "keyboard" =>
            [
                ComponentInput("text", "Text", "text", "multilineText", "Hola 😀😃😄"),
                ComponentInput("currentCharacter", "Current character", "currentCharacter", "number", "1", minimum: 1, maximum: 9999, increment: 1),
                ComponentInput("trigger", "Trigger", "trigger", "boolean", "false"),
            ],
            "keypad" =>
            [
                ComponentInput("availableWidth", "Available width", "availableWidth", "number", "280", minimum: 1, maximum: 10000, increment: 1),
                ComponentInput("activeKey", "Active key", "activeKey", "text", ""),
                ComponentInput("pushedKey", "Pushed key", "pushedKey", "text", ""),
                ComponentInput("enabled", "Enabled", "enabled", "boolean", "true"),
            ],
            "codeIndicator" =>
            [
                ComponentInput("count", "Count", "count", ValueKind.Integer, "4", minimum: 1, maximum: 64, increment: 1),
                ComponentInput("filledCount", "Filled", "filledCount", ValueKind.Integer, "2", minimum: 0, maximum: 64, increment: 1),
                ComponentInput("state", "State", "state", "option", "initial", options:
                [
                    new FieldOption("initial", "Initial"),
                    new FieldOption("correct", "Correct"),
                    new FieldOption("incorrect", "Incorrect"),
                ]),
            ],
            "fingerprint" or "faceRecognition" =>
            [
                ComponentInput("state", "State", "state", "option", "active", options:
                [
                    new FieldOption("initial", "Initial"),
                    new FieldOption("active", "Active"),
                    new FieldOption("correct", "Correct"),
                    new FieldOption("incorrect", "Incorrect"),
                ]),
                ComponentInput("progress", "Progress", "progress", ValueKind.Decimal, "0.5", minimum: 0, maximum: 1, increment: 0.05m),
            ],
            "drawPassword" =>
            [
                ComponentInput("state", "State", "state", "option", "active", options:
                [
                    new FieldOption("initial", "Initial"),
                    new FieldOption("active", "Active"),
                    new FieldOption("correct", "Correct"),
                    new FieldOption("incorrect", "Incorrect"),
                ]),
                ComponentInput("pattern", "Pattern", "pattern", "text", "1258"),
                ComponentInput("visibleCount", "Visible nodes", "visibleCount", ValueKind.Integer, "3", minimum: 0, maximum: 9, increment: 1),
            ],
            "password" => PasswordRuntimeInputs(),
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

    private static JsonArray ComponentStackRuntimeInputs() =>
    [
        new JsonObject
        {
            ["id"] = "sizingMode", ["label"] = "Sizing", ["jsonKey"] = "sizingMode",
            ["kind"] = "option", ["defaultValue"] = "fill",
            ["options"] = new JsonArray
            {
                new JsonObject { ["value"] = "fill", ["label"] = "Fill container" },
                new JsonObject { ["value"] = "content", ["label"] = "Fit content" },
            },
        },
        new JsonObject
        {
            ["id"] = "startGapToken", ["label"] = "Start gap", ["jsonKey"] = "startGapToken",
            ["kind"] = "themeToken", ["defaultValue"] = "theme.spacing.none",
            ["options"] = new JsonArray(ComponentClassFieldCatalog.SpacingTokenOptions
                .Select((option) => (JsonNode?)new JsonObject { ["value"] = option.Value, ["label"] = option.Label }).ToArray()),
        },
        new JsonObject
        {
            ["id"] = "endGapToken", ["label"] = "End gap", ["jsonKey"] = "endGapToken",
            ["kind"] = "themeToken", ["defaultValue"] = "theme.spacing.none",
            ["options"] = new JsonArray(ComponentClassFieldCatalog.SpacingTokenOptions
                .Select((option) => (JsonNode?)new JsonObject { ["value"] = option.Value, ["label"] = option.Label }).ToArray()),
        },
    ];

    private static JsonArray CollectionStackRuntimeInputs() =>
    [
        new JsonObject
        {
            ["id"] = "distributionMode", ["label"] = "Distribution", ["jsonKey"] = "distributionMode",
            ["kind"] = "option", ["defaultValue"] = "stacked", ["refreshOnCommit"] = true,
            ["animatable"] = true, ["animationInterpolations"] = new JsonArray("hold"),
            ["options"] = new JsonArray
            {
                new JsonObject { ["value"] = "flow", ["label"] = "Flow" },
                new JsonObject { ["value"] = "stacked", ["label"] = "Stacked" },
            },
        },
        new JsonObject
        {
            ["id"] = "sizingMode", ["label"] = "Sizing", ["jsonKey"] = "sizingMode",
            ["kind"] = "option", ["defaultValue"] = "content",
            ["enabledWhenPath"] = "distributionMode", ["enabledWhenValue"] = "flow",
            ["options"] = new JsonArray
            {
                new JsonObject { ["value"] = "fill", ["label"] = "Fill container" },
                new JsonObject { ["value"] = "content", ["label"] = "Fit content" },
            },
        },
        new JsonObject
        {
            ["id"] = "startGapToken", ["label"] = "Start gap", ["jsonKey"] = "startGapToken",
            ["kind"] = "themeToken", ["defaultValue"] = "theme.spacing.none",
            ["options"] = new JsonArray(ComponentClassFieldCatalog.SpacingTokenOptions
                .Select((option) => (JsonNode?)new JsonObject { ["value"] = option.Value, ["label"] = option.Label }).ToArray()),
        },
        new JsonObject
        {
            ["id"] = "endGapToken", ["label"] = "End gap", ["jsonKey"] = "endGapToken",
            ["kind"] = "themeToken", ["defaultValue"] = "theme.spacing.none",
            ["options"] = new JsonArray(ComponentClassFieldCatalog.SpacingTokenOptions
                .Select((option) => (JsonNode?)new JsonObject { ["value"] = option.Value, ["label"] = option.Label }).ToArray()),
        },
        new JsonObject
        {
            ["id"] = "stackDirection", ["label"] = "Stack direction", ["jsonKey"] = "stackDirection",
            ["kind"] = "option", ["defaultValue"] = "down",
            ["options"] = new JsonArray
            {
                new JsonObject { ["value"] = "down", ["label"] = "Down" },
                new JsonObject { ["value"] = "up", ["label"] = "Up" },
            },
        },
        new JsonObject
        {
            ["id"] = "stackOffsetToken", ["label"] = "Stack offset", ["jsonKey"] = "stackOffsetToken",
            ["kind"] = "themeToken", ["defaultValue"] = "theme.spacing.m",
            ["options"] = new JsonArray(ComponentClassFieldCatalog.SpacingTokenOptions
                .Select((option) => (JsonNode?)new JsonObject { ["value"] = option.Value, ["label"] = option.Label }).ToArray()),
        },
        new JsonObject
        {
            ["id"] = "itemSizingMode", ["label"] = "Item sizing", ["jsonKey"] = "itemSizingMode",
            ["kind"] = "option", ["defaultValue"] = "intrinsic",
            ["options"] = new JsonArray
            {
                new JsonObject { ["value"] = "intrinsic", ["label"] = "Intrinsic" },
                new JsonObject { ["value"] = "largest", ["label"] = "Largest item" },
            },
        },
        new JsonObject
        {
            ["id"] = "scaleRatio", ["label"] = "Scale ratio", ["jsonKey"] = "scaleRatio",
            ["kind"] = "number", ["valueKind"] = "decimal", ["defaultValue"] = "1",
            ["minimum"] = 0.01, ["maximum"] = 1, ["increment"] = 0.01,
            ["enabledWhenPath"] = "distributionMode", ["enabledWhenValue"] = "stacked",
        },
        new JsonObject
        {
            ["id"] = "opacityRatio", ["label"] = "Opacity ratio", ["jsonKey"] = "opacityRatio",
            ["kind"] = "number", ["valueKind"] = "decimal", ["defaultValue"] = "1",
            ["minimum"] = 0, ["maximum"] = 1, ["increment"] = 0.01,
            ["enabledWhenPath"] = "distributionMode", ["enabledWhenValue"] = "stacked",
        },
    ];

    private static JsonArray NotificationsRuntimeInputs()
    {
        return
        [
            new JsonObject
            {
                ["id"] = "distributionMode", ["label"] = "Distribution", ["jsonKey"] = "distributionMode",
                ["kind"] = "option", ["defaultValue"] = "stacked", ["refreshOnCommit"] = true,
                ["animatable"] = true, ["animationInterpolations"] = new JsonArray("hold"),
                ["options"] = new JsonArray
                {
                    new JsonObject { ["value"] = "flow", ["label"] = "Flow" },
                    new JsonObject { ["value"] = "stacked", ["label"] = "Stacked" },
                },
            },
        ];
    }

    private static void ApplyComponentInputLayout(string componentType, JsonArray inputs)
    {
        switch (componentType)
        {
            case "label":
                SetComponentInputGroup(inputs, ["sampleText", "textMode", "textSizeMultiplier"], "text", "Text", 10);
                SetComponentInputGroup(inputs, ["sampleSubtext", "subtextMode", "subtextSizeMultiplier"], "subtext", "Subtext", 20);
                break;
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
                SetComponentInputGroup(inputs, ["showBadge", "badgeContentMode", "badgeIconToken", "badgeText", "badgeSize", "badgeBackgroundPaletteColor", "badgeContentPaletteColor"], "badge", "Badge", 20);
                break;
            case "badge":
                SetComponentInputGroup(inputs, ["contentMode", "iconToken", "text", "size", "backgroundPaletteColor", "contentPaletteColor"], "content", "Content", 10);
                break;
            case "button":
                SetComponentInputGroup(inputs, ["state"], "state", "State", 10);
                SetComponentInputGroup(inputs, ["contentMode", "sampleText", "iconToken", "iconSizeToken", "textSizeToken"], "content", "Content", 20);
                SetComponentInputGroup(inputs, ["pushTrigger", "pushElapsedMs"], "action", "Action", 30);
                SetComponentInputGroup(inputs, ["showBadge", "badgeContentMode", "badgeIconToken", "badgeText", "badgeSize", "badgeBackgroundPaletteColor", "badgeContentPaletteColor"], "badge", "Badge", 40);
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

    private static JsonObject CalculatedTextTransition(string targetInputId) => new()
    {
        ["targetInputId"] = targetInputId,
        ["triggerValues"] = new JsonArray("countUp", "countDown"),
        ["replacementValue"] = "00:00",
        ["targetValuePattern"] = @"^\d+:[0-5]\d$",
        ["forwardedTargetOnly"] = true,
    };

    private static JsonArray PasswordRuntimeInputs()
    {
        var timing = ComponentInput(
            "entryTiming",
            "Entry timing",
            "entryTiming",
            "behaviorTiming",
            "{\"mode\":\"fixed\",\"fixedFrames\":16,\"paceToken\":\"theme.motion.naturalPace.normal\"}",
            source: "runtime",
            valueKind: ValueKind.BehaviorTiming.ToString());
        timing["options"] = new JsonArray
        {
            new JsonObject { ["value"] = "theme.motion.naturalPace.verySlow", ["label"] = "Very slow" },
            new JsonObject { ["value"] = "theme.motion.naturalPace.slow", ["label"] = "Slow" },
            new JsonObject { ["value"] = "theme.motion.naturalPace.normal", ["label"] = "Normal" },
            new JsonObject { ["value"] = "theme.motion.naturalPace.fast", ["label"] = "Fast" },
            new JsonObject { ["value"] = "theme.motion.naturalPace.veryFast", ["label"] = "Very fast" },
        };
        timing["naturalTiming"] = new JsonObject
        {
            ["sourceFieldId"] = "attemptPassword",
            ["unit"] = "grapheme",
            ["baseFramesPerUnit"] = 4,
        };

        return
        [
            ComponentInput("expectedPassword", "Password", "expectedPassword", "text", "2345"),
            ComponentInput("attemptPassword", "Attempt", "attemptPassword", "text", "2345"),
            ComponentInput("enabled", "Enabled", "enabled", "boolean", "true"),
            timing,
            ComponentInput("entryTrigger", "Enter password", "entryTrigger", "boolean", "false", source: "calculated"),
            ComponentInput("entryFrame", "Entry frame", "entryFrame", ValueKind.Integer, "0", minimum: 0, maximum: 100000, increment: 1, source: "calculated", unit: "frames"),
        ];
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
        string unit = "",
        JsonObject? transition = null)
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
            unit,
            transition);
    }

    private static JsonObject AnimatableComponentInput(JsonObject input)
    {
        input["animatable"] = true;
        input["animationInterpolations"] = new JsonArray("hold");
        return input;
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
        string unit = "",
        JsonObject? transition = null)
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
            ["transition"] = transition,
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
            ["sampleText"] = "Message",
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
            [RuntimeInputForwardingContract.StorageKey] = new JsonObject
            {
                ["sampleText"] = ForwardedTextBoxSampleTextDefinition(),
            },
        };
    }

    private static JsonObject ForwardedTextBoxSampleTextDefinition() => new()
    {
        ["id"] = "forwarded.component.textInput.textBox.inputs.sampleText",
        ["label"] = "Text",
        ["jsonKey"] = "forwarded_component_textInput_textBox_inputs_sampleText",
        ["kind"] = "multilineText",
        ["valueKind"] = ValueKind.StringMultiline.ToString(),
        ["defaultValue"] = "Message",
        ["source"] = "runtime",
        ["componentType"] = "",
        ["options"] = new JsonArray(),
    };

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
        NewComponentSeed("componentStack", "component.componentStack", "Default Component Stack"),
        NewComponentSeed("collectionStack", "component.collectionStack", "Default Collection Stack"),
        NewComponentSeed("badge", "component.badge", "Default Badge"),
        NewComponentSeed("notification", "component.notification", "Default Notification"),
        NewComponentSeed("notifications", "component.notifications", "Default Notifications"),
        NewComponentSeed("status_bar", "component.status_bar", "Default Status Bar"),
        NewComponentSeed("navigation_bar", "component.navigation_bar", "Default Navigation Bar"),
        NewComponentSeed("textInputBar", "component.textInputBar", "Default Text Input Bar"),
        NewComponentSeed("keyboard", "component.keyboard", "Default Keyboard"),
        NewComponentSeed("keypad", "component.keypad", "Default Keypad"),
        NewComponentSeed("codeIndicator", "component.codeIndicator", "Default Code Indicator"),
        NewComponentSeed("fingerprint", "component.fingerprint", "Default Fingerprint"),
        NewComponentSeed("faceRecognition", "component.faceRecognition", "Default Face Recognition"),
        NewComponentSeed("drawPassword", "component.drawPassword", "Default Draw Password"),
        NewComponentSeed("password", "component.password", "Default Password"),
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
