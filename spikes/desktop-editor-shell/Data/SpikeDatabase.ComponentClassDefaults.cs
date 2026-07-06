using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
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
            "textInputBar" => "Text input bar component",
            "keyboard" => "Keyboard component",
            "buttonIcon" => "Button icon component",
            "label" => "Label component",
            "audio" => "Audio component",
            "video" => "Video component",
            _ => componentType,
        };
    }

    private static ComponentSeedRow NewComponentSeed(string componentType, string recordClassId, string name)
    {
        return new ComponentSeedRow(
            componentType,
            recordClassId,
            name,
            DefaultComponentClassConfigJson(componentType),
            DefaultComponentDesignPreviewJson(componentType),
            JsonSerializer.Serialize(new { note = "Seeded reusable component class." }));
    }

    private static JsonObject ComponentSurfaceSlot(string presetName)
    {
        return new JsonObject
        {
            ["presetId"] = presetName,
            ["overrides"] = new JsonObject(),
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
                ["cornerRadiusToken"] = componentType == "avatar" ? "theme.radii.avatar" : "theme.radii.surface",
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
                };
                break;
            case "cursor":
                config["cursor"] = new JsonObject
                {
                    ["colorToken"] = "theme.cursor.color",
                    ["width"] = 2,
                    ["minimumFade"] = 0.15,
                    ["fadeFrames"] = 12,
                };
                break;
            case "avatar":
                config["avatar"] = new JsonObject
                {
                    ["defaultSize"] = 48,
                    ["cornerRadiusToken"] = "theme.radii.avatar",
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
            case "textInputBar":
                config["textInput"] = new JsonObject
                {
                    ["height"] = 44,
                    ["barPadding"] = "12|8",
                    ["textPadding"] = "14|0",
                    ["iconGap"] = 8,
                    ["placeholder"] = "Message",
                    ["surfaceSlot"] = ComponentSurfaceSlot("InputBox"),
                    ["idleTextColorToken"] = "theme.colors.textSecondary",
                    ["textSizeToken"] = "theme.typography.sizes.s",
                    ["cursorColorToken"] = "theme.cursor.color",
                    ["cursorWidth"] = 2,
                    ["cursorBlinkFrames"] = 18,
                };
                break;
            case "keyboard":
                config["keyboard"] = new JsonObject
                {
                    ["backgroundColorToken"] = "theme.colors.surface",
                    ["backgroundAlpha"] = 1,
                    ["keyBackgroundColorToken"] = "theme.colors.field",
                    ["keyTextColorToken"] = "theme.colors.textPrimary",
                    ["bottomIconColorToken"] = "theme.icons.secondary",
                    ["keyPadding"] = 4,
                    ["keyCornerRadius"] = 6,
                    ["keyShadowEnabled"] = false,
                    ["pressedEffect"] = "popup",
                    ["specialKeyTextScale"] = "0.65",
                    ["emojiScale"] = "1.2",
                    ["bottomIconSlots"] = JsonNode.Parse(ComponentClassFieldCatalog.EmptyIconSlots),
                };
                break;
            case "buttonIcon":
                config["buttonIcon"] = new JsonObject
                {
                    ["size"] = 48,
                    ["iconToken"] = "media_mic",
                    ["iconPadding"] = 6,
                    ["surfaceSlot"] = ComponentSurfaceSlot("IconButton"),
                    ["iconColorToken"] = "theme.colors.icon",
                    ["labelSlot"] = new JsonObject
                    {
                        ["showLabel"] = false,
                        ["showSubtext"] = false,
                        ["presetId"] = DefaultComponentPresetId,
                        ["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromDirectionalEdge("bottom", 3).ToJsonString()),
                        ["overrides"] = new JsonObject(),
                    },
                };
                break;
            case "label":
                config["label"] = new JsonObject
                {
                    ["dimensionMode"] = "content",
                    ["size"] = "120|32",
                    ["padding"] = "8|4",
                    ["surfaceSlot"] = ComponentSurfaceSlot("Label"),
                    ["textColorToken"] = "theme.colors.textPrimary",
                    ["textSizeToken"] = "theme.typography.sizes.s",
                    ["textStyle"] = "normal",
                    ["textAlign"] = "center",
                    ["textGap"] = 2,
                    ["subtextColorToken"] = "theme.colors.textSecondary",
                    ["subtextSizeToken"] = "theme.typography.sizes.xs",
                    ["subtextStyle"] = "normal",
                };
                break;
            case "audio":
                config["audio"] = new JsonObject
                {
                    ["padding"] = "10|8",
                    ["surfaceSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["textSize"] = 11,
                    ["textColorToken"] = "theme.icons.secondary",
                    ["playCircleSize"] = 32,
                    ["playIconPadding"] = 9,
                    ["playColorToken"] = "theme.icons.accent",
                    ["playIconColorToken"] = "theme.icons.secondary",
                    ["waveformColorToken"] = "theme.icons.primary",
                    ["waveformPlayedColorToken"] = "theme.icons.accent",
                    ["waveformBarCount"] = 28,
                    ["waveformBarWidth"] = 3,
                    ["waveformGap"] = 2,
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
                                ["iconPadding"] = 3,
                            },
                        },
                    },
                };
                break;
            case "status_bar":
                return DefaultStatusBarConfigJson();
            case "navigation_bar":
                return DefaultNavigationBarConfigJson();
            case "video":
                config["video"] = new JsonObject
                {
                    ["surfaceSlot"] = ComponentSurfaceSlot(DefaultComponentPresetId),
                    ["statusVisible"] = true,
                    ["statusHeight"] = 24,
                    ["statusIconSlots"] = JsonNode.Parse("""{"left":["app_camera"],"center":[],"right":[]}"""),
                    ["statusTextColorToken"] = "theme.colors.textPrimary",
                    ["playOverlayVisible"] = true,
                    ["playColorToken"] = "theme.icons.accent",
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
                "textInputBar" => "Message",
                "audio" => "0:05",
                "video" => "0:12",
                _ => "Sample",
            },
            ["sampleSubtext"] = componentType is "label" or "avatar" or "buttonIcon" ? "Subtitle" : "",
            ["sampleSize"] = componentType == "buttonIcon" ? 48 : 256,
            ["inputs"] = ComponentInputsForComponent(componentType),
        };
        if (componentType == "surface")
        {
            preview["size"] = "180|104";
        }
        if (componentType == "cursor")
        {
            preview["height"] = 32;
        }

        if (componentType == "audio")
        {
            preview["animation"] = new JsonObject
            {
                ["playInputId"] = "isPlaying",
                ["durationInputId"] = "durationSeconds",
                ["timeJsonKey"] = "currentTimeSeconds",
            };
        }

        return preview.ToJsonString();
    }

    private static JsonArray ComponentInputsForComponent(string componentType)
    {
        return componentType switch
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
            "buttonIcon" =>
            [
                ComponentInput("iconToken", "Icon", "iconToken", "icon", "media_mic"),
                ComponentInput("sampleText", "Text", "sampleText", "text", "Action"),
                ComponentInput("sampleSubtext", "Subtext", "sampleSubtext", "text", "Subtitle"),
            ],
            "textInputBar" =>
            [
                ComponentInput("sampleText", "Text", "sampleText", "text", "Message"),
            ],
            "audio" =>
            [
                ComponentInput("isPlaying", "Playing", "isPlaying", "boolean", "false"),
                ComponentInput("durationSeconds", "Duration", "durationSeconds", "number", "65", minimum: 1, maximum: 86400, increment: 1),
                ComponentInput(
                    "actorId",
                    "Actor",
                    "actorId",
                    "recordReference",
                    "",
                    tableId: "actors",
                    resolvedJsonKey: "actor"),
            ],
            "video" =>
            [
                ComponentInput("durationText", "Duration", "sampleText", "text", "0:12"),
            ],
            _ => [],
        };
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
        string resolvedJsonKey = "")
    {
        return new JsonObject
        {
            ["id"] = id,
            ["label"] = label,
            ["jsonKey"] = jsonKey,
            ["kind"] = kind,
            ["defaultValue"] = defaultValue,
            ["minimum"] = minimum,
            ["maximum"] = maximum,
            ["increment"] = increment,
            ["tableId"] = tableId,
            ["resolvedJsonKey"] = resolvedJsonKey,
        };
    }

    private static readonly ComponentSeedRow[] ComponentSeedRows =
    [
        NewComponentSeed("avatar", "component.avatar", "Default Avatar"),
        NewComponentSeed("surface", "component.surface", "Default Surface"),
        NewComponentSeed("cursor", "component.cursor", "Default Cursor"),
        NewComponentSeed("status_bar", "component.status_bar", "Default Status Bar"),
        NewComponentSeed("navigation_bar", "component.navigation_bar", "Default Navigation Bar"),
        NewComponentSeed("textInputBar", "component.textInputBar", "Default Text Input Bar"),
        NewComponentSeed("keyboard", "component.keyboard", "Default Keyboard"),
        NewComponentSeed("buttonIcon", "component.buttonIcon", "Default Button Icon"),
        NewComponentSeed("label", "component.label", "Default Label"),
        NewComponentSeed("audio", "component.audio", "Default Audio"),
        NewComponentSeed("video", "component.video", "Default Video"),
    ];
}
