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
        EnsureNotificationOwnedClass(connection, "badge", NormalizeBadgeConfig, NormalizeBadgePreview);
        EnsureBadgeParentSlots(connection);
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

        var layoutJson = componentType switch
        {
            "notification" => NotificationEditorLayoutJson(),
            "notifications" => NotificationsEditorLayoutJson(),
            "badge" => BadgeEditorLayoutJson(),
            _ => throw new System.InvalidOperationException($"Unsupported notification-owned component {componentType}."),
        };
        Execute(connection,
            "INSERT OR REPLACE INTO editor_layouts (record_class_id, layout_json) VALUES ($recordClassId, $layoutJson)",
            ("$recordClassId", $"component.{componentType}"),
            ("$layoutJson", layoutJson));
    }

    private static void NormalizeNotificationConfig(JsonObject config, string projectId)
    {
        var notification = config["notification"] as JsonObject
            ?? throw new System.InvalidOperationException("Missing Notification config.");
        notification["dimensionMode"] ??= "content";
        notification["size"] ??= "320|88";
        notification["padding"] ??= "theme.spacing.m|theme.spacing.m";
        notification.Remove("avatarPosition");
        notification["gapToken"] ??= "theme.spacing.m";
        notification["avatarPlacement"] ??= JsonNode.Parse("""{"mode":"center","alignX":0.25,"alignY":0.5,"offsetX":0,"offsetY":0}""");
        notification["labelPlacement"] ??= JsonNode.Parse("""{"mode":"center","alignX":0.75,"alignY":0.5,"offsetX":0,"offsetY":0}""");
        notification["surfaceSlot"] ??= ComponentSurfaceSlot(DefaultComponentPresetId);
        if (notification["summaryLabelSlot"] is null)
        {
            notification["summaryLabelSlot"] = notification["labelSlot"]?.DeepClone() ?? ComponentSurfaceSlot(DefaultComponentPresetId);
        }
        notification["detailLabelSlot"] ??= notification["summaryLabelSlot"]?.DeepClone() ?? ComponentSurfaceSlot(DefaultComponentPresetId);
        notification.Remove("labelSlot");
        var avatarInputs = notification["avatarInputs"] as JsonObject;
        if (avatarInputs is null)
        {
            avatarInputs = new JsonObject();
            notification["avatarInputs"] = avatarInputs;
        }
        MergeMissingValues(avatarInputs, DefaultAvatarBadgeInputs());
        QualifyOwnedSlot(notification, "surfaceSlot", projectId, "surface");
        QualifyOwnedSlot(notification, "avatarSlot", projectId, "avatar");
        QualifyOwnedSlot(notification, "summaryLabelSlot", projectId, "label");
        QualifyOwnedSlot(notification, "detailLabelSlot", projectId, "label");
    }

    private static void NormalizeNotificationPreview(JsonObject preview)
    {
        preview.Remove("availableWidth");
        preview["maxWidth"] ??= 90;
        preview["actorId"] ??= "actor_alex";
        preview["displayMode"] ??= "summary";
        preview["summaryText"] ??= preview["sampleText"]?.DeepClone() ?? "New notification";
        preview["summarySubtext"] ??= preview["sampleSubtext"]?.DeepClone() ?? "Now";
        preview["detailText"] ??= preview["summaryText"]?.DeepClone() ?? "New notification";
        preview["detailSubtext"] ??= "Notification detail";
        preview.Remove("sampleText");
        preview.Remove("sampleSubtext");
    }

    private static void NormalizeNotificationsConfig(JsonObject config, string projectId)
    {
        var notifications = config["notifications"] as JsonObject
            ?? throw new System.InvalidOperationException("Missing Notifications config.");
        notifications["closedItemLimit"] ??= 3;
        notifications["distributionMotion"] ??= JsonNode.Parse(Mockups.DesktopEditorShell.EditorShell.MotionVariantValue.Default.ToJsonString());
        QualifyOwnedSlot(notifications, "collectionStackSlot", projectId, "collectionStack");
        notifications["badgeSlot"] ??= new JsonObject
        {
            ["presetId"] = DefaultComponentPresetId,
            ["overrides"] = new JsonObject(),
        };
        if (notifications["badgeSlot"] is JsonObject badgeSlot) NormalizeBadgeSlotOverrides(badgeSlot);
        var badgeInputs = notifications["badgeInputs"] as JsonObject;
        if (badgeInputs is null)
        {
            badgeInputs = new JsonObject();
            notifications["badgeInputs"] = badgeInputs;
        }
        MergeMissingValues(badgeInputs, DefaultNotificationsBadgeInputs());
        QualifyOwnedSlot(notifications, "badgeSlot", projectId, "badge");
    }

    private static void NormalizeNotificationsPreview(JsonObject preview)
    {
        preview["distributionMode"] = "stacked";
        preview["sizingMode"] = "content";
        preview["startGapToken"] ??= "theme.spacing.none";
        preview["endGapToken"] ??= "theme.spacing.none";
        preview["stackDirection"] ??= "down";
        preview["stackOffsetToken"] ??= "theme.spacing.m";
        preview["itemSizingMode"] ??= "largest";
        preview["scaleRatio"] ??= 1;
        preview["opacityRatio"] ??= 1;
        preview["items"] ??= new JsonArray();
        if (preview["items"] is JsonArray items)
        {
            NormalizeComponentCollectionItems(items);
            foreach (var item in items.OfType<JsonObject>())
            {
                var inputs = item["inputs"] as JsonObject ?? new JsonObject();
                item["inputs"] = inputs;
                inputs.Remove("availableWidth");
                inputs["maxWidth"] ??= 90;
                inputs["displayMode"] ??= "summary";
                inputs["summaryText"] ??= inputs["sampleText"]?.DeepClone() ?? "New notification";
                inputs["summarySubtext"] ??= inputs["sampleSubtext"]?.DeepClone() ?? "Now";
                inputs["detailText"] ??= inputs["summaryText"]?.DeepClone() ?? "New notification";
                inputs["detailSubtext"] ??= "Notification detail";
                inputs.Remove("sampleText");
                inputs.Remove("sampleSubtext");
            }
        }
        preview["showBadge"] ??= true;
    }

    private static void NormalizeBadgeConfig(JsonObject config, string projectId)
    {
        var badge = config["badge"] as JsonObject
            ?? throw new System.InvalidOperationException("Missing Badge config.");
        badge.Remove("contentMode");
        badge.Remove("iconSizeToken");
        badge.Remove("backgroundPaletteColor");
        badge.Remove("contentPaletteColor");
    }

    private static void NormalizeBadgePreview(JsonObject preview)
    {
        preview["iconToken"] ??= "system_check";
        preview["text"] ??= "3";
        preview["contentMode"] ??= "icon";
        preview["size"] ??= 20;
        preview["backgroundPaletteColor"] ??= "blue";
        preview["contentPaletteColor"] ??= "gray_100";
    }

    private static void EnsureBadgeParentSlots(SqliteConnection connection)
    {
        foreach (var row in QueryComponentClassRows(connection).Where((candidate) => candidate.ComponentType is "avatar" or "button"))
        {
            var config = ParseJsonObject(row.ConfigJson);
            var metadata = ParseJsonObject(row.MetadataJson);
            var preview = ParseJsonObject(row.DesignPreviewJson);
            NormalizeBadgeParentConfig(config, row.ProjectId, row.ComponentType);
            if (metadata["presets"] is JsonArray presets)
            {
                foreach (var preset in presets.OfType<JsonObject>())
                {
                    if (preset["config"] is JsonObject presetConfig)
                    {
                        NormalizeBadgeParentConfig(presetConfig, row.ProjectId, row.ComponentType);
                    }
                }
            }
            preview["showBadge"] ??= false;
            preview["badgeIconToken"] ??= "system_check";
            preview["badgeText"] ??= "1";
            preview["badgeContentMode"] ??= "icon";
            preview["badgeSize"] ??= 20;
            preview["badgeBackgroundPaletteColor"] ??= "blue";
            preview["badgeContentPaletteColor"] ??= "gray_100";
            Execute(connection,
                "UPDATE component_classes SET config_json = $configJson, design_preview_json = $previewJson, metadata_json = $metadataJson WHERE id = $id",
                ("$id", row.Id),
                ("$configJson", config.ToJsonString()),
                ("$previewJson", preview.ToJsonString()),
                ("$metadataJson", metadata.ToJsonString()));
        }
    }

    private static void NormalizeBadgeParentConfig(JsonObject config, string projectId, string componentType)
    {
        var parent = config[componentType] as JsonObject
            ?? throw new System.InvalidOperationException($"Missing {componentType} config while adding Badge slot.");
        parent["badgeSlot"] ??= ComponentSurfaceSlot(DefaultComponentPresetId);
        if (componentType == "avatar" && parent["badgeSlot"] is JsonObject avatarBadgeSlot)
        {
            avatarBadgeSlot["placement"] ??= JsonNode.Parse("""{"mode":"center","alignX":1,"alignY":0,"offsetX":0,"offsetY":0}""");
        }
        if (parent["badgeSlot"] is JsonObject badgeSlot) NormalizeBadgeSlotOverrides(badgeSlot);
        QualifyOwnedSlot(parent, "badgeSlot", projectId, "badge");
    }

    private static void NormalizeBadgeSlotOverrides(JsonObject badgeSlot)
    {
        if (badgeSlot["overrides"]?["badge"] is not JsonObject badgeOverride) return;
        badgeOverride.Remove("contentMode");
        badgeOverride.Remove("iconSizeToken");
        badgeOverride.Remove("backgroundPaletteColor");
        badgeOverride.Remove("contentPaletteColor");
    }

    private static JsonObject DefaultAvatarBadgeInputs() => new()
    {
        ["showBadge"] = false,
        ["badgeContentMode"] = "icon",
        ["badgeIconToken"] = "system_check",
        ["badgeText"] = "1",
        ["badgeSize"] = 20,
        ["badgeBackgroundPaletteColor"] = "blue",
        ["badgeContentPaletteColor"] = "gray_100",
    };

    private static JsonObject DefaultNotificationsBadgeInputs() => new()
    {
        ["size"] = 20,
        ["backgroundPaletteColor"] = "blue",
        ["contentPaletteColor"] = "gray_100",
    };

    private static void MergeMissingValues(JsonObject target, JsonObject defaults)
    {
        foreach (var entry in defaults)
        {
            if (target.ContainsKey(entry.Key)) continue;
            target[entry.Key] = entry.Value?.DeepClone();
        }
    }

    private static string NotificationEditorLayoutJson() =>
        """
        { "cards": [
          { "id": "general", "label": "General", "subtitle": "Identity", "icon": "general", "order": 10, "visible": true, "defaultOpen": false, "groups": [
            { "id": "identity", "label": "Identity", "order": 10, "visible": true, "fields": [
              { "id": "core.name", "order": 10, "visible": true },
              { "id": "component.type", "order": 20, "visible": true },
              { "id": "core.notes", "order": 30, "visible": true }
            ] }
          ] },
          { "id": "layout", "label": "Layout", "subtitle": "Notification frame and spacing", "icon": "layout", "order": 20, "visible": true, "defaultOpen": false, "groups": [
            { "id": "layout", "label": "Layout", "order": 10, "visible": true, "fields": [
              { "id": "component.notification.dimensionMode", "order": 10, "visible": true },
              { "id": "component.notification.size", "order": 20, "visible": true },
              { "id": "component.notification.padding", "order": 30, "visible": true },
              { "id": "component.notification.gapToken", "order": 40, "visible": true },
              { "id": "component.notification.surface.editor", "order": 50, "visible": true }
            ] }
          ] },
          { "id": "avatar", "label": "Avatar", "subtitle": "Avatar variant and placement", "icon": "avatar", "order": 30, "visible": true, "defaultOpen": false, "groups": [
            { "id": "avatar", "label": "Avatar", "order": 10, "visible": true, "fields": [
              { "id": "component.notification.avatar.editor", "order": 10, "visible": true },
              { "id": "component.notification.avatarPlacement", "order": 20, "visible": true },
              { "id": "component.notification.avatar.inputs", "order": 30, "visible": true }
            ] }
          ] },
          { "id": "summaryLabel", "label": "Summary Label", "subtitle": "Collapsed notification content", "icon": "label", "order": 40, "visible": true, "defaultOpen": false, "groups": [
            { "id": "label", "label": "Summary", "order": 10, "visible": true, "fields": [
              { "id": "component.notification.summaryLabel.editor", "order": 10, "visible": true },
              { "id": "component.notification.labelPlacement", "order": 20, "visible": true }
            ] }
          ] },
          { "id": "detailLabel", "label": "Detail Label", "subtitle": "Expanded notification content", "icon": "label", "order": 50, "visible": true, "defaultOpen": false, "groups": [
            { "id": "label", "label": "Detail", "order": 10, "visible": true, "fields": [
              { "id": "component.notification.detailLabel.editor", "order": 10, "visible": true }
            ] }
          ] }
        ] }
        """;

    private static string NotificationsEditorLayoutJson() =>
        """
        { "cards": [
          { "id": "general", "label": "General", "subtitle": "Closed notification presentation", "icon": "general", "order": 10, "visible": true, "defaultOpen": false, "groups": [
            { "id": "general", "label": "General", "order": 10, "visible": true, "fields": [
              { "id": "component.notifications.closedItemLimit", "order": 10, "visible": true }
            ] }
          ] },
          { "id": "composition", "label": "Composition", "subtitle": "Runtime notification collection", "icon": "component", "order": 20, "visible": true, "defaultOpen": false, "groups": [
            { "id": "stack", "label": "Collection Stack", "order": 10, "visible": true, "fields": [
              { "id": "component.notifications.collectionStack.editor", "order": 10, "visible": true },
              { "id": "component.notifications.badge.editor", "order": 20, "visible": true }
            ] }
          ] },
          { "id": "behavior", "label": "Behavior", "subtitle": "Distribution state transition", "icon": "animation", "order": 30, "visible": true, "defaultOpen": false, "groups": [
            { "id": "motion", "label": "Motion", "order": 10, "visible": true, "fields": [
              { "id": "component.notifications.distributionMotion", "order": 10, "visible": true }
            ] }
          ] }
        ] }
        """;

    private static string BadgeEditorLayoutJson() =>
        """
        { "cards": [
          { "id": "layout", "label": "Layout", "subtitle": "Badge spacing and placement", "icon": "layout", "order": 10, "visible": true, "defaultOpen": false, "groups": [
            { "id": "layout", "label": "General", "order": 10, "visible": true, "fields": [
              { "id": "component.badge.paddingToken", "order": 10, "visible": true },
              { "id": "component.badge.placement", "order": 20, "visible": true }
            ] }
          ] },
          { "id": "text", "label": "Text", "subtitle": "Badge text typography", "icon": "label", "order": 20, "visible": true, "defaultOpen": false, "groups": [
            { "id": "text", "label": "General", "order": 10, "visible": true, "fields": [
              { "id": "component.badge.textTypography", "order": 10, "visible": true }
            ] }
          ] }
        ] }
        """;
}
