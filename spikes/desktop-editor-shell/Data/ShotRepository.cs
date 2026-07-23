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

    public ShotRecord Create(SqliteConnection connection, string episodeId, string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            throw new InvalidOperationException("A Shot requires an explicit owner Actor.");
        }
        var sortOrder = SqliteCommandExecutor.NextSortOrder(connection, "shots", "episode_id", episodeId);
        var record = new ShotRecord(
            $"shot_{Guid.NewGuid():N}",
            episodeId,
            RequiredProjectId(connection, episodeId),
            $"Shot {sortOrder + 1:00}",
            $"shot-{sortOrder + 1:00}",
            1,
            "New shot created in the desktop shell spike.",
            sortOrder,
            null,
            240,
            actorId,
            "",
            "{}",
            "{}");
        Insert(connection, record);
        return Get(connection, record.Id);
    }

    public ShotRecord Duplicate(SqliteConnection connection, string sourceId, string id, string name)
    {
        var source = Get(connection, sourceId);
        var duplicate = source with
        {
            Id = id,
            Name = name,
            Slug = $"{source.Slug}-copy",
            SortOrder = SqliteCommandExecutor.NextSortOrder(
                connection,
                "shots",
                "episode_id",
                source.EpisodeId),
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
        SqliteCommandExecutor.Execute(
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

        var column = fieldId switch
        {
            "shot.slug" => "slug",
            "shot.version" => "version",
            "shot.sortOrder" => "sort_order",
            "shot.fps" => "fps_override",
            "shot.ownerActorId" => "owner_actor_id",
            "shot.renderPresetId" => "render_preset_id",
            "shot.canvas" => "canvas_json",
            "shot.metadata" => "metadata_json",
            _ => throw new InvalidOperationException($"Unknown shot field '{fieldId}'."),
        };
        object nextValue = fieldId is "shot.version" or "shot.sortOrder" or "shot.fps"
            ? NumericText.Int32(value, 0)
            : value;
        if (fieldId is "shot.canvas" or "shot.metadata")
        {
            JsonPath.ParseRequiredObject(value, $"Shot '{shotId}' {column}");
        }
        if (fieldId == "shot.ownerActorId" && string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("A Shot requires an explicit owner Actor.");
        }

        var current = Get(connection, shotId);
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
        if (fieldId == "shot.renderPresetId")
        {
            ProjectReferenceIntegrity.RequireSameProjectReference(
                connection,
                current.ProjectId,
                ProjectReferenceKind.RenderPreset,
                value,
                $"Shot '{shotId}' Render Preset");
        }
        SqliteCommandExecutor.Execute(
            connection,
            $"UPDATE shots SET {column} = $value WHERE id = $id",
            ("$id", shotId),
            ("$value", nextValue));
    }

    public void UpdateDuration(SqliteConnection connection, string shotId, int durationFrames)
    {
        if (durationFrames <= 0)
        {
            throw new InvalidOperationException($"Shot '{shotId}' duration must be positive.");
        }
        _ = Get(connection, shotId);
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE shots SET duration_frames = $duration WHERE id = $id",
            ("$id", shotId),
            ("$duration", durationFrames));
    }

    public void UpdateNode(SqliteConnection connection, string shotId, string name, string notes)
    {
        _ = Get(connection, shotId);
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE shots SET name = $name, notes = $notes WHERE id = $id",
            ("$id", shotId),
            ("$name", name),
            ("$notes", notes));
    }

    public void Delete(SqliteConnection connection, string shotId)
    {
        _ = Get(connection, shotId);
        SqliteCommandExecutor.Execute(
            connection,
            "DELETE FROM shots WHERE id = $id",
            ("$id", shotId));
    }

    private static void Insert(SqliteConnection connection, ShotRecord record)
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
            ProjectReferenceKind.RenderPreset,
            record.RenderPresetId,
            $"Shot '{record.Id}' Render Preset");
        SqliteCommandExecutor.Execute(
            connection,
            """
            INSERT INTO shots (
              id, episode_id, name, slug, version, notes, sort_order, fps_override,
              duration_frames, owner_actor_id, render_preset_id, canvas_json, metadata_json)
            VALUES (
              $id, $episodeId, $name, $slug, $version, $notes, $sortOrder, $fpsOverride,
              $durationFrames, $ownerActorId, $renderPresetId, $canvasJson, $metadataJson)
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
            ("$renderPresetId", record.RenderPresetId),
            ("$canvasJson", record.CanvasJson),
            ("$metadataJson", record.MetadataJson));
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
            SqliteCommandExecutor.ReadString(reader, 6),
            reader.GetInt32(7),
            reader.IsDBNull(8) ? null : reader.GetInt32(8),
            reader.GetInt32(9),
            SqliteCommandExecutor.ReadString(reader, 10),
            SqliteCommandExecutor.ReadString(reader, 11),
            SqliteCommandExecutor.ReadString(reader, 12),
            SqliteCommandExecutor.ReadString(reader, 13));
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
        if (record.DurationFrames <= 0)
        {
            throw new InvalidOperationException($"Shot '{record.Id}' duration must be positive.");
        }
        JsonPath.ParseRequiredObject(record.CanvasJson, $"Shot '{record.Id}' canvas_json");
        JsonPath.ParseRequiredObject(record.MetadataJson, $"Shot '{record.Id}' metadata_json");
    }

    private static string RequiredProjectId(SqliteConnection connection, string episodeId)
    {
        return SqliteCommandExecutor.ScalarString(
            connection,
            "SELECT project_id FROM episodes WHERE id = $episodeId",
            ("$episodeId", episodeId))
            ?? throw new InvalidOperationException($"Missing episode '{episodeId}'.");
    }

    private const string SelectCurrentRows = """
        SELECT s.id, s.episode_id, e.project_id, s.name, s.slug, s.version, s.notes,
               s.sort_order, s.fps_override, s.duration_frames, s.owner_actor_id,
               s.render_preset_id, s.canvas_json, s.metadata_json
        FROM shots s
        JOIN episodes e ON e.id = s.episode_id
        """;
}
