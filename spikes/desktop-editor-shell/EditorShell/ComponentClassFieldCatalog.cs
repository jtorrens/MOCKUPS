using System;
using System.Collections.Generic;
using System.Linq;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ComponentClassFieldDescriptor(
    string Id,
    string Label,
    ValueKind ValueKind,
    string[] JsonPath,
    string DefaultValue,
    bool IsEditable = true,
    IReadOnlyList<FieldOption>? Options = null,
    PairFieldLabels? PairLabels = null,
    NumberDefinition? Number = null,
    IReadOnlyList<ComponentInputBindingDefinition>? ComponentInputBindings = null,
    RuntimeInputCollectionDefinition? StructuredCollection = null,
    string ComponentPresetType = "",
    string RuntimeInputComponentPresetFieldId = "",
    string Unit = "");

internal static class ComponentClassFieldCatalog
{
    public const string EmptyIconSlots = "[]";

    private static readonly FieldOption[] ThemeColorOptions =
    [
        new("theme.colors.background", "colors.background"),
        new("theme.colors.surface", "colors.surface"),
        new("theme.colors.card", "colors.card"),
        new("theme.colors.label", "colors.label"),
        new("theme.colors.text", "colors.text"),
        new("theme.colors.textPrimary", "colors.textPrimary"),
        new("theme.colors.textSecondary", "colors.textSecondary"),
        new("theme.colors.icon", "colors.icon"),
        new("theme.colors.button", "colors.button"),
        new("theme.colors.field", "colors.field"),
        new("theme.colors.checkbox", "colors.checkbox"),
        new("theme.colors.radio", "colors.radio"),
        new("theme.colors.switch", "colors.switch"),
        new("theme.colors.tab", "colors.tab"),
        new("theme.colors.menuItem", "colors.menuItem"),
        new("theme.colors.badge", "colors.badge"),
        new("theme.colors.toast", "colors.toast"),
        new("theme.colors.divider", "colors.divider"),
        new("theme.colors.accent", "colors.accent"),
        new("theme.icons.primary", "icons.primary"),
        new("theme.icons.secondary", "icons.secondary"),
        new("theme.icons.alternate", "icons.alternate"),
        new("theme.icons.accent", "icons.accent"),
        new("theme.borders.primary", "borders.primary"),
        new("theme.borders.secondary", "borders.secondary"),
        new("theme.borders.alternate", "borders.alternate"),
        new("theme.cursor.color", "cursor.color"),
        new("theme.keyboard.background", "keyboard.background"),
        new("theme.keyboard.keyBackground", "keyboard.keyBackground"),
        new("theme.keyboard.specialKeyBackground", "keyboard.specialKeyBackground"),
        new("theme.keyboard.pressedKeyBackground", "keyboard.pressedKeyBackground"),
        new("theme.keyboard.keyBorder", "keyboard.keyBorder"),
        new("theme.keyboard.text", "keyboard.text"),
    ];

    private static readonly FieldOption[] RadiusTokenOptions =
    [
        new("theme.radii.none", "radii.none"),
        new("theme.radii.xs", "radii.xs"),
        new("theme.radii.s", "radii.s"),
        new("theme.radii.m", "radii.m"),
        new("theme.radii.l", "radii.l"),
        new("theme.radii.xl", "radii.xl"),
        new("theme.radii.xxl", "radii.xxl"),
        new("theme.radii.full", "radii.full"),
    ];

    internal static readonly FieldOption[] TypographySizeOptions =
    [
        new("theme.typography.sizes.xs", "typography.sizes.xs"),
        new("theme.typography.sizes.s", "typography.sizes.s"),
        new("theme.typography.sizes.m", "typography.sizes.m"),
        new("theme.typography.sizes.l", "typography.sizes.l"),
        new("theme.typography.sizes.xl", "typography.sizes.xl"),
    ];

    internal static readonly FieldOption[] IconSizeTokenOptions =
    [
        new("theme.iconSizes.xs", "iconSizes.xs"),
        new("theme.iconSizes.s", "iconSizes.s"),
        new("theme.iconSizes.m", "iconSizes.m"),
        new("theme.iconSizes.l", "iconSizes.l"),
        new("theme.iconSizes.xl", "iconSizes.xl"),
    ];

    private static readonly FieldOption[] PasswordAnchorOptions =
    [
        new("container", "Container"),
        new("input", "Input component"),
    ];

    private static readonly FieldOption[] PasswordModeOptions =
    [
        new("pin", "PIN"),
        new("fingerprint", "Fingerprint"),
        new("faceRecognition", "Face recognition"),
        new("drawPassword", "Draw password"),
    ];

    private static readonly FieldOption[] ButtonPushedDurationTokenOptions =
    [
        new("theme.motion.buttonPushedDurationMs", "motion.buttonPushedDurationMs"),
    ];
    private static readonly FieldOption[] IconRowSizeSourceOptions = [new("shared", "Shared"), new("perButton", "Per button")];
    private static readonly FieldOption[] IconBarSizeSourceOptions = [new("shared", "Shared"), new("perRow", "Per row")];

    internal static readonly FieldOption[] SpacingTokenOptions =
    [
        new("theme.spacing.none", "spacing.none"),
        new("theme.spacing.xs", "spacing.xs"),
        new("theme.spacing.s", "spacing.s"),
        new("theme.spacing.m", "spacing.m"),
        new("theme.spacing.l", "spacing.l"),
        new("theme.spacing.xl", "spacing.xl"),
        new("theme.spacing.xxl", "spacing.xxl"),
    ];

    private static readonly FieldOption[] KeyboardHeightTokenOptions =
    [
        new("theme.keyboard.height", "keyboard.height"),
    ];

    private static readonly FieldOption[] KeyboardKeyGapTokenOptions =
    [
        new("theme.keyboard.keyGap", "keyboard.keyGap"),
    ];

    private static readonly FieldOption[] KeyboardRowGapTokenOptions =
    [
        new("theme.keyboard.rowGap", "keyboard.rowGap"),
    ];

    private static readonly FieldOption[] TextStyleOptions =
    [
        new("normal", "Normal"),
        new("italic", "Italic"),
    ];

    private static readonly FieldOption[] TextAlignOptions =
    [
        new("left", "Left"),
        new("center", "Center"),
        new("right", "Right"),
    ];

    internal static readonly FieldOption[] BubbleStatusStateOptions =
    [
        new("none", "None"),
        new("sent", "Sent"),
        new("delivered", "Delivered"),
        new("read", "Read"),
    ];

    private static readonly FieldOption[] MediaTextOverlayModeOptions =
    [
        new("free", "Free text"),
        new("countUp", "Count up"),
        new("countDown", "Count down"),
    ];

    private static readonly FieldOption[] MediaTypeOptions =
    [
        new("none", "None"),
        new("image", "Image"),
        new("video", "Video"),
        new("audio", "Audio"),
    ];

    private static readonly FieldOption[] BubbleMediaPositionOptions =
    [
        new("top", "Top"),
        new("bottom", "Bottom"),
        new("left", "Left"),
        new("right", "Right"),
    ];

    private static readonly FieldOption[] DimensionModeOptions =
    [
        new("fixed", "Fixed size"),
        new("content", "Content + padding"),
    ];

    private static readonly FieldOption[] TextBoxDimensionModeOptions =
    [
        new("fixed", "Fixed size"),
        new("content", "Content + padding"),
        new("growVertical", "Grow vertical"),
    ];

    private static readonly FieldOption[] TextBoxOverflowOptions =
    [
        new("clip", "Clip"),
        new("scroll", "Scroll"),
    ];

    private static readonly FieldOption[] IconRowOrientationOptions =
    [
        new("horizontal", "Horizontal"),
        new("vertical", "Vertical"),
    ];

    private static readonly FieldOption[] SurfaceTailStyleOptions =
    [
        new("rounded_wedge", "Rounded wedge"),
        new("curved_hook", "Curved hook"),
        new("simple_triangle", "Simple triangle"),
        new("cut_corner", "Cut corner"),
    ];

    private static readonly FieldOption[] SurfaceTailSideOptions =
    [
        new("left", "Left"),
        new("right", "Right"),
    ];

    private static readonly FieldOption[] SurfaceTailVerticalOptions =
    [
        new("top", "Top"),
        new("bottom", "Bottom"),
    ];

    private static readonly ComponentInputBindingDefinition[] IconRowParentInputBindings =
    [
        new(
            "items",
            "Buttons",
            "items",
            ValueKind.IconSlots,
            ComponentInputBindingSource.Variant,
            "[]"),
        new(
            "gap",
            "Gap",
            "gap",
            ValueKind.ThemeToken,
            ComponentInputBindingSource.Variant,
            "theme.spacing.s",
            Options: SpacingTokenOptions),
        new(
            "orientation",
            "Orientation",
            "orientation",
            ValueKind.OptionToken,
            ComponentInputBindingSource.Variant,
            "horizontal",
            Options: IconRowOrientationOptions),
    ];

    private static readonly ComponentInputBindingDefinition[] TextBoxParentInputBindings =
    [
        new(
            "sampleText",
            "Text",
            "sampleText",
            ValueKind.StringMultiline,
            ComponentInputBindingSource.Runtime,
            "Message"),
        new(
            "placeholder",
            "Placeholder",
            "placeholder",
            ValueKind.StringSingleLine,
            ComponentInputBindingSource.Variant,
            "Message"),
        new(
            "maxLines",
            "Max lines",
            "maxLines",
            ValueKind.Integer,
            ComponentInputBindingSource.Variant,
            "4",
            Number: new NumberDefinition(1, 64, 1, 0)),
        new(
            "leftIconRowSlot",
            "Left icon row",
            "leftIconRowSlot",
            ValueKind.ComponentPreset,
            ComponentInputBindingSource.Variant,
            "",
            ComponentType: "iconRow"),
        new(
            "leftIcons",
            "Left icons",
            "leftIcons",
            ValueKind.IconTokenList,
            ComponentInputBindingSource.Variant,
            "[]"),
        new(
            "rightIconRowSlot",
            "Right icon row",
            "rightIconRowSlot",
            ValueKind.ComponentPreset,
            ComponentInputBindingSource.Variant,
            "",
            ComponentType: "iconRow"),
        new(
            "rightIcons",
            "Right icons",
            "rightIcons",
            ValueKind.IconTokenList,
            ComponentInputBindingSource.Variant,
            "[]"),
        new(
            "iconGap",
            "Icon gap",
            "iconGap",
            ValueKind.ThemeToken,
            ComponentInputBindingSource.Variant,
            "theme.spacing.m",
            Options: SpacingTokenOptions),
        new(
            "iconRowSize",
            "Icon size",
            "iconRowSize",
            ValueKind.ThemeToken,
            ComponentInputBindingSource.Variant,
            "theme.iconSizes.xl",
            Options: IconSizeTokenOptions),
        new(
            "iconRowGap",
            "Icon row gap",
            "iconRowGap",
            ValueKind.ThemeToken,
            ComponentInputBindingSource.Variant,
            "theme.spacing.s",
            Options: SpacingTokenOptions),
        new(
            "iconRowOrientation",
            "Icon row orientation",
            "iconRowOrientation",
            ValueKind.OptionToken,
            ComponentInputBindingSource.Variant,
            "horizontal",
            Options: IconRowOrientationOptions),
        new(
            "size",
            "Size",
            "size",
            ValueKind.IntegerPair,
            ComponentInputBindingSource.Calculated,
            "220|44",
            Number: new NumberDefinition(1, 9999, 1, 0)),
        new(
            "maxWidth",
            "Max width",
            "maxWidth",
            ValueKind.Integer,
            ComponentInputBindingSource.Calculated,
            "220",
            Number: new NumberDefinition(1, 9999, 1, 0)),
    ];

    internal static IReadOnlyList<ComponentInputBindingDefinition> RuntimeInputBindingsForComponent(string componentType)
    {
        return componentType switch
        {
            "iconRow" => IconRowParentInputBindings
                .Where((binding) => binding.Source == ComponentInputBindingSource.Runtime)
                .ToList(),
            _ => [],
        };
    }

    internal static IReadOnlyList<ComponentInputBindingDefinition> VariantInputBindingsForComponent(string componentType)
    {
        return componentType switch
        {
            "iconRow" => IconRowParentInputBindings
                .Where((binding) => binding.Source == ComponentInputBindingSource.Variant)
                .ToList(),
            _ => [],
        };
    }

    private static readonly FieldOption[] PressedEffectOptions =
    [
        new("popup", "Popup"),
        new("scale", "Scale in place"),
        new("none", "None"),
    ];

    private static readonly RuntimeInputCollectionDefinition KeypadKeysCollection = new(
        "keys",
        "Keys",
        "keys",
        "Key",
        [
            new ComponentInputDefinition(
                "kind", "Kind", "kind", ComponentInputKind.Option, ValueKind.OptionToken, "text",
                [new("text", "Text"), new("icon", "Icon"), new("spacer", "Spacer")]),
            new ComponentInputDefinition(
                "value", "Value", "value", ComponentInputKind.Text, ValueKind.StringSingleLine, "",
                EnabledWhenItemJsonKey: "kind", EnabledWhenItemValues: ["text", "icon"]),
            new ComponentInputDefinition(
                "text", "Text", "text", ComponentInputKind.Text, ValueKind.StringSingleLine, "",
                EnabledWhenItemJsonKey: "kind", EnabledWhenItemValues: ["text"]),
            new ComponentInputDefinition(
                "subtext", "Subtext", "subtext", ComponentInputKind.Text, ValueKind.StringSingleLine, "",
                EnabledWhenItemJsonKey: "kind", EnabledWhenItemValues: ["text"]),
            new ComponentInputDefinition(
                "iconToken", "Icon", "iconToken", ComponentInputKind.Icon, ValueKind.IconToken, "app_clock",
                EnabledWhenItemJsonKey: "kind", EnabledWhenItemValues: ["icon"]),
            new ComponentInputDefinition(
                "disabled", "Disabled", "disabled", ComponentInputKind.Boolean, ValueKind.Boolean, "false",
                EnabledWhenItemJsonKey: "kind", EnabledWhenItemValues: ["text", "icon"]),
        ],
        ItemPresentation: new RuntimeInputCollectionItemPresentation(
            ["text", "subtext", "value"],
            72,
            "iconToken",
            "keypad",
            new Dictionary<string, string>()));

    private static readonly FieldOption[] KeyboardLanguageOptions =
    [
        new("es", "Spanish"),
        new("en", "English"),
    ];

    private static readonly FieldOption[] KeyboardIconRowPlacementOptions =
    [
        new("top", "Top"),
        new("bottom", "Bottom"),
    ];

    private static readonly FieldOption[] NavigationBarTypeOptions =
    [
        new("buttons", "Buttons"),
        new("gestureBar", "Gesture Bar"),
    ];

    private static readonly Dictionary<string, ComponentClassFieldDescriptor> Fields = new(StringComparer.Ordinal)
    {
        ["component.type"] = new("component.type", "Component Type", ValueKind.StringReadOnly, [], "", false),
        ["component.style.shadowEnabled"] = new("component.style.shadowEnabled", "Shadow", ValueKind.Boolean, ["style", "shadowEnabled"], "false"),
        ["component.style.reliefEnabled"] = new("component.style.reliefEnabled", "Relief", ValueKind.Boolean, ["style", "reliefEnabled"], "false"),
        ["component.style.borderWidth"] = new("component.style.borderWidth", "Border width", ValueKind.Integer, ["style", "borderWidth"], "0"),
        ["component.style.borderColorToken"] = new("component.style.borderColorToken", "Border color", ValueKind.ThemeToken, ["style", "borderColorToken"], "theme.borders.primary", Options: ThemeColorOptions),
        ["component.style.cornerRadiusToken"] = new("component.style.cornerRadiusToken", "Corner radius", ValueKind.ThemeToken, ["style", "cornerRadiusToken"], "theme.radii.xl", Options: RadiusTokenOptions),
        ["component.style.reliefAngle"] = new("component.style.reliefAngle", "Relief angle", ValueKind.Integer, ["style", "reliefAngle"], "-45"),
        ["component.style.reliefExtent"] = new(
            "component.style.reliefExtent",
            "Relief extent",
            ValueKind.Decimal,
            ["style", "reliefExtent"],
            "1",
            Number: new NumberDefinition(0, 12, 0.05m, 2)),
        ["component.style.reliefSpread"] = new(
            "component.style.reliefSpread",
            "Relief spread",
            ValueKind.Decimal,
            ["style", "reliefSpread"],
            "0",
            Number: new NumberDefinition(0, 12, 0.05m, 2)),
        ["component.style.reliefTopIntensity"] = new(
            "component.style.reliefTopIntensity",
            "Relief top",
            ValueKind.Decimal,
            ["style", "reliefTopIntensity"],
            "0.12",
            Number: new NumberDefinition(-1, 1, 0.05m, 2)),
        ["component.style.reliefBottomIntensity"] = new(
            "component.style.reliefBottomIntensity",
            "Relief bottom",
            ValueKind.Decimal,
            ["style", "reliefBottomIntensity"],
            "-0.1",
            Number: new NumberDefinition(-1, 1, 0.05m, 2)),

        ["component.surface.backgroundColorToken"] = new("component.surface.backgroundColorToken", "Background", ValueKind.ThemeToken, ["surface", "backgroundColorToken"], "theme.colors.surface", Options: ThemeColorOptions),
        ["component.surface.backgroundAlpha"] = new("component.surface.backgroundAlpha", "Surface alpha", ValueKind.Alpha, ["surface", "backgroundAlpha"], "1"),
        ["component.surface.borderAlpha"] = new("component.surface.borderAlpha", "Border alpha", ValueKind.Alpha, ["surface", "borderAlpha"], "1"),
        ["component.surface.tail.enabled"] = new("component.surface.tail.enabled", "Tail", ValueKind.Boolean, ["surface", "tail", "enabled"], "false"),
        ["component.surface.tail.style"] = new("component.surface.tail.style", "Tail style", ValueKind.OptionToken, ["surface", "tail", "style"], "rounded_wedge", Options: SurfaceTailStyleOptions),
        ["component.surface.tail.side"] = new("component.surface.tail.side", "Tail side", ValueKind.OptionToken, ["surface", "tail", "side"], "left", Options: SurfaceTailSideOptions),
        ["component.surface.tail.vertical"] = new("component.surface.tail.vertical", "Tail vertical", ValueKind.OptionToken, ["surface", "tail", "vertical"], "bottom", Options: SurfaceTailVerticalOptions),
        ["component.surface.tail.size"] = new("component.surface.tail.size", "Tail size", ValueKind.IntegerPair, ["surface", "tail", "size"], "18|14", PairLabels: new("W", "H")),
        ["component.surface.tail.outerCornerRadius"] = new(
            "component.surface.tail.outerCornerRadius",
            "Tail outer radius",
            ValueKind.Integer,
            ["surface", "tail", "outerCornerRadius"],
            "0",
            Number: new NumberDefinition(0, 64, 1, 0)),

        ["component.cursor.colorToken"] = new("component.cursor.colorToken", "Color", ValueKind.ThemeToken, ["cursor", "colorToken"], "theme.cursor.color", Options: ThemeColorOptions),
        ["component.cursor.width"] = new("component.cursor.width", "Width", ValueKind.Integer, ["cursor", "width"], "2"),
        ["component.cursor.minimumFade"] = new("component.cursor.minimumFade", "Minimum fade", ValueKind.Alpha, ["cursor", "minimumFade"], "0.15"),
        ["component.cursor.fadeDurationMs"] = new("component.cursor.fadeDurationMs", "Fade duration", ValueKind.Integer, ["cursor", "fadeDurationMs"], "480", Unit: "ms"),

        ["component.textBox.dimensionMode"] = new("component.textBox.dimensionMode", "Dimension mode", ValueKind.OptionToken, ["textBox", "dimensionMode"], "fixed", Options: TextBoxDimensionModeOptions),
        ["component.textBox.padding"] = new("component.textBox.padding", "Padding", ValueKind.ThemeTokenPair, ["textBox", "padding"], "theme.spacing.m|theme.spacing.s", PairLabels: new("X", "Y"), Options: SpacingTokenOptions),
        ["component.textBox.surface.editor"] = new("component.textBox.surface.editor", "Surface", ValueKind.ComponentPreset, ["textBox", "surfaceSlot", "presetId"], "InputBox"),
        ["component.textBox.placeholder"] = new("component.textBox.placeholder", "Placeholder", ValueKind.StringSingleLine, ["textBox", "placeholder"], "Message"),
        ["component.textBox.textColorToken"] = new("component.textBox.textColorToken", "Text color", ValueKind.ThemeToken, ["textBox", "textColorToken"], "theme.colors.textPrimary", Options: ThemeColorOptions),
        ["component.textBox.placeholderColorToken"] = new("component.textBox.placeholderColorToken", "Placeholder color", ValueKind.ThemeToken, ["textBox", "placeholderColorToken"], "theme.colors.textSecondary", Options: ThemeColorOptions),
        ["component.textBox.typography"] = new("component.textBox.typography", "Typography", ValueKind.TypographyStyle, ["textBox", "typography"], TypographyStyleValue.CreateDefault("theme.typography.sizes.s")),
        ["component.textBox.textAlign"] = new("component.textBox.textAlign", "Text align", ValueKind.OptionToken, ["textBox", "textAlign"], "left", Options: TextAlignOptions),
        ["component.textBox.overflowMode"] = new("component.textBox.overflowMode", "Overflow", ValueKind.OptionToken, ["textBox", "overflowMode"], "clip", Options: TextBoxOverflowOptions),
        ["component.textBox.cursor.showCursor"] = new("component.textBox.cursor.showCursor", "Show cursor", ValueKind.Boolean, ["textBox", "cursorSlot", "showCursor"], "true"),
        ["component.textBox.cursor.editor"] = new("component.textBox.cursor.editor", "Cursor", ValueKind.ComponentPreset, ["textBox", "cursorSlot", "presetId"], "default"),

        ["component.iconRow.orientation"] = new("component.iconRow.orientation", "Orientation", ValueKind.OptionToken, ["iconRow", "orientation"], "horizontal", Options: IconRowOrientationOptions),
        ["component.iconRow.gap"] = new("component.iconRow.gap", "Gap", ValueKind.ThemeToken, ["iconRow", "gap"], "theme.spacing.s", Options: SpacingTokenOptions),
        ["component.iconRow.sizeSource"] = new("component.iconRow.sizeSource", "Size source", ValueKind.OptionToken, ["iconRow", "sizeSource"], "shared", Options: IconRowSizeSourceOptions),
        ["component.iconRow.iconSizeToken"] = new("component.iconRow.iconSizeToken", "Shared icon size", ValueKind.ThemeToken, ["iconRow", "iconSizeToken"], "theme.iconSizes.m", Options: IconSizeTokenOptions),
        ["component.iconRow.textSizeToken"] = new("component.iconRow.textSizeToken", "Shared text size", ValueKind.ThemeToken, ["iconRow", "textSizeToken"], "theme.typography.sizes.s", Options: TypographySizeOptions),

        ["component.iconBar.edgePadding"] = new("component.iconBar.edgePadding", "Edge padding", ValueKind.ThemeToken, ["iconBar", "edgePadding"], "theme.spacing.m", Options: SpacingTokenOptions),
        ["component.iconBar.sizeSource"] = new("component.iconBar.sizeSource", "Size source", ValueKind.OptionToken, ["iconBar", "sizeSource"], "shared", Options: IconBarSizeSourceOptions),
        ["component.iconBar.iconSizeToken"] = new("component.iconBar.iconSizeToken", "Shared icon size", ValueKind.ThemeToken, ["iconBar", "iconSizeToken"], "theme.iconSizes.m", Options: IconSizeTokenOptions),
        ["component.iconBar.textSizeToken"] = new("component.iconBar.textSizeToken", "Shared text size", ValueKind.ThemeToken, ["iconBar", "textSizeToken"], "theme.typography.sizes.s", Options: TypographySizeOptions),
        ["component.iconBar.idleLeftIconRow.editor"] = new("component.iconBar.idleLeftIconRow.editor", "Idle left row", ValueKind.ComponentPreset, ["iconBar", "idleLeftIconRowSlot", "presetId"], "default"),
        ["component.iconBar.idleLeftIconRow.inputs"] = new(
            "component.iconBar.idleLeftIconRow.inputs",
            "Idle left settings",
            ValueKind.ComponentInputBindings,
            ["iconBar", "idleLeftIconRowInputs"],
            """{"gap":"theme.spacing.m","orientation":"horizontal","items":[{"id":"button_001","buttonPresetId":"button::preset::default","contentMode":"icon","state":"normal","iconToken":"media_camera","text":"","pushTrigger":false,"pushElapsedMs":0,"buttonOverrides":{}}]}""",
            ComponentInputBindings: IconRowParentInputBindings),
        ["component.iconBar.idleCenterIconRow.editor"] = new("component.iconBar.idleCenterIconRow.editor", "Idle center row", ValueKind.ComponentPreset, ["iconBar", "idleCenterIconRowSlot", "presetId"], "default"),
        ["component.iconBar.idleCenterIconRow.inputs"] = new(
            "component.iconBar.idleCenterIconRow.inputs",
            "Idle center settings",
            ValueKind.ComponentInputBindings,
            ["iconBar", "idleCenterIconRowInputs"],
            """{"gap":"theme.spacing.m","orientation":"horizontal","items":[{"id":"button_001","buttonPresetId":"button::preset::default","contentMode":"icon","state":"normal","iconToken":"media_play","text":"","pushTrigger":false,"pushElapsedMs":0,"buttonOverrides":{}}]}""",
            ComponentInputBindings: IconRowParentInputBindings),
        ["component.iconBar.idleRightIconRow.editor"] = new("component.iconBar.idleRightIconRow.editor", "Idle right row", ValueKind.ComponentPreset, ["iconBar", "idleRightIconRowSlot", "presetId"], "default"),
        ["component.iconBar.idleRightIconRow.inputs"] = new(
            "component.iconBar.idleRightIconRow.inputs",
            "Idle right settings",
            ValueKind.ComponentInputBindings,
            ["iconBar", "idleRightIconRowInputs"],
            """{"gap":"theme.spacing.m","orientation":"horizontal","items":[{"id":"button_001","buttonPresetId":"button::preset::default","contentMode":"icon","state":"normal","iconToken":"nav_more_horizontal","text":"","pushTrigger":false,"pushElapsedMs":0,"buttonOverrides":{}}]}""",
            ComponentInputBindings: IconRowParentInputBindings),
        ["component.iconBar.activeLeftIconRow.editor"] = new("component.iconBar.activeLeftIconRow.editor", "Active left row", ValueKind.ComponentPreset, ["iconBar", "activeLeftIconRowSlot", "presetId"], "default"),
        ["component.iconBar.activeLeftIconRow.inputs"] = new(
            "component.iconBar.activeLeftIconRow.inputs",
            "Active left settings",
            ValueKind.ComponentInputBindings,
            ["iconBar", "activeLeftIconRowInputs"],
            """{"gap":"theme.spacing.m","orientation":"horizontal","items":[{"id":"button_001","buttonPresetId":"button::preset::default","contentMode":"icon","state":"normal","iconToken":"media_camera","text":"","pushTrigger":false,"pushElapsedMs":0,"buttonOverrides":{}}]}""",
            ComponentInputBindings: IconRowParentInputBindings),
        ["component.iconBar.activeCenterIconRow.editor"] = new("component.iconBar.activeCenterIconRow.editor", "Active center row", ValueKind.ComponentPreset, ["iconBar", "activeCenterIconRowSlot", "presetId"], "default"),
        ["component.iconBar.activeCenterIconRow.inputs"] = new(
            "component.iconBar.activeCenterIconRow.inputs",
            "Active center settings",
            ValueKind.ComponentInputBindings,
            ["iconBar", "activeCenterIconRowInputs"],
            """{"gap":"theme.spacing.m","orientation":"horizontal","items":[{"id":"button_001","buttonPresetId":"button::preset::default","contentMode":"icon","state":"normal","iconToken":"media_pause","text":"","pushTrigger":false,"pushElapsedMs":0,"buttonOverrides":{}}]}""",
            ComponentInputBindings: IconRowParentInputBindings),
        ["component.iconBar.activeRightIconRow.editor"] = new("component.iconBar.activeRightIconRow.editor", "Active right row", ValueKind.ComponentPreset, ["iconBar", "activeRightIconRowSlot", "presetId"], "default"),
        ["component.iconBar.activeRightIconRow.inputs"] = new(
            "component.iconBar.activeRightIconRow.inputs",
            "Active right settings",
            ValueKind.ComponentInputBindings,
            ["iconBar", "activeRightIconRowInputs"],
            """{"gap":"theme.spacing.m","orientation":"horizontal","items":[{"id":"button_001","buttonPresetId":"button::preset::default","contentMode":"icon","state":"normal","iconToken":"nav_more_horizontal","text":"","pushTrigger":false,"pushElapsedMs":0,"buttonOverrides":{}}]}""",
            ComponentInputBindings: IconRowParentInputBindings),

        ["component.avatar.defaultSize"] = new("component.avatar.defaultSize", "Default size", ValueKind.Integer, ["avatar", "defaultSize"], "48"),
        ["component.avatar.cornerRadiusToken"] = new("component.avatar.cornerRadiusToken", "Avatar radius", ValueKind.ThemeToken, ["avatar", "cornerRadiusToken"], "theme.radii.full", Options: RadiusTokenOptions),
        ["component.avatar.label.showLabel"] = new("component.avatar.label.showLabel", "Show label", ValueKind.Boolean, ["avatar", "labelSlot", "showLabel"], "false"),
        ["component.avatar.label.showSubtext"] = new("component.avatar.label.showSubtext", "Show subtext", ValueKind.Boolean, ["avatar", "labelSlot", "showSubtext"], "false"),
        ["component.avatar.label.placement"] = new("component.avatar.label.placement", "Placement", ValueKind.AlignmentPlacement, ["avatar", "labelSlot", "placement"], """{"mode":"edge","alignX":1,"alignY":0.5,"offsetX":4,"offsetY":0}"""),
        ["component.avatar.label.presetId"] = new("component.avatar.label.presetId", "Variant", ValueKind.OptionToken, ["avatar", "labelSlot", "presetId"], "default"),
        ["component.avatar.label.editor"] = new("component.avatar.label.editor", "Label", ValueKind.ComponentPreset, ["avatar", "labelSlot", "presetId"], "default"),

        ["component.textInput.height"] = new("component.textInput.height", "Height", ValueKind.Integer, ["textInput", "height"], "44"),
        ["component.textInput.barPadding"] = new("component.textInput.barPadding", "Bar padding", ValueKind.ThemeTokenPair, ["textInput", "barPadding"], "theme.spacing.l|theme.spacing.m", PairLabels: new("X", "Y"), Options: SpacingTokenOptions),
        ["component.textInput.barSurface.editor"] = new("component.textInput.barSurface.editor", "Bar surface", ValueKind.ComponentPreset, ["textInput", "barSurfaceSlot", "presetId"], "default"),
        ["component.textInput.iconGap"] = new("component.textInput.iconGap", "Icon gap", ValueKind.ThemeToken, ["textInput", "iconGap"], "theme.spacing.m", Options: SpacingTokenOptions),
        ["component.textInput.iconBar.editor"] = new("component.textInput.iconBar.editor", "Icon bar", ValueKind.ComponentPreset, ["textInput", "iconBarSlot", "presetId"], "default"),
        ["component.textInput.textBox.editor"] = new("component.textInput.textBox.editor", "Text box variant", ValueKind.ComponentPreset, ["textInput", "textBoxSlot", "presetId"], "default"),
        ["component.textInput.textBox.inputs"] = new(
            "component.textInput.textBox.inputs",
            "Text box inputs",
            ValueKind.ComponentInputBindings,
            ["textInput", "textBoxInputs"],
            """{"placeholder":"Message"}""",
            ComponentInputBindings: TextBoxParentInputBindings,
            RuntimeInputComponentPresetFieldId: "component.textInput.textBox.editor"),

        ["component.keyboard.language"] = new("component.keyboard.language", "Language", ValueKind.OptionToken, ["keyboard", "language"], "es", Options: KeyboardLanguageOptions),
        ["component.keyboard.heightToken"] = new("component.keyboard.heightToken", "Height", ValueKind.ThemeToken, ["keyboard", "heightToken"], "theme.keyboard.height", Options: KeyboardHeightTokenOptions),
        ["component.keyboard.keyGapToken"] = new("component.keyboard.keyGapToken", "Key gap", ValueKind.ThemeToken, ["keyboard", "keyGapToken"], "theme.keyboard.keyGap", Options: KeyboardKeyGapTokenOptions),
        ["component.keyboard.rowGapToken"] = new("component.keyboard.rowGapToken", "Row gap", ValueKind.ThemeToken, ["keyboard", "rowGapToken"], "theme.keyboard.rowGap", Options: KeyboardRowGapTokenOptions),
        ["component.keyboard.typography"] = new("component.keyboard.typography", "Typography", ValueKind.TypographySystemStyle, ["keyboard", "typography"], TypographyStyleValue.CreateDefault("theme.typography.sizes.s", "theme.system")),
        ["component.keyboard.keyPadding"] = new("component.keyboard.keyPadding", "Key padding", ValueKind.ThemeToken, ["keyboard", "keyPadding"], "theme.spacing.s", Options: SpacingTokenOptions),
        ["component.keyboard.keyCornerRadiusToken"] = new("component.keyboard.keyCornerRadiusToken", "Key radius", ValueKind.ThemeToken, ["keyboard", "keyCornerRadiusToken"], "theme.radii.m", Options: RadiusTokenOptions),
        ["component.keyboard.keyBorderWidth"] = new("component.keyboard.keyBorderWidth", "Key border width", ValueKind.Decimal, ["keyboard", "keyBorderWidth"], "0", Number: new NumberDefinition(0, 32, 0.5m, 2)),
        ["component.keyboard.keyShadowEnabled"] = new("component.keyboard.keyShadowEnabled", "Key shadow", ValueKind.Boolean, ["keyboard", "keyShadowEnabled"], "false"),
        ["component.keyboard.pressedEffect"] = new("component.keyboard.pressedEffect", "Pressed effect", ValueKind.OptionToken, ["keyboard", "pressedEffect"], "popup", Options: PressedEffectOptions),
        ["component.keyboard.emojiScale"] = new(
            "component.keyboard.emojiScale",
            "Emoji scale",
            ValueKind.Decimal,
            ["keyboard", "emojiScale"],
            "1.2",
            Number: new NumberDefinition(0.1m, 4, 0.05m, 2)),
        ["component.keyboard.motion"] = new(
            "component.keyboard.motion",
            "Motion",
            ValueKind.Motion,
            ["keyboard", "motion"],
            MotionVariantValue.Default.ToJsonString()),
        ["component.keyboard.iconRowPlacement"] = new("component.keyboard.iconRowPlacement", "Placement", ValueKind.OptionToken, ["keyboard", "iconRowPlacement"], "bottom", Options: KeyboardIconRowPlacementOptions),
        ["component.keyboard.iconRowsHeight"] = new("component.keyboard.iconRowsHeight", "Icon zone height", ValueKind.Integer, ["keyboard", "iconRowsHeight"], "52", Number: new NumberDefinition(0, 240, 1, 0)),
        ["component.keyboard.iconEdgePadding"] = new("component.keyboard.iconEdgePadding", "Icon edge padding", ValueKind.ThemeToken, ["keyboard", "iconEdgePadding"], "theme.spacing.none", Options: SpacingTokenOptions),
        ["component.keyboard.iconBar.editor"] = new("component.keyboard.iconBar.editor", "Icon bar", ValueKind.ComponentPreset, ["keyboard", "iconBarSlot", "presetId"], "default"),

        ["component.keypad.sizingMode"] = new("component.keypad.sizingMode", "Sizing", ValueKind.OptionToken, ["keypad", "sizingMode"], "content", Options: [new("content", "Fit content"), new("fill", "Fill available width")]),
        ["component.keypad.columns"] = new("component.keypad.columns", "Columns", ValueKind.Integer, ["keypad", "columns"], "3", Number: new NumberDefinition(1, 12, 1, 0)),
        ["component.keypad.keySize"] = new("component.keypad.keySize", "Key size", ValueKind.IntegerPair, ["keypad", "keySize"], "72|72", PairLabels: new("W", "H")),
        ["component.keypad.padding"] = new("component.keypad.padding", "Padding", ValueKind.ThemeTokenPair, ["keypad", "padding"], "theme.spacing.none|theme.spacing.none", PairLabels: new("X", "Y"), Options: SpacingTokenOptions),
        ["component.keypad.columnGapToken"] = new("component.keypad.columnGapToken", "Column gap", ValueKind.ThemeToken, ["keypad", "columnGapToken"], "theme.spacing.l", Options: SpacingTokenOptions),
        ["component.keypad.rowGapToken"] = new("component.keypad.rowGapToken", "Row gap", ValueKind.ThemeToken, ["keypad", "rowGapToken"], "theme.spacing.l", Options: SpacingTokenOptions),
        ["component.keypad.iconSizeToken"] = new("component.keypad.iconSizeToken", "Icon size", ValueKind.ThemeToken, ["keypad", "iconSizeToken"], "theme.iconSizes.m", Options: IconSizeTokenOptions),
        ["component.keypad.keys"] = new("component.keypad.keys", "Keys", ValueKind.StructuredCollection, ["keypad", "keys"], "[]", StructuredCollection: KeypadKeysCollection),
        ["component.keypad.label.editor"] = new("component.keypad.label.editor", "Label", ValueKind.ComponentPreset, ["keypad", "labelSlot", "presetId"], "default"),
        ["component.keypad.states.normal.backgroundColorToken"] = new("component.keypad.states.normal.backgroundColorToken", "Background", ValueKind.ThemeToken, ["keypad", "states", "normal", "backgroundColorToken"], "theme.colors.surface", Options: ThemeColorOptions),
        ["component.keypad.states.normal.textColorToken"] = new("component.keypad.states.normal.textColorToken", "Text / icon", ValueKind.ThemeToken, ["keypad", "states", "normal", "textColorToken"], "theme.colors.textPrimary", Options: ThemeColorOptions),
        ["component.keypad.states.normal.backgroundAlpha"] = new("component.keypad.states.normal.backgroundAlpha", "Background alpha", ValueKind.Alpha, ["keypad", "states", "normal", "backgroundAlpha"], "1"),
        ["component.keypad.states.normal.borderAlpha"] = new("component.keypad.states.normal.borderAlpha", "Border alpha", ValueKind.Alpha, ["keypad", "states", "normal", "borderAlpha"], "1"),
        ["component.keypad.states.active.backgroundColorToken"] = new("component.keypad.states.active.backgroundColorToken", "Background", ValueKind.ThemeToken, ["keypad", "states", "active", "backgroundColorToken"], "theme.colors.surface", Options: ThemeColorOptions),
        ["component.keypad.states.active.textColorToken"] = new("component.keypad.states.active.textColorToken", "Text / icon", ValueKind.ThemeToken, ["keypad", "states", "active", "textColorToken"], "theme.colors.accent", Options: ThemeColorOptions),
        ["component.keypad.states.active.backgroundAlpha"] = new("component.keypad.states.active.backgroundAlpha", "Background alpha", ValueKind.Alpha, ["keypad", "states", "active", "backgroundAlpha"], "1"),
        ["component.keypad.states.active.borderAlpha"] = new("component.keypad.states.active.borderAlpha", "Border alpha", ValueKind.Alpha, ["keypad", "states", "active", "borderAlpha"], "1"),
        ["component.keypad.states.pushed.backgroundColorToken"] = new("component.keypad.states.pushed.backgroundColorToken", "Background", ValueKind.ThemeToken, ["keypad", "states", "pushed", "backgroundColorToken"], "theme.colors.surface", Options: ThemeColorOptions),
        ["component.keypad.states.pushed.textColorToken"] = new("component.keypad.states.pushed.textColorToken", "Text / icon", ValueKind.ThemeToken, ["keypad", "states", "pushed", "textColorToken"], "theme.colors.accent", Options: ThemeColorOptions),
        ["component.keypad.states.pushed.backgroundAlpha"] = new("component.keypad.states.pushed.backgroundAlpha", "Background alpha", ValueKind.Alpha, ["keypad", "states", "pushed", "backgroundAlpha"], "1"),
        ["component.keypad.states.pushed.borderAlpha"] = new("component.keypad.states.pushed.borderAlpha", "Border alpha", ValueKind.Alpha, ["keypad", "states", "pushed", "borderAlpha"], "1"),
        ["component.keypad.states.disabled.backgroundColorToken"] = new("component.keypad.states.disabled.backgroundColorToken", "Background", ValueKind.ThemeToken, ["keypad", "states", "disabled", "backgroundColorToken"], "theme.colors.surface", Options: ThemeColorOptions),
        ["component.keypad.states.disabled.textColorToken"] = new("component.keypad.states.disabled.textColorToken", "Text / icon", ValueKind.ThemeToken, ["keypad", "states", "disabled", "textColorToken"], "theme.colors.textSecondary", Options: ThemeColorOptions),
        ["component.keypad.states.disabled.backgroundAlpha"] = new("component.keypad.states.disabled.backgroundAlpha", "Background alpha", ValueKind.Alpha, ["keypad", "states", "disabled", "backgroundAlpha"], "1"),
        ["component.keypad.states.disabled.borderAlpha"] = new("component.keypad.states.disabled.borderAlpha", "Border alpha", ValueKind.Alpha, ["keypad", "states", "disabled", "borderAlpha"], "1"),

        ["component.codeIndicator.glyphSize"] = new("component.codeIndicator.glyphSize", "Glyph size", ValueKind.IntegerPair, ["codeIndicator", "glyphSize"], "16|16", PairLabels: new("W", "H")),
        ["component.codeIndicator.displayMode"] = new("component.codeIndicator.displayMode", "Display", ValueKind.OptionToken, ["codeIndicator", "displayMode"], "visible", Options: [new("visible", "Visible"), new("collapsed", "Collapsed")]),
        ["component.codeIndicator.gapToken"] = new("component.codeIndicator.gapToken", "Gap", ValueKind.ThemeToken, ["codeIndicator", "gapToken"], "theme.spacing.m", Options: SpacingTokenOptions),
        ["component.codeIndicator.states.initial.empty.editor"] = new("component.codeIndicator.states.initial.empty.editor", "Empty", ValueKind.ComponentPreset, ["codeIndicator", "states", "initial", "emptySurfaceSlot", "presetId"], "default"),
        ["component.codeIndicator.states.initial.filled.editor"] = new("component.codeIndicator.states.initial.filled.editor", "Filled", ValueKind.ComponentPreset, ["codeIndicator", "states", "initial", "filledSurfaceSlot", "presetId"], "default"),
        ["component.codeIndicator.states.correct.empty.editor"] = new("component.codeIndicator.states.correct.empty.editor", "Empty", ValueKind.ComponentPreset, ["codeIndicator", "states", "correct", "emptySurfaceSlot", "presetId"], "default"),
        ["component.codeIndicator.states.correct.filled.editor"] = new("component.codeIndicator.states.correct.filled.editor", "Filled", ValueKind.ComponentPreset, ["codeIndicator", "states", "correct", "filledSurfaceSlot", "presetId"], "default"),
        ["component.codeIndicator.states.incorrect.empty.editor"] = new("component.codeIndicator.states.incorrect.empty.editor", "Empty", ValueKind.ComponentPreset, ["codeIndicator", "states", "incorrect", "emptySurfaceSlot", "presetId"], "default"),
        ["component.codeIndicator.states.incorrect.filled.editor"] = new("component.codeIndicator.states.incorrect.filled.editor", "Filled", ValueKind.ComponentPreset, ["codeIndicator", "states", "incorrect", "filledSurfaceSlot", "presetId"], "default"),

        ["component.fingerprint.size"] = new("component.fingerprint.size", "Size", ValueKind.IntegerPair, ["fingerprint", "size"], "120|120", PairLabels: new("W", "H")),
        ["component.fingerprint.iconToken"] = new("component.fingerprint.iconToken", "Icon", ValueKind.IconToken, ["fingerprint", "iconToken"], "print"),
        ["component.fingerprint.iconSizeToken"] = new("component.fingerprint.iconSizeToken", "Icon size", ValueKind.ThemeToken, ["fingerprint", "iconSizeToken"], "theme.iconSizes.xl", Options: IconSizeTokenOptions),
        ["component.fingerprint.iconSizeMultiplier"] = new("component.fingerprint.iconSizeMultiplier", "Icon size multiplier", ValueKind.Decimal, ["fingerprint", "iconSizeMultiplier"], "1", Number: new NumberDefinition(0.1m, 20, 0.1m, 2)),
        ["component.fingerprint.scanLineThickness"] = new("component.fingerprint.scanLineThickness", "Scan line", ValueKind.Decimal, ["fingerprint", "scanLineThickness"], "3", Number: new NumberDefinition(0.1m, 100, 0.1m, 1)),
        ["component.fingerprint.states.initial.colorToken"] = new("component.fingerprint.states.initial.colorToken", "Initial", ValueKind.ThemeToken, ["fingerprint", "states", "initial", "colorToken"], "theme.colors.textSecondary", Options: ThemeColorOptions),
        ["component.fingerprint.states.active.colorToken"] = new("component.fingerprint.states.active.colorToken", "Active", ValueKind.ThemeToken, ["fingerprint", "states", "active", "colorToken"], "theme.colors.accent", Options: ThemeColorOptions),
        ["component.fingerprint.states.correct.colorToken"] = new("component.fingerprint.states.correct.colorToken", "Correct", ValueKind.ThemeToken, ["fingerprint", "states", "correct", "colorToken"], "theme.colors.accent", Options: ThemeColorOptions),
        ["component.fingerprint.states.incorrect.colorToken"] = new("component.fingerprint.states.incorrect.colorToken", "Incorrect", ValueKind.ThemeToken, ["fingerprint", "states", "incorrect", "colorToken"], "theme.colors.textSecondary", Options: ThemeColorOptions),

        ["component.faceRecognition.size"] = new("component.faceRecognition.size", "Size", ValueKind.IntegerPair, ["faceRecognition", "size"], "140|140", PairLabels: new("W", "H")),
        ["component.faceRecognition.iconToken"] = new("component.faceRecognition.iconToken", "Icon", ValueKind.IconToken, ["faceRecognition", "iconToken"], "face"),
        ["component.faceRecognition.iconSizeToken"] = new("component.faceRecognition.iconSizeToken", "Icon size", ValueKind.ThemeToken, ["faceRecognition", "iconSizeToken"], "theme.iconSizes.xl", Options: IconSizeTokenOptions),
        ["component.faceRecognition.iconSizeMultiplier"] = new("component.faceRecognition.iconSizeMultiplier", "Icon size multiplier", ValueKind.Decimal, ["faceRecognition", "iconSizeMultiplier"], "1", Number: new NumberDefinition(0.1m, 20, 0.1m, 2)),
        ["component.faceRecognition.strokeWidth"] = new("component.faceRecognition.strokeWidth", "Frame stroke", ValueKind.Decimal, ["faceRecognition", "strokeWidth"], "3", Number: new NumberDefinition(0.1m, 100, 0.1m, 1)),
        ["component.faceRecognition.states.initial.colorToken"] = new("component.faceRecognition.states.initial.colorToken", "Initial", ValueKind.ThemeToken, ["faceRecognition", "states", "initial", "colorToken"], "theme.colors.textSecondary", Options: ThemeColorOptions),
        ["component.faceRecognition.states.active.colorToken"] = new("component.faceRecognition.states.active.colorToken", "Active", ValueKind.ThemeToken, ["faceRecognition", "states", "active", "colorToken"], "theme.colors.accent", Options: ThemeColorOptions),
        ["component.faceRecognition.states.correct.colorToken"] = new("component.faceRecognition.states.correct.colorToken", "Correct", ValueKind.ThemeToken, ["faceRecognition", "states", "correct", "colorToken"], "theme.colors.accent", Options: ThemeColorOptions),
        ["component.faceRecognition.states.incorrect.colorToken"] = new("component.faceRecognition.states.incorrect.colorToken", "Incorrect", ValueKind.ThemeToken, ["faceRecognition", "states", "incorrect", "colorToken"], "theme.colors.textSecondary", Options: ThemeColorOptions),

        ["component.drawPassword.grid"] = new("component.drawPassword.grid", "Grid", ValueKind.IntegerPair, ["drawPassword", "grid"], "3|3", PairLabels: new("Columns", "Rows")),
        ["component.drawPassword.nodeSize"] = new("component.drawPassword.nodeSize", "Node size", ValueKind.Decimal, ["drawPassword", "nodeSize"], "18", Number: new NumberDefinition(1, 1000, 1, 0)),
        ["component.drawPassword.columnGapToken"] = new("component.drawPassword.columnGapToken", "Column gap", ValueKind.ThemeToken, ["drawPassword", "columnGapToken"], "theme.spacing.xl", Options: SpacingTokenOptions),
        ["component.drawPassword.rowGapToken"] = new("component.drawPassword.rowGapToken", "Row gap", ValueKind.ThemeToken, ["drawPassword", "rowGapToken"], "theme.spacing.xl", Options: SpacingTokenOptions),
        ["component.drawPassword.lineWidth"] = new("component.drawPassword.lineWidth", "Line width", ValueKind.Decimal, ["drawPassword", "lineWidth"], "3", Number: new NumberDefinition(0.1m, 100, 0.1m, 1)),
        ["component.drawPassword.states.initial.nodeColorToken"] = new("component.drawPassword.states.initial.nodeColorToken", "Node", ValueKind.ThemeToken, ["drawPassword", "states", "initial", "nodeColorToken"], "theme.colors.textSecondary", Options: ThemeColorOptions),
        ["component.drawPassword.states.initial.lineColorToken"] = new("component.drawPassword.states.initial.lineColorToken", "Line", ValueKind.ThemeToken, ["drawPassword", "states", "initial", "lineColorToken"], "theme.colors.textSecondary", Options: ThemeColorOptions),
        ["component.drawPassword.states.active.nodeColorToken"] = new("component.drawPassword.states.active.nodeColorToken", "Node", ValueKind.ThemeToken, ["drawPassword", "states", "active", "nodeColorToken"], "theme.colors.accent", Options: ThemeColorOptions),
        ["component.drawPassword.states.active.lineColorToken"] = new("component.drawPassword.states.active.lineColorToken", "Line", ValueKind.ThemeToken, ["drawPassword", "states", "active", "lineColorToken"], "theme.colors.accent", Options: ThemeColorOptions),
        ["component.drawPassword.states.correct.nodeColorToken"] = new("component.drawPassword.states.correct.nodeColorToken", "Node", ValueKind.ThemeToken, ["drawPassword", "states", "correct", "nodeColorToken"], "theme.colors.accent", Options: ThemeColorOptions),
        ["component.drawPassword.states.correct.lineColorToken"] = new("component.drawPassword.states.correct.lineColorToken", "Line", ValueKind.ThemeToken, ["drawPassword", "states", "correct", "lineColorToken"], "theme.colors.accent", Options: ThemeColorOptions),
        ["component.drawPassword.states.incorrect.nodeColorToken"] = new("component.drawPassword.states.incorrect.nodeColorToken", "Node", ValueKind.ThemeToken, ["drawPassword", "states", "incorrect", "nodeColorToken"], "theme.colors.textSecondary", Options: ThemeColorOptions),
        ["component.drawPassword.states.incorrect.lineColorToken"] = new("component.drawPassword.states.incorrect.lineColorToken", "Line", ValueKind.ThemeToken, ["drawPassword", "states", "incorrect", "lineColorToken"], "theme.colors.textSecondary", Options: ThemeColorOptions),

        ["component.password.mode"] = new("component.password.mode", "Mode", ValueKind.OptionToken, ["password", "mode"], "pin", Options: PasswordModeOptions),
        ["component.password.initialText"] = new("component.password.initialText", "Initial text", ValueKind.StringSingleLine, ["password", "initialText"], "Enter password"),
        ["component.password.correctText"] = new("component.password.correctText", "Correct text", ValueKind.StringSingleLine, ["password", "correctText"], "Password correct"),
        ["component.password.incorrectText"] = new("component.password.incorrectText", "Incorrect text", ValueKind.StringSingleLine, ["password", "incorrectText"], "Password incorrect"),
        ["component.password.upperAnchor"] = new("component.password.upperAnchor", "Upper block anchor", ValueKind.OptionToken, ["password", "upperAnchor"], "container", Options: PasswordAnchorOptions),
        ["component.password.lowerAnchor"] = new("component.password.lowerAnchor", "Lower block anchor", ValueKind.OptionToken, ["password", "lowerAnchor"], "container", Options: PasswordAnchorOptions),
        ["component.password.labelIndicatorGapToken"] = new("component.password.labelIndicatorGapToken", "Label / indicator gap", ValueKind.ThemeToken, ["password", "labelIndicatorGapToken"], "theme.spacing.l", Options: SpacingTokenOptions),
        ["component.password.startGapToken"] = new("component.password.startGapToken", "Start gap", ValueKind.ThemeToken, ["password", "startGapToken"], "theme.spacing.xl", Options: SpacingTokenOptions),
        ["component.password.upperGapToken"] = new("component.password.upperGapToken", "Upper gap", ValueKind.ThemeToken, ["password", "upperGapToken"], "theme.spacing.xl", Options: SpacingTokenOptions),
        ["component.password.lowerGapToken"] = new("component.password.lowerGapToken", "Lower gap", ValueKind.ThemeToken, ["password", "lowerGapToken"], "theme.spacing.l", Options: SpacingTokenOptions),
        ["component.password.endGapToken"] = new("component.password.endGapToken", "End gap", ValueKind.ThemeToken, ["password", "endGapToken"], "theme.spacing.l", Options: SpacingTokenOptions),
        ["component.password.iconBarHeight"] = new("component.password.iconBarHeight", "Icon bar height", ValueKind.Integer, ["password", "iconBarHeight"], "52", Number: new NumberDefinition(1, 1000, 1, 0)),
        ["component.password.initialLabel.editor"] = new("component.password.initialLabel.editor", "Initial label", ValueKind.ComponentPreset, ["password", "initialLabelSlot", "presetId"], "default"),
        ["component.password.correctLabel.editor"] = new("component.password.correctLabel.editor", "Correct label", ValueKind.ComponentPreset, ["password", "correctLabelSlot", "presetId"], "default"),
        ["component.password.incorrectLabel.editor"] = new("component.password.incorrectLabel.editor", "Incorrect label", ValueKind.ComponentPreset, ["password", "incorrectLabelSlot", "presetId"], "default"),
        ["component.password.indicator.editor"] = new("component.password.indicator.editor", "Code indicator", ValueKind.ComponentPreset, ["password", "indicatorSlot", "presetId"], "default"),
        ["component.password.keypad.editor"] = new("component.password.keypad.editor", "Keypad", ValueKind.ComponentPreset, ["password", "keypadSlot", "presetId"], "default"),
        ["component.password.fingerprint.editor"] = new("component.password.fingerprint.editor", "Fingerprint", ValueKind.ComponentPreset, ["password", "fingerprintSlot", "presetId"], "default"),
        ["component.password.faceRecognition.editor"] = new("component.password.faceRecognition.editor", "Face recognition", ValueKind.ComponentPreset, ["password", "faceRecognitionSlot", "presetId"], "default"),
        ["component.password.drawPassword.editor"] = new("component.password.drawPassword.editor", "Draw password", ValueKind.ComponentPreset, ["password", "drawPasswordSlot", "presetId"], "default"),
        ["component.password.iconBar.editor"] = new("component.password.iconBar.editor", "Icon bar", ValueKind.ComponentPreset, ["password", "iconBarSlot", "presetId"], "default"),

        ["component.button.dimensionMode"] = new("component.button.dimensionMode", "Dimension mode", ValueKind.OptionToken, ["button", "dimensionMode"], "content", Options: DimensionModeOptions),
        ["component.button.size"] = new("component.button.size", "Fixed size", ValueKind.IntegerPair, ["button", "size"], "120|44", PairLabels: new("W", "H")),
        ["component.button.padding"] = new("component.button.padding", "Padding", ValueKind.ThemeTokenPair, ["button", "padding"], "theme.spacing.l|theme.spacing.m", PairLabels: new("X", "Y"), Options: SpacingTokenOptions),
        ["component.button.contentGapToken"] = new("component.button.contentGapToken", "Content gap", ValueKind.ThemeToken, ["button", "contentGapToken"], "theme.spacing.s", Options: SpacingTokenOptions),
        ["component.button.iconToken"] = new("component.button.iconToken", "Default icon", ValueKind.IconToken, ["button", "iconToken"], "media_play_fill"),
        ["component.button.pushedDurationToken"] = new("component.button.pushedDurationToken", "Pushed duration", ValueKind.ThemeToken, ["button", "pushedDurationToken"], "theme.motion.buttonPushedDurationMs", Options: ButtonPushedDurationTokenOptions),
        ["component.button.states.normal.surface.editor"] = new("component.button.states.normal.surface.editor", "Surface", ValueKind.ComponentPreset, ["button", "states", "normal", "surfaceSlot", "presetId"], "default"),
        ["component.button.states.normal.label.editor"] = new("component.button.states.normal.label.editor", "Label", ValueKind.ComponentPreset, ["button", "states", "normal", "labelSlot", "presetId"], "default"),
        ["component.button.states.normal.iconColorToken"] = new("component.button.states.normal.iconColorToken", "Icon color", ValueKind.ThemeToken, ["button", "states", "normal", "iconColorToken"], "theme.colors.icon", Options: ThemeColorOptions),
        ["component.button.states.active.surface.editor"] = new("component.button.states.active.surface.editor", "Surface", ValueKind.ComponentPreset, ["button", "states", "active", "surfaceSlot", "presetId"], "default"),
        ["component.button.states.active.label.editor"] = new("component.button.states.active.label.editor", "Label", ValueKind.ComponentPreset, ["button", "states", "active", "labelSlot", "presetId"], "default"),
        ["component.button.states.active.iconColorToken"] = new("component.button.states.active.iconColorToken", "Icon color", ValueKind.ThemeToken, ["button", "states", "active", "iconColorToken"], "theme.colors.accent", Options: ThemeColorOptions),
        ["component.button.states.pushed.surface.editor"] = new("component.button.states.pushed.surface.editor", "Surface", ValueKind.ComponentPreset, ["button", "states", "pushed", "surfaceSlot", "presetId"], "default"),
        ["component.button.states.pushed.label.editor"] = new("component.button.states.pushed.label.editor", "Label", ValueKind.ComponentPreset, ["button", "states", "pushed", "labelSlot", "presetId"], "default"),
        ["component.button.states.pushed.iconColorToken"] = new("component.button.states.pushed.iconColorToken", "Icon color", ValueKind.ThemeToken, ["button", "states", "pushed", "iconColorToken"], "theme.colors.accent", Options: ThemeColorOptions),
        ["component.button.states.disabled.surface.editor"] = new("component.button.states.disabled.surface.editor", "Surface", ValueKind.ComponentPreset, ["button", "states", "disabled", "surfaceSlot", "presetId"], "default"),
        ["component.button.states.disabled.label.editor"] = new("component.button.states.disabled.label.editor", "Label", ValueKind.ComponentPreset, ["button", "states", "disabled", "labelSlot", "presetId"], "default"),
        ["component.button.states.disabled.iconColorToken"] = new("component.button.states.disabled.iconColorToken", "Icon color", ValueKind.ThemeToken, ["button", "states", "disabled", "iconColorToken"], "theme.colors.icon", Options: ThemeColorOptions),

        ["component.label.dimensionMode"] = new("component.label.dimensionMode", "Dimension mode", ValueKind.OptionToken, ["label", "dimensionMode"], "content", Options: DimensionModeOptions),
        ["component.label.size"] = new("component.label.size", "Size", ValueKind.IntegerPair, ["label", "size"], "120|32", PairLabels: new("W", "H")),
        ["component.label.padding"] = new("component.label.padding", "Padding", ValueKind.ThemeTokenPair, ["label", "padding"], "theme.spacing.m|theme.spacing.s", PairLabels: new("X", "Y"), Options: SpacingTokenOptions),
        ["component.label.surface.editor"] = new("component.label.surface.editor", "Surface", ValueKind.ComponentPreset, ["label", "surfaceSlot", "presetId"], "Label"),
        ["component.label.textColorToken"] = new("component.label.textColorToken", "Text color", ValueKind.ThemeToken, ["label", "textColorToken"], "theme.colors.textPrimary", Options: ThemeColorOptions),
        ["component.label.textTypography"] = new("component.label.textTypography", "Text typography", ValueKind.TypographyStyle, ["label", "textTypography"], TypographyStyleValue.CreateDefault("theme.typography.sizes.s")),
        ["component.label.textAlign"] = new("component.label.textAlign", "Text align", ValueKind.OptionToken, ["label", "textAlign"], "center", Options: TextAlignOptions),
        ["component.label.textGapToken"] = new("component.label.textGapToken", "Text gap", ValueKind.ThemeToken, ["label", "textGapToken"], "theme.spacing.xs", Options: SpacingTokenOptions),
        ["component.label.reserveSubtextSpace"] = new("component.label.reserveSubtextSpace", "Reserve empty subtext", ValueKind.Boolean, ["label", "reserveSubtextSpace"], "false"),
        ["component.label.subtextPlacement"] = new("component.label.subtextPlacement", "Placement", ValueKind.AlignmentPlacement, ["label", "subtextPlacement"], """{"mode":"edge","alignX":0.5,"alignY":1,"offsetX":0,"offsetY":0}"""),
        ["component.label.subtextColorToken"] = new("component.label.subtextColorToken", "Subtext color", ValueKind.ThemeToken, ["label", "subtextColorToken"], "theme.colors.textSecondary", Options: ThemeColorOptions),
        ["component.label.subtextTypography"] = new("component.label.subtextTypography", "Subtext typography", ValueKind.TypographyStyle, ["label", "subtextTypography"], TypographyStyleValue.CreateDefault("theme.typography.sizes.xs")),

        ["component.audio.padding"] = new("component.audio.padding", "Padding", ValueKind.ThemeTokenPair, ["audio", "padding"], "theme.spacing.l|theme.spacing.m", PairLabels: new("X", "Y"), Options: SpacingTokenOptions),
        ["component.audio.surface.editor"] = new("component.audio.surface.editor", "Surface", ValueKind.ComponentPreset, ["audio", "surfaceSlot", "presetId"], "default"),
        ["component.audio.durationLabel.editor"] = new("component.audio.durationLabel.editor", "Duration label", ValueKind.ComponentPreset, ["audio", "durationLabelSlot", "presetId"], "default"),
        ["component.audio.playCircleSize"] = new("component.audio.playCircleSize", "Play size", ValueKind.Integer, ["audio", "playCircleSize"], "32"),
        ["component.audio.playIconPadding"] = new("component.audio.playIconPadding", "Play icon padding", ValueKind.ThemeToken, ["audio", "playIconPadding"], "theme.spacing.m", Options: SpacingTokenOptions),
        ["component.audio.playColorToken"] = new("component.audio.playColorToken", "Play color", ValueKind.ThemeToken, ["audio", "playColorToken"], "theme.icons.accent", Options: ThemeColorOptions),
        ["component.audio.playIconColorToken"] = new("component.audio.playIconColorToken", "Play icon color", ValueKind.ThemeToken, ["audio", "playIconColorToken"], "theme.icons.secondary", Options: ThemeColorOptions),
        ["component.audio.waveformColorToken"] = new("component.audio.waveformColorToken", "Waveform color", ValueKind.ThemeToken, ["audio", "waveformColorToken"], "theme.icons.primary", Options: ThemeColorOptions),
        ["component.audio.waveformPlayedColorToken"] = new("component.audio.waveformPlayedColorToken", "Waveform played", ValueKind.ThemeToken, ["audio", "waveformPlayedColorToken"], "theme.icons.accent", Options: ThemeColorOptions),
        ["component.audio.waveformBarCount"] = new("component.audio.waveformBarCount", "Waveform bars", ValueKind.Integer, ["audio", "waveformBarCount"], "28"),
        ["component.audio.waveformGap"] = new("component.audio.waveformGap", "Waveform gap", ValueKind.ThemeToken, ["audio", "waveformGap"], "theme.spacing.xs", Options: SpacingTokenOptions),
        ["component.audio.waveformMinHeight"] = new("component.audio.waveformMinHeight", "Waveform min height", ValueKind.Integer, ["audio", "waveformMinHeight"], "4"),
        ["component.audio.waveformMaxHeight"] = new("component.audio.waveformMaxHeight", "Waveform max height", ValueKind.Integer, ["audio", "waveformMaxHeight"], "22"),
        ["component.audio.progressKnobSize"] = new("component.audio.progressKnobSize", "Progress knob", ValueKind.Integer, ["audio", "progressKnobSize"], "9"),
        ["component.audio.avatar.showAvatar"] = new("component.audio.avatar.showAvatar", "Show avatar", ValueKind.Boolean, ["audio", "avatarSlot", "showAvatar"], "true"),
        ["component.audio.avatar.placement"] = new("component.audio.avatar.placement", "Placement", ValueKind.AlignmentPlacement, ["audio", "avatarSlot", "placement"], """{"mode":"edge","alignX":1,"alignY":0.5,"offsetX":4,"offsetY":0}"""),
        ["component.audio.avatar.presetId"] = new("component.audio.avatar.presetId", "Variant", ValueKind.OptionToken, ["audio", "avatarSlot", "presetId"], "default"),
        ["component.audio.avatar.editor"] = new("component.audio.avatar.editor", "Avatar", ValueKind.ComponentPreset, ["audio", "avatarSlot", "presetId"], "default"),
        ["component.audio.badge.showBadge"] = new("component.audio.badge.showBadge", "Show badge", ValueKind.Boolean, ["audio", "badgeSlot", "showBadge"], "false"),
        ["component.audio.badge.size"] = new("component.audio.badge.size", "Badge size", ValueKind.Integer, ["audio", "badgeSlot", "size"], "16"),
        ["component.audio.badge.iconToken"] = new("component.audio.badge.iconToken", "Icon", ValueKind.IconToken, ["audio", "badgeSlot", "iconToken"], "media_mic"),
        ["component.audio.badge.placement"] = new("component.audio.badge.placement", "Placement", ValueKind.AlignmentPlacement, ["audio", "badgeSlot", "placement"], """{"mode":"center","alignX":1,"alignY":1,"offsetX":0,"offsetY":0}"""),
        ["component.audio.badge.presetId"] = new("component.audio.badge.presetId", "Variant", ValueKind.OptionToken, ["audio", "badgeSlot", "presetId"], "default"),
        ["component.audio.badge.editor"] = new("component.audio.badge.editor", "Badge", ValueKind.ComponentPreset, ["audio", "badgeSlot", "presetId"], "default"),

        ["component.statusBar.foregroundColorToken"] = new("component.statusBar.foregroundColorToken", "Foreground", ValueKind.ThemeToken, ["foregroundColorToken"], "theme.icons.primary", Options: ThemeColorOptions),
        ["component.statusBar.backgroundColorToken"] = new("component.statusBar.backgroundColorToken", "Background", ValueKind.ThemeToken, ["backgroundColorToken"], "theme.colors.background", Options: ThemeColorOptions),
        ["component.statusBar.backgroundAlpha"] = new("component.statusBar.backgroundAlpha", "Background alpha", ValueKind.Alpha, ["backgroundAlpha"], "1"),
        ["component.statusBar.layout.height"] = new("component.statusBar.layout.height", "Height", ValueKind.Integer, ["layout", "height"], "54"),
        ["component.statusBar.layout.itemSize"] = new("component.statusBar.layout.itemSize", "Item size", ValueKind.Integer, ["layout", "itemSize"], "18"),
        ["component.statusBar.layout.gap"] = new("component.statusBar.layout.gap", "Gap", ValueKind.ThemeToken, ["layout", "gap"], "theme.spacing.m", Options: SpacingTokenOptions),
        ["component.statusBar.layout.sidePadding"] = new("component.statusBar.layout.sidePadding", "Side padding", ValueKind.ThemeToken, ["layout", "sidePadding"], "theme.spacing.xxl", Options: SpacingTokenOptions),

        ["component.navigationBar.foregroundColorToken"] = new("component.navigationBar.foregroundColorToken", "Foreground", ValueKind.ThemeToken, ["foregroundColorToken"], "theme.icons.primary", Options: ThemeColorOptions),
        ["component.navigationBar.backgroundColorToken"] = new("component.navigationBar.backgroundColorToken", "Background", ValueKind.ThemeToken, ["backgroundColorToken"], "theme.colors.background", Options: ThemeColorOptions),
        ["component.navigationBar.backgroundAlpha"] = new("component.navigationBar.backgroundAlpha", "Background alpha", ValueKind.Alpha, ["backgroundAlpha"], "1"),
        ["component.navigationBar.type"] = new("component.navigationBar.type", "Type", ValueKind.OptionToken, ["type"], "buttons", Options: NavigationBarTypeOptions),
        ["component.navigationBar.layout.height"] = new("component.navigationBar.layout.height", "Height", ValueKind.Integer, ["layout", "height"], "34"),
        ["component.navigationBar.layout.itemSize"] = new("component.navigationBar.layout.itemSize", "Item size", ValueKind.Integer, ["layout", "itemSize"], "18"),
        ["component.navigationBar.layout.sidePadding"] = new("component.navigationBar.layout.sidePadding", "Side padding", ValueKind.ThemeToken, ["layout", "sidePadding"], "theme.spacing.xxl", Options: SpacingTokenOptions),
        ["component.navigationBar.layout.strokeWidth"] = new("component.navigationBar.layout.strokeWidth", "Stroke width", ValueKind.Integer, ["layout", "strokeWidth"], "2"),
        ["component.navigationBar.layout.cornerRadius"] = new("component.navigationBar.layout.cornerRadius", "Corner radius", ValueKind.Integer, ["layout", "cornerRadius"], "3"),
        ["component.navigationBar.layout.filled"] = new("component.navigationBar.layout.filled", "Filled", ValueKind.Boolean, ["layout", "filled"], "false"),
        ["component.navigationBar.gesture.width"] = new("component.navigationBar.gesture.width", "Width", ValueKind.Integer, ["gesture", "width"], "134"),
        ["component.navigationBar.gesture.height"] = new("component.navigationBar.gesture.height", "Height", ValueKind.Integer, ["gesture", "height"], "5"),
        ["component.navigationBar.gesture.cornerRadius"] = new("component.navigationBar.gesture.cornerRadius", "Corner radius", ValueKind.Integer, ["gesture", "cornerRadius"], "999"),

        ["component.media.surface.editor"] = new("component.media.surface.editor", "Surface", ValueKind.ComponentPreset, ["media", "surfaceSlot", "presetId"], "default"),
        ["component.media.controlBarHeight"] = new("component.media.controlBarHeight", "Control bar height", ValueKind.Integer, ["media", "controlBarHeight"], "56", Number: new NumberDefinition(1, 512, 1, 0)),
        ["component.media.iconBarPadding"] = new("component.media.iconBarPadding", "Icon bar padding", ValueKind.ThemeTokenPair, ["media", "iconBarPadding"], "theme.spacing.xl|theme.spacing.xl", PairLabels: new PairFieldLabels("X", "Y"), Options: SpacingTokenOptions),
        ["component.media.iconColorTokenOverride"] = new("component.media.iconColorTokenOverride", "Icon color override", ValueKind.ThemeToken, ["media", "iconColorTokenOverride"], "theme.icons.alternate", Options: ThemeColorOptions),
        ["component.media.inlineTopIconBar.editor"] = new("component.media.inlineTopIconBar.editor", "Top icon bar", ValueKind.ComponentPreset, ["media", "inlineTopIconBarSlot", "presetId"], "default"),
        ["component.media.inlineCenterIconBar.editor"] = new("component.media.inlineCenterIconBar.editor", "Center icon bar", ValueKind.ComponentPreset, ["media", "inlineCenterIconBarSlot", "presetId"], "default"),
        ["component.media.inlineBottomIconBar.editor"] = new("component.media.inlineBottomIconBar.editor", "Bottom icon bar", ValueKind.ComponentPreset, ["media", "inlineBottomIconBarSlot", "presetId"], "default"),
        ["component.media.fullScreenTopIconBar.editor"] = new("component.media.fullScreenTopIconBar.editor", "Top icon bar", ValueKind.ComponentPreset, ["media", "fullScreenTopIconBarSlot", "presetId"], "default"),
        ["component.media.fullScreenCenterIconBar.editor"] = new("component.media.fullScreenCenterIconBar.editor", "Center icon bar", ValueKind.ComponentPreset, ["media", "fullScreenCenterIconBarSlot", "presetId"], "default"),
        ["component.media.fullScreenBottomIconBar.editor"] = new("component.media.fullScreenBottomIconBar.editor", "Bottom icon bar", ValueKind.ComponentPreset, ["media", "fullScreenBottomIconBarSlot", "presetId"], "default"),
        ["component.media.idleText.enabled"] = new("component.media.idleText.enabled", "Enabled", ValueKind.Boolean, ["media", "idleText", "enabled"], "false"),
        ["component.media.idleText.mode"] = new("component.media.idleText.mode", "Mode", ValueKind.OptionToken, ["media", "idleText", "mode"], "free", Options: MediaTextOverlayModeOptions),
        ["component.media.idleText.text"] = new("component.media.idleText.text", "Text", ValueKind.StringMultiline, ["media", "idleText", "text"], ""),
        ["component.media.idleText.targetSeconds"] = new("component.media.idleText.targetSeconds", "Target seconds", ValueKind.Decimal, ["media", "idleText", "targetSeconds"], "0", Number: new NumberDefinition(0, 86400, 0.1m, 1)),
        ["component.media.idleText.label.editor"] = new("component.media.idleText.label.editor", "Label", ValueKind.ComponentPreset, ["media", "idleText", "labelSlot", "presetId"], "default"),
        ["component.media.idleText.placement"] = new("component.media.idleText.placement", "Placement", ValueKind.AlignmentPlacement, ["media", "idleText", "placement"], """{"mode":"center","alignX":0.5,"alignY":0.5,"offsetX":0,"offsetY":0}"""),
        ["component.media.playText.enabled"] = new("component.media.playText.enabled", "Enabled", ValueKind.Boolean, ["media", "playText", "enabled"], "true"),
        ["component.media.playText.mode"] = new("component.media.playText.mode", "Mode", ValueKind.OptionToken, ["media", "playText", "mode"], "countDown", Options: MediaTextOverlayModeOptions),
        ["component.media.playText.text"] = new("component.media.playText.text", "Text", ValueKind.StringMultiline, ["media", "playText", "text"], ""),
        ["component.media.playText.targetSeconds"] = new("component.media.playText.targetSeconds", "Target seconds", ValueKind.Decimal, ["media", "playText", "targetSeconds"], "0", Number: new NumberDefinition(0, 86400, 0.1m, 1)),
        ["component.media.playText.label.editor"] = new("component.media.playText.label.editor", "Label", ValueKind.ComponentPreset, ["media", "playText", "labelSlot", "presetId"], "default"),
        ["component.media.playText.placement"] = new("component.media.playText.placement", "Placement", ValueKind.AlignmentPlacement, ["media", "playText", "placement"], """{"mode":"center","alignX":0.5,"alignY":1,"offsetX":0,"offsetY":-18}"""),
        ["component.media.controlsFadeDelayMs"] = new("component.media.controlsFadeDelayMs", "Fade delay ms", ValueKind.Integer, ["media", "controlsFadeDelayMs"], "900", Number: new NumberDefinition(0, 10000, 10, 0)),
        ["component.media.controlsFadeDurationMs"] = new("component.media.controlsFadeDurationMs", "Fade duration ms", ValueKind.Integer, ["media", "controlsFadeDurationMs"], "180", Number: new NumberDefinition(0, 10000, 10, 0)),
        ["component.media.motion"] = new("component.media.motion", "Motion", ValueKind.Motion, ["media", "motion"], (MotionVariantValue.Default with { Scale = true }).ToJsonString()),

        ["component.bubble.surface.editor"] = new("component.bubble.surface.editor", "Surface", ValueKind.ComponentPreset, ["bubble", "surfaceSlot", "presetId"], "default"),
        ["component.bubble.textBox.editor"] = new("component.bubble.textBox.editor", "Text box", ValueKind.ComponentPreset, ["bubble", "textBoxSlot", "presetId"], "default"),
        ["component.bubble.padding"] = new("component.bubble.padding", "Padding", ValueKind.ThemeTokenPair, ["bubble", "padding"], "theme.spacing.l|theme.spacing.m", PairLabels: new("X", "Y"), Options: SpacingTokenOptions),
        ["component.bubble.media.type"] = new("component.bubble.media.type", "Media type", ValueKind.OptionToken, ["bubble", "mediaType"], "none", Options: MediaTypeOptions),
        ["component.bubble.media.position"] = new("component.bubble.media.position", "Media position", ValueKind.OptionToken, ["bubble", "mediaPosition"], "bottom", Options: BubbleMediaPositionOptions),
        ["component.bubble.media.image.editor"] = new("component.bubble.media.image.editor", "Image variant", ValueKind.ComponentPreset, ["bubble", "imageMediaSlot", "presetId"], "default"),
        ["component.bubble.media.video.editor"] = new("component.bubble.media.video.editor", "Video variant", ValueKind.ComponentPreset, ["bubble", "videoMediaSlot", "presetId"], "default"),
        ["component.bubble.media.audio.editor"] = new("component.bubble.media.audio.editor", "Audio variant", ValueKind.ComponentPreset, ["bubble", "audioSlot", "presetId"], "default"),
        ["component.bubble.incomingBackground"] = new("component.bubble.incomingBackground", "Incoming background", ValueKind.PaletteColorPair, ["bubble", "incomingBackground"], "gray_080|gray_020", PairLabels: new("Light", "Dark")),
        ["component.bubble.incomingText"] = new("component.bubble.incomingText", "Incoming text", ValueKind.PaletteColorPair, ["bubble", "incomingText"], "gray_010|gray_100", PairLabels: new("Light", "Dark")),
        ["component.bubble.systemBackground"] = new("component.bubble.systemBackground", "System background", ValueKind.PaletteColorPair, ["bubble", "systemBackground"], "gray_080|gray_020", PairLabels: new("Light", "Dark")),
        ["component.bubble.systemText"] = new("component.bubble.systemText", "System text", ValueKind.PaletteColorPair, ["bubble", "systemText"], "gray_010|gray_100", PairLabels: new("Light", "Dark")),
        ["component.bubble.outgoingBackground"] = new("component.bubble.outgoingBackground", "Outgoing background", ValueKind.PaletteColorPair, ["bubble", "outgoingBackground"], "aqua_green|aqua_green", PairLabels: new("Light", "Dark")),
        ["component.bubble.outgoingText"] = new("component.bubble.outgoingText", "Outgoing text", ValueKind.PaletteColorPair, ["bubble", "outgoingText"], "gray_100|gray_100", PairLabels: new("Light", "Dark")),
        ["component.bubble.actorLabel.showLabel"] = new("component.bubble.actorLabel.showLabel", "Show actor label", ValueKind.Boolean, ["bubble", "actorLabelSlot", "showLabel"], "false"),
        ["component.bubble.actorLabel.useActorColor"] = new("component.bubble.actorLabel.useActorColor", "Use actor color", ValueKind.Boolean, ["bubble", "actorLabelSlot", "useActorColor"], "false"),
        ["component.bubble.actorLabel.placement"] = new("component.bubble.actorLabel.placement", "Actor label placement", ValueKind.AlignmentPlacement, ["bubble", "actorLabelSlot", "placement"], """{"mode":"edge","alignX":0.5,"alignY":0,"offsetX":0,"offsetY":-4}"""),
        ["component.bubble.actorLabel.editor"] = new("component.bubble.actorLabel.editor", "Actor label", ValueKind.ComponentPreset, ["bubble", "actorLabelSlot", "presetId"], "default"),
        ["component.bubble.avatar.showAvatar"] = new("component.bubble.avatar.showAvatar", "Show avatar", ValueKind.Boolean, ["bubble", "avatarSlot", "showAvatar"], "false"),
        ["component.bubble.avatar.placement"] = new("component.bubble.avatar.placement", "Avatar placement", ValueKind.AlignmentPlacement, ["bubble", "avatarSlot", "placement"], """{"mode":"edge","alignX":0,"alignY":1,"offsetX":-8,"offsetY":0}"""),
        ["component.bubble.avatar.editor"] = new("component.bubble.avatar.editor", "Avatar", ValueKind.ComponentPreset, ["bubble", "avatarSlot", "presetId"], "default"),
        ["component.bubble.status.size"] = new("component.bubble.status.size", "Status icon size", ValueKind.ThemeToken, ["bubble", "status", "sizeToken"], "theme.iconSizes.s", Options: IconSizeTokenOptions),
        ["component.bubble.status.textSize"] = new("component.bubble.status.textSize", "Status text size", ValueKind.ThemeToken, ["bubble", "status", "textSizeToken"], "theme.iconSizes.s", Options: IconSizeTokenOptions),
        ["component.bubble.status.gap"] = new("component.bubble.status.gap", "Status gap", ValueKind.ThemeToken, ["bubble", "status", "gapToken"], "theme.spacing.xs", Options: SpacingTokenOptions),
        ["component.bubble.status.sent.icon"] = new("component.bubble.status.sent.icon", "Sent icon", ValueKind.IconToken, ["bubble", "status", "sent", "iconToken"], "system_check"),
        ["component.bubble.status.sent.color"] = new("component.bubble.status.sent.color", "Sent color", ValueKind.ThemeToken, ["bubble", "status", "sent", "colorToken"], "theme.icons.secondary", Options: ThemeColorOptions),
        ["component.bubble.status.delivered.icon"] = new("component.bubble.status.delivered.icon", "Delivered icon", ValueKind.IconToken, ["bubble", "status", "delivered", "iconToken"], "system_check"),
        ["component.bubble.status.delivered.color"] = new("component.bubble.status.delivered.color", "Delivered color", ValueKind.ThemeToken, ["bubble", "status", "delivered", "colorToken"], "theme.icons.secondary", Options: ThemeColorOptions),
        ["component.bubble.status.read.icon"] = new("component.bubble.status.read.icon", "Read icon", ValueKind.IconToken, ["bubble", "status", "read", "iconToken"], "system_check"),
        ["component.bubble.status.read.color"] = new("component.bubble.status.read.color", "Read color", ValueKind.ThemeToken, ["bubble", "status", "read", "colorToken"], "theme.icons.accent", Options: ThemeColorOptions),
    };

    public static ComponentClassFieldDescriptor Get(string fieldId)
    {
        if (Fields.TryGetValue(fieldId, out var field))
        {
            return field;
        }

        throw new InvalidOperationException($"Unknown component class field '{fieldId}'.");
    }

    public static bool IsRuntimeOverrideField(string fieldId) =>
        Fields.TryGetValue(fieldId, out var field)
        && field.IsEditable
        && field.JsonPath.Length > 0;
}
