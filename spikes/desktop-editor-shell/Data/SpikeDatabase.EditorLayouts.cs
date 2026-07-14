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

    internal static void SeedEditorLayouts(SqliteConnection connection)
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
            "app.system",
            "module.generic",
            "module.core.chat",
            "module.core.lockScreen",
            "module_instance",
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

    private static void NormalizeEditorLayouts(SqliteConnection connection)
    {
        foreach (var retiredRecordClassId in new[]
        {
            "component.text_input_bar", "component.button_icon", "component.video",
            "status_bar", "navigation_bar", "navigation.status_bars", "navigation.navigation_bars",
        })
        {
            Execute(
                connection,
                "DELETE FROM editor_layouts WHERE record_class_id = $recordClassId",
                ("$recordClassId", retiredRecordClassId));
        }

        var conversationLayout = ScalarString(
            connection,
            "SELECT layout_json FROM editor_layouts WHERE record_class_id = 'module.core.chat'");
        if (!string.IsNullOrWhiteSpace(conversationLayout)
            && (conversationLayout.Contains("module.conversation.headerLeftIconRowVariant", StringComparison.Ordinal)
                || conversationLayout.Contains("module.conversation.headerRightIconRowVariant", StringComparison.Ordinal)
                || conversationLayout.Contains("module.conversation.headerLeftIconRow.inputs", StringComparison.Ordinal)
                || conversationLayout.Contains("module.conversation.headerRightIconRow.inputs", StringComparison.Ordinal)
                || !conversationLayout.Contains("\"id\": \"header\"", StringComparison.Ordinal)))
        {
            Execute(
                connection,
                "UPDATE editor_layouts SET layout_json = $layoutJson WHERE record_class_id = 'module.core.chat'",
                ("$layoutJson", MinimalEditorLayoutJson("module.core.chat")));
        }

        var audioLayout = ScalarString(
            connection,
            "SELECT layout_json FROM editor_layouts WHERE record_class_id = 'component.audio'");
        if (!string.IsNullOrWhiteSpace(audioLayout)
            && (audioLayout.Contains("component.audio.waveformBarWidth", StringComparison.Ordinal)
                || audioLayout.Contains("component.audio.badge.backgroundColor", StringComparison.Ordinal)
                || audioLayout.Contains("component.audio.badge.iconColor", StringComparison.Ordinal)
                || audioLayout.Contains("component.audio.textSize", StringComparison.Ordinal)
                || audioLayout.Contains("component.audio.textColorToken", StringComparison.Ordinal)
                || !audioLayout.Contains("component.audio.badge.size", StringComparison.Ordinal)
                || !audioLayout.Contains("navigation-asset:Waveform", StringComparison.Ordinal)
                || !audioLayout.Contains("navigation-asset:Badge", StringComparison.Ordinal)
                || !audioLayout.Contains("\"groupLayout\": \"verticalCards\"", StringComparison.Ordinal)))
        {
            Execute(
                connection,
                "UPDATE editor_layouts SET layout_json = $layoutJson WHERE record_class_id = 'component.audio'",
                ("$layoutJson", MinimalEditorLayoutJson("component.audio")));
        }

        var labelLayout = ScalarString(
            connection,
            "SELECT layout_json FROM editor_layouts WHERE record_class_id = 'component.label'");
        if (!string.IsNullOrWhiteSpace(labelLayout)
            && (labelLayout.Contains("component.label.textGap\"", StringComparison.Ordinal)
                || !labelLayout.Contains("component.label.subtextPlacement", StringComparison.Ordinal)))
        {
            Execute(connection,
                "UPDATE editor_layouts SET layout_json = $layoutJson WHERE record_class_id = 'component.label'",
                ("$layoutJson", MinimalEditorLayoutJson("component.label")));
        }

        var actorLayout = ScalarString(
            connection,
            "SELECT layout_json FROM editor_layouts WHERE record_class_id = 'actor'");
        if (!string.IsNullOrWhiteSpace(actorLayout)
            && (!actorLayout.Contains("actor.wallpaper.images.light.filePath", StringComparison.Ordinal)
                || !actorLayout.Contains("actor.wallpaper.images.dark.filePath", StringComparison.Ordinal)))
        {
            Execute(connection,
                "UPDATE editor_layouts SET layout_json = $layoutJson WHERE record_class_id = 'actor'",
                ("$layoutJson", MinimalEditorLayoutJson("actor")));
        }

        foreach (var appRecordClassId in new[] { "app.generic", "app.core.chat" })
        {
            var appLayout = ScalarString(
                connection,
                "SELECT layout_json FROM editor_layouts WHERE record_class_id = $recordClassId",
                ("$recordClassId", appRecordClassId));
            if (!string.IsNullOrWhiteSpace(appLayout)
                && (!appLayout.Contains("app.wallpaper.images.light.filePath", StringComparison.Ordinal)
                    || !appLayout.Contains("app.wallpaper.images.dark.filePath", StringComparison.Ordinal)))
            {
                Execute(connection,
                    "UPDATE editor_layouts SET layout_json = $layoutJson WHERE record_class_id = $recordClassId",
                    ("$recordClassId", appRecordClassId),
                    ("$layoutJson", MinimalEditorLayoutJson(appRecordClassId)));
            }
        }

        var mediaLayout = ScalarString(
            connection,
            "SELECT layout_json FROM editor_layouts WHERE record_class_id = 'component.media'");
        if (!string.IsNullOrWhiteSpace(mediaLayout)
            && (mediaLayout.Contains("component.media.idleText.textColorToken", StringComparison.Ordinal)
                || mediaLayout.Contains("component.media.idleText.typography", StringComparison.Ordinal)
                || mediaLayout.Contains("component.media.idleText.textAlign", StringComparison.Ordinal)
                || mediaLayout.Contains("component.media.playText.textColorToken", StringComparison.Ordinal)
                || mediaLayout.Contains("component.media.playText.typography", StringComparison.Ordinal)
                || mediaLayout.Contains("component.media.playText.textAlign", StringComparison.Ordinal)
                || !mediaLayout.Contains("\"groupLayout\": \"separatedSections\"", StringComparison.Ordinal)))
        {
            Execute(
                connection,
                "UPDATE editor_layouts SET layout_json = $layoutJson WHERE record_class_id = 'component.media'",
                ("$layoutJson", MinimalEditorLayoutJson("component.media")));
        }

        var bubbleLayout = ScalarString(
            connection,
            "SELECT layout_json FROM editor_layouts WHERE record_class_id = 'component.bubble'");
        if (!string.IsNullOrWhiteSpace(bubbleLayout)
            && !bubbleLayout.Contains("\"groupLayout\": \"separatedSections\"", StringComparison.Ordinal))
        {
            Execute(
                connection,
                "UPDATE editor_layouts SET layout_json = $layoutJson WHERE record_class_id = 'component.bubble'",
                ("$layoutJson", MinimalEditorLayoutJson("component.bubble")));
        }

        var themeLayout = ScalarString(
            connection,
            "SELECT layout_json FROM editor_layouts WHERE record_class_id = 'theme'");
        if (!string.IsNullOrWhiteSpace(themeLayout)
            && (!themeLayout.Contains("Keyboard dimensions and color tokens", StringComparison.Ordinal)
                || !themeLayout.Contains("theme.keyboard.keyGap", StringComparison.Ordinal)
                || !themeLayout.Contains("theme.motion.buttonPushedDurationMs", StringComparison.Ordinal)
                || !themeLayout.Contains("theme.motion.naturalPace.normal", StringComparison.Ordinal)
                || !themeLayout.Contains("theme.keyboard.keyBorder", StringComparison.Ordinal)
                || themeLayout.Contains("theme.keyboard.popoverBackground", StringComparison.Ordinal)
                || themeLayout.Contains("theme.radii.control", StringComparison.Ordinal)
                || themeLayout.Contains("theme.typography.size\"", StringComparison.Ordinal)
                || !themeLayout.Contains("\"id\": \"fontFamilies\"", StringComparison.Ordinal)
                || !themeLayout.Contains("\"id\": \"iconSizes\"", StringComparison.Ordinal)
                || !themeLayout.Contains("\"pairLayout\": \"sharedHeader\"", StringComparison.Ordinal)
                || !IsValidLayoutJson(themeLayout)))
        {
            Execute(
                connection,
                "UPDATE editor_layouts SET layout_json = $layoutJson WHERE record_class_id = 'theme'",
                ("$layoutJson", MinimalEditorLayoutJson("theme")));
        }

        var keyboardLayout = ScalarString(
            connection,
            "SELECT layout_json FROM editor_layouts WHERE record_class_id = 'component.keyboard'");
        if (!string.IsNullOrWhiteSpace(keyboardLayout)
            && (keyboardLayout.Contains("component.keyboard.popoverBackgroundColorToken", StringComparison.Ordinal)
                || keyboardLayout.Contains("component.keyboard.backgroundColorToken", StringComparison.Ordinal)
                || keyboardLayout.Contains("component.keyboard.heightToken", StringComparison.Ordinal)
                || keyboardLayout.Contains("component.keyboard.keyGapToken", StringComparison.Ordinal)
                || keyboardLayout.Contains("component.keyboard.rowGapToken", StringComparison.Ordinal)
                || keyboardLayout.Contains("component.keyboard.specialKeyTextScale", StringComparison.Ordinal)
                || keyboardLayout.Contains("component.style.cornerRadiusToken", StringComparison.Ordinal)
                || !keyboardLayout.Contains("navigation-asset:Keys", StringComparison.Ordinal)))
        {
            Execute(
                connection,
                "UPDATE editor_layouts SET layout_json = $layoutJson WHERE record_class_id = 'component.keyboard'",
                ("$layoutJson", MinimalEditorLayoutJson("component.keyboard")));
        }

        var iconBarLayout = ScalarString(
            connection,
            "SELECT layout_json FROM editor_layouts WHERE record_class_id = 'component.iconBar'");
        if (!string.IsNullOrWhiteSpace(iconBarLayout)
            && (iconBarLayout.Contains("component.iconBar.iconButton.editor", StringComparison.Ordinal)
                || !iconBarLayout.Contains("component.iconBar.sizeSource", StringComparison.Ordinal)
                || !iconBarLayout.Contains("navigation-asset:Center", StringComparison.Ordinal)))
        {
            Execute(
                connection,
                "UPDATE editor_layouts SET layout_json = $layoutJson WHERE record_class_id = 'component.iconBar'",
                ("$layoutJson", MinimalEditorLayoutJson("component.iconBar")));
        }

        foreach (var (recordClassId, requiredPresentation, requiredAsset) in new[]
        {
            ("component.surface", "\"groupLayout\": \"separatedSections\"", ""),
            ("component.textBox", "\"groupLayout\": \"separatedSections\"", ""),
            ("component.avatar", "\"groupLayout\": \"separatedSections\"", ""),
            ("component.label", "\"groupLayout\": \"separatedSections\"", ""),
            ("component.iconBar", "\"groupLayout\": \"verticalCards\"", "navigation-asset:Center"),
            ("component.button", "\"presentation\": \"verticalCards\"", "navigation-asset:Interaction"),
            ("component.status_bar", "\"groupLayout\": \"separatedSections\"", ""),
            ("component.navigation_bar", "\"groupLayout\": \"separatedSections\"", ""),
            ("component.textInputBar", "\"groupLayout\": \"separatedSections\"", ""),
            ("component.keyboard", "\"groupLayout\": \"verticalCards\"", "navigation-asset:Keys"),
            ("theme", "\"groupLayout\": \"verticalCards\"", "navigation-asset:Neutral tint"),
        })
        {
            var layout = ScalarString(
                connection,
                "SELECT layout_json FROM editor_layouts WHERE record_class_id = $recordClassId",
                ("$recordClassId", recordClassId));
            if (!string.IsNullOrWhiteSpace(layout)
                && (!layout.Contains(requiredPresentation, StringComparison.Ordinal)
                    || (requiredAsset.Length > 0 && !layout.Contains(requiredAsset, StringComparison.Ordinal))))
            {
                Execute(
                    connection,
                    "UPDATE editor_layouts SET layout_json = $layoutJson WHERE record_class_id = $recordClassId",
                    ("$recordClassId", recordClassId),
                    ("$layoutJson", MinimalEditorLayoutJson(recordClassId)));
            }
        }

        foreach (var (recordClassId, requiredField, retiredField) in new[]
        {
            ("component.iconRow", "component.iconRow.sizeSource", ""),
            ("component.button", "component.button.contentGapToken", "component.button.iconSizeToken"),
        })
        {
            var layout = ScalarString(connection, "SELECT layout_json FROM editor_layouts WHERE record_class_id = $recordClassId", ("$recordClassId", recordClassId));
            if (!string.IsNullOrWhiteSpace(layout)
                && (!layout.Contains(requiredField, StringComparison.Ordinal)
                    || (!string.IsNullOrWhiteSpace(retiredField) && layout.Contains(retiredField, StringComparison.Ordinal))))
            {
                Execute(connection, "UPDATE editor_layouts SET layout_json = $layoutJson WHERE record_class_id = $recordClassId", ("$recordClassId", recordClassId), ("$layoutJson", MinimalEditorLayoutJson(recordClassId)));
            }
        }

        foreach (var recordClassId in new[] { "module.generic", "module.core.chat", "module.core.lockScreen" })
        {
            var moduleLayout = ScalarString(
                connection,
                "SELECT layout_json FROM editor_layouts WHERE record_class_id = $recordClassId",
                ("$recordClassId", recordClassId));
            if (string.IsNullOrWhiteSpace(moduleLayout)
                || (moduleLayout.Contains("module.appearanceMode", StringComparison.Ordinal)
                    && (recordClassId != "module.core.chat"
                        || (moduleLayout.Contains("module.conversation.headerAvatarAlignment", StringComparison.Ordinal)
                            && !moduleLayout.Contains("module.conversation.statusBarVariant", StringComparison.Ordinal)
                            && !moduleLayout.Contains("module.conversation.navigationBarVariant", StringComparison.Ordinal)))))
            {
                continue;
            }

            Execute(
                connection,
                "UPDATE editor_layouts SET layout_json = $layoutJson WHERE record_class_id = $recordClassId",
                ("$recordClassId", recordClassId),
                ("$layoutJson", MinimalEditorLayoutJson(recordClassId)));
        }

    }

    private static bool IsValidLayoutJson(string layoutJson)
    {
        try
        {
            using var _ = JsonDocument.Parse(layoutJson);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string WallpaperCardJson(string fieldOwner, int order) => $$"""
        ,
        {
          "id": "wallpaper",
          "label": "Wallpaper",
          "subtitle": "Wallpaper color, images and opacity",
          "icon": "{{EditorIcons.Image}}",
          "order": {{order}},
          "visible": true,
          "defaultOpen": false,
          "groups": [
            {
              "id": "wallpaper",
              "label": "Wallpaper",
              "order": 10,
              "visible": true,
              "fields": [
                { "id": "{{fieldOwner}}.wallpaper.kind", "order": 10, "visible": true },
                { "id": "{{fieldOwner}}.wallpaper.opacity", "order": 20, "visible": true },
                { "id": "{{fieldOwner}}.wallpaper.color", "order": 30, "visible": true },
                { "id": "{{fieldOwner}}.wallpaper.images.light.filePath", "order": 40, "visible": true },
                { "id": "{{fieldOwner}}.wallpaper.images.dark.filePath", "order": 50, "visible": true }
              ]
            }
          ]
        }
        """;

    private static string AppNotesCardJson(int order) => $$"""
        {
          "id": "notes",
          "label": "Notes",
          "subtitle": "App notes",
          "icon": "{{EditorIcons.Content}}",
          "order": {{order}},
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
        """;

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
            : recordClassId is "app.generic" or "app.core.chat" or "app.system"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "app.bundleKey", "order": 20, "visible": true },
                    { "id": "app.appType", "order": 30, "visible": true },
                    { "id": "core.kind", "order": 40, "visible": false }
                  """
            : recordClassId is "module.generic" or "module.core.chat" or "module.core.lockScreen"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "module.recordClassId", "order": 20, "visible": false },
                    { "id": "module.sortOrder", "order": 30, "visible": true },
                    { "id": "module.appearanceMode", "order": 40, "visible": true },
                    { "id": "core.notes", "order": 50, "visible": true },
                    { "id": "module.metadata", "order": 60, "visible": false }
                  """
            : recordClassId == "module_instance"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "moduleInstance.module", "order": 20, "visible": true },
                    { "id": "moduleInstance.durationFrames", "order": 30, "visible": true },
                    { "id": "moduleInstance.transition", "order": 40, "visible": true },
                    { "id": "moduleInstance.sortOrder", "order": 50, "visible": false },
                    { "id": "core.notes", "order": 60, "visible": true }
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
            {{WallpaperCardJson("actor", 30)}}
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
              "order": 40,
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
            {{WallpaperCardJson("app", 20)}}
            ,
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
            {{AppNotesCardJson(40)}}
            """
            : recordClassId == "app.system"
            ? $"{Environment.NewLine},{Environment.NewLine}{AppNotesCardJson(20)}"
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
              "groupLayout": "verticalCards",
              "groups": [
                {
                  "id": "neutralTint",
                  "label": "Neutral tint",
                  "icon": "{{EditorIcons.SemanticAsset("Neutral tint")}}",
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
                  "icon": "{{EditorIcons.SemanticAsset("App colors")}}",
                  "pairLayout": "sharedHeader",
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
                  "icon": "{{EditorIcons.SemanticAsset("Content colors")}}",
                  "pairLayout": "sharedHeader",
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
                  "icon": "{{EditorIcons.SemanticAsset("Action and input colors")}}",
                  "pairLayout": "sharedHeader",
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
                  "icon": "{{EditorIcons.SemanticAsset("Navigation and feedback colors")}}",
                  "pairLayout": "sharedHeader",
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
                  "icon": "{{EditorIcons.SemanticAsset("Border colors")}}",
                  "pairLayout": "sharedHeader",
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
                  "icon": "{{EditorIcons.TextInput}}",
                  "order": 70,
                  "visible": true,
                  "fields": [
                    { "id": "theme.cursor.color", "order": 10, "visible": true },
                    { "id": "theme.cursor.width", "order": 20, "visible": true },
                    { "id": "theme.cursor.blinkDurationMs", "order": 30, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "keyboard",
              "label": "Keyboard",
              "subtitle": "Keyboard dimensions and color tokens",
              "icon": "{{EditorIcons.Keyboard}}",
              "order": 35,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "keyboardTokens",
                  "label": "Keyboard",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "theme.keyboard.height", "order": 10, "visible": true },
                    { "id": "theme.keyboard.keyGap", "order": 20, "visible": true },
                    { "id": "theme.keyboard.rowGap", "order": 30, "visible": true },
                    { "id": "theme.keyboard.background", "order": 40, "visible": true },
                    { "id": "theme.keyboard.keyBackground", "order": 50, "visible": true },
                    { "id": "theme.keyboard.specialKeyBackground", "order": 60, "visible": true },
                    { "id": "theme.keyboard.pressedKeyBackground", "order": 70, "visible": true },
                    { "id": "theme.keyboard.keyBorder", "order": 80, "visible": true },
                    { "id": "theme.keyboard.text", "order": 90, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "motion",
              "label": "Motion",
              "subtitle": "Theme transition timing and easing",
              "icon": "{{EditorIcons.Animation}}",
              "order": 40,
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
                    { "id": "theme.motion.scale", "order": 40, "visible": true },
                    { "id": "theme.motion.buttonPushedDurationMs", "order": 50, "visible": true }
                  ]
                },
                {
                  "id": "naturalPace",
                  "label": "Natural pace",
                  "order": 20,
                  "visible": true,
                  "fields": [
                    { "id": "theme.motion.naturalPace.verySlow", "order": 10, "visible": true },
                    { "id": "theme.motion.naturalPace.slow", "order": 20, "visible": true },
                    { "id": "theme.motion.naturalPace.normal", "order": 30, "visible": true },
                    { "id": "theme.motion.naturalPace.fast", "order": 40, "visible": true },
                    { "id": "theme.motion.naturalPace.veryFast", "order": 50, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "icons",
              "label": "Icons",
              "subtitle": "Shared icon colors and glyph sizes",
              "icon": "{{EditorIcons.Icon}}",
              "order": 38,
              "visible": true,
              "defaultOpen": false,
              "groupLayout": "verticalCards",
              "groups": [
                {
                  "id": "iconColors",
                  "label": "Icon colors",
                  "icon": "{{EditorIcons.SemanticAsset("Icon colors")}}",
                  "pairLayout": "sharedHeader",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "theme.icons.primary", "order": 10, "visible": true },
                    { "id": "theme.icons.secondary", "order": 20, "visible": true },
                    { "id": "theme.icons.alternate", "order": 30, "visible": true },
                    { "id": "theme.icons.accent", "order": 40, "visible": true }
                  ]
                },
                {
                  "id": "iconSizes",
                  "label": "Icon sizes",
                  "icon": "{{EditorIcons.SemanticAsset("Icon sizes")}}",
                  "order": 20,
                  "visible": true,
                  "fields": [
                    { "id": "theme.iconSizes.xs", "order": 10, "visible": true },
                    { "id": "theme.iconSizes.s", "order": 20, "visible": true },
                    { "id": "theme.iconSizes.m", "order": 30, "visible": true },
                    { "id": "theme.iconSizes.l", "order": 40, "visible": true },
                    { "id": "theme.iconSizes.xl", "order": 50, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "typography",
              "label": "Typography",
              "subtitle": "Default text, system-component and emoji font tokens",
              "icon": "{{EditorIcons.Typography}}",
              "order": 40,
              "visible": true,
              "defaultOpen": false,
              "groupLayout": "verticalCards",
              "groups": [
                {
                  "id": "fontFamilies",
                  "label": "Font families",
                  "icon": "{{EditorIcons.SemanticAsset("Font families")}}",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "theme.typography.fontFamilyId", "order": 10, "visible": true },
                    { "id": "theme.typography.systemFontFamilyId", "order": 20, "visible": true },
                    { "id": "theme.typography.emojiFontFamilyId", "order": 30, "visible": true }
                  ]
                },
                {
                  "id": "typographySizes",
                  "label": "Text sizes",
                  "icon": "{{EditorIcons.SemanticAsset("Text sizes")}}",
                  "order": 20,
                  "visible": true,
                  "fields": [
                    { "id": "theme.typography.sizes.xs", "order": 10, "visible": true },
                    { "id": "theme.typography.sizes.s", "order": 20, "visible": true },
                    { "id": "theme.typography.sizes.m", "order": 30, "visible": true },
                    { "id": "theme.typography.sizes.l", "order": 40, "visible": true },
                    { "id": "theme.typography.sizes.xl", "order": 50, "visible": true }
                  ]
                },
                {
                  "id": "typographyStyle",
                  "label": "Style and line heights",
                  "icon": "{{EditorIcons.SemanticAsset("Style and line heights")}}",
                  "order": 30,
                  "visible": true,
                  "fields": [
                    { "id": "theme.typography.weight", "order": 10, "visible": true },
                    { "id": "theme.typography.style", "order": 20, "visible": true },
                    { "id": "theme.typography.lineHeights.tight", "order": 30, "visible": true },
                    { "id": "theme.typography.lineHeights.compact", "order": 40, "visible": true },
                    { "id": "theme.typography.lineHeights.normal", "order": 50, "visible": true },
                    { "id": "theme.typography.lineHeights.relaxed", "order": 60, "visible": true },
                    { "id": "theme.typography.lineHeights.loose", "order": 70, "visible": true }
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
                    { "id": "theme.radii.xs", "order": 20, "visible": true },
                    { "id": "theme.radii.s", "order": 30, "visible": true },
                    { "id": "theme.radii.m", "order": 40, "visible": true },
                    { "id": "theme.radii.l", "order": 50, "visible": true },
                    { "id": "theme.radii.xl", "order": 60, "visible": true },
                    { "id": "theme.radii.xxl", "order": 70, "visible": true },
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
        var moduleCards = recordClassId == "module.core.chat"
            ? $$"""
            ,
            {
              "id": "layout",
              "label": "Layout",
              "subtitle": "Conversation frame and spacing",
              "icon": "{{EditorIcons.Layout}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "layout",
                  "label": "Layout",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "module.conversation.useAppWallpaper", "order": 10, "visible": true },
                    { "id": "module.conversation.screenGutter", "order": 20, "visible": true },
                    { "id": "module.conversation.messageGap", "order": 30, "visible": true },
                    { "id": "module.conversation.messageViewportMotion", "order": 40, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "header",
              "label": "Header",
              "subtitle": "Conversation header composition",
              "icon": "{{EditorIcons.Header}}",
              "order": 25,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "header",
                  "label": "Header",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "module.conversation.showHeader", "order": 10, "visible": true },
                    { "id": "module.conversation.headerHeight", "order": 20, "visible": true },
                    { "id": "module.conversation.headerAvatarVariant", "order": 30, "visible": true },
                    { "id": "module.conversation.headerAvatarAlignment", "order": 40, "visible": true },
                    { "id": "module.conversation.headerLeftIconRow.editor", "order": 50, "visible": true },
                    { "id": "module.conversation.headerRightIconRow.editor", "order": 60, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "status-bar",
              "label": "Status Bar",
              "subtitle": "System status component",
              "icon": "{{EditorIcons.Status}}",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "status-bar",
                  "label": "Status Bar",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "module.conversation.showStatusBar", "order": 10, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "navigation-bar",
              "label": "Navigation Bar",
              "subtitle": "System navigation component",
              "icon": "{{EditorIcons.Navigation}}",
              "order": 40,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "navigation-bar",
                  "label": "Navigation Bar",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "module.conversation.showNavigationBar", "order": 30, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "keyboard",
              "label": "Keyboard",
              "subtitle": "Keyboard component",
              "icon": "{{EditorIcons.Keyboard}}",
              "order": 50,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "keyboard",
                  "label": "Keyboard",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "module.conversation.showKeyboard", "order": 50, "visible": true },
                    { "id": "module.conversation.keyboardVariant", "order": 60, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "text-input",
              "label": "Text Input Bar",
              "subtitle": "Input bar component",
              "icon": "{{EditorIcons.TextInput}}",
              "order": 60,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "text-input",
                  "label": "Text Input Bar",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "module.conversation.showTextInputBar", "order": 70, "visible": true },
                    { "id": "module.conversation.textInputBarVariant", "order": 80, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "bubble",
              "label": "Bubble",
              "subtitle": "Message bubble component",
              "icon": "{{EditorIcons.Bubble}}",
              "order": 70,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "bubble",
                  "label": "Bubble",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "module.conversation.bubbleVariant", "order": 10, "visible": true },
                    { "id": "module.conversation.bubbleMaxWidth", "order": 20, "visible": true }
                  ]
                }
              ]
            }
            """
            : recordClassId == "module.core.lockScreen"
            ? $$"""
            ,
            {
              "id": "content-stack",
              "label": "Content Stack",
              "subtitle": "Runtime-owned Lock Screen content",
              "icon": "{{EditorIcons.Layout}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "content-stack", "label": "Content Stack", "order": 10, "visible": true, "fields": [
                  { "id": "module.lockScreen.stackVariant", "order": 10, "visible": true },
                  { "id": "module.lockScreen.stackInputs", "order": 20, "visible": true },
                  { "id": "module.lockScreen.stackItems", "order": 30, "visible": true }
                ] }
              ]
            },
            {
              "id": "status-bar",
              "label": "Status Bar",
              "subtitle": "Lock Screen system variant",
              "icon": "{{EditorIcons.Status}}",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "status-bar", "label": "Status Bar", "order": 10, "visible": true, "fields": [
                  { "id": "module.lockScreen.statusBarVariant", "order": 10, "visible": true }
                ] }
              ]
            },
            {
              "id": "navigation-bar",
              "label": "Navigation Bar",
              "subtitle": "Lock Screen system variant",
              "icon": "{{EditorIcons.Navigation}}",
              "order": 40,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "navigation-bar", "label": "Navigation Bar", "order": 10, "visible": true, "fields": [
                  { "id": "module.lockScreen.navigationBarVariant", "order": 10, "visible": true }
                ] }
              ]
            }
            """
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
              "defaultOpen": false,
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
            }{{appCards}}{{actorCards}}{{themeCards}}{{moduleCards}}{{componentCards}}
          ]
        }
        """;
    }
}
