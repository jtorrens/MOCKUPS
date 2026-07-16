using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
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
                NormalizeLockScreenSystemSlots(connection, moduleId, project.Id);
                NormalizeLockScreenStackSlot(connection, moduleId, project.Id);
                NormalizeLockScreenStackBindings(connection, moduleId);
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
        Execute(connection,
            "INSERT OR REPLACE INTO editor_layouts (record_class_id, layout_json) VALUES ('app.system', $layoutJson)",
            ("$layoutJson", MinimalEditorLayoutJson("app.system")));
    }

    private static string EnsureSystemApplication(SqliteConnection connection, string projectId)
    {
        var appId = $"app_{projectId}_system";
        if (ScalarLong(connection, "SELECT COUNT(*) FROM apps WHERE id = $id", ("$id", appId)) == 0)
        {
            Execute(connection,
                """
                INSERT INTO apps (id, project_id, record_class_id, name, bundle_key, app_type, notes, sort_order, config_json, metadata_json)
                VALUES ($id, $projectId, 'app.system', 'System', 'system', 'system', $notes, $sortOrder, $configJson, '{}')
                """,
                ("$id", appId),
                ("$projectId", projectId),
                ("$notes", "Dedicated system app for device-level modules."),
                ("$sortOrder", NextSortOrder(connection, "apps", "project_id", projectId)),
                ("$configJson", SystemAppConfigJson()));
        }
        else
        {
            Execute(connection,
                "UPDATE apps SET record_class_id = 'app.system' WHERE id = $id",
                ("$id", appId));
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
        NormalizeNonDefaultSystemVariantProtection(connection, projectId, "status_bar");
        NormalizeNonDefaultSystemVariantProtection(connection, projectId, "navigation_bar");
    }

    private static void EnsureLockScreenSystemVariant(
        SqliteConnection connection,
        string projectId,
        string componentType,
        string presetId,
        string presetName,
        Action<JsonObject> apply)
    {
        EnsureSeededComponentVariant(connection, projectId, componentType, presetId, presetName, apply);
    }

    private static void NormalizeNonDefaultSystemVariantProtection(
        SqliteConnection connection,
        string projectId,
        string componentType)
    {
        var componentId = ScalarString(connection,
            "SELECT id FROM component_classes WHERE project_id = $projectId AND component_type = $componentType",
            ("$projectId", projectId), ("$componentType", componentType))
            ?? throw new InvalidOperationException($"Project '{projectId}' has no {componentType} component class.");
        var settings = GetComponentClassSettings(connection, componentId);
        var metadata = ParseJsonObject(settings.MetadataJson);
        const string migrationKey = "nonDefaultVariantProtectionPolicyVersion";
        if (metadata[migrationKey]?.GetValue<int>() == 1) return;
        foreach (var preset in EnsurePresetArray(metadata).OfType<JsonObject>())
        {
            if (string.Equals(preset["id"]?.GetValue<string>(), DefaultComponentPresetId, StringComparison.Ordinal))
            {
                continue;
            }

            preset["protected"] = false;
            preset["locked"] = false;
        }

        metadata[migrationKey] = 1;
        Execute(connection,
            "UPDATE component_classes SET metadata_json = $metadataJson WHERE id = $id",
            ("$id", componentId), ("$metadataJson", metadata.ToJsonString()));
    }

    private static string DefaultLockScreenConfigJson(string projectId) => new JsonObject
    {
        ["appearanceMode"] = "inherit",
        ["lockScreen"] = new JsonObject
        {
            ["statusBarSlot"] = new JsonObject
            {
                ["presetId"] = SeededComponentPresetReference(projectId, "status_bar").Replace("::preset::default", "::preset::lock_screen", StringComparison.Ordinal),
                ["overrides"] = new JsonObject(),
            },
            ["navigationBarSlot"] = new JsonObject
            {
                ["presetId"] = SeededComponentPresetReference(projectId, "navigation_bar").Replace("::preset::default", "::preset::lock_screen", StringComparison.Ordinal),
                ["overrides"] = new JsonObject(),
            },
            ["stackSlot"] = new JsonObject
            {
                ["presetId"] = SeededComponentPresetReference(projectId, "componentStack"),
                ["overrides"] = new JsonObject(),
            },
            ["stackInputs"] = new JsonObject
            {
                ["sizingMode"] = "fill",
                ["startGapToken"] = "theme.spacing.none",
                ["endGapToken"] = "theme.spacing.none",
                ["items"] = new JsonArray(),
                [RuntimeInputForwardingContract.StorageKey] = ComponentStackStateRuntimeForwarding(),
            },
        },
    }.ToJsonString();

    private static void NormalizeLockScreenSystemSlots(
        SqliteConnection connection,
        string moduleId,
        string projectId)
    {
        var original = ScalarString(connection, "SELECT config_json FROM modules WHERE id = $id", ("$id", moduleId)) ?? "{}";
        var config = ParseJsonObject(original);
        var lockScreen = config["lockScreen"] as JsonObject ?? new JsonObject();
        config["lockScreen"] = lockScreen;
        NormalizeSlot(
            lockScreen,
            "statusBarSlot",
            "statusBarVariant",
            SeededComponentPresetReference(projectId, "status_bar").Replace("::preset::default", "::preset::lock_screen", StringComparison.Ordinal));
        NormalizeSlot(
            lockScreen,
            "navigationBarSlot",
            "navigationBarVariant",
            SeededComponentPresetReference(projectId, "navigation_bar").Replace("::preset::default", "::preset::lock_screen", StringComparison.Ordinal));
        var next = config.ToJsonString();
        if (next == original) return;
        Execute(connection, "UPDATE modules SET config_json = $configJson WHERE id = $id",
            ("$id", moduleId), ("$configJson", next));
    }

    private static void NormalizeSlot(JsonObject owner, string slotKey, string retiredReferenceKey, string defaultPresetId)
    {
        if (owner[slotKey] is not JsonObject slot)
        {
            var presetId = owner[retiredReferenceKey]?.GetValue<string>();
            slot = new JsonObject
            {
                ["presetId"] = string.IsNullOrWhiteSpace(presetId) ? defaultPresetId : presetId,
                ["overrides"] = new JsonObject(),
            };
            owner[slotKey] = slot;
        }
        slot["overrides"] ??= new JsonObject();
        owner.Remove(retiredReferenceKey);
    }

    private static void RemoveLockScreenWallpaperSetting(SqliteConnection connection, string moduleId)
    {
        var original = ScalarString(connection, "SELECT config_json FROM modules WHERE id = $id", ("$id", moduleId)) ?? "{}";
        var config = ParseJsonObject(original);
        if (config["lockScreen"] is not JsonObject lockScreen || !lockScreen.Remove("useAppWallpaper")) return;
        Execute(connection, "UPDATE modules SET config_json = $configJson WHERE id = $id",
            ("$id", moduleId), ("$configJson", config.ToJsonString()));
    }

    private static void NormalizeLockScreenStackSlot(
        SqliteConnection connection,
        string moduleId,
        string projectId)
    {
        var original = ScalarString(connection, "SELECT config_json FROM modules WHERE id = $id", ("$id", moduleId)) ?? "{}";
        var config = ParseJsonObject(original);
        var lockScreen = config["lockScreen"] as JsonObject ?? new JsonObject();
        config["lockScreen"] = lockScreen;
        if (lockScreen["stackSlot"] is not JsonObject stackSlot)
        {
            var presetId = lockScreen["stackVariant"]?.GetValue<string>();
            stackSlot = new JsonObject
            {
                ["presetId"] = string.IsNullOrWhiteSpace(presetId)
                    ? SeededComponentPresetReference(projectId, "componentStack")
                    : presetId,
                ["overrides"] = new JsonObject(),
            };
            lockScreen["stackSlot"] = stackSlot;
        }
        stackSlot["overrides"] ??= new JsonObject();
        lockScreen.Remove("stackVariant");
        var next = config.ToJsonString();
        if (next == original) return;
        Execute(connection, "UPDATE modules SET config_json = $configJson WHERE id = $id",
            ("$id", moduleId), ("$configJson", next));
    }

    private static void NormalizeLockScreenStackBindings(SqliteConnection connection, string moduleId)
    {
        var originalConfig = ScalarString(connection, "SELECT config_json FROM modules WHERE id = $id", ("$id", moduleId)) ?? "{}";
        var originalPreview = ScalarString(connection, "SELECT design_preview_json FROM modules WHERE id = $id", ("$id", moduleId)) ?? "{}";
        var config = ParseJsonObject(originalConfig);
        var preview = ParseJsonObject(originalPreview);
        var lockScreen = config["lockScreen"] as JsonObject ?? new JsonObject();
        config["lockScreen"] = lockScreen;
        if (lockScreen["stackInputs"] is not JsonObject stackInputs)
        {
            stackInputs = new JsonObject
            {
                ["sizingMode"] = preview["sizingMode"]?.DeepClone() ?? "fill",
                ["startGapToken"] = preview["startGapToken"]?.DeepClone() ?? "theme.spacing.none",
                ["endGapToken"] = preview["endGapToken"]?.DeepClone() ?? "theme.spacing.none",
                ["items"] = preview["items"]?.DeepClone() ?? new JsonArray(),
            };
            lockScreen["stackInputs"] = stackInputs;
        }
        stackInputs["items"] ??= new JsonArray();
        var next = config.ToJsonString();
        if (next == originalConfig) return;
        Execute(connection, "UPDATE modules SET config_json = $configJson WHERE id = $id",
            ("$id", moduleId), ("$configJson", next));
    }

    private static JsonObject ComponentStackStateRuntimeForwarding() => new()
    {
        ["items"] = new JsonObject
        {
            ["id"] = "forwarded.module.lockScreen.stackStates",
            ["label"] = "Slots",
            ["jsonKey"] = "forwarded_module_lockScreen_stackStates",
            ["kind"] = "collection",
            ["collection"] = new JsonObject
            {
                ["id"] = "stackStates",
                ["label"] = "Slots",
                ["jsonKey"] = "forwarded_module_lockScreen_stackStates",
                ["itemLabel"] = "Slot",
                ["fields"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "runtimeStateId",
                        ["label"] = "State",
                        ["jsonKey"] = "runtimeStateId",
                        ["kind"] = "option",
                        ["valueKind"] = "OptionToken",
                        ["defaultValue"] = "",
                        ["actionOnly"] = true,
                        ["animatable"] = true,
                        ["animationInterpolations"] = new JsonArray("hold"),
                        ["animationTimeline"] = new JsonObject
                        {
                            ["extendsOwnerDuration"] = false,
                        },
                        ["optionsSourceCollectionJsonKey"] = "alternatives",
                        ["optionsSourceValueJsonKey"] = "id",
                        ["optionsSourceLabelJsonKey"] = "presetId",
                    },
                },
                ["itemActions"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "changeState",
                        ["label"] = "State",
                        ["playInputId"] = "runtimeStateTransition",
                        ["targetInputId"] = "runtimeStateId",
                        ["targetMode"] = "option",
                        ["targetFromJsonKey"] = "runtimeStateFromId",
                        ["durationStateCollectionJsonKey"] = "alternatives",
                        ["durationStateIdJsonKey"] = "id",
                        ["durationEnterMotionJsonKey"] = "enterMotion",
                        ["durationExitMotionJsonKey"] = "exitMotion",
                        ["durationAdditionalThemeTokens"] = new JsonArray("theme.motion.reflowDurationMs"),
                        ["timeJsonKey"] = "runtimeStateElapsedMs",
                        ["timeUnit"] = "milliseconds",
                        ["prewarmFrames"] = false,
                        ["completionBehavior"] = "reset",
                    },
                },
                ["itemPresentation"] = new JsonObject
                {
                    ["subtitleFieldIds"] = new JsonArray("runtimeStateId"),
                    ["subtitleMaxCharacters"] = 72,
                    ["fallbackIcon"] = "component",
                },
            },
            ["projection"] = new JsonObject
            {
                ["optionsSourceCollectionJsonKey"] = "alternatives",
                ["stateJsonKey"] = "runtimeStateId",
                ["transitionJsonKey"] = "runtimeStateTransition",
                ["elapsedJsonKey"] = "runtimeStateElapsedMs",
                ["fromJsonKey"] = "runtimeStateFromId",
                ["childCollection"] = new JsonObject
                {
                    ["id"] = "stackStateInputs",
                    ["label"] = "States",
                    ["jsonKey"] = "forwarded_module_lockScreen_stackStateInputs",
                    ["itemLabel"] = "State",
                    ["sourceCollectionJsonKey"] = "alternatives",
                    ["parentItemIdJsonKey"] = "slotId",
                    ["presetJsonKey"] = "presetId",
                    ["runtimeContractJsonKey"] = "inputs",
                    ["sourceRuntimeContractJsonKey"] = "inputs",
                    ["fields"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "slotId", ["label"] = "Slot", ["jsonKey"] = "slotId",
                            ["kind"] = "text", ["valueKind"] = "StringSingleLine",
                            ["defaultValue"] = "", ["actionOnly"] = true,
                        },
                        new JsonObject
                        {
                            ["id"] = "presetId", ["label"] = "Component", ["jsonKey"] = "presetId",
                            ["kind"] = "text", ["valueKind"] = "StringSingleLine",
                            ["defaultValue"] = "", ["actionOnly"] = true,
                        },
                    },
                    ["itemPresentation"] = new JsonObject
                    {
                        ["subtitleFieldIds"] = new JsonArray("presetId"),
                        ["subtitleMaxCharacters"] = 72,
                        ["fallbackIcon"] = "component",
                    },
                    ["animationTimeline"] = new JsonObject { ["sequenceItems"] = false },
                },
            },
        },
    };

    private static string DefaultLockScreenDesignPreviewJson()
    {
        var preview = new JsonObject();
        var inputs = new JsonArray();
        preview["inputs"] = inputs;
        inputs.Insert(0, new JsonObject
        {
            ["id"] = "showNavigationBar",
            ["label"] = "Show Navigation Bar",
            ["jsonKey"] = "showNavigationBar",
            ["kind"] = "boolean",
            ["defaultValue"] = "true",
        });
        inputs.Insert(0, new JsonObject
        {
            ["id"] = "showStatusBar",
            ["label"] = "Show Status Bar",
            ["jsonKey"] = "showStatusBar",
            ["kind"] = "boolean",
            ["defaultValue"] = "true",
        });
        inputs.Insert(0, new JsonObject
        {
            ["id"] = "actor",
            ["label"] = "Actor",
            ["jsonKey"] = "actorId",
            ["kind"] = "recordReference",
            ["defaultValue"] = "actor_alex",
            ["tableId"] = "actors",
            ["resolvedJsonKey"] = "actor",
        });
        preview["actorId"] = "actor_alex";
        preview["showStatusBar"] = true;
        preview["showNavigationBar"] = true;
        return preview.ToJsonString();
    }

    private static void NormalizeLockScreenDesignPreview(SqliteConnection connection, string moduleId)
    {
        var original = ScalarString(connection, "SELECT design_preview_json FROM modules WHERE id = $id", ("$id", moduleId)) ?? "{}";
        var preview = ParseJsonObject(original);
        var defaults = ParseJsonObject(DefaultLockScreenDesignPreviewJson());
        preview["inputs"] = defaults["inputs"]?.DeepClone();
        preview.Remove("collections");
        preview.Remove("testValues");
        preview.Remove("actor");
        if (string.IsNullOrWhiteSpace(preview["actorId"]?.GetValue<string>())) preview["actorId"] = "actor_alex";
        preview["showStatusBar"] ??= true;
        preview["showNavigationBar"] ??= true;
        preview.Remove("sizingMode");
        preview.Remove("startGapToken");
        preview.Remove("endGapToken");
        preview.Remove("items");
        Execute(connection, "UPDATE modules SET design_preview_json = $previewJson WHERE id = $id",
            ("$id", moduleId), ("$previewJson", preview.ToJsonString()));
    }

    private static string SystemAppConfigJson() => "{}";
}
