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
        command.CommandText = """
            SELECT
              slug,
              default_fps,
              media_root,
              production_code,
              production_season_code,
              output_name_separator,
              shot_prefix,
              shot_number_padding,
              output_version_padding,
              output_frame_padding,
              output_relative_directory_template
            FROM projects
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", projectId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing project '{projectId}'.");
        }

        var output = ProductionOutputContract.Require(
            new ProductionOutputSettings(
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                reader.GetInt32(9),
                reader.GetString(10)),
            $"Project '{projectId}' Production output");
        return new ProjectSettings(
            SqliteCommandExecutor.ReadString(reader, 0),
            reader.IsDBNull(1) ? 25 : reader.GetInt32(1),
            SqliteCommandExecutor.ReadString(reader, 2),
            output);
    }

    public void UpdateProjectField(string projectId, string fieldId, string value)
    {
        lock (_context.WriteGate)
        {
            using var connection = _context.OpenConnection();
            using var transaction = connection.BeginTransaction();
            var settings = GetProjectSettings(connection, projectId);
            var normalized = value;
            var output = settings.ProductionOutput;
            output = fieldId switch
            {
                "project.productionCode" =>
                    output with
                    {
                        TechnicalCode = value.Trim().ToUpperInvariant(),
                    },
                "project.productionSeasonCode" =>
                    output with
                    {
                        SeasonCode = value.Trim().ToUpperInvariant(),
                    },
                "project.outputNameSeparator" =>
                    output with { NameSeparator = value },
                "project.shotPrefix" =>
                    output with
                    {
                        ShotPrefix = value.Trim().ToUpperInvariant(),
                    },
                "project.shotNumberPadding" =>
                    output with
                    {
                        ShotNumberPadding = NumericText.Int32(value, 0),
                    },
                "project.outputVersionPadding" =>
                    output with
                    {
                        VersionPadding = NumericText.Int32(value, 0),
                    },
                "project.outputFramePadding" =>
                    output with
                    {
                        FramePadding = NumericText.Int32(value, 0),
                    },
                "project.outputRelativeDirectoryTemplate" =>
                    output with
                    {
                        RelativeDirectoryTemplate = value.Trim(),
                    },
                _ => output,
            };
            ProductionOutputContract.Require(
                output,
                $"Project '{projectId}' Production output");
            normalized = fieldId switch
            {
                "project.productionCode" => output.TechnicalCode,
                "project.productionSeasonCode" => output.SeasonCode,
                "project.outputNameSeparator" => output.NameSeparator,
                "project.shotPrefix" => output.ShotPrefix,
                "project.shotNumberPadding" =>
                    output.ShotNumberPadding.ToString(),
                "project.outputVersionPadding" =>
                    output.VersionPadding.ToString(),
                "project.outputFramePadding" =>
                    output.FramePadding.ToString(),
                "project.outputRelativeDirectoryTemplate" =>
                    output.RelativeDirectoryTemplate,
                _ => value,
            };
            var column = fieldId switch
            {
                "project.slug" => "slug",
                "project.defaultFps" => "default_fps",
                "project.mediaRoot" => "media_root",
                "project.productionCode" => "production_code",
                "project.productionSeasonCode" =>
                    "production_season_code",
                "project.outputNameSeparator" =>
                    "output_name_separator",
                "project.shotPrefix" => "shot_prefix",
                "project.shotNumberPadding" =>
                    "shot_number_padding",
                "project.outputVersionPadding" =>
                    "output_version_padding",
                "project.outputFramePadding" =>
                    "output_frame_padding",
                "project.outputRelativeDirectoryTemplate" =>
                    "output_relative_directory_template",
                _ => throw new InvalidOperationException(
                    $"Unknown project field '{fieldId}'."),
            };

            _context.Execute(
                connection,
                transaction,
                $"UPDATE projects SET {column} = $value WHERE id = $id",
                ("$id", projectId),
                ("$value", fieldId is "project.defaultFps"
                    or "project.shotNumberPadding"
                    or "project.outputVersionPadding"
                    or "project.outputFramePadding"
                        ? NumericText.Int32(normalized, 0)
                        : normalized));
            if (fieldId is "project.shotPrefix"
                or "project.shotNumberPadding")
            {
                var episodeCodes = QueryEpisodes(connection)
                    .Where((episode) =>
                        episode.ProjectId == projectId)
                    .ToDictionary(
                        (episode) => episode.Id,
                        (episode) => episode.Slug,
                        StringComparer.Ordinal);
                var shotCodes = _shotRepository.QueryAll(connection)
                    .Where((shot) => shot.ProjectId == projectId)
                    .ToDictionary(
                        (shot) => shot.Id,
                        (shot) => ProductionOutputContract.Resolve(
                            projectId,
                            shot.Id,
                            shot.ShotNumber,
                            episodeCodes[shot.EpisodeId],
                            output).ShotCode,
                        StringComparer.Ordinal);
                _shotRepository.UpdateGeneratedCodes(
                    connection,
                    transaction,
                    shotCodes);
            }
            transaction.Commit();
        }
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

        _context.Execute(
            connection,
            $"UPDATE episodes SET {column} = $value WHERE id = $id",
            ("$id", episodeId),
            ("$value", fieldId == "episode.sortOrder"
                ? NumericText.Int32(value, 0)
                : ProductionOutputContract.RequireEpisodeCode(
                    value,
                    $"Episode '{episodeId}' code")));
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
        var slug = $"EP_{sortOrder + 1:00}";
        const string notes = "New Episode created in MOCKUPS.";
        _context.Execute(
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
        _context.Execute(
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
        var slug = $"EP_{sortOrder + 1:00}";
        _context.Execute(
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
        _context.Execute(connection, "DELETE FROM episodes WHERE id = $id", ("$id", episodeId));
    }

    public void UpdateProjectNode(SqliteConnection connection, string projectId, string name, string notes)
    {
        _context.Execute(
            connection,
            "UPDATE projects SET name = $name, notes = $notes WHERE id = $id",
            ("$id", projectId),
            ("$name", name),
            ("$notes", notes));
    }

    public void UpdateEpisodeNode(SqliteConnection connection, string episodeId, string name, string notes)
    {
        _context.Execute(
            connection,
            "UPDATE episodes SET name = $name, notes = $notes WHERE id = $id",
            ("$id", episodeId),
            ("$name", name),
            ("$notes", notes));
    }

}
