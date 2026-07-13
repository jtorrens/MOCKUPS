using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void EnsureLockScreenModule(SqliteConnection connection)
    {
        foreach (var project in QueryProjectRows(connection))
        {
            var appId = EnsureSystemApplication(connection, project.Id);

            EnsureLockScreenSystemVariants(connection, project.Id);
            var moduleId = ScalarString(connection,
                "SELECT id FROM modules WHERE record_class_id = 'module.core.lockScreen' AND app_id IN (SELECT id FROM apps WHERE project_id = $projectId)",
                ("$projectId", project.Id));
            if (!string.IsNullOrWhiteSpace(moduleId))
            {
                Execute(connection, "UPDATE modules SET app_id = $appId WHERE id = $moduleId", ("$appId", appId), ("$moduleId", moduleId));
                Execute(connection, "UPDATE module_instances SET app_id = $appId WHERE module_id = $moduleId", ("$appId", appId), ("$moduleId", moduleId));
                Execute(connection, "UPDATE modules SET notes = $notes WHERE id = $moduleId",
                    ("$notes", "Initial Lock Screen module: inherited Actor wallpaper and dedicated system component variants."),
                    ("$moduleId", moduleId));
                RemoveLockScreenWallpaperSetting(connection, moduleId);
                NormalizeLockScreenDesignPreview(connection, moduleId);
                continue;
            }

            Execute(connection,
                """
                INSERT INTO modules (id, app_id, record_class_id, name, notes, sort_order, config_json, design_preview_json, metadata_json)
                VALUES ($id, $appId, 'module.core.lockScreen', 'Lock Screen', $notes, $sortOrder, $configJson, $previewJson, '{}')
                """,
                ("$id", $"module_{project.Id}_lock_screen"),
                ("$appId", appId),
                ("$notes", "Initial Lock Screen module: inherited Actor wallpaper and dedicated system component variants."),
                ("$sortOrder", NextSortOrder(connection, "modules", "app_id", appId)),
                ("$configJson", DefaultLockScreenConfigJson(project.Id)),
                ("$previewJson", DefaultLockScreenDesignPreviewJson()));
        }

        Execute(connection,
            "INSERT OR REPLACE INTO editor_layouts (record_class_id, layout_json) VALUES ('module.core.lockScreen', $layoutJson)",
            ("$layoutJson", MinimalEditorLayoutJson("module.core.lockScreen")));
    }

    private static string EnsureSystemApplication(SqliteConnection connection, string projectId)
    {
        var appId = $"app_{projectId}_system";
        if (ScalarLong(connection, "SELECT COUNT(*) FROM apps WHERE id = $id", ("$id", appId)) == 0)
        {
            Execute(connection,
                """
                INSERT INTO apps (id, project_id, record_class_id, name, bundle_key, app_type, notes, sort_order, config_json, metadata_json)
                VALUES ($id, $projectId, 'app.generic', 'System', 'system', 'system', $notes, $sortOrder, $configJson, '{}')
                """,
                ("$id", appId),
                ("$projectId", projectId),
                ("$notes", "Dedicated system app for device-level modules."),
                ("$sortOrder", NextSortOrder(connection, "apps", "project_id", projectId)),
                ("$configJson", SystemAppConfigJson()));
        }
        return appId;
    }

    private static void EnsureLockScreenSystemVariants(SqliteConnection connection, string projectId)
    {
        EnsureLockScreenSystemVariant(connection, projectId, "status_bar", "lock_screen", "Lock Screen", (config) =>
        {
            config["backgroundAlpha"] = 0;
        });
        EnsureLockScreenSystemVariant(connection, projectId, "navigation_bar", "lock_screen", "Lock Screen", (config) =>
        {
            config["type"] = "gestureBar";
            config["backgroundAlpha"] = 0;
            if (config["gesture"] is JsonObject gesture) gesture["width"] = 134;
        });
        EnsureLockScreenSystemVariant(connection, projectId, "navigation_bar", "none", "None", (config) =>
        {
            config["type"] = "buttons";
            config["backgroundAlpha"] = 0;
            config["items"] = new JsonArray();
        });
    }

    private static void EnsureLockScreenSystemVariant(
        SqliteConnection connection,
        string projectId,
        string componentType,
        string presetId,
        string presetName,
        Action<JsonObject> apply)
    {
        var componentId = ScalarString(connection,
            "SELECT id FROM component_classes WHERE project_id = $projectId AND component_type = $componentType",
            ("$projectId", projectId), ("$componentType", componentType))
            ?? throw new InvalidOperationException($"Project '{projectId}' has no {componentType} component class.");
        var settings = GetComponentClassSettings(connection, componentId);
        var metadata = ParseJsonObject(settings.MetadataJson);
        var presets = EnsurePresetArray(metadata);
        if (FindPreset(presets, presetId) is not null) return;
        var source = FindPreset(presets, DefaultComponentPresetId)
            ?? throw new InvalidOperationException($"Component class '{componentId}' has no Default variant.");
        var config = (source["config"] as JsonObject)?.DeepClone() as JsonObject
            ?? throw new InvalidOperationException($"Component class '{componentId}' has an invalid Default variant.");
        apply(config);
        presets.Add(new JsonObject
        {
            ["id"] = presetId,
            ["name"] = presetName,
            ["protected"] = true,
            ["locked"] = true,
            ["config"] = config,
        });
        Execute(connection,
            "UPDATE component_classes SET metadata_json = $metadataJson WHERE id = $id",
            ("$id", componentId), ("$metadataJson", metadata.ToJsonString()));
    }

    private static string DefaultLockScreenConfigJson(string projectId) => new JsonObject
    {
        ["appearanceMode"] = "inherit",
        ["lockScreen"] = new JsonObject
        {
            ["statusBarVariant"] = SeededComponentPresetReference(projectId, "status_bar").Replace("::preset::default", "::preset::lock_screen", StringComparison.Ordinal),
            ["navigationBarVariant"] = SeededComponentPresetReference(projectId, "navigation_bar").Replace("::preset::default", "::preset::lock_screen", StringComparison.Ordinal),
        },
    }.ToJsonString();

    private static void RemoveLockScreenWallpaperSetting(SqliteConnection connection, string moduleId)
    {
        var original = ScalarString(connection, "SELECT config_json FROM modules WHERE id = $id", ("$id", moduleId)) ?? "{}";
        var config = ParseJsonObject(original);
        if (config["lockScreen"] is not JsonObject lockScreen || !lockScreen.Remove("useAppWallpaper")) return;
        Execute(connection, "UPDATE modules SET config_json = $configJson WHERE id = $id",
            ("$id", moduleId), ("$configJson", config.ToJsonString()));
    }

    private static string DefaultLockScreenDesignPreviewJson() => new JsonObject
    {
        ["inputs"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "actor",
                ["label"] = "Actor",
                ["jsonKey"] = "actorId",
                ["kind"] = "recordReference",
                ["defaultValue"] = "actor_alex",
                ["tableId"] = "actors",
                ["resolvedJsonKey"] = "actor",
            },
        },
        ["actorId"] = "actor_alex",
    }.ToJsonString();

    private static void NormalizeLockScreenDesignPreview(SqliteConnection connection, string moduleId)
    {
        var original = ScalarString(connection, "SELECT design_preview_json FROM modules WHERE id = $id", ("$id", moduleId)) ?? "{}";
        var preview = ParseJsonObject(original);
        if (preview["inputs"] is JsonArray inputs)
        {
            for (var index = inputs.Count - 1; index >= 0; index--)
            {
                if (inputs[index]?["id"]?.GetValue<string>() == "actor") inputs.RemoveAt(index);
            }
        }
        else
        {
            inputs = new JsonArray();
            preview["inputs"] = inputs;
        }
        inputs.Add((JsonNode.Parse(DefaultLockScreenDesignPreviewJson())?["inputs"]?[0])?.DeepClone());
        preview.Remove("testValues");
        if (string.IsNullOrWhiteSpace(preview["actorId"]?.GetValue<string>())) preview["actorId"] = "actor_alex";
        Execute(connection, "UPDATE modules SET design_preview_json = $previewJson WHERE id = $id",
            ("$id", moduleId), ("$previewJson", preview.ToJsonString()));
    }

    private static string SystemAppConfigJson() => "{}";
}
