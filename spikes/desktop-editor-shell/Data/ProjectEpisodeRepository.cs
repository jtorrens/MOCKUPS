using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class ProjectEpisodeRepository : IProjectEpisodeRepository
{
    private readonly SqliteProjectContext _context;
    private readonly IShotRepository _shotRepository;

    public ProjectEpisodeRepository(SqliteProjectContext context, IShotRepository shotRepository)
    {
        _context = context;
        _shotRepository = shotRepository;
    }

    public ProjectSettings GetProjectSettings(string projectId)
    {
        using var connection = _context.OpenConnection();
        return GetProjectSettings(connection, projectId);
    }

    public ProjectSettings GetProjectSettings(SqliteConnection connection, string projectId)
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
            SqliteCommandExecutor.ReadString(reader, 0),
            reader.IsDBNull(1) ? 25 : reader.GetInt32(1),
            SqliteCommandExecutor.ReadString(reader, 2));
    }

    public void UpdateProjectField(string projectId, string fieldId, string value)
    {
        using var connection = _context.OpenConnection();
        var column = fieldId switch
        {
            "project.slug" => "slug",
            "project.defaultFps" => "default_fps",
            "project.mediaRoot" => "media_root",
            _ => throw new InvalidOperationException($"Unknown project field '{fieldId}'."),
        };

        SqliteCommandExecutor.Execute(
            connection,
            $"UPDATE projects SET {column} = $value WHERE id = $id",
            ("$id", projectId),
            ("$value", fieldId == "project.defaultFps" ? NumericText.Int32(value, 0) : value));
    }

    public EpisodeSettings GetEpisodeSettings(string episodeId)
    {
        using var connection = _context.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT slug, sort_order FROM episodes WHERE id = $id";
        command.Parameters.AddWithValue("$id", episodeId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing episode '{episodeId}'.");
        }

        return new EpisodeSettings(
            SqliteCommandExecutor.ReadString(reader, 0),
            reader.IsDBNull(1) ? 0 : reader.GetInt32(1));
    }

    public void UpdateEpisodeField(string episodeId, string fieldId, string value)
    {
        using var connection = _context.OpenConnection();
        var column = fieldId switch
        {
            "episode.slug" => "slug",
            "episode.sortOrder" => "sort_order",
            _ => throw new InvalidOperationException($"Unknown episode field '{fieldId}'."),
        };

        SqliteCommandExecutor.Execute(
            connection,
            $"UPDATE episodes SET {column} = $value WHERE id = $id",
            ("$id", episodeId),
            ("$value", fieldId == "episode.sortOrder" ? NumericText.Int32(value, 0) : value));
    }

    public IReadOnlyList<ProjectRecord> QueryProjects(SqliteConnection connection)
    {
        var rows = new List<ProjectRecord>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, notes FROM projects ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ProjectRecord(
                reader.GetString(0),
                reader.GetString(1),
                SqliteCommandExecutor.ReadString(reader, 2)));
        }

        return rows;
    }

    public IReadOnlyList<EpisodeRecord> QueryEpisodes(SqliteConnection connection)
    {
        var rows = new List<EpisodeRecord>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, slug, notes, sort_order FROM episodes ORDER BY sort_order, name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new EpisodeRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                SqliteCommandExecutor.ReadString(reader, 3),
                SqliteCommandExecutor.ReadString(reader, 4),
                reader.GetInt32(5)));
        }

        return rows;
    }

    public EpisodeRecord CreateEpisode(SqliteConnection connection, string projectId)
    {
        var sortOrder = SqliteCommandExecutor.NextSortOrder(connection, "episodes", "project_id", projectId);
        var id = $"episode_{Guid.NewGuid():N}";
        var name = $"Episode {sortOrder + 1}";
        var slug = $"episode-{sortOrder + 1}";
        const string notes = "New episode created in the desktop shell spike.";
        SqliteCommandExecutor.Execute(
            connection,
            """
            INSERT INTO episodes (id, project_id, name, notes, sort_order)
            VALUES ($id, $projectId, $name, $notes, $sortOrder)
            """,
            ("$id", id),
            ("$projectId", projectId),
            ("$name", name),
            ("$notes", notes),
            ("$sortOrder", sortOrder));
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE episodes SET slug = $slug WHERE id = $id",
            ("$id", id),
            ("$slug", slug));

        return new EpisodeRecord(id, projectId, name, slug, notes, sortOrder);
    }

    public EpisodeRecord DuplicateEpisode(SqliteConnection connection, string sourceEpisodeId, string copyName)
    {
        var source = QueryEpisodes(connection).SingleOrDefault((episode) => episode.Id == sourceEpisodeId)
            ?? throw new InvalidOperationException($"Missing episode '{sourceEpisodeId}'.");
        var id = $"episode_{Guid.NewGuid():N}";
        var sortOrder = SqliteCommandExecutor.NextSortOrder(connection, "episodes", "project_id", source.ProjectId);
        var slug = $"{source.Slug}-copy";
        SqliteCommandExecutor.Execute(
            connection,
            """
            INSERT INTO episodes (id, project_id, name, slug, notes, sort_order)
            VALUES ($id, $projectId, $name, $slug, $notes, $sortOrder)
            """,
            ("$id", id),
            ("$projectId", source.ProjectId),
            ("$name", copyName),
            ("$slug", slug),
            ("$notes", source.Notes),
            ("$sortOrder", sortOrder));

        _shotRepository.DuplicateForEpisode(connection, sourceEpisodeId, id);
        return new EpisodeRecord(id, source.ProjectId, copyName, slug, source.Notes, sortOrder);
    }

    public void DeleteEpisode(SqliteConnection connection, string episodeId)
    {
        SqliteCommandExecutor.Execute(connection, "DELETE FROM episodes WHERE id = $id", ("$id", episodeId));
    }

    public void UpdateProjectNode(SqliteConnection connection, string projectId, string name, string notes)
    {
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE projects SET name = $name, notes = $notes WHERE id = $id",
            ("$id", projectId),
            ("$name", name),
            ("$notes", notes));
    }

    public void UpdateEpisodeNode(SqliteConnection connection, string episodeId, string name, string notes)
    {
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE episodes SET name = $name, notes = $notes WHERE id = $id",
            ("$id", episodeId),
            ("$name", name),
            ("$notes", notes));
    }

}
