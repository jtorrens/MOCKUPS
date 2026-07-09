using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public ProjectSettings GetProjectSettings(string projectId)
    {
        using var connection = OpenConnection();
        return GetProjectSettings(connection, projectId);
    }

    public void UpdateProjectField(string projectId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        var column = fieldId switch
        {
            "project.slug" => "slug",
            "project.defaultFps" => "default_fps",
            "project.mediaRoot" => "media_root",
            _ => throw new InvalidOperationException($"Unknown project field '{fieldId}'."),
        };

        Execute(
            connection,
            $"UPDATE projects SET {column} = $value WHERE id = $id",
            ("$id", projectId),
            ("$value", fieldId == "project.defaultFps" ? NumericText.Int32(value, 0) : value));
    }

    public EpisodeSettings GetEpisodeSettings(string episodeId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT slug, sort_order FROM episodes WHERE id = $id";
        command.Parameters.AddWithValue("$id", episodeId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing episode '{episodeId}'.");
        }

        return new EpisodeSettings(
            ReadString(reader, 0),
            reader.IsDBNull(1) ? 0 : reader.GetInt32(1));
    }

    public void UpdateEpisodeField(string episodeId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        var column = fieldId switch
        {
            "episode.slug" => "slug",
            "episode.sortOrder" => "sort_order",
            _ => throw new InvalidOperationException($"Unknown episode field '{fieldId}'."),
        };

        Execute(
            connection,
            $"UPDATE episodes SET {column} = $value WHERE id = $id",
            ("$id", episodeId),
            ("$value", fieldId == "episode.sortOrder" ? NumericText.Int32(value, 0) : value));
    }

    private static void SeedIfEmpty(SqliteConnection connection)
    {
        if (ScalarLong(connection, "SELECT COUNT(*) FROM projects") > 0) return;

        Execute(
            connection,
            "INSERT INTO projects (id, name, notes, media_root) VALUES ($id, $name, $notes, $mediaRoot)",
            ("$id", "project_foqn_s2"),
            ("$name", "FOQN S2"),
            ("$notes", "Spike project seeded from the current production structure."),
            ("$mediaRoot", "assets/FOQN_S2"));
        Execute(
            connection,
            "UPDATE projects SET slug = $slug, default_fps = $defaultFps WHERE id = $id",
            ("$id", "project_foqn_s2"),
            ("$slug", "foqn_s2"),
            ("$defaultFps", 25));

        Execute(
            connection,
            "INSERT INTO episodes (id, project_id, name, slug, notes, sort_order) VALUES ($id, $projectId, $name, $slug, $notes, $sortOrder)",
            ("$id", "episode_001"),
            ("$projectId", "project_foqn_s2"),
            ("$name", "Episode 1"),
            ("$slug", "episode-1"),
            ("$notes", "First seeded episode."),
            ("$sortOrder", 0));

        Execute(
            connection,
            "INSERT INTO episodes (id, project_id, name, slug, notes, sort_order) VALUES ($id, $projectId, $name, $slug, $notes, $sortOrder)",
            ("$id", "episode_002"),
            ("$projectId", "project_foqn_s2"),
            ("$name", "Episode 2"),
            ("$slug", "episode-2"),
            ("$notes", "Second seeded episode."),
            ("$sortOrder", 1));

        SeedShot(connection, "shot_001", "episode_001", "Shot 01 · Opening chat", 0);
        SeedShot(connection, "shot_002", "episode_001", "Shot 02 · Incoming media", 1);
        SeedShot(connection, "shot_003", "episode_001", "Shot 03 · Audio reply", 2);
        SeedShot(connection, "shot_004", "episode_002", "Shot 01 · Lock to chat", 0);
        SeedShot(connection, "shot_005", "episode_002", "Shot 02 · Message escalation", 1);

        Execute(
            connection,
            """
            INSERT INTO apps (id, project_id, record_class_id, name, notes, sort_order)
            VALUES ($id, $projectId, $recordClassId, $name, $notes, 0)
            """,
            ("$id", "app_core_chat"),
            ("$projectId", "project_foqn_s2"),
            ("$recordClassId", "app.core.chat"),
            ("$name", "Chat"),
            ("$notes", "Seed app. Modules hang below their app."));

        Execute(
            connection,
            """
            INSERT INTO modules (id, app_id, record_class_id, name, notes, sort_order, metadata_json)
            VALUES ($id, $appId, $recordClassId, $name, $notes, 0, $metadataJson)
            """,
            ("$id", "module_core_chat"),
            ("$appId", "app_core_chat"),
            ("$recordClassId", "module.core.chat"),
            ("$name", "Conversation"),
            ("$notes", "Seed conversation module linked to Chat app."),
            ("$metadataJson", JsonSerializer.Serialize(new { note = "Seed module linked to Chat app." })));
    }

    private static void SeedShot(SqliteConnection connection, string id, string episodeId, string name, int sortOrder)
    {
        Execute(
            connection,
            """
            INSERT INTO shots (id, episode_id, name, notes, sort_order, fps, duration_frames)
            VALUES ($id, $episodeId, $name, $notes, $sortOrder, 25, 240)
            """,
            ("$id", id),
            ("$episodeId", episodeId),
            ("$name", name),
            ("$notes", "Seed shot. Screen level is intentionally added later."),
            ("$sortOrder", sortOrder));
    }

    private static List<ProjectRow> QueryProjectRows(SqliteConnection connection)
    {
        var rows = new List<ProjectRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, notes FROM projects ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ProjectRow(reader.GetString(0), reader.GetString(1), ReadString(reader, 2)));
        }

        return rows;
    }

    private static List<EpisodeRow> QueryEpisodeRows(SqliteConnection connection)
    {
        var rows = new List<EpisodeRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, slug, notes, sort_order FROM episodes ORDER BY sort_order, name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new EpisodeRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                ReadString(reader, 4),
                reader.GetInt32(5)));
        }

        return rows;
    }

    private static ProjectSettings GetProjectSettings(SqliteConnection connection, string projectId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT slug, default_fps, media_root FROM projects WHERE id = $id";
        command.Parameters.AddWithValue("$id", projectId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing project '{projectId}'.");
        }

        return new ProjectSettings(
            ReadString(reader, 0),
            reader.IsDBNull(1) ? 25 : reader.GetInt32(1),
            ReadString(reader, 2));
    }

    private static void EnsureProjectColumns(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "projects", "slug", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "projects", "default_fps", "INTEGER NOT NULL DEFAULT 25");
    }

    private static void EnsureEpisodeColumns(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "episodes", "slug", "TEXT NOT NULL DEFAULT ''");
        Execute(
            connection,
            """
            UPDATE episodes
            SET slug = lower(replace(trim(name), ' ', '-'))
            WHERE trim(slug) = ''
            """);
    }
}
