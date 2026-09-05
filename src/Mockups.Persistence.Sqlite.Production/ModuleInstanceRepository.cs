using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class ModuleInstanceRepository : IModuleInstanceRepository
{
    private readonly SqliteProjectContext _context;

    public ModuleInstanceRepository(SqliteProjectContext context)
    {
        _context = context;
    }

    public ModuleInstanceRecord Get(string moduleInstanceId)
    {
        using var connection = _context.OpenConnection();
        return Get(connection, moduleInstanceId);
    }

    public ModuleInstanceRecord Get(SqliteConnection connection, string moduleInstanceId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, shot_id, app_id, module_id, name, notes, sort_order, start_frame, duration_frames, duration_policy, action_delay_frames,
                   device_overrides_json, theme_override_id, transition_json, content_json, behavior_json, animation_json, metadata_json
            FROM module_instances
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", moduleInstanceId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing module instance '{moduleInstanceId}'.");
        }

        return Read(reader);
    }

    public IReadOnlyList<ModuleInstanceRecord> QueryAll(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, shot_id, app_id, module_id, name, notes, sort_order, start_frame, duration_frames, duration_policy, action_delay_frames,
                   device_overrides_json, theme_override_id, transition_json, content_json, behavior_json, animation_json, metadata_json
            FROM module_instances
            ORDER BY shot_id, sort_order, name, id
            """;
        return ReadAll(command);
    }

    public IReadOnlyList<ModuleInstanceRecord> QueryByShot(SqliteConnection connection, string shotId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, shot_id, app_id, module_id, name, notes, sort_order, start_frame, duration_frames, duration_policy, action_delay_frames,
                   device_overrides_json, theme_override_id, transition_json, content_json, behavior_json, animation_json, metadata_json
            FROM module_instances
            WHERE shot_id = $shotId
            ORDER BY sort_order, name, id
            """;
        command.Parameters.AddWithValue("$shotId", shotId);
        return ReadAll(command);
    }

    public int NextSortOrder(SqliteConnection connection, string shotId) =>
        SqliteCommandExecutor.NextSortOrder(connection, "module_instances", "shot_id", shotId);

    public string UniqueName(SqliteConnection connection, string shotId, string requestedName)
    {
        var baseName = requestedName.Trim();
        var candidate = baseName;
        var suffix = 2;
        while (SqliteCommandExecutor.ScalarLong(
            connection,
            "SELECT COUNT(*) FROM module_instances WHERE shot_id = $shotId AND name = $name",
            ("$shotId", shotId),
            ("$name", candidate)) > 0)
        {
            candidate = $"{baseName} {suffix++}";
        }

        return candidate;
    }

    public void Insert(
        SqliteConnection connection,
        ModuleInstanceRecord record,
        SqliteTransaction? transaction = null)
    {
        Validate(record);
        _context.Execute(
            connection,
            transaction,
            """
            INSERT INTO module_instances (
              id, shot_id, app_id, module_id, name, notes, sort_order, start_frame, duration_frames,
              duration_policy, action_delay_frames, device_overrides_json, theme_override_id,
              transition_json, content_json, behavior_json, animation_json, metadata_json)
            VALUES (
              $id, $shotId, $appId, $moduleId, $name, $notes, $sortOrder, $startFrame, $durationFrames,
              $durationPolicy, $actionDelayFrames, $deviceOverridesJson, $themeOverrideId,
              $transitionJson, $contentJson, $behaviorJson, $animationJson, $metadataJson)
            """,
            ("$id", record.Id),
            ("$shotId", record.ShotId),
            ("$appId", record.AppId),
            ("$moduleId", record.ModuleId),
            ("$name", record.Name),
            ("$notes", record.Notes),
            ("$sortOrder", record.SortOrder),
            ("$startFrame", record.StartFrame),
            ("$durationFrames", record.DurationFrames),
            ("$durationPolicy", record.DurationPolicy),
            ("$actionDelayFrames", record.ActionDelayFrames),
            ("$deviceOverridesJson", record.DeviceOverridesJson),
            ("$themeOverrideId", (object?)record.ThemeOverrideId ?? DBNull.Value),
            ("$transitionJson", record.TransitionJson),
            ("$contentJson", record.ContentJson),
            ("$behaviorJson", record.BehaviorJson),
            ("$animationJson", record.AnimationJson),
            ("$metadataJson", record.MetadataJson));
    }

    public ModuleInstanceRecord Duplicate(
        SqliteConnection connection,
        string sourceId,
        string id,
        string targetShotId,
        string name,
        int sortOrder,
        SqliteTransaction? transaction = null)
    {
        _ = Get(connection, sourceId);
        _context.Execute(
            connection,
            transaction,
            """
            INSERT INTO module_instances (
              id, shot_id, app_id, module_id, name, notes, sort_order, start_frame, duration_frames,
              duration_policy, action_delay_frames, device_overrides_json, theme_override_id,
              transition_json, content_json, behavior_json, animation_json, metadata_json)
            SELECT $id, $shotId, app_id, module_id, $name, notes, $sortOrder, start_frame, duration_frames,
                   duration_policy, action_delay_frames, device_overrides_json, theme_override_id,
                   transition_json, content_json, behavior_json, animation_json, metadata_json
            FROM module_instances
            WHERE id = $sourceId
            """,
            ("$id", id),
            ("$shotId", targetShotId),
            ("$name", name),
            ("$sortOrder", sortOrder),
            ("$sourceId", sourceId));
        return Get(connection, id);
    }

    public void UpdateContent(SqliteConnection connection, string moduleInstanceId, string contentJson)
    {
        ValidateObject(contentJson, moduleInstanceId, "content_json");
        _ = Get(connection, moduleInstanceId);
        _context.Execute(
            connection,
            "UPDATE module_instances SET content_json = $contentJson WHERE id = $id",
            ("$contentJson", contentJson),
            ("$id", moduleInstanceId));
    }

    public void UpdateAnimation(SqliteConnection connection, string moduleInstanceId, string animationJson)
    {
        ValidateObject(animationJson, moduleInstanceId, "animation_json");
        _ = Get(connection, moduleInstanceId);
        _context.Execute(
            connection,
            "UPDATE module_instances SET animation_json = $animationJson WHERE id = $id",
            ("$animationJson", animationJson),
            ("$id", moduleInstanceId));
    }

    public void UpdateTransition(
        SqliteConnection connection,
        string moduleInstanceId,
        string transitionJson)
    {
        _ = MotionVariantValue.Parse(
            transitionJson);
        _ = Get(
            connection,
            moduleInstanceId);
        _context.Execute(
            connection,
            "UPDATE module_instances SET transition_json = $transitionJson WHERE id = $id",
            ("$transitionJson", transitionJson),
            ("$id", moduleInstanceId));
    }

    public void UpdateActionDelay(
        SqliteConnection connection,
        string moduleInstanceId,
        int actionDelayFrames)
    {
        if (actionDelayFrames < 0)
        {
            throw new InvalidOperationException(
                $"Module instance '{moduleInstanceId}' action delay must be non-negative.");
        }
        _ = Get(connection, moduleInstanceId);
        _context.Execute(
            connection,
            "UPDATE module_instances SET action_delay_frames = $actionDelayFrames WHERE id = $id",
            ("$actionDelayFrames", actionDelayFrames),
            ("$id", moduleInstanceId));
    }

    public void UpdateStartFrame(
        SqliteConnection connection,
        string moduleInstanceId,
        int startFrame)
    {
        _ = Get(connection, moduleInstanceId);
        _context.Execute(
            connection,
            "UPDATE module_instances SET start_frame = $startFrame WHERE id = $id",
            ("$startFrame", startFrame),
            ("$id", moduleInstanceId));
    }

    public void UpdateResourceOverride(
        SqliteConnection connection,
        string moduleInstanceId,
        string column,
        string? value)
    {
        if (column != "theme_override_id")
            throw new InvalidOperationException($"Unsupported Screen resource column '{column}'.");
        _ = Get(connection, moduleInstanceId);
        _context.Execute(
            connection,
            $"UPDATE module_instances SET {column} = $value WHERE id = $id",
            ("$value", (object?)value ?? DBNull.Value),
            ("$id", moduleInstanceId));
    }

    public void UpdateDeviceOverrides(
        SqliteConnection connection,
        string moduleInstanceId,
        string overridesJson)
    {
        _ = DeviceSettingsFieldContract.ParseScreenOverrides(
            overridesJson,
            $"Screen '{moduleInstanceId}' device_overrides_json");
        _ = Get(connection, moduleInstanceId);
        _context.Execute(
            connection,
            "UPDATE module_instances SET device_overrides_json = $value WHERE id = $id",
            ("$value", overridesJson),
            ("$id", moduleInstanceId));
    }

    public void UpdateContentAndAnimation(
        SqliteConnection connection,
        string moduleInstanceId,
        string contentJson,
        string animationJson,
        SqliteTransaction? transaction = null)
    {
        ValidateObject(contentJson, moduleInstanceId, "content_json");
        ValidateObject(animationJson, moduleInstanceId, "animation_json");
        _ = Get(connection, moduleInstanceId);
        _context.Execute(
            connection,
            transaction,
            "UPDATE module_instances SET content_json = $contentJson, animation_json = $animationJson WHERE id = $id",
            ("$contentJson", contentJson),
            ("$animationJson", animationJson),
            ("$id", moduleInstanceId));
    }

    public void UpdateVariantDocuments(
        SqliteConnection connection,
        string moduleInstanceId,
        string metadataJson,
        string contentJson,
        string animationJson)
    {
        ValidateObject(metadataJson, moduleInstanceId, "metadata_json");
        ValidateObject(contentJson, moduleInstanceId, "content_json");
        ValidateObject(animationJson, moduleInstanceId, "animation_json");
        _ = Get(connection, moduleInstanceId);
        _context.Execute(
            connection,
            "UPDATE module_instances SET metadata_json = $metadataJson, content_json = $contentJson, animation_json = $animationJson WHERE id = $id",
            ("$metadataJson", metadataJson),
            ("$contentJson", contentJson),
            ("$animationJson", animationJson),
            ("$id", moduleInstanceId));
    }

    public void UpdateDuration(
        SqliteConnection connection,
        string moduleInstanceId,
        int durationFrames,
        SqliteTransaction? transaction = null)
    {
        if (durationFrames <= 0)
        {
            throw new InvalidOperationException($"Module instance '{moduleInstanceId}' duration must be positive.");
        }
        _ = Get(connection, moduleInstanceId);
        _context.Execute(
            connection,
            transaction,
            "UPDATE module_instances SET duration_frames = $duration WHERE id = $id",
            ("$duration", durationFrames),
            ("$id", moduleInstanceId));
    }

    public void UpdateDurationPolicy(
        SqliteConnection connection,
        string moduleInstanceId,
        string durationPolicy,
        SqliteTransaction? transaction = null)
    {
        _ = RuntimeDurationContract.ParsePolicy(durationPolicy);
        _ = Get(connection, moduleInstanceId);
        _context.Execute(
            connection,
            transaction,
            "UPDATE module_instances SET duration_policy = $policy WHERE id = $id",
            ("$policy", durationPolicy),
            ("$id", moduleInstanceId));
    }

    public void SwapSortOrder(
        SqliteConnection connection,
        string firstId,
        int firstSortOrder,
        string secondId,
        int secondSortOrder)
    {
        _ = Get(connection, firstId);
        _ = Get(connection, secondId);
        lock (_context.WriteGate)
        {
            using var transaction = connection.BeginTransaction();
            _context.Execute(
                connection,
                transaction,
                "UPDATE module_instances SET sort_order = $sortOrder WHERE id = $id",
                ("$sortOrder", secondSortOrder),
                ("$id", firstId));
            _context.Execute(
                connection,
                transaction,
                "UPDATE module_instances SET sort_order = $sortOrder WHERE id = $id",
                ("$sortOrder", firstSortOrder),
                ("$id", secondId));
            transaction.Commit();
        }
    }

    public long CountVariantReferences(SqliteConnection connection, string moduleId, string variantReference)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, metadata_json FROM module_instances WHERE module_id = $moduleId";
        command.Parameters.AddWithValue("$moduleId", moduleId);
        using var reader = command.ExecuteReader();
        long count = 0;
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var metadata = JsonPath.ParseRequiredObject(
                SqliteCommandExecutor.ReadString(reader, 1),
                $"Module instance '{id}' metadata_json");
            var storedReference = metadata["moduleVariantReference"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(storedReference))
            {
                throw new InvalidOperationException(
                    $"Module instance '{id}' has no explicit Module Variant reference.");
            }
            if (storedReference.Equals(variantReference, StringComparison.Ordinal)) count++;
        }
        return count;
    }

    public void Rename(SqliteConnection connection, string moduleInstanceId, string name)
    {
        _ = Get(connection, moduleInstanceId);
        _context.Execute(
            connection,
            "UPDATE module_instances SET name = $name WHERE id = $id",
            ("$name", name),
            ("$id", moduleInstanceId));
    }

    public void Delete(SqliteConnection connection, string moduleInstanceId)
    {
        _ = Get(connection, moduleInstanceId);
        _context.Execute(
            connection,
            "DELETE FROM module_instances WHERE id = $id",
            ("$id", moduleInstanceId));
    }

    private static IReadOnlyList<ModuleInstanceRecord> ReadAll(SqliteCommand command)
    {
        var rows = new List<ModuleInstanceRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) rows.Add(Read(reader));
        return rows;
    }

    private static ModuleInstanceRecord Read(SqliteDataReader reader)
    {
        var record = new ModuleInstanceRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            SqliteCommandExecutor.ReadString(reader, 5),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetString(9),
            reader.GetInt32(10),
            SqliteCommandExecutor.ReadString(reader, 11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            SqliteCommandExecutor.ReadString(reader, 13),
            SqliteCommandExecutor.ReadString(reader, 14),
            SqliteCommandExecutor.ReadString(reader, 15),
            SqliteCommandExecutor.ReadString(reader, 16),
            SqliteCommandExecutor.ReadString(reader, 17));
        Validate(record);
        return record;
    }

    private static void Validate(ModuleInstanceRecord record)
    {
        if (record.DurationFrames <= 0)
        {
            throw new InvalidOperationException($"Module instance '{record.Id}' duration must be positive.");
        }
        if (record.ThemeOverrideId is not null && string.IsNullOrWhiteSpace(record.ThemeOverrideId))
            throw new InvalidOperationException($"Screen '{record.Id}' Theme override must be null or exact.");
        _ = DeviceSettingsFieldContract.ParseScreenOverrides(
            record.DeviceOverridesJson,
            $"Screen '{record.Id}' device_overrides_json");
        _ = RuntimeDurationContract.ParsePolicy(record.DurationPolicy);
        if (record.ActionDelayFrames < 0)
        {
            throw new InvalidOperationException(
                $"Module instance '{record.Id}' action delay must be non-negative.");
        }
        ValidateObject(record.TransitionJson, record.Id, "transition_json");
        _ = MotionVariantValue.Parse(
            record.TransitionJson);
        ValidateObject(record.ContentJson, record.Id, "content_json");
        ValidateObject(record.BehaviorJson, record.Id, "behavior_json");
        ValidateObject(record.AnimationJson, record.Id, "animation_json");
        ValidateObject(record.MetadataJson, record.Id, "metadata_json");
    }

    private static void ValidateObject(string json, string moduleInstanceId, string column)
    {
        JsonPath.ParseRequiredObject(json, $"Module instance '{moduleInstanceId}' {column}");
    }
}
