using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record RecordClassFieldDescriptor(
    string Id,
    string Label,
    ValueKind ValueKind,
    bool IsEditable = true,
    IReadOnlyList<FieldOption>? Options = null,
    PairFieldLabels? PairLabels = null,
    ImagePreviewDefinition? ImagePreview = null,
    NumberDefinition? Number = null);

internal static class RecordClassFieldCatalog
{
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
        ["shot.durationFrames"] = new("shot.durationFrames", "Duration Frames", ValueKind.Integer, IsEditable: false),
        ["shot.fps"] = new("shot.fps", "FPS", ValueKind.Integer),
        ["shot.ownerActorId"] = new("shot.ownerActorId", "Owner Actor", ValueKind.OptionToken),
        ["shot.ownerDevice"] = new("shot.ownerDevice", "Device", ValueKind.StringReadOnly, IsEditable: false),
        ["shot.renderPresetId"] = new("shot.renderPresetId", "Render Preset", ValueKind.OptionToken),
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
        ["app.wallpaper.color"] = new("app.wallpaper.color", "Wallpaper Color", ValueKind.PaletteColorPair, PairLabels: new("Light", "Dark")),
        ["app.wallpaper.image.filePath"] = new(
            "app.wallpaper.image.filePath",
            "Image",
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
        ["app.icon.offset"] = new("app.icon.offset", "Icon offset", ValueKind.IntegerPair, PairLabels: new("X", "Y")),

        ["module.recordClassId"] = new("module.recordClassId", "Module class", ValueKind.StringReadOnly, IsEditable: false),
        ["module.sortOrder"] = new("module.sortOrder", "Sort Order", ValueKind.Integer),
        ["module.metadata"] = new("module.metadata", "Metadata", ValueKind.StringMultiline),

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
        ["device.metrics.designSpace.size"] = new("device.metrics.designSpace.size", "Design space", ValueKind.IntegerPair),
        ["device.metrics.renderSize"] = new("device.metrics.renderSize", "Render size", ValueKind.IntegerPair),
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
        ["device.metrics.canvas.size"] = new("device.metrics.canvas.size", "Canvas size", ValueKind.IntegerPair),
        ["device.metrics.screen.position"] = new("device.metrics.screen.position", "Screen position", ValueKind.IntegerPair),
        ["device.metrics.screen.size"] = new("device.metrics.screen.size", "Screen size", ValueKind.IntegerPair),
        ["device.metrics.cornerRadius"] = new("device.metrics.cornerRadius", "Corner radius", ValueKind.Integer),
        ["device.metrics.viewport.position"] = new("device.metrics.viewport.position", "Viewport position", ValueKind.IntegerPair),
        ["device.metrics.viewport.size"] = new("device.metrics.viewport.size", "Viewport size", ValueKind.IntegerPair),
        ["device.metrics.safeArea.vertical"] = new("device.metrics.safeArea.vertical", "Safe vertical", ValueKind.IntegerPair),
        ["device.metrics.safeArea.horizontal"] = new("device.metrics.safeArea.horizontal", "Safe horizontal", ValueKind.IntegerPair),
        ["device.metrics.statusBar.position"] = new("device.metrics.statusBar.position", "Status bar position", ValueKind.IntegerPair),
        ["device.metrics.statusBar.size"] = new("device.metrics.statusBar.size", "Status bar size", ValueKind.IntegerPair),
        ["device.metrics.dynamicIsland.position"] = new("device.metrics.dynamicIsland.position", "Dynamic island position", ValueKind.IntegerPair),
        ["device.metrics.dynamicIsland.size"] = new("device.metrics.dynamicIsland.size", "Dynamic island size", ValueKind.IntegerPair),

        ["theme.family"] = new("theme.family", "Family", ValueKind.OptionToken),
        ["theme.iconThemeId"] = new("theme.iconThemeId", "Icon theme", ValueKind.OptionToken),
        ["theme.statusBarId"] = new("theme.statusBarId", "Status bar", ValueKind.OptionToken),
        ["theme.navigationBarId"] = new("theme.navigationBarId", "Navigation bar", ValueKind.OptionToken),
        ["theme.defaultMode"] = new("theme.defaultMode", "Default mode", ValueKind.OptionToken),
        ["theme.neutralTint.hueDeg"] = new("theme.neutralTint.hueDeg", "Hue", ValueKind.HueDegrees),
        ["theme.neutralTint.saturation"] = new("theme.neutralTint.saturation", "Saturation", ValueKind.StringSingleLine),
        ["theme.colors.background"] = new("theme.colors.background", "Background", ValueKind.PaletteColorPair),
        ["theme.colors.textPrimary"] = new("theme.colors.textPrimary", "Text primary", ValueKind.PaletteColorPair),
        ["theme.colors.textSecondary"] = new("theme.colors.textSecondary", "Text secondary", ValueKind.PaletteColorPair),
        ["theme.colors.accent"] = new("theme.colors.accent", "Accent", ValueKind.PaletteColorPair),
        ["theme.icons.primary"] = new("theme.icons.primary", "Icon primary", ValueKind.PaletteColorPair),
        ["theme.icons.secondary"] = new("theme.icons.secondary", "Icon secondary", ValueKind.PaletteColorPair),
        ["theme.icons.accent"] = new("theme.icons.accent", "Icon accent", ValueKind.PaletteColorPair),
        ["theme.borders.primary"] = new("theme.borders.primary", "Border primary", ValueKind.PaletteColorPair),
        ["theme.borders.secondary"] = new("theme.borders.secondary", "Border secondary", ValueKind.PaletteColorPair),
        ["theme.borders.alternate"] = new("theme.borders.alternate", "Border alternate", ValueKind.PaletteColorPair),
        ["theme.cursor.color"] = new("theme.cursor.color", "Cursor color", ValueKind.PaletteColorPair),
        ["theme.cursor.width"] = new("theme.cursor.width", "Cursor width", ValueKind.Integer),
        ["theme.cursor.blinkFrames"] = new("theme.cursor.blinkFrames", "Blink frames", ValueKind.Integer),
        ["theme.statusBar.foreground"] = new("theme.statusBar.foreground", "Foreground", ValueKind.PaletteColorPair),
        ["theme.statusBar.background"] = new("theme.statusBar.background", "Background", ValueKind.PaletteColorPair),
        ["theme.navigationBar.foreground"] = new("theme.navigationBar.foreground", "Foreground", ValueKind.PaletteColorPair),
        ["theme.navigationBar.background"] = new("theme.navigationBar.background", "Background", ValueKind.PaletteColorPair),
        ["theme.keyboard.background"] = new("theme.keyboard.background", "Background", ValueKind.PaletteColorPair),
        ["theme.keyboard.keyBackground"] = new("theme.keyboard.keyBackground", "Key background", ValueKind.PaletteColorPair),
        ["theme.keyboard.specialKeyBackground"] = new("theme.keyboard.specialKeyBackground", "Special key background", ValueKind.PaletteColorPair),
        ["theme.keyboard.pressedKeyBackground"] = new("theme.keyboard.pressedKeyBackground", "Pressed key background", ValueKind.PaletteColorPair),
        ["theme.keyboard.popoverBackground"] = new("theme.keyboard.popoverBackground", "Popover background", ValueKind.PaletteColorPair),
        ["theme.keyboard.text"] = new("theme.keyboard.text", "Text", ValueKind.PaletteColorPair),
        ["theme.typography.fontFamilyId"] = new("theme.typography.fontFamilyId", "Text font", ValueKind.OptionToken),
        ["theme.typography.emojiFontFamilyId"] = new("theme.typography.emojiFontFamilyId", "Emoji font", ValueKind.OptionToken),
        ["theme.typography.size"] = new("theme.typography.size", "Size", ValueKind.Integer),
        ["theme.typography.weight"] = new("theme.typography.weight", "Weight", ValueKind.OptionToken),
        ["theme.typography.style"] = new("theme.typography.style", "Style", ValueKind.OptionToken),

        ["actor.shortName"] = new("actor.shortName", "Short name", ValueKind.StringSingleLine),
        ["actor.defaultDeviceId"] = new("actor.defaultDeviceId", "Default device", ValueKind.OptionToken),
        ["actor.defaultThemeId"] = new("actor.defaultThemeId", "Default theme", ValueKind.OptionToken),
        ["actor.color.modes"] = new("actor.color.modes", "Actor Color", ValueKind.PaletteColorPair),
        ["actor.avatarTextColor.modes"] = new("actor.avatarTextColor.modes", "Actor Text Color", ValueKind.PaletteColorPair),
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
        ["actor.avatar.offset"] = new("actor.avatar.offset", "Avatar offset", ValueKind.IntegerPair, PairLabels: new("X", "Y")),
        ["actor.avatar.useInitials"] = new("actor.avatar.useInitials", "Use initials", ValueKind.Boolean),
        ["actor.avatar.initialsPadding"] = new("actor.avatar.initialsPadding", "Initials padding", ValueKind.Integer),

        ["font.family"] = new("font.family", "Family", ValueKind.StringReadOnly, IsEditable: false),
        ["font.category"] = new("font.category", "Category", ValueKind.OptionToken),
        ["font.sourceDirectory"] = new("font.sourceDirectory", "Source Directory", ValueKind.StringReadOnly, IsEditable: false),
        ["font.files"] = new("font.files", "Font Files", ValueKind.StringMultiline, IsEditable: false),

        ["iconTheme.assetRoot"] = new("iconTheme.assetRoot", "Asset Root", ValueKind.StringReadOnly, IsEditable: false),
        ["iconTheme.tokenCount"] = new("iconTheme.tokenCount", "Token Count", ValueKind.StringReadOnly, IsEditable: false),
        ["iconTheme.metadata"] = new("iconTheme.metadata", "Metadata", ValueKind.StringMultiline, IsEditable: false),

        ["statusBar.family"] = new("statusBar.family", "Family", ValueKind.StringSingleLine),
        ["statusBar.layout.height"] = new("statusBar.layout.height", "Height", ValueKind.Integer),
        ["statusBar.layout.itemSize"] = new("statusBar.layout.itemSize", "Item size", ValueKind.Integer),
        ["statusBar.layout.gap"] = new("statusBar.layout.gap", "Gap", ValueKind.Integer),
        ["statusBar.layout.sidePadding"] = new("statusBar.layout.sidePadding", "Side padding", ValueKind.Integer),

        ["navigationBar.family"] = new("navigationBar.family", "Family", ValueKind.StringSingleLine),
        ["navigationBar.type"] = new("navigationBar.type", "Style", ValueKind.OptionToken),
        ["navigationBar.layout.height"] = new("navigationBar.layout.height", "Height", ValueKind.Integer),
        ["navigationBar.layout.itemSize"] = new("navigationBar.layout.itemSize", "Item size", ValueKind.Integer),
        ["navigationBar.layout.sidePadding"] = new("navigationBar.layout.sidePadding", "Side padding", ValueKind.Integer),
        ["navigationBar.layout.strokeWidth"] = new(
            "navigationBar.layout.strokeWidth",
            "Stroke width",
            ValueKind.Decimal,
            Number: new NumberDefinition(0, 20, 0.5m, 2)),
        ["navigationBar.layout.cornerRadius"] = new("navigationBar.layout.cornerRadius", "Corner radius", ValueKind.Integer),
        ["navigationBar.layout.filled"] = new("navigationBar.layout.filled", "Filled", ValueKind.Boolean),
        ["navigationBar.gesture.width"] = new("navigationBar.gesture.width", "Width", ValueKind.Integer),
        ["navigationBar.gesture.height"] = new("navigationBar.gesture.height", "Height", ValueKind.Integer),
        ["navigationBar.gesture.cornerRadius"] = new("navigationBar.gesture.cornerRadius", "Corner radius", ValueKind.Integer),
    };

    public static RecordClassFieldDescriptor Get(string fieldId)
    {
        if (Fields.TryGetValue(fieldId, out var field))
        {
            return field;
        }

        throw new InvalidOperationException($"Unknown record class field '{fieldId}'.");
    }
}
