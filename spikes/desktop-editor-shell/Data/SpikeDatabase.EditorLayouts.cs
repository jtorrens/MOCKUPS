using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Linq;
using System.Text.Json;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public EditorLayout LoadEditorLayout(string recordClassId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT layout_json FROM editor_layouts WHERE record_class_id = $recordClassId";
        command.Parameters.AddWithValue("$recordClassId", recordClassId);
        var json = command.ExecuteScalar() as string
            ?? throw new InvalidOperationException($"Missing editor layout for record class '{recordClassId}'.");

        return JsonSerializer.Deserialize<EditorLayout>(json)
            ?? throw new InvalidOperationException($"Invalid editor layout JSON for record class '{recordClassId}'.");
    }

    private static void SeedEditorLayouts(SqliteConnection connection)
    {
        var recordClassIds = new[]
        {
            "project",
            "navigation.production_data",
            "navigation.system_data",
            "navigation.apps",
            "navigation.palette",
            "navigation.devices",
            "navigation.actors",
            "navigation.themes",
            "navigation.production_fonts",
            "navigation.icon_themes",
            "navigation.render_presets",
            "navigation.component_classes",
            "navigation.episodes",
            "app.generic",
            "app.core.chat",
            "module.generic",
            "module.core.chat",
            "episode",
            "shot",
            "palette_color",
            "device",
            "actor",
            "theme",
            "production_font",
            "icon_theme",
            "render_preset",
        }.Concat(ComponentSeedRows.Select((seed) => seed.RecordClassId));

        foreach (var recordClassId in recordClassIds)
        {
            Execute(
                connection,
                """
                INSERT OR REPLACE INTO editor_layouts (record_class_id, layout_json)
                VALUES ($recordClassId, $layoutJson)
                """,
                ("$recordClassId", recordClassId),
                ("$layoutJson", MinimalEditorLayoutJson(recordClassId)));
        }
    }

    private static string MinimalEditorLayoutJson(string recordClassId)
    {
        var generalFields = recordClassId.StartsWith("component.", StringComparison.Ordinal)
            ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "component.type", "order": 20, "visible": true },
                    { "id": "core.notes", "order": 30, "visible": true }
              """
            : recordClassId == "project"
            ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "project.slug", "order": 20, "visible": true },
                    { "id": "project.defaultFps", "order": 30, "visible": true },
                    { "id": "project.mediaRoot", "order": 40, "visible": true },
                    { "id": "core.kind", "order": 50, "visible": false },
                    { "id": "core.notes", "order": 60, "visible": true }
              """
            : recordClassId == "episode"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "episode.slug", "order": 20, "visible": true },
                    { "id": "episode.sortOrder", "order": 30, "visible": true },
                    { "id": "core.kind", "order": 40, "visible": false },
                    { "id": "core.notes", "order": 50, "visible": true }
                  """
            : recordClassId == "shot"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "shot.slug", "order": 20, "visible": true },
                    { "id": "shot.version", "order": 30, "visible": true },
                    { "id": "shot.renderName", "order": 40, "visible": true },
                    { "id": "shot.durationFrames", "order": 50, "visible": true },
                    { "id": "shot.fps", "order": 60, "visible": true },
                    { "id": "shot.ownerActorId", "order": 70, "visible": true },
                    { "id": "shot.ownerDevice", "order": 80, "visible": true },
                    { "id": "core.notes", "order": 90, "visible": true }
                  """
            : recordClassId is "app.generic" or "app.core.chat"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "app.bundleKey", "order": 20, "visible": true },
                    { "id": "app.appType", "order": 30, "visible": true },
                    { "id": "core.kind", "order": 40, "visible": false }
                  """
            : recordClassId is "module.generic" or "module.core.chat"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "module.recordClassId", "order": 20, "visible": false },
                    { "id": "module.sortOrder", "order": 30, "visible": true },
                    { "id": "core.notes", "order": 40, "visible": true },
                    { "id": "module.metadata", "order": 50, "visible": false }
                  """
            : recordClassId == "render_preset"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "renderPreset.format", "order": 20, "visible": true },
                    { "id": "renderPreset.codec", "order": 30, "visible": true },
                    { "id": "renderPreset.export.ffmpegArgs", "order": 40, "visible": true }
                  """
            : recordClassId == "palette_color"
                ? """
                    { "id": "palette.token", "order": 10, "visible": true },
                    { "id": "palette.valueHex", "order": 20, "visible": true },
                    { "id": "palette.isNeutral", "order": 30, "visible": true },
                    { "id": "palette.source", "order": 40, "visible": true },
                    { "id": "palette.protected", "order": 50, "visible": true },
                    { "id": "palette.hiddenFromPickers", "order": 60, "visible": true },
                    { "id": "palette.note", "order": 70, "visible": true }
                  """
            : recordClassId == "device"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "device.manufacturer", "order": 20, "visible": true },
                    { "id": "device.model", "order": 30, "visible": true },
                    { "id": "device.osFamily", "order": 40, "visible": true },
                    { "id": "device.metrics.designSpace.size", "order": 110, "visible": true },
                    { "id": "device.metrics.renderSize", "order": 120, "visible": true },
                    { "id": "device.metrics.scaleToPixels", "order": 130, "visible": true },
                    { "id": "device.metrics.pixelRatio", "order": 140, "visible": true },
                    { "id": "device.metrics.defaultScreenScale", "order": 150, "visible": true },
                    { "id": "device.metrics.canvas.size", "order": 160, "visible": true },
                    { "id": "device.metrics.screen.position", "order": 210, "visible": true },
                    { "id": "device.metrics.screen.size", "order": 220, "visible": true },
                    { "id": "device.metrics.cornerRadius", "order": 230, "visible": true },
                    { "id": "device.metrics.viewport.position", "order": 310, "visible": true },
                    { "id": "device.metrics.viewport.size", "order": 320, "visible": true },
                    { "id": "device.metrics.safeArea.vertical", "order": 410, "visible": true },
                    { "id": "device.metrics.safeArea.horizontal", "order": 420, "visible": true },
                    { "id": "device.metrics.statusBar.position", "order": 510, "visible": true },
                    { "id": "device.metrics.statusBar.size", "order": 520, "visible": true },
                    { "id": "device.metrics.dynamicIsland.position", "order": 610, "visible": true },
                    { "id": "device.metrics.dynamicIsland.size", "order": 620, "visible": true }
                  """
            : recordClassId == "actor"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "actor.shortName", "order": 20, "visible": true },
                    { "id": "actor.defaultDeviceId", "order": 30, "visible": true },
                    { "id": "actor.defaultThemeId", "order": 40, "visible": true }
                  """
            : recordClassId == "theme"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "theme.family", "order": 20, "visible": true },
                    { "id": "theme.defaultMode", "order": 30, "visible": true }
                  """
            : recordClassId == "production_font"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "font.family", "order": 20, "visible": true },
                    { "id": "font.category", "order": 30, "visible": true },
                    { "id": "font.sourceDirectory", "order": 40, "visible": false },
                    { "id": "font.files", "order": 50, "visible": true }
                  """
            : recordClassId == "icon_theme"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "iconTheme.assetRoot", "order": 20, "visible": true },
                    { "id": "iconTheme.tokenCount", "order": 30, "visible": true },
                    { "id": "iconTheme.metadata", "order": 40, "visible": false }
                  """
            : """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "core.kind", "order": 20, "visible": false },
                    { "id": "core.notes", "order": 30, "visible": true }
              """;

        var actorCards = recordClassId == "actor"
            ? $$"""
            ,
            {
              "id": "colors",
              "label": "Colors",
              "subtitle": "Light and dark actor palette tokens",
              "icon": "{{EditorIcons.Color}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "modes",
                  "label": "Modes",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "actor.color.modes", "order": 10, "visible": true },
                    { "id": "actor.avatarTextColor.modes", "order": 20, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "avatar",
              "label": "Avatar",
              "subtitle": "Image crop and initials fallback",
              "icon": "{{EditorIcons.Avatar}}",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "avatar",
                  "label": "Avatar image",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "actor.avatar.filePath", "order": 10, "visible": true },
                    { "id": "actor.avatar.scale", "order": 20, "visible": true },
                    { "id": "actor.avatar.offset", "order": 30, "visible": true },
                    { "id": "actor.avatar.useInitials", "order": 40, "visible": true },
                    { "id": "actor.avatar.initialsPadding", "order": 50, "visible": true }
                  ]
                }
              ]
            }
            """
            : "";
        var appCards = recordClassId is "app.generic" or "app.core.chat"
            ? $$"""
            ,
            {
              "id": "wallpaper",
              "label": "Wallpaper",
              "subtitle": "Wallpaper color, image and opacity",
              "icon": "{{EditorIcons.Image}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "wallpaper",
                  "label": "Wallpaper",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "app.wallpaper.kind", "order": 10, "visible": true },
                    { "id": "app.wallpaper.opacity", "order": 20, "visible": true },
                    { "id": "app.wallpaper.color", "order": 30, "visible": true },
                    { "id": "app.wallpaper.image.filePath", "order": 40, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "icon",
              "label": "Icon",
              "subtitle": "App icon image crop",
              "icon": "{{EditorIcons.Icon}}",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "icon",
                  "label": "Icon",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "app.icon.filePath", "order": 10, "visible": true },
                    { "id": "app.icon.scale", "order": 20, "visible": true },
                    { "id": "app.icon.offset", "order": 30, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "notes",
              "label": "Notes",
              "subtitle": "App notes",
              "icon": "{{EditorIcons.Content}}",
              "order": 40,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "notes",
                  "label": "Notes",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "app.note", "order": 10, "visible": true }
                  ]
                }
              ]
            }
            """
            : "";
        var themeCards = recordClassId == "theme"
            ? $$"""
            ,
            {
              "id": "references",
              "label": "References",
              "subtitle": "Linked icon, status and navigation resources",
              "icon": "{{EditorIcons.Design}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "references",
                  "label": "References",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "theme.iconThemeId", "order": 10, "visible": true },
                    { "id": "theme.statusBarId", "order": 20, "visible": true },
                    { "id": "theme.navigationBarId", "order": 30, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "colors",
              "label": "Colors",
              "subtitle": "Theme color behavior",
              "icon": "{{EditorIcons.Color}}",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "neutralTint",
                  "label": "Neutral tint",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "theme.neutralTint.hueDeg", "order": 10, "visible": true },
                    { "id": "theme.neutralTint.saturation", "order": 20, "visible": true }
                  ]
                },
                {
                  "id": "appColors",
                  "label": "App colors",
                  "order": 20,
                  "visible": true,
                  "fields": [
                    { "id": "theme.colors.background", "order": 10, "visible": true },
                    { "id": "theme.colors.surface", "order": 20, "visible": true },
                    { "id": "theme.colors.card", "order": 30, "visible": true },
                    { "id": "theme.colors.accent", "order": 40, "visible": true }
                  ]
                },
                {
                  "id": "contentColors",
                  "label": "Content colors",
                  "order": 30,
                  "visible": true,
                  "fields": [
                    { "id": "theme.colors.label", "order": 10, "visible": true },
                    { "id": "theme.colors.text", "order": 20, "visible": true },
                    { "id": "theme.colors.textPrimary", "order": 30, "visible": true },
                    { "id": "theme.colors.textSecondary", "order": 40, "visible": true },
                    { "id": "theme.colors.icon", "order": 50, "visible": true }
                  ]
                },
                {
                  "id": "actionInputColors",
                  "label": "Action and input colors",
                  "order": 40,
                  "visible": true,
                  "fields": [
                    { "id": "theme.colors.button", "order": 10, "visible": true },
                    { "id": "theme.colors.field", "order": 20, "visible": true },
                    { "id": "theme.colors.checkbox", "order": 30, "visible": true },
                    { "id": "theme.colors.radio", "order": 40, "visible": true },
                    { "id": "theme.colors.switch", "order": 50, "visible": true }
                  ]
                },
                {
                  "id": "navigationFeedbackColors",
                  "label": "Navigation and feedback colors",
                  "order": 50,
                  "visible": true,
                  "fields": [
                    { "id": "theme.colors.tab", "order": 10, "visible": true },
                    { "id": "theme.colors.menuItem", "order": 20, "visible": true },
                    { "id": "theme.colors.badge", "order": 30, "visible": true },
                    { "id": "theme.colors.toast", "order": 40, "visible": true },
                    { "id": "theme.colors.divider", "order": 50, "visible": true }
                  ]
                },
                {
                  "id": "borderColors",
                  "label": "Border colors",
                  "order": 60,
                  "visible": true,
                  "fields": [
                    { "id": "theme.borders.primary", "order": 10, "visible": true },
                    { "id": "theme.borders.secondary", "order": 20, "visible": true },
                    { "id": "theme.borders.alternate", "order": 30, "visible": true }
                  ]
                },
                {
                  "id": "cursor",
                  "label": "Cursor",
                  "order": 70,
                  "visible": true,
                  "fields": [
                    { "id": "theme.cursor.color", "order": 10, "visible": true },
                    { "id": "theme.cursor.width", "order": 20, "visible": true },
                    { "id": "theme.cursor.blinkFrames", "order": 30, "visible": true }
                  ]
                },
                {
                  "id": "iconColors",
                  "label": "Icon colors",
                  "order": 80,
                  "visible": true,
                  "fields": [
                    { "id": "theme.icons.primary", "order": 10, "visible": true },
                    { "id": "theme.icons.secondary", "order": 20, "visible": true },
                    { "id": "theme.icons.accent", "order": 30, "visible": true }
                  ]
                },
                {
                  "id": "keyboard",
                  "label": "Keyboard",
                  "order": 90,
                  "visible": true,
                  "fields": [
                    { "id": "theme.keyboard.background", "order": 10, "visible": true },
                    { "id": "theme.keyboard.keyBackground", "order": 20, "visible": true },
                    { "id": "theme.keyboard.specialKeyBackground", "order": 30, "visible": true },
                    { "id": "theme.keyboard.pressedKeyBackground", "order": 40, "visible": true },
                    { "id": "theme.keyboard.popoverBackground", "order": 50, "visible": true },
                    { "id": "theme.keyboard.text", "order": 60, "visible": true }
                  ]
                },
                {
                  "id": "navigationBar",
                  "label": "Navigation bar",
                  "order": 70,
                  "visible": true,
                  "fields": [
                    { "id": "theme.navigationBar.foreground", "order": 10, "visible": true },
                    { "id": "theme.navigationBar.background", "order": 20, "visible": true }
                  ]
                },
                {
                  "id": "statusBar",
                  "label": "Status bar",
                  "order": 80,
                  "visible": true,
                  "fields": [
                    { "id": "theme.statusBar.foreground", "order": 10, "visible": true },
                    { "id": "theme.statusBar.background", "order": 20, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "motion",
              "label": "Motion",
              "subtitle": "Theme transition timing and easing",
              "icon": "{{EditorIcons.Animation}}",
              "order": 35,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "transitions",
                  "label": "Transitions",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "theme.motion.fade", "order": 10, "visible": true },
                    { "id": "theme.motion.slide", "order": 20, "visible": true },
                    { "id": "theme.motion.swipe", "order": 30, "visible": true },
                    { "id": "theme.motion.scale", "order": 40, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "typography",
              "label": "Typography",
              "subtitle": "Default text and emoji font tokens",
              "icon": "{{EditorIcons.Typography}}",
              "order": 40,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "typography",
                  "label": "Typography",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "theme.typography.fontFamilyId", "order": 10, "visible": true },
                    { "id": "theme.typography.emojiFontFamilyId", "order": 20, "visible": true },
                    { "id": "theme.typography.size", "order": 30, "visible": true },
                    { "id": "theme.typography.sizes.xs", "order": 40, "visible": true },
                    { "id": "theme.typography.sizes.s", "order": 50, "visible": true },
                    { "id": "theme.typography.sizes.m", "order": 60, "visible": true },
                    { "id": "theme.typography.sizes.l", "order": 70, "visible": true },
                    { "id": "theme.typography.sizes.xl", "order": 80, "visible": true },
                    { "id": "theme.typography.weight", "order": 90, "visible": true },
                    { "id": "theme.typography.style", "order": 100, "visible": true },
                    { "id": "theme.typography.lineHeights.tight", "order": 110, "visible": true },
                    { "id": "theme.typography.lineHeights.compact", "order": 120, "visible": true },
                    { "id": "theme.typography.lineHeights.normal", "order": 130, "visible": true },
                    { "id": "theme.typography.lineHeights.relaxed", "order": 140, "visible": true },
                    { "id": "theme.typography.lineHeights.loose", "order": 150, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "spacing",
              "label": "Spacing",
              "subtitle": "Shared padding and gap tokens",
              "icon": "{{EditorIcons.Layout}}",
              "order": 45,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "spacing",
                  "label": "Spacing",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "theme.spacing.none", "order": 10, "visible": true },
                    { "id": "theme.spacing.xs", "order": 20, "visible": true },
                    { "id": "theme.spacing.s", "order": 30, "visible": true },
                    { "id": "theme.spacing.m", "order": 40, "visible": true },
                    { "id": "theme.spacing.l", "order": 50, "visible": true },
                    { "id": "theme.spacing.xl", "order": 60, "visible": true },
                    { "id": "theme.spacing.xxl", "order": 70, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "radii",
              "label": "Radii",
              "subtitle": "Shared corner radius tokens",
              "icon": "{{EditorIcons.Layout}}",
              "order": 50,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "radii",
                  "label": "Radii",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "theme.radii.none", "order": 10, "visible": true },
                    { "id": "theme.radii.control", "order": 20, "visible": true },
                    { "id": "theme.radii.card", "order": 30, "visible": true },
                    { "id": "theme.radii.panel", "order": 40, "visible": true },
                    { "id": "theme.radii.surface", "order": 50, "visible": true },
                    { "id": "theme.radii.pill", "order": 60, "visible": true },
                    { "id": "theme.radii.avatar", "order": 70, "visible": true },
                    { "id": "theme.radii.full", "order": 80, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "shadow",
              "label": "Shadow",
              "subtitle": "Single reusable shadow token",
              "icon": "{{EditorIcons.Shadow}}",
              "order": 60,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "default",
                  "label": "Shadow",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "theme.shadows.default.color", "order": 10, "visible": true },
                    { "id": "theme.shadows.default.alpha", "order": 20, "visible": true },
                    { "id": "theme.shadows.default.offsetX", "order": 30, "visible": true },
                    { "id": "theme.shadows.default.offsetY", "order": 40, "visible": true },
                    { "id": "theme.shadows.default.blur", "order": 50, "visible": true }
                  ]
                }
              ]
            }
            """
            : "";
        var componentCards = recordClassId.StartsWith("component.", StringComparison.Ordinal)
            ? ComponentClassLayoutCardsJson(recordClassId)
            : "";

        return $$"""
        {
          "cards": [
            {
              "id": "general",
              "label": "General",
              "subtitle": "Identity",
              "icon": "{{EditorIcons.General}}",
              "order": 10,
              "visible": true,
              "defaultOpen": true,
              "groups": [
                {
                  "id": "identity",
                  "label": "Identity",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    {{generalFields}}
                  ]
                }
              ]
            }{{appCards}}{{actorCards}}{{themeCards}}{{componentCards}}
          ]
        }
        """;
    }
}
