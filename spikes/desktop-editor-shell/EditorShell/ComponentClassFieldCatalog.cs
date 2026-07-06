using System;
using System.Collections.Generic;

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
    NumberDefinition? Number = null);

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

    private static readonly FieldOption[] PressedEffectOptions =
    [
        new("popup", "Popup"),
        new("scale", "Scale in place"),
        new("none", "None"),
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

        ["component.avatar.defaultSize"] = new("component.avatar.defaultSize", "Default size", ValueKind.Integer, ["avatar", "defaultSize"], "48"),
        ["component.avatar.cornerRadiusToken"] = new("component.avatar.cornerRadiusToken", "Avatar radius", ValueKind.ThemeToken, ["avatar", "cornerRadiusToken"], "theme.radii.avatar", Options: RadiusTokenOptions),
        ["component.avatar.label.showLabel"] = new("component.avatar.label.showLabel", "Show label", ValueKind.Boolean, ["avatar", "labelSlot", "showLabel"], "false"),
        ["component.avatar.label.showSubtext"] = new("component.avatar.label.showSubtext", "Show subtext", ValueKind.Boolean, ["avatar", "labelSlot", "showSubtext"], "false"),
        ["component.avatar.label.placement"] = new("component.avatar.label.placement", "Placement", ValueKind.AlignmentPlacement, ["avatar", "labelSlot", "placement"], """{"mode":"edge","alignX":1,"alignY":0.5,"offsetX":4,"offsetY":0}"""),
        ["component.avatar.label.editor"] = new("component.avatar.label.editor", "Label", ValueKind.EmbeddedComponent, [], "component.label"),

        ["component.textInput.height"] = new("component.textInput.height", "Height", ValueKind.Integer, ["textInput", "height"], "44"),
        ["component.textInput.placeholder"] = new("component.textInput.placeholder", "Placeholder", ValueKind.StringSingleLine, ["textInput", "placeholder"], "Message"),
        ["component.textInput.idleTextColorToken"] = new("component.textInput.idleTextColorToken", "Idle text color", ValueKind.ThemeToken, ["textInput", "idleTextColorToken"], "theme.colors.textSecondary", Options: ThemeColorOptions),
        ["component.textInput.cursorColorToken"] = new("component.textInput.cursorColorToken", "Cursor color", ValueKind.ThemeToken, ["textInput", "cursorColorToken"], "theme.cursor.color", Options: ThemeColorOptions),
        ["component.textInput.cursorWidth"] = new("component.textInput.cursorWidth", "Cursor width", ValueKind.Integer, ["textInput", "cursorWidth"], "2"),
        ["component.textInput.cursorBlinkFrames"] = new("component.textInput.cursorBlinkFrames", "Blink frames", ValueKind.Integer, ["textInput", "cursorBlinkFrames"], "18"),

        ["component.keyboard.keyPadding"] = new("component.keyboard.keyPadding", "Key padding", ValueKind.Integer, ["keyboard", "keyPadding"], "4"),
        ["component.keyboard.keyCornerRadius"] = new("component.keyboard.keyCornerRadius", "Key radius", ValueKind.Integer, ["keyboard", "keyCornerRadius"], "6"),
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
        ["component.keyboard.bottomIconSlots"] = new("component.keyboard.bottomIconSlots", "Bottom icons", ValueKind.IconSlots, ["keyboard", "bottomIconSlots"], EmptyIconSlots),

        ["component.buttonIcon.size"] = new("component.buttonIcon.size", "Button size", ValueKind.Integer, ["buttonIcon", "size"], "48"),
        ["component.buttonIcon.iconToken"] = new("component.buttonIcon.iconToken", "Icon", ValueKind.IconToken, ["buttonIcon", "iconToken"], "media_mic"),
        ["component.buttonIcon.iconPadding"] = new("component.buttonIcon.iconPadding", "Icon padding", ValueKind.Integer, ["buttonIcon", "iconPadding"], "6"),
        ["component.buttonIcon.backgroundColorToken"] = new("component.buttonIcon.backgroundColorToken", "Background", ValueKind.ThemeToken, ["buttonIcon", "backgroundColorToken"], "theme.colors.button", Options: ThemeColorOptions),
        ["component.buttonIcon.backgroundAlpha"] = new("component.buttonIcon.backgroundAlpha", "Surface alpha", ValueKind.Alpha, ["buttonIcon", "backgroundAlpha"], "1"),
        ["component.buttonIcon.iconColorToken"] = new("component.buttonIcon.iconColorToken", "Icon color", ValueKind.ThemeToken, ["buttonIcon", "iconColorToken"], "theme.colors.icon", Options: ThemeColorOptions),
        ["component.buttonIcon.label.showLabel"] = new("component.buttonIcon.label.showLabel", "Show label", ValueKind.Boolean, ["buttonIcon", "labelSlot", "showLabel"], "false"),
        ["component.buttonIcon.label.showSubtext"] = new("component.buttonIcon.label.showSubtext", "Show subtext", ValueKind.Boolean, ["buttonIcon", "labelSlot", "showSubtext"], "false"),
        ["component.buttonIcon.label.placement"] = new("component.buttonIcon.label.placement", "Placement", ValueKind.AlignmentPlacement, ["buttonIcon", "labelSlot", "placement"], """{"mode":"edge","alignX":0.5,"alignY":1,"offsetX":0,"offsetY":3}"""),
        ["component.buttonIcon.label.editor"] = new("component.buttonIcon.label.editor", "Label", ValueKind.EmbeddedComponent, [], "component.label"),

        ["component.label.dimensionMode"] = new("component.label.dimensionMode", "Dimension mode", ValueKind.OptionToken, ["label", "dimensionMode"], "content", Options: DimensionModeOptions),
        ["component.label.size"] = new("component.label.size", "Size", ValueKind.IntegerPair, ["label", "size"], "120|32", PairLabels: new("W", "H")),
        ["component.label.padding"] = new("component.label.padding", "Padding", ValueKind.IntegerPair, ["label", "padding"], "8|4", PairLabels: new("X", "Y")),
        ["component.label.backgroundColorToken"] = new("component.label.backgroundColorToken", "Background", ValueKind.ThemeToken, ["label", "backgroundColorToken"], "theme.colors.background", Options: ThemeColorOptions),
        ["component.label.alpha"] = new("component.label.alpha", "Surface alpha", ValueKind.Alpha, ["label", "alpha"], "1"),
        ["component.label.textColorToken"] = new("component.label.textColorToken", "Text color", ValueKind.ThemeToken, ["label", "textColorToken"], "theme.colors.textPrimary", Options: ThemeColorOptions),
        ["component.label.textSizeToken"] = new("component.label.textSizeToken", "Text size", ValueKind.ThemeToken, ["label", "textSizeToken"], "theme.typography.sizes.s", Options: TypographySizeOptions),
        ["component.label.textStyle"] = new("component.label.textStyle", "Text style", ValueKind.OptionToken, ["label", "textStyle"], "normal", Options: TextStyleOptions),
        ["component.label.textAlign"] = new("component.label.textAlign", "Text align", ValueKind.OptionToken, ["label", "textAlign"], "center", Options: TextAlignOptions),
        ["component.label.textGap"] = new("component.label.textGap", "Text gap", ValueKind.Decimal, ["label", "textGap"], "2", Number: new NumberDefinition(-64, 64, 0.5m, 2)),
        ["component.label.subtextColorToken"] = new("component.label.subtextColorToken", "Subtext color", ValueKind.ThemeToken, ["label", "subtextColorToken"], "theme.colors.textSecondary", Options: ThemeColorOptions),
        ["component.label.subtextSizeToken"] = new("component.label.subtextSizeToken", "Subtext size", ValueKind.ThemeToken, ["label", "subtextSizeToken"], "theme.typography.sizes.xs", Options: TypographySizeOptions),
        ["component.label.subtextStyle"] = new("component.label.subtextStyle", "Subtext style", ValueKind.OptionToken, ["label", "subtextStyle"], "normal", Options: TextStyleOptions),

        ["component.audio.size"] = new("component.audio.size", "Size", ValueKind.IntegerPair, ["audio", "size"], "230|54", PairLabels: new("W", "H")),
        ["component.audio.backgroundColorToken"] = new("component.audio.backgroundColorToken", "Background", ValueKind.ThemeToken, ["audio", "backgroundColorToken"], "theme.colors.surface", Options: ThemeColorOptions),
        ["component.audio.backgroundAlpha"] = new("component.audio.backgroundAlpha", "Surface alpha", ValueKind.Alpha, ["audio", "backgroundAlpha"], "1"),
        ["component.audio.textSize"] = new("component.audio.textSize", "Text size", ValueKind.Integer, ["audio", "textSize"], "13"),
        ["component.audio.textColorToken"] = new("component.audio.textColorToken", "Text color", ValueKind.ThemeToken, ["audio", "textColorToken"], "theme.icons.secondary", Options: ThemeColorOptions),
        ["component.audio.playCircleSize"] = new("component.audio.playCircleSize", "Play size", ValueKind.Integer, ["audio", "playCircleSize"], "32"),
        ["component.audio.playColorToken"] = new("component.audio.playColorToken", "Play color", ValueKind.ThemeToken, ["audio", "playColorToken"], "theme.icons.accent", Options: ThemeColorOptions),
        ["component.audio.playIconColorToken"] = new("component.audio.playIconColorToken", "Play icon color", ValueKind.ThemeToken, ["audio", "playIconColorToken"], "theme.icons.secondary", Options: ThemeColorOptions),
        ["component.audio.waveformColorToken"] = new("component.audio.waveformColorToken", "Waveform color", ValueKind.ThemeToken, ["audio", "waveformColorToken"], "theme.icons.primary", Options: ThemeColorOptions),
        ["component.audio.waveformPlayedColorToken"] = new("component.audio.waveformPlayedColorToken", "Waveform played", ValueKind.ThemeToken, ["audio", "waveformPlayedColorToken"], "theme.icons.accent", Options: ThemeColorOptions),
        ["component.audio.waveformBarCount"] = new("component.audio.waveformBarCount", "Waveform bars", ValueKind.Integer, ["audio", "waveformBarCount"], "28"),
        ["component.audio.waveformGap"] = new("component.audio.waveformGap", "Waveform gap", ValueKind.Integer, ["audio", "waveformGap"], "2"),
        ["component.audio.waveformMinHeight"] = new("component.audio.waveformMinHeight", "Waveform min height", ValueKind.Integer, ["audio", "waveformMinHeight"], "4"),
        ["component.audio.waveformMaxHeight"] = new("component.audio.waveformMaxHeight", "Waveform max height", ValueKind.Integer, ["audio", "waveformMaxHeight"], "22"),
        ["component.audio.progressKnobSize"] = new("component.audio.progressKnobSize", "Progress knob", ValueKind.Integer, ["audio", "progressKnobSize"], "9"),
        ["component.audio.avatar.showAvatar"] = new("component.audio.avatar.showAvatar", "Show avatar", ValueKind.Boolean, ["audio", "avatarSlot", "showAvatar"], "true"),
        ["component.audio.avatar.placement"] = new("component.audio.avatar.placement", "Placement", ValueKind.AlignmentPlacement, ["audio", "avatarSlot", "placement"], """{"mode":"edge","alignX":1,"alignY":0.5,"offsetX":4,"offsetY":0}"""),
        ["component.audio.avatar.editor"] = new("component.audio.avatar.editor", "Avatar", ValueKind.EmbeddedComponent, [], "component.avatar"),
        ["component.audio.badge.showBadge"] = new("component.audio.badge.showBadge", "Show badge", ValueKind.Boolean, ["audio", "badgeSlot", "showBadge"], "false"),
        ["component.audio.badge.iconToken"] = new("component.audio.badge.iconToken", "Icon", ValueKind.IconToken, ["audio", "badgeSlot", "iconToken"], "media_mic"),
        ["component.audio.badge.backgroundColor"] = new("component.audio.badge.backgroundColor", "Badge color", ValueKind.PaletteColorToken, ["audio", "badgeSlot", "backgroundColor"], "blue"),
        ["component.audio.badge.iconColor"] = new("component.audio.badge.iconColor", "Icon color", ValueKind.PaletteColorToken, ["audio", "badgeSlot", "iconColor"], "gray_100"),
        ["component.audio.badge.placement"] = new("component.audio.badge.placement", "Placement", ValueKind.AlignmentPlacement, ["audio", "badgeSlot", "placement"], """{"mode":"center","alignX":1,"alignY":1,"offsetX":0,"offsetY":0}"""),
        ["component.audio.badge.editor"] = new("component.audio.badge.editor", "Badge", ValueKind.EmbeddedComponent, [], "component.button_icon"),

        ["component.video.statusVisible"] = new("component.video.statusVisible", "Show status", ValueKind.Boolean, ["video", "statusVisible"], "true"),
        ["component.video.statusHeight"] = new("component.video.statusHeight", "Status height", ValueKind.Integer, ["video", "statusHeight"], "24"),
        ["component.video.statusIconSlots"] = new("component.video.statusIconSlots", "Status icons", ValueKind.IconSlots, ["video", "statusIconSlots"], """{"left":["app_camera"],"center":[],"right":[]}"""),
        ["component.video.playOverlayVisible"] = new("component.video.playOverlayVisible", "Play overlay", ValueKind.Boolean, ["video", "playOverlayVisible"], "true"),
        ["component.video.playColorToken"] = new("component.video.playColorToken", "Play color", ValueKind.ThemeToken, ["video", "playColorToken"], "theme.icons.accent", Options: ThemeColorOptions),
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
