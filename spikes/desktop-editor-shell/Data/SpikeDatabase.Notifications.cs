using Microsoft.Data.Sqlite;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void EnsureNotificationComponentClasses(SqliteConnection connection)
    {
        EnsureNotificationOwnedClass(connection, "notification", NormalizeNotificationConfig, NormalizeNotificationPreview);
        EnsureNotificationOwnedClass(connection, "notifications", NormalizeNotificationsConfig, NormalizeNotificationsPreview);
    }

    private static void EnsureNotificationOwnedClass(
        SqliteConnection connection,
        string componentType,
        System.Action<JsonObject, string> normalizeConfig,
        System.Action<JsonObject> normalizePreview)
    {
        var seed = ComponentSeedRows.Single((candidate) => candidate.ComponentType == componentType);
        foreach (var projectId in QueryProjectRows(connection).Select((project) => project.Id))
        {
            var existing = QueryComponentClassRows(connection).FirstOrDefault((candidate) =>
                candidate.ProjectId == projectId && candidate.ComponentType == componentType);
            if (existing is null)
            {
                var config = ParseJsonObject(seed.ConfigJson);
                var metadata = ParseJsonObject(seed.MetadataJson);
                var preview = ParseJsonObject(seed.DesignPreviewJson);
                normalizeConfig(config, projectId);
                normalizePreview(preview);
                SetDefaultComponentPresetConfig(metadata, config);
                Execute(connection,
                    """
                    INSERT INTO component_classes (id, project_id, component_type, record_class_id, name, notes, config_json, design_preview_json, metadata_json)
                    VALUES ($id, $projectId, $componentType, $recordClassId, $name, $notes, $configJson, $designPreviewJson, $metadataJson)
                    """,
                    ("$id", $"component_{projectId}_{componentType}"),
                    ("$projectId", projectId),
                    ("$componentType", componentType),
                    ("$recordClassId", seed.RecordClassId),
                    ("$name", seed.Name),
                    ("$notes", ComponentTypeLabel(componentType)),
                    ("$configJson", config.ToJsonString()),
                    ("$designPreviewJson", preview.ToJsonString()),
                    ("$metadataJson", metadata.ToJsonString()));
                continue;
            }

            var currentConfig = ParseJsonObject(existing.ConfigJson);
            var currentMetadata = ParseJsonObject(existing.MetadataJson);
            var currentPreview = ParseJsonObject(existing.DesignPreviewJson);
            normalizeConfig(currentConfig, projectId);
            normalizePreview(currentPreview);
            if (currentMetadata["presets"] is JsonArray presets)
            {
                foreach (var preset in presets.OfType<JsonObject>())
                {
                    if (preset["config"] is JsonObject presetConfig) normalizeConfig(presetConfig, projectId);
                }
            }
            Execute(connection,
                "UPDATE component_classes SET config_json = $configJson, design_preview_json = $designPreviewJson, metadata_json = $metadataJson WHERE id = $id",
                ("$id", existing.Id),
                ("$configJson", currentConfig.ToJsonString()),
                ("$designPreviewJson", currentPreview.ToJsonString()),
                ("$metadataJson", currentMetadata.ToJsonString()));
        }

        var layoutJson = componentType == "notification"
            ? NotificationEditorLayoutJson()
            : NotificationsEditorLayoutJson();
        Execute(connection,
            "INSERT OR REPLACE INTO editor_layouts (record_class_id, layout_json) VALUES ($recordClassId, $layoutJson)",
            ("$recordClassId", $"component.{componentType}"),
            ("$layoutJson", layoutJson));
    }

    private static void NormalizeNotificationConfig(JsonObject config, string projectId)
    {
        var notification = config["notification"] as JsonObject
            ?? throw new System.InvalidOperationException("Missing Notification config.");
        notification["avatarPosition"] ??= "start";
        notification["gapToken"] ??= "theme.spacing.m";
        QualifyOwnedSlot(notification, "avatarSlot", projectId, "avatar");
        QualifyOwnedSlot(notification, "labelSlot", projectId, "label");
    }

    private static void NormalizeNotificationPreview(JsonObject preview)
    {
        preview["actorId"] ??= "actor_alex";
        preview["sampleText"] ??= "New notification";
        preview["sampleSubtext"] ??= "Now";
    }

    private static void NormalizeNotificationsConfig(JsonObject config, string projectId)
    {
        var notifications = config["notifications"] as JsonObject
            ?? throw new System.InvalidOperationException("Missing Notifications config.");
        QualifyOwnedSlot(notifications, "collectionStackSlot", projectId, "collectionStack");
    }

    private static void NormalizeNotificationsPreview(JsonObject preview)
    {
        preview["distributionMode"] = "stacked";
        preview["sizingMode"] = "content";
        preview["startGapToken"] ??= "theme.spacing.none";
        preview["endGapToken"] ??= "theme.spacing.none";
        preview["stackDirection"] ??= "down";
        preview["stackOffsetToken"] ??= "theme.spacing.m";
        preview["items"] ??= new JsonArray();
    }

    private static string NotificationEditorLayoutJson() =>
        """
        { "cards": [
          { "id": "general", "label": "General", "subtitle": "Notification composition", "icon": "component", "order": 10, "visible": true, "defaultOpen": false, "groups": [
            { "id": "layout", "label": "Layout", "order": 10, "visible": true, "fields": [
              { "id": "component.notification.avatarPosition", "order": 10, "visible": true },
              { "id": "component.notification.gapToken", "order": 20, "visible": true }
            ] },
            { "id": "components", "label": "Components", "order": 20, "visible": true, "presentation": "flatStack", "fields": [
              { "id": "component.notification.avatar.editor", "order": 10, "visible": true },
              { "id": "component.notification.label.editor", "order": 20, "visible": true }
            ] }
          ] }
        ] }
        """;

    private static string NotificationsEditorLayoutJson() =>
        """
        { "cards": [
          { "id": "composition", "label": "Composition", "subtitle": "Runtime notification collection", "icon": "component", "order": 10, "visible": true, "defaultOpen": false, "groups": [
            { "id": "stack", "label": "Collection Stack", "order": 10, "visible": true, "fields": [
              { "id": "component.notifications.collectionStack.editor", "order": 10, "visible": true }
            ] }
          ] }
        ] }
        """;
}
