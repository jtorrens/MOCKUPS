using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
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
            actorId,
            null,
            null,
            "{}",
            ShotReferenceVideoDocument.Empty.ToJson(),
            "{}");
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
        string shotCode)
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
        };
        Insert(connection, duplicate);
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
            UpdateDuration(connection, shotId, NumericText.Int32(value, 0));
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

    private void Insert(SqliteConnection connection, ShotRecord record)
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
        InsertRow(connection, transaction: null, record);
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
              duration_frames, owner_actor_id, device_override_id, theme_override_id,
              canvas_json, reference_video_json, metadata_json, shot_number)
            VALUES (
              $id, $episodeId, $name, $slug, $version, $notes, $sortOrder, $fpsOverride,
              $durationFrames, $ownerActorId, $deviceOverrideId, $themeOverrideId,
              $canvasJson, $referenceVideoJson, $metadataJson, $shotNumber)
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
            ("$ownerActorId", record.OwnerActorId),
            ("$deviceOverrideId", (object?)record.DeviceOverrideId ?? DBNull.Value),
            ("$themeOverrideId", (object?)record.ThemeOverrideId ?? DBNull.Value),
            ("$canvasJson", record.CanvasJson),
            ("$referenceVideoJson", record.ReferenceVideoJson),
            ("$metadataJson", record.MetadataJson),
            ("$shotNumber", record.ShotNumber));
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
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            SqliteCommandExecutor.ReadString(reader, 14),
            SqliteCommandExecutor.ReadString(reader, 15),
            SqliteCommandExecutor.ReadString(reader, 16));
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
        if (record.ShotNumber <= 0)
        {
            throw new InvalidOperationException(
                $"Shot '{record.Id}' requires a positive stable number.");
        }
        if (record.DurationFrames <= 0)
        {
            throw new InvalidOperationException($"Shot '{record.Id}' duration must be positive.");
        }
        JsonPath.ParseRequiredObject(record.CanvasJson, $"Shot '{record.Id}' canvas_json");
        JsonPath.ParseRequiredObject(record.MetadataJson, $"Shot '{record.Id}' metadata_json");
        _ = ShotReferenceVideoDocument.ParseRequired(
            record.ReferenceVideoJson,
            $"Shot '{record.Id}' reference_video_json");
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
               s.sort_order, s.fps_override, s.duration_frames, s.owner_actor_id,
               s.device_override_id, s.theme_override_id,
               s.canvas_json, s.reference_video_json, s.metadata_json
        FROM shots s
        JOIN episodes e ON e.id = s.episode_id
        """;
}
