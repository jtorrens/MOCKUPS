using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record RecordClassFieldDescriptor(
    string Id,
    string Label,
    ValueKind ValueKind,
    bool IsEditable = true,
    IReadOnlyList<FieldOption>? Options = null,
    PairFieldLabels? PairLabels = null,
    ImagePreviewDefinition? ImagePreview = null,
    NumberDefinition? Number = null,
    RecordReferenceDefinition? RecordReference = null,
    string ComponentVariantType = "",
    string Unit = "",
    IReadOnlyList<ComponentInputBindingDefinition>? ComponentInputBindings = null,
    string RuntimeInputComponentVariantFieldId = "",
    string RuntimeCollectionComponentVariantFieldId = "",
    MotionTimingDefinition? MotionTiming = null);

internal static class RecordClassFieldCatalog
{
    static RecordClassFieldCatalog()
    {
        GeneratedModuleScaffoldFieldCatalog.AddFields(Fields);
    }

    private static readonly FieldOption[] SpacingTokenOptions = ComponentClassFieldCatalog.SpacingTokenOptions;
    private static readonly FieldOption[] TypographySizeOptions = ComponentClassFieldCatalog.TypographySizeOptions;
    private static readonly PairFieldLabels WidthHeightPairLabels = new("W", "H");
    private static readonly PairFieldLabels PositionPairLabels = new("X", "Y");
    private static readonly PairFieldLabels VerticalInsetPairLabels = new("Top", "Bottom");
    private static readonly PairFieldLabels HorizontalInsetPairLabels = new("Left", "Right");
    private static readonly PairFieldLabels LightDarkPairLabels = new("Light", "Dark");

    private static readonly FieldOption[] TypingIndicatorAnimationOptions =
    [
        new("none", "None"),
        new("pulsating", "Pulsating"),
        new("wave", "Wave"),
    ];

    private static readonly FieldOption[] MotionEasingOptions =
    [
        new("linear", "Linear"),
        new("ease-in", "Ease in"),
        new("ease-out", "Ease out"),
        new("ease", "Ease"),
        new("spring", "Spring"),
        new("bounce", "Bounce"),
    ];

    private static readonly Dictionary<string, RecordClassFieldDescriptor> Fields = new(StringComparer.Ordinal)
    {
        ["project.slug"] = new("project.slug", "Slug", ValueKind.StringSingleLine),
        ["project.defaultFps"] = new("project.defaultFps", "Default FPS", ValueKind.Integer),
        ["project.mediaRoot"] = new("project.mediaRoot", "Media Root", ValueKind.DirectoryPath),

        ["episode.slug"] = new("episode.slug", "Slug", ValueKind.StringSingleLine),
        ["episode.sortOrder"] = new("episode.sortOrder", "Sort Order", ValueKind.Integer),

        ["shot.slug"] = new("shot.slug", "Slug", ValueKind.StringSingleLine),
        ["shot.version"] = new("shot.version", "Version", ValueKind.Integer),
        ["shot.sortOrder"] = new("shot.sortOrder", "Sort Order", ValueKind.Integer),
        ["shot.durationFrames"] = new("shot.durationFrames", "Duration", ValueKind.Integer, IsEditable: false, Unit: "frames"),
        ["shot.fps"] = new("shot.fps", "Frame rate", ValueKind.Integer, Unit: "fps"),
        ["shot.ownerActorId"] = new(
            "shot.ownerActorId",
            "Owner Actor",
            ValueKind.RecordReference,
            RecordReference: new RecordReferenceDefinition("actors")),
        ["shot.ownerDevice"] = new("shot.ownerDevice", "Device", ValueKind.StringReadOnly, IsEditable: false),
        ["shot.renderPresetId"] = new(
            "shot.renderPresetId",
            "Render Preset",
            ValueKind.RecordReference,
            RecordReference: new RecordReferenceDefinition("render_presets")),
        ["shot.renderName"] = new("shot.renderName", "Render Name", ValueKind.StringReadOnly, IsEditable: false),
        ["shot.canvas"] = new("shot.canvas", "Canvas", ValueKind.StringMultiline),
        ["shot.metadata"] = new("shot.metadata", "Metadata", ValueKind.StringMultiline),

        ["app.bundleKey"] = new("app.bundleKey", "Bundle Key", ValueKind.StringSingleLine),
        ["app.appType"] = new("app.appType", "App Type", ValueKind.OptionToken),
        ["app.config"] = new("app.config", "Config", ValueKind.StringMultiline),
        ["app.metadata"] = new("app.metadata", "Metadata", ValueKind.StringMultiline),
        ["app.wallpaper.kind"] = new("app.wallpaper.kind", "Kind", ValueKind.OptionToken),
        ["app.wallpaper.opacity"] = new(
            "app.wallpaper.opacity",
            "Opacity",
            ValueKind.Decimal,
            Number: new NumberDefinition(0, 1, 0.05m, 2)),
        ["app.wallpaper.color"] = new("app.wallpaper.color", "Wallpaper Color", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["app.wallpaper.images.light.filePath"] = new(
            "app.wallpaper.images.light.filePath",
            "Light image",
            ValueKind.ImageFilePath,
            ImagePreview: new ImagePreviewDefinition(ImagePreviewMode.Aspect)),
        ["app.wallpaper.images.dark.filePath"] = new(
            "app.wallpaper.images.dark.filePath",
            "Dark image",
            ValueKind.ImageFilePath,
            ImagePreview: new ImagePreviewDefinition(ImagePreviewMode.Aspect)),
        ["app.note"] = new("app.note", "Note", ValueKind.StringMultiline),
        ["app.icon.filePath"] = new(
            "app.icon.filePath",
            "App icon image",
            ValueKind.ImageFilePath,
            ImagePreview: new ImagePreviewDefinition(
                ImagePreviewMode.SquareCrop,
                BaseSize: 256,
                ScaleFieldId: "app.icon.scale",
                OffsetFieldId: "app.icon.offset")),
        ["app.icon.scale"] = new(
            "app.icon.scale",
            "Icon scale",
            ValueKind.Decimal,
            Number: new NumberDefinition(0.01m, 8, 0.05m, 2)),
        ["app.icon.offset"] = new("app.icon.offset", "Icon offset", ValueKind.IntegerPair, PairLabels: PositionPairLabels),

        ["module.recordClassId"] = new(
            "module.recordClassId",
            "Module class",
            ValueKind.OptionToken,
            IsEditable: false,
            Options: DesktopPreviewManifest.Modules
                .OrderBy((entry) => entry.Value.Label, StringComparer.Ordinal)
                .Select((entry) => new FieldOption(entry.Key, entry.Value.Label))
                .ToList()),
        ["module.sortOrder"] = new("module.sortOrder", "Sort Order", ValueKind.Integer),
        ["module.appearanceMode"] = new("module.appearanceMode", "Appearance mode", ValueKind.OptionToken, Options:
        [
            new FieldOption("inherit", "Follow preview"),
            new FieldOption("light", "Light"),
            new FieldOption("dark", "Dark"),
        ]),
        ["module.metadata"] = new("module.metadata", "Metadata", ValueKind.StringMultiline),
        ["module.conversation.showHeader"] = new("module.conversation.showHeader", "Header", ValueKind.Boolean),
        ["module.conversation.useAppWallpaper"] = new("module.conversation.useAppWallpaper", "Use app wallpaper", ValueKind.Boolean),
        ["module.conversation.headerHeight"] = new(
            "module.conversation.headerHeight",
            "Header height",
            ValueKind.Integer,
            Number: new NumberDefinition(0, 240, 1, 0)),
        ["module.conversation.headerAvatarVariant"] = new(
            "module.conversation.headerAvatarVariant",
            "Header avatar variant",
            ValueKind.ComponentVariant,
            ComponentVariantType: "avatar"),
        ["module.conversation.headerAvatarAlignment"] = new("module.conversation.headerAvatarAlignment", "Avatar alignment", ValueKind.OptionToken, Options:
        [
            new FieldOption("left", "Left"),
            new FieldOption("center", "Center"),
            new FieldOption("right", "Right"),
        ]),
        ["module.conversation.headerLeftIconRow.editor"] = new("module.conversation.headerLeftIconRow.editor", "Left icon row", ValueKind.ComponentVariant, ComponentVariantType: "iconRow"),
        ["module.conversation.headerLeftIconRow.inputs"] = new("module.conversation.headerLeftIconRow.inputs", "Left row settings", ValueKind.ComponentInputBindings, ComponentInputBindings: ComponentClassFieldCatalog.VariantInputBindingsForComponent("iconRow")),
        ["module.conversation.headerRightIconRow.editor"] = new("module.conversation.headerRightIconRow.editor", "Right icon row", ValueKind.ComponentVariant, ComponentVariantType: "iconRow"),
        ["module.conversation.headerRightIconRow.inputs"] = new("module.conversation.headerRightIconRow.inputs", "Right row settings", ValueKind.ComponentInputBindings, ComponentInputBindings: ComponentClassFieldCatalog.VariantInputBindingsForComponent("iconRow")),
        ["module.conversation.showStatusBar"] = new("module.conversation.showStatusBar", "Status bar", ValueKind.Boolean),
        ["module.conversation.showNavigationBar"] = new("module.conversation.showNavigationBar", "Navigation bar", ValueKind.Boolean),
        ["module.conversation.showTextInputBar"] = new("module.conversation.showTextInputBar", "Text input bar", ValueKind.Boolean),
        ["module.conversation.textInputBarVariant"] = new(
            "module.conversation.textInputBarVariant",
            "Text input variant",
            ValueKind.ComponentVariant,
            ComponentVariantType: "textInputBar"),
        ["module.conversation.showKeyboard"] = new("module.conversation.showKeyboard", "Keyboard", ValueKind.Boolean),
        ["module.conversation.keyboardVariant"] = new(
            "module.conversation.keyboardVariant",
            "Keyboard variant",
            ValueKind.ComponentVariant,
            ComponentVariantType: "keyboard"),
        ["module.conversation.bubbleVariant"] = new(
            "module.conversation.bubbleVariant",
            "Bubble variant",
            ValueKind.ComponentVariant,
            ComponentVariantType: "bubble"),
        ["module.conversation.bubbleMaxWidth"] = new(
            "module.conversation.bubbleMaxWidth",
            "Max width %",
            ValueKind.Integer,
            Number: new NumberDefinition(1, 100, 1, 0)),
        ["module.conversation.screenGutter"] = new(
            "module.conversation.screenGutter",
            "Screen gutter",
            ValueKind.ThemeTokenPair,
            PairLabels: PositionPairLabels,
            Options: SpacingTokenOptions),
        ["module.conversation.messageGap"] = new(
            "module.conversation.messageGap",
            "Message gap",
            ValueKind.ThemeToken,
            Options: SpacingTokenOptions),
        ["module.conversation.messageViewportMotion"] = new(
            "module.conversation.messageViewportMotion",
            "Message viewport motion",
            ValueKind.Motion),
        ["module.lockScreen.statusBarVariant"] = new(
            "module.lockScreen.statusBarVariant", "Status bar variant", ValueKind.ComponentVariant, ComponentVariantType: "status_bar"),
        ["module.lockScreen.navigationBarVariant"] = new(
            "module.lockScreen.navigationBarVariant", "Navigation bar variant", ValueKind.ComponentVariant, ComponentVariantType: "navigation_bar"),
        ["module.lockScreen.stackVariant"] = new(
            "module.lockScreen.stackVariant", "Stack variant", ValueKind.ComponentVariant, ComponentVariantType: "componentStack"),
        ["module.lockScreen.stackInputs"] = new(
            "module.lockScreen.stackInputs",
            "Stack inputs",
            ValueKind.ComponentInputBindings,
            RuntimeInputComponentVariantFieldId: "module.lockScreen.stackVariant"),
        ["module.lockScreen.stackItems"] = new(
            "module.lockScreen.stackItems",
            "Components",
            ValueKind.StructuredCollection,
            RuntimeCollectionComponentVariantFieldId: "module.lockScreen.stackVariant"),

        ["moduleInstance.module"] = new("moduleInstance.module", "Module", ValueKind.StringReadOnly, IsEditable: false),
        ["moduleInstance.variant"] = new("moduleInstance.variant", "Variant", ValueKind.ComponentVariant),
        ["moduleInstance.sortOrder"] = new("moduleInstance.sortOrder", "Sort Order", ValueKind.Integer, IsEditable: false),
        ["moduleInstance.durationFrames"] = new(
            "moduleInstance.durationFrames",
            "Duration",
            ValueKind.Integer,
            IsEditable: false,
            Number: new NumberDefinition(1, 100000, 1, 0),
            Unit: "frames"),
        ["moduleInstance.transition"] = new(
            "moduleInstance.transition",
            "Transition",
            ValueKind.OptionToken,
            IsEditable: false,
            Options: [new FieldOption("cut", "Cut")]),
        ["renderPreset.width"] = new("renderPreset.width", "Width", ValueKind.Integer, IsEditable: false),
        ["renderPreset.height"] = new("renderPreset.height", "Height", ValueKind.Integer, IsEditable: false),
        ["renderPreset.fps"] = new("renderPreset.fps", "FPS", ValueKind.Integer, IsEditable: false),
        ["renderPreset.format"] = new("renderPreset.format", "Format", ValueKind.OptionToken),
        ["renderPreset.codec"] = new("renderPreset.codec", "Codec", ValueKind.StringMultiline),
        ["renderPreset.color"] = new("renderPreset.color", "Color", ValueKind.StringMultiline),
        ["renderPreset.quality"] = new("renderPreset.quality", "Quality", ValueKind.StringMultiline),
        ["renderPreset.export"] = new("renderPreset.export", "Export", ValueKind.StringMultiline),
        ["renderPreset.export.ffmpegArgs"] = new("renderPreset.export.ffmpegArgs", "FFmpeg Args", ValueKind.StringMultiline),

        ["palette.token"] = new("palette.token", "Token", ValueKind.StringSingleLine),
        ["palette.valueHex"] = new("palette.valueHex", "Hex", ValueKind.HexColor),
        ["palette.isNeutral"] = new("palette.isNeutral", "Neutral", ValueKind.Boolean),
        ["palette.source"] = new("palette.source", "Source", ValueKind.StringSingleLine),
        ["palette.protected"] = new("palette.protected", "Protected", ValueKind.Boolean),
        ["palette.hiddenFromPickers"] = new("palette.hiddenFromPickers", "Hidden From Pickers", ValueKind.Boolean),
        ["palette.note"] = new("palette.note", "Note", ValueKind.StringMultiline),

        ["device.manufacturer"] = new("device.manufacturer", "Manufacturer", ValueKind.StringSingleLine),
        ["device.model"] = new("device.model", "Model", ValueKind.StringSingleLine),
        ["device.osFamily"] = new("device.osFamily", "OS Family", ValueKind.StringSingleLine),
        ["device.metrics.designSpace.size"] = new("device.metrics.designSpace.size", "Design space", ValueKind.IntegerPair, PairLabels: WidthHeightPairLabels),
        ["device.metrics.renderSize"] = new("device.metrics.renderSize", "Render size", ValueKind.IntegerPair, PairLabels: WidthHeightPairLabels),
        ["device.metrics.scaleToPixels"] = new(
            "device.metrics.scaleToPixels",
            "Scale to pixels",
            ValueKind.Decimal,
            Number: new NumberDefinition(0.1m, 10, 0.1m, 2)),
        ["device.metrics.pixelRatio"] = new(
            "device.metrics.pixelRatio",
            "Pixel ratio",
            ValueKind.Decimal,
            Number: new NumberDefinition(0.1m, 10, 0.1m, 2)),
        ["device.metrics.defaultScreenScale"] = new(
            "device.metrics.defaultScreenScale",
            "Default screen scale",
            ValueKind.Decimal,
            Number: new NumberDefinition(0.1m, 10, 0.1m, 2)),
        ["device.metrics.canvas.size"] = new("device.metrics.canvas.size", "Canvas size", ValueKind.IntegerPair, PairLabels: WidthHeightPairLabels),
        ["device.metrics.screen.position"] = new("device.metrics.screen.position", "Screen position", ValueKind.IntegerPair, PairLabels: PositionPairLabels),
        ["device.metrics.screen.size"] = new("device.metrics.screen.size", "Screen size", ValueKind.IntegerPair, PairLabels: WidthHeightPairLabels),
        ["device.metrics.cornerRadius"] = new(
            "device.metrics.cornerRadius",
            "Corner radius",
            ValueKind.Decimal,
            Number: new NumberDefinition(0, 9999, 0.1m, 3)),
        ["device.metrics.viewport.position"] = new("device.metrics.viewport.position", "Viewport position", ValueKind.IntegerPair, PairLabels: PositionPairLabels),
        ["device.metrics.viewport.size"] = new("device.metrics.viewport.size", "Viewport size", ValueKind.IntegerPair, PairLabels: WidthHeightPairLabels),
        ["device.metrics.safeArea.vertical"] = new("device.metrics.safeArea.vertical", "Safe vertical", ValueKind.IntegerPair, PairLabels: VerticalInsetPairLabels),
        ["device.metrics.safeArea.horizontal"] = new("device.metrics.safeArea.horizontal", "Safe horizontal", ValueKind.IntegerPair, PairLabels: HorizontalInsetPairLabels),
        ["device.metrics.statusBar.position"] = new("device.metrics.statusBar.position", "Status bar position", ValueKind.IntegerPair, PairLabels: PositionPairLabels),
        ["device.metrics.statusBar.size"] = new("device.metrics.statusBar.size", "Status bar size", ValueKind.IntegerPair, PairLabels: WidthHeightPairLabels),
        ["device.metrics.dynamicIsland.position"] = new("device.metrics.dynamicIsland.position", "Dynamic island position", ValueKind.IntegerPair, PairLabels: PositionPairLabels),
        ["device.metrics.dynamicIsland.size"] = new("device.metrics.dynamicIsland.size", "Dynamic island size", ValueKind.IntegerPair, PairLabels: WidthHeightPairLabels),

        ["theme.family"] = new("theme.family", "Family", ValueKind.OptionToken),
        ["theme.iconThemeId"] = new(
            "theme.iconThemeId",
            "Icon theme",
            ValueKind.RecordReference,
            RecordReference: new RecordReferenceDefinition("icon_themes")),
        ["theme.statusBarId"] = new(
            "theme.statusBarId",
            "Status bar",
            ValueKind.ComponentVariant,
            ComponentVariantType: "status_bar"),
        ["theme.navigationBarId"] = new(
            "theme.navigationBarId",
            "Navigation bar",
            ValueKind.ComponentVariant,
            ComponentVariantType: "navigation_bar"),
        ["theme.defaultMode"] = new("theme.defaultMode", "Default mode", ValueKind.OptionToken),
        ["theme.neutralTint.hueDeg"] = new("theme.neutralTint.hueDeg", "Hue", ValueKind.HueDegrees),
        ["theme.neutralTint.saturation"] = new(
            "theme.neutralTint.saturation",
            "Saturation",
            ValueKind.Decimal,
            Number: new NumberDefinition(0, 1, 0.05m, 2, UseSlider: true)),
        ["theme.colors.background"] = new("theme.colors.background", "Background", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.surface"] = new("theme.colors.surface", "Surface", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.card"] = new("theme.colors.card", "Card", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.label"] = new("theme.colors.label", "Label", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.text"] = new("theme.colors.text", "Text", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.textPrimary"] = new("theme.colors.textPrimary", "Text primary", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.textSecondary"] = new("theme.colors.textSecondary", "Text secondary", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.icon"] = new("theme.colors.icon", "Icon", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.button"] = new("theme.colors.button", "Button", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.field"] = new("theme.colors.field", "Field", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.checkbox"] = new("theme.colors.checkbox", "Checkbox", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.radio"] = new("theme.colors.radio", "Radio", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.switch"] = new("theme.colors.switch", "Switch", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.tab"] = new("theme.colors.tab", "Tab", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.menuItem"] = new("theme.colors.menuItem", "Menu item", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.badge"] = new("theme.colors.badge", "Badge", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.toast"] = new("theme.colors.toast", "Toast", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.divider"] = new("theme.colors.divider", "Divider", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.colors.accent"] = new("theme.colors.accent", "Accent", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.icons.primary"] = new("theme.icons.primary", "Icon primary", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.icons.secondary"] = new("theme.icons.secondary", "Icon secondary", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.icons.alternate"] = new("theme.icons.alternate", "Icon alternate", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.icons.accent"] = new("theme.icons.accent", "Icon accent", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.borders.primary"] = new("theme.borders.primary", "Border primary", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.borders.secondary"] = new("theme.borders.secondary", "Border secondary", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.borders.alternate"] = new("theme.borders.alternate", "Border alternate", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.cursor.color"] = new("theme.cursor.color", "Cursor color", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.cursor.width"] = new("theme.cursor.width", "Cursor width", ValueKind.Integer),
        ["theme.cursor.blinkDurationMs"] = new("theme.cursor.blinkDurationMs", "Blink duration", ValueKind.Integer, Unit: "ms"),
        ["theme.shadows.default.color"] = new("theme.shadows.default.color", "Color", ValueKind.PaletteColorToken),
        ["theme.shadows.default.alpha"] = new("theme.shadows.default.alpha", "Alpha", ValueKind.Alpha),
        ["theme.shadows.default.offsetX"] = new("theme.shadows.default.offsetX", "Offset X", ValueKind.Decimal),
        ["theme.shadows.default.offsetY"] = new("theme.shadows.default.offsetY", "Offset Y", ValueKind.Decimal),
        ["theme.shadows.default.blur"] = new("theme.shadows.default.blur", "Blur", ValueKind.Decimal),
        ["theme.keyboard.height"] = new("theme.keyboard.height", "Height", ValueKind.Integer),
        ["theme.keyboard.keyGap"] = new("theme.keyboard.keyGap", "Key gap", ValueKind.Integer),
        ["theme.keyboard.rowGap"] = new("theme.keyboard.rowGap", "Row gap", ValueKind.Integer),
        ["theme.keyboard.background"] = new("theme.keyboard.background", "Background", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.keyboard.keyBackground"] = new("theme.keyboard.keyBackground", "Key background", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.keyboard.specialKeyBackground"] = new("theme.keyboard.specialKeyBackground", "Special key background", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.keyboard.pressedKeyBackground"] = new("theme.keyboard.pressedKeyBackground", "Pressed key background", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.keyboard.keyBorder"] = new("theme.keyboard.keyBorder", "Key border", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.keyboard.text"] = new("theme.keyboard.text", "Text", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["theme.motion.fade"] = new("theme.motion.fade", "Fade", ValueKind.MotionTiming, Options: MotionEasingOptions),
        ["theme.motion.slide"] = new("theme.motion.slide", "Slide", ValueKind.MotionTiming, Options: MotionEasingOptions),
        ["theme.motion.swipe"] = new("theme.motion.swipe", "Swipe", ValueKind.MotionTiming, Options: MotionEasingOptions),
        ["theme.motion.scale"] = new("theme.motion.scale", "Scale", ValueKind.MotionTiming, Options: MotionEasingOptions),
        ["theme.motion.buttonPushedDurationMs"] = new("theme.motion.buttonPushedDurationMs", "Button pushed", ValueKind.Integer, Unit: "ms"),
        ["theme.motion.reflow"] = new(
            "theme.motion.reflow",
            "Reflow",
            ValueKind.MotionTiming,
            Options: MotionEasingOptions,
            MotionTiming: new MotionTimingDefinition(ShowDelay: false, ShowIntensity: false)),
        ["theme.motion.naturalPace.verySlow"] = new("theme.motion.naturalPace.verySlow", "Very slow", ValueKind.Decimal, Number: new NumberDefinition(0.1m, 10, 0.05m, 2)),
        ["theme.motion.naturalPace.slow"] = new("theme.motion.naturalPace.slow", "Slow", ValueKind.Decimal, Number: new NumberDefinition(0.1m, 10, 0.05m, 2)),
        ["theme.motion.naturalPace.normal"] = new("theme.motion.naturalPace.normal", "Normal", ValueKind.Decimal, Number: new NumberDefinition(0.1m, 10, 0.05m, 2)),
        ["theme.motion.naturalPace.fast"] = new("theme.motion.naturalPace.fast", "Fast", ValueKind.Decimal, Number: new NumberDefinition(0.1m, 10, 0.05m, 2)),
        ["theme.motion.naturalPace.veryFast"] = new("theme.motion.naturalPace.veryFast", "Very fast", ValueKind.Decimal, Number: new NumberDefinition(0.1m, 10, 0.05m, 2)),
        ["theme.typography.fontFamilyId"] = new(
            "theme.typography.fontFamilyId",
            "Text font",
            ValueKind.RecordReference,
            RecordReference: new RecordReferenceDefinition("production_fonts")),
        ["theme.typography.systemFontFamilyId"] = new(
            "theme.typography.systemFontFamilyId",
            "System components font",
            ValueKind.RecordReference,
            RecordReference: new RecordReferenceDefinition("production_fonts")),
        ["theme.typography.emojiFontFamilyId"] = new(
            "theme.typography.emojiFontFamilyId",
            "Emoji font",
            ValueKind.RecordReference,
            RecordReference: new RecordReferenceDefinition("production_fonts")),
        ["theme.typography.sizes.xs"] = new("theme.typography.sizes.xs", "Text XS", ValueKind.Integer),
        ["theme.typography.sizes.s"] = new("theme.typography.sizes.s", "Text S", ValueKind.Integer),
        ["theme.typography.sizes.m"] = new("theme.typography.sizes.m", "Text M", ValueKind.Integer),
        ["theme.typography.sizes.l"] = new("theme.typography.sizes.l", "Text L", ValueKind.Integer),
        ["theme.typography.sizes.xl"] = new("theme.typography.sizes.xl", "Text XL", ValueKind.Integer),
        ["theme.typography.weight"] = new("theme.typography.weight", "Weight", ValueKind.OptionToken),
        ["theme.typography.style"] = new("theme.typography.style", "Style", ValueKind.OptionToken),
        ["theme.typography.lineHeights.tight"] = new("theme.typography.lineHeights.tight", "Line tight", ValueKind.Decimal, Number: new NumberDefinition(0.5m, 3, 0.05m, 2)),
        ["theme.typography.lineHeights.compact"] = new("theme.typography.lineHeights.compact", "Line compact", ValueKind.Decimal, Number: new NumberDefinition(0.5m, 3, 0.05m, 2)),
        ["theme.typography.lineHeights.normal"] = new("theme.typography.lineHeights.normal", "Line normal", ValueKind.Decimal, Number: new NumberDefinition(0.5m, 3, 0.05m, 2)),
        ["theme.typography.lineHeights.relaxed"] = new("theme.typography.lineHeights.relaxed", "Line relaxed", ValueKind.Decimal, Number: new NumberDefinition(0.5m, 3, 0.05m, 2)),
        ["theme.typography.lineHeights.loose"] = new("theme.typography.lineHeights.loose", "Line loose", ValueKind.Decimal, Number: new NumberDefinition(0.5m, 3, 0.05m, 2)),
        ["theme.iconSizes.xs"] = new("theme.iconSizes.xs", "Icon XS", ValueKind.Integer),
        ["theme.iconSizes.s"] = new("theme.iconSizes.s", "Icon S", ValueKind.Integer),
        ["theme.iconSizes.m"] = new("theme.iconSizes.m", "Icon M", ValueKind.Integer),
        ["theme.iconSizes.l"] = new("theme.iconSizes.l", "Icon L", ValueKind.Integer),
        ["theme.iconSizes.xl"] = new("theme.iconSizes.xl", "Icon XL", ValueKind.Integer),
        ["theme.spacing.none"] = new("theme.spacing.none", "None", ValueKind.Integer),
        ["theme.spacing.xs"] = new("theme.spacing.xs", "Spacing XS", ValueKind.Integer),
        ["theme.spacing.s"] = new("theme.spacing.s", "Spacing S", ValueKind.Integer),
        ["theme.spacing.m"] = new("theme.spacing.m", "Spacing M", ValueKind.Integer),
        ["theme.spacing.l"] = new("theme.spacing.l", "Spacing L", ValueKind.Integer),
        ["theme.spacing.xl"] = new("theme.spacing.xl", "Spacing XL", ValueKind.Integer),
        ["theme.spacing.xxl"] = new("theme.spacing.xxl", "Spacing XXL", ValueKind.Integer),
        ["theme.radii.none"] = new("theme.radii.none", "None", ValueKind.Integer),
        ["theme.radii.xs"] = new("theme.radii.xs", "XS", ValueKind.Integer),
        ["theme.radii.s"] = new("theme.radii.s", "S", ValueKind.Integer),
        ["theme.radii.m"] = new("theme.radii.m", "M", ValueKind.Integer),
        ["theme.radii.l"] = new("theme.radii.l", "L", ValueKind.Integer),
        ["theme.radii.xl"] = new("theme.radii.xl", "XL", ValueKind.Integer),
        ["theme.radii.xxl"] = new("theme.radii.xxl", "XXL", ValueKind.Integer),
        ["theme.radii.full"] = new("theme.radii.full", "Full", ValueKind.Integer),

        ["actor.shortName"] = new("actor.shortName", "Short name", ValueKind.StringSingleLine),
        ["actor.defaultDeviceId"] = new(
            "actor.defaultDeviceId",
            "Default device",
            ValueKind.RecordReference,
            RecordReference: new RecordReferenceDefinition("devices")),
        ["actor.defaultThemeId"] = new(
            "actor.defaultThemeId",
            "Default theme",
            ValueKind.RecordReference,
            RecordReference: new RecordReferenceDefinition("themes")),
        ["actor.color.modes"] = new("actor.color.modes", "Actor Color", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["actor.avatarTextColor.modes"] = new("actor.avatarTextColor.modes", "Actor Text Color", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["actor.wallpaper.kind"] = new("actor.wallpaper.kind", "Kind", ValueKind.OptionToken),
        ["actor.wallpaper.opacity"] = new("actor.wallpaper.opacity", "Opacity", ValueKind.Decimal, Number: new NumberDefinition(0, 1, 0.05m, 2)),
        ["actor.wallpaper.color"] = new("actor.wallpaper.color", "Wallpaper Color", ValueKind.PaletteColorPair, PairLabels: LightDarkPairLabels),
        ["actor.wallpaper.images.light.filePath"] = new(
            "actor.wallpaper.images.light.filePath",
            "Light image",
            ValueKind.ImageFilePath,
            ImagePreview: new ImagePreviewDefinition(ImagePreviewMode.Aspect)),
        ["actor.wallpaper.images.dark.filePath"] = new(
            "actor.wallpaper.images.dark.filePath",
            "Dark image",
            ValueKind.ImageFilePath,
            ImagePreview: new ImagePreviewDefinition(ImagePreviewMode.Aspect)),
        ["actor.avatar.filePath"] = new(
            "actor.avatar.filePath",
            "Avatar image",
            ValueKind.ImageFilePath,
            ImagePreview: new ImagePreviewDefinition(
                ImagePreviewMode.SquareCrop,
                BaseSize: 640,
                ScaleFieldId: "actor.avatar.scale",
                OffsetFieldId: "actor.avatar.offset")),
        ["actor.avatar.scale"] = new(
            "actor.avatar.scale",
            "Avatar scale",
            ValueKind.Decimal,
            Number: new NumberDefinition(0.01m, 8, 0.05m, 2)),
        ["actor.avatar.offset"] = new("actor.avatar.offset", "Avatar offset", ValueKind.IntegerPair, PairLabels: PositionPairLabels),
        ["actor.avatar.useInitials"] = new("actor.avatar.useInitials", "Use initials", ValueKind.Boolean),
        ["actor.avatar.initialsPadding"] = new("actor.avatar.initialsPadding", "Initials padding", ValueKind.Integer),

        ["font.family"] = new("font.family", "Family", ValueKind.StringReadOnly, IsEditable: false),
        ["font.category"] = new("font.category", "Category", ValueKind.OptionToken),
        ["font.sourceDirectory"] = new("font.sourceDirectory", "Source Directory", ValueKind.StringReadOnly, IsEditable: false),
        ["font.files"] = new("font.files", "Font Files", ValueKind.StringMultiline, IsEditable: false),

        ["iconTheme.assetRoot"] = new("iconTheme.assetRoot", "Asset Root", ValueKind.StringReadOnly, IsEditable: false),
        ["iconTheme.tokenCount"] = new("iconTheme.tokenCount", "Token Count", ValueKind.StringReadOnly, IsEditable: false),
        ["iconTheme.metadata"] = new("iconTheme.metadata", "Metadata", ValueKind.StringMultiline, IsEditable: false),

    };

    internal static IReadOnlyCollection<RecordClassFieldDescriptor> All => Fields.Values;

    public static RecordClassFieldDescriptor Get(string fieldId)
    {
        if (Fields.TryGetValue(fieldId, out var field))
        {
            return field;
        }

        throw new InvalidOperationException($"Unknown record class field '{fieldId}'.");
    }
}
