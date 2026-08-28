using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class ShotRepository : IShotRepository
{
    private readonly SqliteProjectContext _context;

    public ShotRepository(SqliteProjectContext context)
    {
        _context = context;
    }

    public ShotRecord Get(string shotId)
    {
        using var connection = _context.OpenConnection();
        return Get(connection, shotId);
    }

    public ShotRecord Get(SqliteConnection connection, string shotId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            {SelectCurrentRows}
            WHERE s.id = $id
            """;
        command.Parameters.AddWithValue("$id", shotId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing shot '{shotId}'.");
        }
        return Read(reader);
    }

    public IReadOnlyList<ShotRecord> QueryAll(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            {SelectCurrentRows}
            ORDER BY s.episode_id, s.sort_order, s.name, s.id
            """;
        return ReadAll(command);
    }

    public IReadOnlyList<ShotRecord> QueryByEpisode(SqliteConnection connection, string episodeId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            {SelectCurrentRows}
            WHERE s.episode_id = $episodeId
            ORDER BY s.sort_order, s.name, s.id
            """;
        command.Parameters.AddWithValue("$episodeId", episodeId);
        return ReadAll(command);
    }

    public int SuggestShotNumber(
        SqliteConnection connection,
        string episodeId)
    {
        var maximum = SqliteCommandExecutor.ScalarLong(
            connection,
            """
            SELECT COALESCE(MAX(shot_number), 0)
            FROM shots
            WHERE episode_id = $episodeId
            """,
            ("$episodeId", episodeId));
        return checked((int)maximum + 1);
    }

    public ShotRecord Create(
        SqliteConnection connection,
        string episodeId,
        string actorId,
        int shotNumber,
        string shotCode)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            throw new InvalidOperationException("A Shot requires an explicit owner Actor.");
        }
        RequireAvailableShotNumber(connection, episodeId, shotNumber);
        var sortOrder = SqliteCommandExecutor.NextSortOrder(connection, "shots", "episode_id", episodeId);
        var record = new ShotRecord(
            $"shot_{Guid.NewGuid():N}",
            episodeId,
            RequiredProjectId(connection, episodeId),
            $"Shot {shotNumber:00}",
            shotCode,
            shotNumber,
            1,
            "New Shot created in MOCKUPS.",
            sortOrder,
            null,
            240,
            "calculated",
            240,
            actorId,
            null,
            "{}",
            null,
            "{}",
            ShotReferenceVideoDocument.Empty.ToJson(),
            "{}",
            "free",
            "",
            "",
            "");
        Insert(connection, record);
        return Get(connection, record.Id);
    }

    public ShotRecord Duplicate(
        SqliteConnection connection,
        string sourceId,
        string id,
        string name,
        string actorId,
        int shotNumber,
        string shotCode,
        SqliteTransaction? transaction = null)
    {
        var source = Get(connection, sourceId);
        ProjectReferenceIntegrity.RequireSameProjectReference(
            connection,
            source.ProjectId,
            ProjectReferenceKind.Actor,
            actorId,
            $"Shot '{name}' owner Actor",
            required: true);
        RequireAvailableShotNumber(
            connection,
            source.EpisodeId,
            shotNumber);
        var duplicate = source with
        {
            Id = id,
            Name = name,
            Slug = shotCode,
            ShotNumber = shotNumber,
            SortOrder = SqliteCommandExecutor.NextSortOrder(
                connection,
                "shots",
                "episode_id",
                source.EpisodeId),
            OwnerActorId = actorId,
            ShotManagerAssociationState = "free",
            ShotManagerReferenceProductionId = "",
            ShotManagerShotId = "",
            ShotManagerCanonicalName = "",
        };
        Insert(connection, duplicate, transaction);
        return Get(connection, id);
    }

    public void DuplicateForEpisode(
        SqliteConnection connection,
        string sourceEpisodeId,
        string targetEpisodeId)
    {
        var targetProjectId = RequiredProjectId(connection, targetEpisodeId);
        var sourceShots = QueryByEpisode(connection, sourceEpisodeId);
        for (var index = 0; index < sourceShots.Count; index++)
        {
            Insert(
                connection,
                sourceShots[index] with
                {
                    Id = $"shot_{Guid.NewGuid():N}",
                    EpisodeId = targetEpisodeId,
                    ProjectId = targetProjectId,
                    SortOrder = index,
                    ShotManagerAssociationState = "free",
                    ShotManagerReferenceProductionId = "",
                    ShotManagerShotId = "",
                    ShotManagerCanonicalName = "",
                });
        }
    }

    public void ClearFpsOverride(SqliteConnection connection, string shotId)
    {
        _ = Get(connection, shotId);
        _context.Execute(
            connection,
            "UPDATE shots SET fps_override = NULL WHERE id = $id",
            ("$id", shotId));
    }

    public void UpdateField(SqliteConnection connection, string shotId, string fieldId, string value)
    {
        if (fieldId == "shot.durationFrames")
        {
            var record = Get(connection, shotId);
            if (ShotTimelineDuration.ParsePolicy(record.DurationPolicy)
                != ShotDurationPolicy.Explicit)
            {
                throw new InvalidOperationException(
                    "Shot duration is calculated. Select Explicit duration to edit it.");
            }
            UpdateExplicitDuration(connection, shotId, NumericText.Int32(value, 0));
            return;
        }
        if (fieldId == "shot.durationPolicy")
        {
            var record = Get(connection, shotId);
            var policy = ShotTimelineDuration.ParsePolicy(value);
            _context.Execute(
                connection,
                "UPDATE shots SET duration_policy = $policy, explicit_duration_frames = $duration WHERE id = $id",
                ("$id", shotId),
                ("$policy", ShotTimelineDuration.FormatPolicy(policy)),
                ("$duration", policy == ShotDurationPolicy.Explicit
                    ? record.DurationFrames
                    : record.ExplicitDurationFrames));
            return;
        }

        if (fieldId is "shot.referenceVideoPath" or "shot.referenceVideo")
        {
            var referenceRecord = Get(connection, shotId);
            var reference = fieldId == "shot.referenceVideo"
                ? ShotReferenceVideoDocument.ParseRequired(
                    value,
                    $"Shot '{shotId}' reference video")
                : referenceRecord.ReferenceVideo() with
                {
                    SourcePath = value,
                    InFrame = null,
                    Markers = [],
                };
            _context.Execute(
                connection,
                "UPDATE shots SET reference_video_json = $value WHERE id = $id",
                ("$id", shotId),
                ("$value", reference.ToJson()));
            return;
        }

        var column = fieldId switch
        {
            "shot.slug" => "slug",
            "shot.version" => "version",
            "shot.sortOrder" => "sort_order",
            "shot.fps" => "fps_override",
            "shot.ownerActorId" => "owner_actor_id",
            "shot.deviceOverrideId" => "device_override_id",
            "shot.themeOverrideId" => "theme_override_id",
            "shot.canvas" => "canvas_json",
            "shot.metadata" => "metadata_json",
            _ => throw new InvalidOperationException($"Unknown shot field '{fieldId}'."),
        };
        object nextValue = fieldId is "shot.version" or "shot.sortOrder" or "shot.fps"
            ? NumericText.Int32(value, 0)
            : value;
        if (fieldId == "shot.slug")
        {
            nextValue = ProductionOutputContract.RequireShotCode(
                value,
                $"Shot '{shotId}' code");
        }
        if (fieldId is "shot.canvas" or "shot.metadata")
        {
            JsonPath.ParseRequiredObject(value, $"Shot '{shotId}' {column}");
        }
        if (fieldId == "shot.ownerActorId" && string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("A Shot requires an explicit owner Actor.");
        }

        var current = Get(connection, shotId);
        if (fieldId == "shot.slug")
        {
            RequireAvailableShotCode(
                connection,
                current.EpisodeId,
                shotId,
                (string)nextValue);
        }
        if (fieldId == "shot.ownerActorId")
        {
            ProjectReferenceIntegrity.RequireSameProjectReference(
                connection,
                current.ProjectId,
                ProjectReferenceKind.Actor,
                value,
                $"Shot '{shotId}' owner Actor",
                required: true);
        }
        if (fieldId is "shot.deviceOverrideId" or "shot.themeOverrideId")
        {
            var isDevice = fieldId == "shot.deviceOverrideId";
            ProjectReferenceIntegrity.RequireSameProjectReference(
                connection,
                current.ProjectId,
                isDevice
                    ? ProjectReferenceKind.Device
                    : ProjectReferenceKind.Theme,
                value,
                $"Shot '{shotId}' {(isDevice ? "Device" : "Theme")} override",
                required: true);
        }
        _context.Execute(
            connection,
            $"UPDATE shots SET {column} = $value WHERE id = $id",
            ("$id", shotId),
            ("$value", nextValue));
    }

    public void AssociateShotManagerShot(
        SqliteConnection connection,
        string shotId,
        ShotManagerReadonlyShot? shot)
    {
        var current = Get(connection, shotId);
        if (shot is null)
        {
            _context.Execute(
                connection,
                "UPDATE shots SET shot_manager_association_state = 'free', shot_manager_reference_production_id = '', shot_manager_shot_id = '', shot_manager_canonical_name = '' WHERE id = $id",
                ("$id", shotId));
            return;
        }
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT p.production_output_mode, p.shot_manager_production_id, e.shot_manager_association_state, e.shot_manager_episode_id FROM episodes e JOIN projects p ON p.id = e.project_id WHERE e.id = $episodeId";
        command.Parameters.AddWithValue("$episodeId", current.EpisodeId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException(
                $"Missing Episode '{current.EpisodeId}'.");
        var productionId = reader.GetString(1);
        var episodeId = reader.GetString(3);
        if (reader.GetString(0) != "shot_manager"
            || reader.GetString(2) != "associated"
            || !shot.EpisodeId.Equals(episodeId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "A Shot Manager Shot requires its exact associated Episode.");
        reader.Close();
        _context.Execute(
            connection,
            "UPDATE shots SET shot_manager_association_state = 'associated', shot_manager_reference_production_id = $productionId, shot_manager_shot_id = $shotId, shot_manager_canonical_name = $canonicalName WHERE id = $id",
            ("$id", shotId),
            ("$productionId", productionId),
            ("$shotId", shot.Id),
            ("$canonicalName", shot.CanonicalName));
    }

    public void ClearResourceOverride(
        SqliteConnection connection,
        string shotId,
        string fieldId)
    {
        _ = Get(connection, shotId);
        var column = fieldId switch
        {
            "shot.deviceOverrideId" => "device_override_id",
            "shot.themeOverrideId" => "theme_override_id",
            _ => throw new InvalidOperationException(
                $"Unknown Shot resource override field '{fieldId}'."),
        };
        _context.Execute(
            connection,
            $"UPDATE shots SET {column} = NULL WHERE id = $id",
            ("$id", shotId));
    }

    public void UpdateDeviceOverrides(
        SqliteConnection connection,
        string shotId,
        string overridesJson)
    {
        _ = Get(connection, shotId);
        _ = DeviceSettingsFieldContract.ParseOverrides(
            overridesJson,
            $"Shot '{shotId}' device_overrides_json");
        _context.Execute(
            connection,
            "UPDATE shots SET device_overrides_json = $value WHERE id = $id",
            ("$id", shotId),
            ("$value", overridesJson));
    }

    public void UpdateDuration(
        SqliteConnection connection,
        string shotId,
        int durationFrames,
        SqliteTransaction? transaction = null)
    {
        if (durationFrames <= 0)
        {
            throw new InvalidOperationException($"Shot '{shotId}' duration must be positive.");
        }
        _ = Get(connection, shotId);
        _context.Execute(
            connection,
            transaction,
            "UPDATE shots SET duration_frames = $duration WHERE id = $id",
            ("$id", shotId),
            ("$duration", durationFrames));
    }

    public void UpdateExplicitDuration(
        SqliteConnection connection,
        string shotId,
        int durationFrames)
    {
        if (durationFrames <= 0)
        {
            throw new InvalidOperationException($"Shot '{shotId}' explicit duration must be positive.");
        }
        _ = Get(connection, shotId);
        _context.Execute(
            connection,
            "UPDATE shots SET explicit_duration_frames = $duration, duration_frames = $duration WHERE id = $id",
            ("$duration", durationFrames),
            ("$id", shotId));
    }

    public void UpdateNode(SqliteConnection connection, string shotId, string name, string notes)
    {
        _ = Get(connection, shotId);
        _context.Execute(
            connection,
            "UPDATE shots SET name = $name, notes = $notes WHERE id = $id",
            ("$id", shotId),
            ("$name", name),
            ("$notes", notes));
    }

    public void Delete(SqliteConnection connection, string shotId)
    {
        _ = Get(connection, shotId);
        _context.Execute(
            connection,
            "DELETE FROM shots WHERE id = $id",
            ("$id", shotId));
    }

    private void Insert(
        SqliteConnection connection,
        ShotRecord record,
        SqliteTransaction? transaction = null)
    {
        Validate(record);
        var projectId = RequiredProjectId(connection, record.EpisodeId);
        if (!projectId.Equals(record.ProjectId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Shot '{record.Id}' Project ownership does not match Episode '{record.EpisodeId}'.");
        }
        ProjectReferenceIntegrity.RequireSameProjectReference(
            connection,
            record.ProjectId,
            ProjectReferenceKind.Actor,
            record.OwnerActorId,
            $"Shot '{record.Id}' owner Actor",
            required: true);
        ProjectReferenceIntegrity.RequireSameProjectReference(
            connection,
            record.ProjectId,
            ProjectReferenceKind.Device,
            record.DeviceOverrideId ?? "",
            $"Shot '{record.Id}' Device override");
        ProjectReferenceIntegrity.RequireSameProjectReference(
            connection,
            record.ProjectId,
            ProjectReferenceKind.Theme,
            record.ThemeOverrideId ?? "",
            $"Shot '{record.Id}' Theme override");
        InsertRow(connection, transaction, record);
    }

    private void InsertRow(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        ShotRecord record)
    {
        Validate(record);
        _context.Execute(
            connection,
            transaction,
            """
            INSERT INTO shots (
              id, episode_id, name, slug, version, notes, sort_order, fps_override,
              duration_frames, duration_policy, explicit_duration_frames,
              owner_actor_id, device_override_id, device_overrides_json,
              theme_override_id,
              canvas_json, reference_video_json, metadata_json, shot_number,
              shot_manager_association_state,
              shot_manager_reference_production_id,
              shot_manager_shot_id,
              shot_manager_canonical_name)
            VALUES (
              $id, $episodeId, $name, $slug, $version, $notes, $sortOrder, $fpsOverride,
              $durationFrames, $durationPolicy, $explicitDurationFrames,
              $ownerActorId, $deviceOverrideId, $deviceOverridesJson,
              $themeOverrideId,
              $canvasJson, $referenceVideoJson, $metadataJson, $shotNumber,
              $shotManagerAssociationState,
              $shotManagerReferenceProductionId,
              $shotManagerShotId,
              $shotManagerCanonicalName)
            """,
            ("$id", record.Id),
            ("$episodeId", record.EpisodeId),
            ("$name", record.Name),
            ("$slug", record.Slug),
            ("$version", record.Version),
            ("$notes", record.Notes),
            ("$sortOrder", record.SortOrder),
            ("$fpsOverride", record.FpsOverride),
            ("$durationFrames", record.DurationFrames),
            ("$durationPolicy", record.DurationPolicy),
            ("$explicitDurationFrames", record.ExplicitDurationFrames),
            ("$ownerActorId", record.OwnerActorId),
            ("$deviceOverrideId", (object?)record.DeviceOverrideId ?? DBNull.Value),
            ("$deviceOverridesJson", record.DeviceOverridesJson),
            ("$themeOverrideId", (object?)record.ThemeOverrideId ?? DBNull.Value),
            ("$canvasJson", record.CanvasJson),
            ("$referenceVideoJson", record.ReferenceVideoJson),
            ("$metadataJson", record.MetadataJson),
            ("$shotNumber", record.ShotNumber),
            ("$shotManagerAssociationState", record.ShotManagerAssociationState),
            ("$shotManagerReferenceProductionId", record.ShotManagerReferenceProductionId),
            ("$shotManagerShotId", record.ShotManagerShotId),
            ("$shotManagerCanonicalName", record.ShotManagerCanonicalName));
    }

    private static IReadOnlyList<ShotRecord> ReadAll(SqliteCommand command)
    {
        var rows = new List<ShotRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) rows.Add(Read(reader));
        return rows;
    }

    private static ShotRecord Read(SqliteDataReader reader)
    {
        var record = new ShotRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            SqliteCommandExecutor.ReadString(reader, 4),
            reader.GetInt32(5),
            reader.GetInt32(6),
            SqliteCommandExecutor.ReadString(reader, 7),
            reader.GetInt32(8),
            reader.IsDBNull(9) ? null : reader.GetInt32(9),
            reader.GetInt32(10),
            SqliteCommandExecutor.ReadString(reader, 11),
            reader.GetInt32(12),
            SqliteCommandExecutor.ReadString(reader, 13),
            reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.GetString(15),
            reader.IsDBNull(16) ? null : reader.GetString(16),
            SqliteCommandExecutor.ReadString(reader, 17),
            SqliteCommandExecutor.ReadString(reader, 18),
            SqliteCommandExecutor.ReadString(reader, 19),
            SqliteCommandExecutor.ReadString(reader, 20),
            SqliteCommandExecutor.ReadString(reader, 21),
            SqliteCommandExecutor.ReadString(reader, 22),
            SqliteCommandExecutor.ReadString(reader, 23));
        Validate(record);
        return record;
    }

    private static void Validate(ShotRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.Id)
            || string.IsNullOrWhiteSpace(record.EpisodeId)
            || string.IsNullOrWhiteSpace(record.ProjectId)
            || string.IsNullOrWhiteSpace(record.Name))
        {
            throw new InvalidOperationException("A current Shot requires stable identity and explicit ownership.");
        }
        if (string.IsNullOrWhiteSpace(record.OwnerActorId))
        {
            throw new InvalidOperationException($"Shot '{record.Id}' requires an explicit owner Actor.");
        }
        if (record.DeviceOverrideId is not null
            && string.IsNullOrWhiteSpace(record.DeviceOverrideId))
        {
            throw new InvalidOperationException(
                $"Shot '{record.Id}' Device override must be null or an exact reference.");
        }
        if (record.ThemeOverrideId is not null
            && string.IsNullOrWhiteSpace(record.ThemeOverrideId))
        {
            throw new InvalidOperationException(
                $"Shot '{record.Id}' Theme override must be null or an exact reference.");
        }
        _ = DeviceSettingsFieldContract.ParseOverrides(
            record.DeviceOverridesJson,
            $"Shot '{record.Id}' device_overrides_json");
        if (record.ShotNumber <= 0)
        {
            throw new InvalidOperationException(
                $"Shot '{record.Id}' requires a positive stable number.");
        }
        if (record.DurationFrames <= 0)
        {
            throw new InvalidOperationException($"Shot '{record.Id}' duration must be positive.");
        }
        _ = ShotTimelineDuration.ParsePolicy(record.DurationPolicy);
        if (record.ExplicitDurationFrames <= 0)
        {
            throw new InvalidOperationException($"Shot '{record.Id}' explicit duration must be positive.");
        }
        JsonPath.ParseRequiredObject(record.CanvasJson, $"Shot '{record.Id}' canvas_json");
        JsonPath.ParseRequiredObject(record.MetadataJson, $"Shot '{record.Id}' metadata_json");
        _ = ShotReferenceVideoDocument.ParseRequired(
            record.ReferenceVideoJson,
            $"Shot '{record.Id}' reference_video_json");
        if (record.ShotManagerAssociationState is not ("associated" or "free"))
            throw new InvalidOperationException(
                $"Shot '{record.Id}' has an invalid Shot Manager state.");
        _ = ShotManagerReadonlyContract.RequireShotAssociation(
            new ShotManagerShotAssociation(
                record.ShotManagerAssociationState == "associated",
                record.ShotManagerReferenceProductionId,
                record.ShotManagerShotId,
                record.ShotManagerCanonicalName),
            $"Shot '{record.Id}' Shot Manager association");
    }

    private static string RequiredProjectId(SqliteConnection connection, string episodeId)
    {
        return SqliteCommandExecutor.ScalarString(
            connection,
            "SELECT project_id FROM episodes WHERE id = $episodeId",
            ("$episodeId", episodeId))
            ?? throw new InvalidOperationException($"Missing episode '{episodeId}'.");
    }

    private static void RequireAvailableShotNumber(
        SqliteConnection connection,
        string episodeId,
        int shotNumber)
    {
        if (shotNumber <= 0)
        {
            throw new InvalidOperationException(
                "A Shot requires a positive stable number.");
        }
        if (SqliteCommandExecutor.ScalarLong(
                connection,
                """
                SELECT COUNT(*)
                FROM shots
                WHERE episode_id = $episodeId
                  AND shot_number = $shotNumber
                """,
                ("$episodeId", episodeId),
                ("$shotNumber", shotNumber)) != 0)
        {
            throw new InvalidOperationException(
                $"Shot number {shotNumber} already exists in this Episode.");
        }
    }

    private static void RequireAvailableShotCode(
        SqliteConnection connection,
        string episodeId,
        string shotId,
        string shotCode)
    {
        if (SqliteCommandExecutor.ScalarLong(
                connection,
                """
                SELECT COUNT(*)
                FROM shots
                WHERE episode_id = $episodeId
                  AND slug = $shotCode
                  AND id <> $shotId
                """,
                ("$episodeId", episodeId),
                ("$shotCode", shotCode),
                ("$shotId", shotId)) != 0)
        {
            throw new InvalidOperationException(
                $"Shot code '{shotCode}' already exists in this Episode.");
        }
    }

    private const string SelectCurrentRows = """
        SELECT s.id, s.episode_id, e.project_id, s.name, s.slug, s.shot_number,
               s.version, s.notes,
               s.sort_order, s.fps_override, s.duration_frames, s.duration_policy,
               s.explicit_duration_frames, s.owner_actor_id,
               s.device_override_id, s.device_overrides_json,
               s.theme_override_id, s.canvas_json,
               s.reference_video_json, s.metadata_json,
               s.shot_manager_association_state,
               s.shot_manager_reference_production_id,
               s.shot_manager_shot_id,
               s.shot_manager_canonical_name
        FROM shots s
        JOIN episodes e ON e.id = s.episode_id
        """;
}
