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
    string ComponentPresetType = "");

internal static class ComponentClassFieldCatalog
{
    public const string EmptyIconSlots = """{"left":[],"center":[],"right":[]}""";

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
        new("theme.icons.accent", "icons.accent"),
        new("theme.borders.primary", "borders.primary"),
        new("theme.borders.secondary", "borders.secondary"),
        new("theme.borders.alternate", "borders.alternate"),
        new("theme.cursor.color", "cursor.color"),
        new("theme.keyboard.background", "keyboard.background"),
        new("theme.keyboard.keyBackground", "keyboard.keyBackground"),
        new("theme.keyboard.specialKeyBackground", "keyboard.specialKeyBackground"),
        new("theme.keyboard.pressedKeyBackground", "keyboard.pressedKeyBackground"),
        new("theme.keyboard.popoverBackground", "keyboard.popoverBackground"),
        new("theme.keyboard.text", "keyboard.text"),
    ];

    private static readonly FieldOption[] RadiusTokenOptions =
    [
        new("theme.radii.none", "radii.none"),
        new("theme.radii.control", "radii.control"),
        new("theme.radii.card", "radii.card"),
        new("theme.radii.panel", "radii.panel"),
        new("theme.radii.surface", "radii.surface"),
        new("theme.radii.pill", "radii.pill"),
        new("theme.radii.avatar", "radii.avatar"),
        new("theme.radii.full", "radii.full"),
    ];

    private static readonly FieldOption[] TypographySizeOptions =
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

    private static readonly FieldOption[] ButtonIconSizeModeOptions =
    [
        new("fixed", "Fixed"),
        new("iconSize", "Icon size + padding"),
    ];

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
            "size",
            "Size",
            "size",
            ValueKind.ThemeToken,
            ComponentInputBindingSource.Variant,
            "theme.iconSizes.xl",
            Options: IconSizeTokenOptions),
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
        new(
            "actionIconNumber",
            "Action icon #",
            "actionIconNumber",
            ValueKind.Integer,
            ComponentInputBindingSource.Variant,
            "0",
            Number: new NumberDefinition(0, 10, 1, 0),
            UiGroupId: "actionIcon",
            UiGroupLabel: "Action icon"),
        new(
            "actionBackgroundAlpha",
            "Action bg alpha",
            "actionBackgroundAlpha",
            ValueKind.Alpha,
            ComponentInputBindingSource.Variant,
            "1",
            Number: new NumberDefinition(0, 1, 0.05m, 2, true),
            UiGroupId: "actionIcon",
            UiGroupLabel: "Action icon"),
        new(
            "actionBackgroundColor",
            "Action bg color",
            "actionBackgroundColor",
            ValueKind.PaletteColorToken,
            ComponentInputBindingSource.Variant,
            "aqua_green",
            UiGroupId: "actionIcon",
            UiGroupLabel: "Action icon"),
        new(
            "actionIconColor",
            "Action icon color",
            "actionIconColor",
            ValueKind.PaletteColorToken,
            ComponentInputBindingSource.Variant,
            "gray_100",
            UiGroupId: "actionIcon",
            UiGroupLabel: "Action icon"),
        new(
            "buttonIconPresetId",
            "Button icon",
            "buttonIconPresetId",
            ValueKind.ComponentPreset,
            ComponentInputBindingSource.Calculated,
            "",
            ComponentType: "buttonIcon"),
        new(
            "icons",
            "Icons",
            "icons",
            ValueKind.IconTokenList,
            ComponentInputBindingSource.Variant,
            "[]"),
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
            "buttonIconSlot",
            "Icon button",
            "buttonIconSlot",
            ValueKind.ComponentPreset,
            ComponentInputBindingSource.Variant,
            "",
            ComponentType: "buttonIcon"),
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

    private static readonly FieldOption[] PressedEffectOptions =
    [
        new("popup", "Popup"),
        new("scale", "Scale in place"),
        new("none", "None"),
    ];

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
        ["component.style.cornerRadiusToken"] = new("component.style.cornerRadiusToken", "Corner radius", ValueKind.ThemeToken, ["style", "cornerRadiusToken"], "theme.radii.surface", Options: RadiusTokenOptions),
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
        ["component.cursor.fadeFrames"] = new("component.cursor.fadeFrames", "Fade frames", ValueKind.Integer, ["cursor", "fadeFrames"], "12"),

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
        ["component.iconRow.size"] = new("component.iconRow.size", "Size", ValueKind.ThemeToken, ["iconRow", "size"], "theme.iconSizes.xl", Options: IconSizeTokenOptions),
        ["component.iconRow.gap"] = new("component.iconRow.gap", "Gap", ValueKind.ThemeToken, ["iconRow", "gap"], "theme.spacing.s", Options: SpacingTokenOptions),
        ["component.iconRow.buttonIcon.editor"] = new("component.iconRow.buttonIcon.editor", "Button icon", ValueKind.ComponentPreset, ["iconRow", "buttonIconSlot", "presetId"], "default"),

        ["component.iconBar.edgePadding"] = new("component.iconBar.edgePadding", "Edge padding", ValueKind.ThemeToken, ["iconBar", "edgePadding"], "theme.spacing.m", Options: SpacingTokenOptions),
        ["component.iconBar.iconButton.editor"] = new("component.iconBar.iconButton.editor", "Button icon", ValueKind.ComponentPreset, ["iconBar", "iconButtonSlot", "presetId"], "default"),
        ["component.iconBar.idleLeftIconRow.editor"] = new("component.iconBar.idleLeftIconRow.editor", "Idle left row", ValueKind.ComponentPreset, ["iconBar", "idleLeftIconRowSlot", "presetId"], "default"),
        ["component.iconBar.idleLeftIconRow.inputs"] = new(
            "component.iconBar.idleLeftIconRow.inputs",
            "Idle left settings",
            ValueKind.ComponentInputBindings,
            ["iconBar", "idleLeftIconRowInputs"],
            """{"size":"theme.iconSizes.xl","gap":"theme.spacing.m","orientation":"horizontal","icons":["media_camera"]}""",
            ComponentInputBindings: IconRowParentInputBindings),
        ["component.iconBar.idleCenterIconRow.editor"] = new("component.iconBar.idleCenterIconRow.editor", "Idle center row", ValueKind.ComponentPreset, ["iconBar", "idleCenterIconRowSlot", "presetId"], "default"),
        ["component.iconBar.idleCenterIconRow.inputs"] = new(
            "component.iconBar.idleCenterIconRow.inputs",
            "Idle center settings",
            ValueKind.ComponentInputBindings,
            ["iconBar", "idleCenterIconRowInputs"],
            """{"size":"theme.iconSizes.xl","gap":"theme.spacing.m","orientation":"horizontal","icons":["media_play"],"actionIconNumber":1,"actionBackgroundAlpha":1,"actionBackgroundColor":"aqua_green","actionIconColor":"gray_100"}""",
            ComponentInputBindings: IconRowParentInputBindings),
        ["component.iconBar.idleRightIconRow.editor"] = new("component.iconBar.idleRightIconRow.editor", "Idle right row", ValueKind.ComponentPreset, ["iconBar", "idleRightIconRowSlot", "presetId"], "default"),
        ["component.iconBar.idleRightIconRow.inputs"] = new(
            "component.iconBar.idleRightIconRow.inputs",
            "Idle right settings",
            ValueKind.ComponentInputBindings,
            ["iconBar", "idleRightIconRowInputs"],
            """{"size":"theme.iconSizes.xl","gap":"theme.spacing.m","orientation":"horizontal","icons":["nav_more_horizontal"]}""",
            ComponentInputBindings: IconRowParentInputBindings),
        ["component.iconBar.activeLeftIconRow.editor"] = new("component.iconBar.activeLeftIconRow.editor", "Active left row", ValueKind.ComponentPreset, ["iconBar", "activeLeftIconRowSlot", "presetId"], "default"),
        ["component.iconBar.activeLeftIconRow.inputs"] = new(
            "component.iconBar.activeLeftIconRow.inputs",
            "Active left settings",
            ValueKind.ComponentInputBindings,
            ["iconBar", "activeLeftIconRowInputs"],
            """{"size":"theme.iconSizes.xl","gap":"theme.spacing.m","orientation":"horizontal","icons":["media_camera"]}""",
            ComponentInputBindings: IconRowParentInputBindings),
        ["component.iconBar.activeCenterIconRow.editor"] = new("component.iconBar.activeCenterIconRow.editor", "Active center row", ValueKind.ComponentPreset, ["iconBar", "activeCenterIconRowSlot", "presetId"], "default"),
        ["component.iconBar.activeCenterIconRow.inputs"] = new(
            "component.iconBar.activeCenterIconRow.inputs",
            "Active center settings",
            ValueKind.ComponentInputBindings,
            ["iconBar", "activeCenterIconRowInputs"],
            """{"size":"theme.iconSizes.xl","gap":"theme.spacing.m","orientation":"horizontal","icons":["media_pause"],"actionIconNumber":1,"actionBackgroundAlpha":1,"actionBackgroundColor":"aqua_green","actionIconColor":"gray_100"}""",
            ComponentInputBindings: IconRowParentInputBindings),
        ["component.iconBar.activeRightIconRow.editor"] = new("component.iconBar.activeRightIconRow.editor", "Active right row", ValueKind.ComponentPreset, ["iconBar", "activeRightIconRowSlot", "presetId"], "default"),
        ["component.iconBar.activeRightIconRow.inputs"] = new(
            "component.iconBar.activeRightIconRow.inputs",
            "Active right settings",
            ValueKind.ComponentInputBindings,
            ["iconBar", "activeRightIconRowInputs"],
            """{"size":"theme.iconSizes.xl","gap":"theme.spacing.m","orientation":"horizontal","icons":["nav_more_horizontal"]}""",
            ComponentInputBindings: IconRowParentInputBindings),

        ["component.avatar.defaultSize"] = new("component.avatar.defaultSize", "Default size", ValueKind.Integer, ["avatar", "defaultSize"], "48"),
        ["component.avatar.cornerRadiusToken"] = new("component.avatar.cornerRadiusToken", "Avatar radius", ValueKind.ThemeToken, ["avatar", "cornerRadiusToken"], "theme.radii.avatar", Options: RadiusTokenOptions),
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
            ComponentInputBindings: TextBoxParentInputBindings),

        ["component.keyboard.language"] = new("component.keyboard.language", "Language", ValueKind.OptionToken, ["keyboard", "language"], "es", Options: KeyboardLanguageOptions),
        ["component.keyboard.backgroundColorToken"] = new("component.keyboard.backgroundColorToken", "Background", ValueKind.ThemeToken, ["keyboard", "backgroundColorToken"], "theme.keyboard.background", Options: ThemeColorOptions),
        ["component.keyboard.backgroundAlpha"] = new("component.keyboard.backgroundAlpha", "Background alpha", ValueKind.Alpha, ["keyboard", "backgroundAlpha"], "1"),
        ["component.keyboard.keyBackgroundColorToken"] = new("component.keyboard.keyBackgroundColorToken", "Key background", ValueKind.ThemeToken, ["keyboard", "keyBackgroundColorToken"], "theme.keyboard.keyBackground", Options: ThemeColorOptions),
        ["component.keyboard.specialKeyBackgroundColorToken"] = new("component.keyboard.specialKeyBackgroundColorToken", "Special key background", ValueKind.ThemeToken, ["keyboard", "specialKeyBackgroundColorToken"], "theme.keyboard.specialKeyBackground", Options: ThemeColorOptions),
        ["component.keyboard.pressedKeyBackgroundColorToken"] = new("component.keyboard.pressedKeyBackgroundColorToken", "Pressed key background", ValueKind.ThemeToken, ["keyboard", "pressedKeyBackgroundColorToken"], "theme.keyboard.pressedKeyBackground", Options: ThemeColorOptions),
        ["component.keyboard.keyTextColorToken"] = new("component.keyboard.keyTextColorToken", "Key text", ValueKind.ThemeToken, ["keyboard", "keyTextColorToken"], "theme.keyboard.text", Options: ThemeColorOptions),
        ["component.keyboard.typography"] = new("component.keyboard.typography", "Typography", ValueKind.TypographyStyle, ["keyboard", "typography"], TypographyStyleValue.CreateDefault("theme.typography.sizes.s")),
        ["component.keyboard.keyPadding"] = new("component.keyboard.keyPadding", "Key padding", ValueKind.ThemeToken, ["keyboard", "keyPadding"], "theme.spacing.s", Options: SpacingTokenOptions),
        ["component.keyboard.keyCornerRadiusToken"] = new("component.keyboard.keyCornerRadiusToken", "Key radius", ValueKind.ThemeToken, ["keyboard", "keyCornerRadiusToken"], "theme.radii.control", Options: RadiusTokenOptions),
        ["component.keyboard.keyBorderWidth"] = new("component.keyboard.keyBorderWidth", "Key border width", ValueKind.Decimal, ["keyboard", "keyBorderWidth"], "0", Number: new NumberDefinition(0, 32, 0.5m, 2)),
        ["component.keyboard.keyBorderColorToken"] = new("component.keyboard.keyBorderColorToken", "Key border color", ValueKind.ThemeToken, ["keyboard", "keyBorderColorToken"], "theme.borders.secondary", Options: ThemeColorOptions),
        ["component.keyboard.keyShadowEnabled"] = new("component.keyboard.keyShadowEnabled", "Key shadow", ValueKind.Boolean, ["keyboard", "keyShadowEnabled"], "false"),
        ["component.keyboard.pressedEffect"] = new("component.keyboard.pressedEffect", "Pressed effect", ValueKind.OptionToken, ["keyboard", "pressedEffect"], "popup", Options: PressedEffectOptions),
        ["component.keyboard.specialKeyTextScale"] = new(
            "component.keyboard.specialKeyTextScale",
            "Special key scale",
            ValueKind.Decimal,
            ["keyboard", "specialKeyTextScale"],
            "0.65",
            Number: new NumberDefinition(0.1m, 4, 0.05m, 2)),
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

        ["component.buttonIcon.sizeMode"] = new("component.buttonIcon.sizeMode", "Size mode", ValueKind.OptionToken, ["buttonIcon", "sizeMode"], "fixed", Options: ButtonIconSizeModeOptions),
        ["component.buttonIcon.size"] = new("component.buttonIcon.size", "Fixed size", ValueKind.Integer, ["buttonIcon", "size"], "48"),
        ["component.buttonIcon.iconSizeToken"] = new("component.buttonIcon.iconSizeToken", "Icon size", ValueKind.ThemeToken, ["buttonIcon", "iconSizeToken"], "theme.iconSizes.xl", Options: IconSizeTokenOptions),
        ["component.buttonIcon.iconToken"] = new("component.buttonIcon.iconToken", "Icon", ValueKind.IconToken, ["buttonIcon", "iconToken"], "media_mic"),
        ["component.buttonIcon.iconPadding"] = new("component.buttonIcon.iconPadding", "Icon padding", ValueKind.ThemeToken, ["buttonIcon", "iconPadding"], "theme.spacing.m", Options: SpacingTokenOptions),
        ["component.buttonIcon.surface.editor"] = new("component.buttonIcon.surface.editor", "Surface", ValueKind.ComponentPreset, ["buttonIcon", "surfaceSlot", "presetId"], "IconButton"),
        ["component.buttonIcon.iconColorToken"] = new("component.buttonIcon.iconColorToken", "Icon color", ValueKind.ThemeToken, ["buttonIcon", "iconColorToken"], "theme.colors.icon", Options: ThemeColorOptions),
        ["component.buttonIcon.label.showLabel"] = new("component.buttonIcon.label.showLabel", "Show label", ValueKind.Boolean, ["buttonIcon", "labelSlot", "showLabel"], "false"),
        ["component.buttonIcon.label.showSubtext"] = new("component.buttonIcon.label.showSubtext", "Show subtext", ValueKind.Boolean, ["buttonIcon", "labelSlot", "showSubtext"], "false"),
        ["component.buttonIcon.label.placement"] = new("component.buttonIcon.label.placement", "Placement", ValueKind.AlignmentPlacement, ["buttonIcon", "labelSlot", "placement"], """{"mode":"edge","alignX":0.5,"alignY":1,"offsetX":0,"offsetY":3}"""),
        ["component.buttonIcon.label.presetId"] = new("component.buttonIcon.label.presetId", "Variant", ValueKind.OptionToken, ["buttonIcon", "labelSlot", "presetId"], "default"),
        ["component.buttonIcon.label.editor"] = new("component.buttonIcon.label.editor", "Label", ValueKind.ComponentPreset, ["buttonIcon", "labelSlot", "presetId"], "default"),

        ["component.label.dimensionMode"] = new("component.label.dimensionMode", "Dimension mode", ValueKind.OptionToken, ["label", "dimensionMode"], "content", Options: DimensionModeOptions),
        ["component.label.size"] = new("component.label.size", "Size", ValueKind.IntegerPair, ["label", "size"], "120|32", PairLabels: new("W", "H")),
        ["component.label.padding"] = new("component.label.padding", "Padding", ValueKind.ThemeTokenPair, ["label", "padding"], "theme.spacing.m|theme.spacing.s", PairLabels: new("X", "Y"), Options: SpacingTokenOptions),
        ["component.label.surface.editor"] = new("component.label.surface.editor", "Surface", ValueKind.ComponentPreset, ["label", "surfaceSlot", "presetId"], "Label"),
        ["component.label.textColorToken"] = new("component.label.textColorToken", "Text color", ValueKind.ThemeToken, ["label", "textColorToken"], "theme.colors.textPrimary", Options: ThemeColorOptions),
        ["component.label.textTypography"] = new("component.label.textTypography", "Text typography", ValueKind.TypographyStyle, ["label", "textTypography"], TypographyStyleValue.CreateDefault("theme.typography.sizes.s")),
        ["component.label.textAlign"] = new("component.label.textAlign", "Text align", ValueKind.OptionToken, ["label", "textAlign"], "center", Options: TextAlignOptions),
        ["component.label.textGap"] = new("component.label.textGap", "Text gap", ValueKind.Decimal, ["label", "textGap"], "2", Number: new NumberDefinition(-64, 64, 0.5m, 2)),
        ["component.label.subtextColorToken"] = new("component.label.subtextColorToken", "Subtext color", ValueKind.ThemeToken, ["label", "subtextColorToken"], "theme.colors.textSecondary", Options: ThemeColorOptions),
        ["component.label.subtextTypography"] = new("component.label.subtextTypography", "Subtext typography", ValueKind.TypographyStyle, ["label", "subtextTypography"], TypographyStyleValue.CreateDefault("theme.typography.sizes.xs")),

        ["component.audio.padding"] = new("component.audio.padding", "Padding", ValueKind.ThemeTokenPair, ["audio", "padding"], "theme.spacing.l|theme.spacing.m", PairLabels: new("X", "Y"), Options: SpacingTokenOptions),
        ["component.audio.surface.editor"] = new("component.audio.surface.editor", "Surface", ValueKind.ComponentPreset, ["audio", "surfaceSlot", "presetId"], "default"),
        ["component.audio.textSize"] = new("component.audio.textSize", "Text size", ValueKind.Integer, ["audio", "textSize"], "13"),
        ["component.audio.textColorToken"] = new("component.audio.textColorToken", "Text color", ValueKind.ThemeToken, ["audio", "textColorToken"], "theme.icons.secondary", Options: ThemeColorOptions),
        ["component.audio.playCircleSize"] = new("component.audio.playCircleSize", "Play size", ValueKind.Integer, ["audio", "playCircleSize"], "32"),
        ["component.audio.playIconPadding"] = new("component.audio.playIconPadding", "Play icon padding", ValueKind.ThemeToken, ["audio", "playIconPadding"], "theme.spacing.m", Options: SpacingTokenOptions),
        ["component.audio.playColorToken"] = new("component.audio.playColorToken", "Play color", ValueKind.ThemeToken, ["audio", "playColorToken"], "theme.icons.accent", Options: ThemeColorOptions),
        ["component.audio.playIconColorToken"] = new("component.audio.playIconColorToken", "Play icon color", ValueKind.ThemeToken, ["audio", "playIconColorToken"], "theme.icons.secondary", Options: ThemeColorOptions),
        ["component.audio.waveformColorToken"] = new("component.audio.waveformColorToken", "Waveform color", ValueKind.ThemeToken, ["audio", "waveformColorToken"], "theme.icons.primary", Options: ThemeColorOptions),
        ["component.audio.waveformPlayedColorToken"] = new("component.audio.waveformPlayedColorToken", "Waveform played", ValueKind.ThemeToken, ["audio", "waveformPlayedColorToken"], "theme.icons.accent", Options: ThemeColorOptions),
        ["component.audio.waveformBarCount"] = new("component.audio.waveformBarCount", "Waveform bars", ValueKind.Integer, ["audio", "waveformBarCount"], "28"),
        ["component.audio.waveformBarWidth"] = new("component.audio.waveformBarWidth", "Waveform bar width", ValueKind.Integer, ["audio", "waveformBarWidth"], "3"),
        ["component.audio.waveformGap"] = new("component.audio.waveformGap", "Waveform gap", ValueKind.ThemeToken, ["audio", "waveformGap"], "theme.spacing.xs", Options: SpacingTokenOptions),
        ["component.audio.waveformMinHeight"] = new("component.audio.waveformMinHeight", "Waveform min height", ValueKind.Integer, ["audio", "waveformMinHeight"], "4"),
        ["component.audio.waveformMaxHeight"] = new("component.audio.waveformMaxHeight", "Waveform max height", ValueKind.Integer, ["audio", "waveformMaxHeight"], "22"),
        ["component.audio.progressKnobSize"] = new("component.audio.progressKnobSize", "Progress knob", ValueKind.Integer, ["audio", "progressKnobSize"], "9"),
        ["component.audio.avatar.showAvatar"] = new("component.audio.avatar.showAvatar", "Show avatar", ValueKind.Boolean, ["audio", "avatarSlot", "showAvatar"], "true"),
        ["component.audio.avatar.placement"] = new("component.audio.avatar.placement", "Placement", ValueKind.AlignmentPlacement, ["audio", "avatarSlot", "placement"], """{"mode":"edge","alignX":1,"alignY":0.5,"offsetX":4,"offsetY":0}"""),
        ["component.audio.avatar.presetId"] = new("component.audio.avatar.presetId", "Variant", ValueKind.OptionToken, ["audio", "avatarSlot", "presetId"], "default"),
        ["component.audio.avatar.editor"] = new("component.audio.avatar.editor", "Avatar", ValueKind.ComponentPreset, ["audio", "avatarSlot", "presetId"], "default"),
        ["component.audio.badge.showBadge"] = new("component.audio.badge.showBadge", "Show badge", ValueKind.Boolean, ["audio", "badgeSlot", "showBadge"], "false"),
        ["component.audio.badge.iconToken"] = new("component.audio.badge.iconToken", "Icon", ValueKind.IconToken, ["audio", "badgeSlot", "iconToken"], "media_mic"),
        ["component.audio.badge.backgroundColor"] = new("component.audio.badge.backgroundColor", "Badge color", ValueKind.PaletteColorToken, ["audio", "badgeSlot", "backgroundColor"], "blue"),
        ["component.audio.badge.iconColor"] = new("component.audio.badge.iconColor", "Icon color", ValueKind.PaletteColorToken, ["audio", "badgeSlot", "iconColor"], "gray_100"),
        ["component.audio.badge.placement"] = new("component.audio.badge.placement", "Placement", ValueKind.AlignmentPlacement, ["audio", "badgeSlot", "placement"], """{"mode":"center","alignX":1,"alignY":1,"offsetX":0,"offsetY":0}"""),
        ["component.audio.badge.presetId"] = new("component.audio.badge.presetId", "Variant", ValueKind.OptionToken, ["audio", "badgeSlot", "presetId"], "default"),
        ["component.audio.badge.editor"] = new("component.audio.badge.editor", "Badge", ValueKind.ComponentPreset, ["audio", "badgeSlot", "presetId"], "default"),

        ["component.statusBar.layout.height"] = new("component.statusBar.layout.height", "Height", ValueKind.Integer, ["layout", "height"], "54"),
        ["component.statusBar.layout.itemSize"] = new("component.statusBar.layout.itemSize", "Item size", ValueKind.Integer, ["layout", "itemSize"], "18"),
        ["component.statusBar.layout.gap"] = new("component.statusBar.layout.gap", "Gap", ValueKind.ThemeToken, ["layout", "gap"], "theme.spacing.m", Options: SpacingTokenOptions),
        ["component.statusBar.layout.sidePadding"] = new("component.statusBar.layout.sidePadding", "Side padding", ValueKind.ThemeToken, ["layout", "sidePadding"], "theme.spacing.xxl", Options: SpacingTokenOptions),

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
        ["component.media.topIconBar.editor"] = new("component.media.topIconBar.editor", "Top icon bar", ValueKind.ComponentPreset, ["media", "topIconBarSlot", "presetId"], "default"),
        ["component.media.centerIconBar.editor"] = new("component.media.centerIconBar.editor", "Center icon bar", ValueKind.ComponentPreset, ["media", "centerIconBarSlot", "presetId"], "default"),
        ["component.media.bottomIconBar.editor"] = new("component.media.bottomIconBar.editor", "Bottom icon bar", ValueKind.ComponentPreset, ["media", "bottomIconBarSlot", "presetId"], "default"),
        ["component.media.controlsFadeDelayMs"] = new("component.media.controlsFadeDelayMs", "Fade delay ms", ValueKind.Integer, ["media", "controlsFadeDelayMs"], "900", Number: new NumberDefinition(0, 10000, 10, 0)),
        ["component.media.controlsFadeDurationMs"] = new("component.media.controlsFadeDurationMs", "Fade duration ms", ValueKind.Integer, ["media", "controlsFadeDurationMs"], "180", Number: new NumberDefinition(0, 10000, 10, 0)),
        ["component.media.motion"] = new("component.media.motion", "Motion", ValueKind.Motion, ["media", "motion"], MotionVariantValue.Default.ToJsonString()),
    };

    public static ComponentClassFieldDescriptor Get(string fieldId)
    {
        if (Fields.TryGetValue(fieldId, out var field))
        {
            return field;
        }

        throw new InvalidOperationException($"Unknown component class field '{fieldId}'.");
    }
}
