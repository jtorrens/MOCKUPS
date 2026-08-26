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
              episode_prefix,
              shot_prefix,
              shot_number_padding,
              output_version_padding,
              output_frame_padding,
              output_relative_directory_template,
              production_output_mode,
              shot_manager_production_id,
              shot_manager_production_slug,
              shot_manager_season_slug,
              shot_manager_workstream_name,
              shot_manager_folder_name,
              shot_manager_folder_suffix
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
            output,
            ShotManagerReadonlyContract.RequireSettings(
                new ShotManagerOutputSettings(
                RequireOutputMode(
                    reader.GetString(11),
                    $"Project '{projectId}' Production output")
                    == "shot_manager",
                SqliteCommandExecutor.ReadString(reader, 12),
                SqliteCommandExecutor.ReadString(reader, 13),
                SqliteCommandExecutor.ReadString(reader, 14),
                SqliteCommandExecutor.ReadString(reader, 15),
                SqliteCommandExecutor.ReadString(reader, 16),
                SqliteCommandExecutor.ReadString(reader, 17)),
                $"Project '{projectId}' Shot Manager output"));
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
                        TechnicalCode = value.Trim(),
                    },
                "project.productionSeasonCode" =>
                    output with
                    {
                        SeasonCode = value.Trim(),
                    },
                "project.episodePrefix" =>
                    output with
                    {
                        EpisodePrefix = value.Trim(),
                    },
                "project.shotPrefix" =>
                    output with
                    {
                        ShotPrefix = value.Trim(),
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
                "project.episodePrefix" => output.EpisodePrefix,
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
                "project.episodePrefix" => "episode_prefix",
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
            transaction.Commit();
        }
    }

    public EpisodeSettings GetEpisodeSettings(string episodeId)
    {
        using var connection = _context.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT slug, sort_order, shot_manager_association_state, shot_manager_reference_production_id, shot_manager_episode_id, shot_manager_episode_order, shot_manager_episode_slug FROM episodes WHERE id = $id";
        command.Parameters.AddWithValue("$id", episodeId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing episode '{episodeId}'.");
        }

        return new EpisodeSettings(
            SqliteCommandExecutor.ReadString(reader, 0),
            reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
            ShotManagerReadonlyContract.RequireEpisodeAssociation(
                new ShotManagerEpisodeAssociation(
                    SqliteCommandExecutor.ReadString(reader, 2) == "associated",
                    SqliteCommandExecutor.ReadString(reader, 3),
                    SqliteCommandExecutor.ReadString(reader, 4),
                    reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    SqliteCommandExecutor.ReadString(reader, 6)),
                $"Episode '{episodeId}' Shot Manager association"));
    }

    public void UpdateEpisodeField(string episodeId, string fieldId, string value)
    {
        lock (_context.WriteGate)
        {
            using var connection = _context.OpenConnection();
            using var transaction = connection.BeginTransaction();
            var column = fieldId switch
            {
                "episode.slug" => "slug",
                "episode.sortOrder" => "sort_order",
                _ => throw new InvalidOperationException($"Unknown episode field '{fieldId}'."),
            };

            _context.Execute(
                connection,
                transaction,
                $"UPDATE episodes SET {column} = $value WHERE id = $id",
                ("$id", episodeId),
                ("$value", fieldId switch
                {
                    "episode.sortOrder" => NumericText.Int32(value, 0),
                    "episode.slug" => ProductionOutputContract.RequireEpisodeCode(
                        value,
                        $"Episode '{episodeId}' code"),
                    _ => value.Trim(),
                }));
            transaction.Commit();
        }
    }

    public void ConnectShotManagerProduction(
        string projectId,
        ShotManagerReadonlyProduction production,
        string workstreamName,
        string folderName)
    {
        var association = ResolveProductionAssociation(
            production,
            workstreamName,
            folderName);
        lock (_context.WriteGate)
        {
            using var connection = _context.OpenConnection();
            using var transaction = connection.BeginTransaction();
            var current = GetProjectSettings(connection, projectId)
                .ShotManagerOutput;
            if (!string.IsNullOrEmpty(current.ProductionId)
                && !current.ProductionId.Equals(
                    association.ProductionId,
                    StringComparison.Ordinal))
            {
                SetProjectDescendantsFree(
                    connection,
                    transaction,
                    projectId);
            }
            WriteProductionAssociation(
                connection,
                transaction,
                projectId,
                association with { Enabled = true });
            transaction.Commit();
        }
    }

    public void SetShotManagerProductionEnabled(
        string projectId,
        bool enabled)
    {
        lock (_context.WriteGate)
        {
            using var connection = _context.OpenConnection();
            using var transaction = connection.BeginTransaction();
            var association = GetProjectSettings(connection, projectId)
                .ShotManagerOutput;
            if (enabled)
            {
                _ = ShotManagerReadonlyContract.RequireSettings(
                    association with { Enabled = true },
                    $"Project '{projectId}' Shot Manager association");
                _context.Execute(
                    connection,
                    transaction,
                    "UPDATE projects SET production_output_mode = 'shot_manager' WHERE id = $id",
                    ("$id", projectId));
            }
            else
            {
                _context.Execute(
                    connection,
                    transaction,
                    "UPDATE projects SET production_output_mode = 'manual' WHERE id = $id",
                    ("$id", projectId));
                SetProjectDescendantsFree(
                    connection,
                    transaction,
                    projectId);
            }
            transaction.Commit();
        }
    }

    public void RefreshShotManagerProduction(
        string projectId,
        ShotManagerReadonlyProduction production)
    {
        lock (_context.WriteGate)
        {
            using var connection = _context.OpenConnection();
            using var transaction = connection.BeginTransaction();
            var current = GetProjectSettings(connection, projectId)
                .ShotManagerOutput;
            if (!current.Enabled
                || !current.ProductionId.Equals(
                    production.ProductionId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Shot Manager reconnection requires the same associated productionId.");
            }
            var association = ResolveProductionAssociation(
                production,
                current.WorkstreamName,
                current.FolderName) with { Enabled = true };
            WriteProductionAssociation(
                connection,
                transaction,
                projectId,
                association);
            RefreshEpisodeAssociations(
                connection,
                transaction,
                projectId,
                production);
            RefreshShotAssociations(
                connection,
                transaction,
                projectId,
                production);
            transaction.Commit();
        }
    }

    public void AssociateShotManagerEpisode(
        string episodeId,
        ShotManagerReadonlyEpisode? episode)
    {
        lock (_context.WriteGate)
        {
            using var connection = _context.OpenConnection();
            using var transaction = connection.BeginTransaction();
            var projectId = SqliteCommandExecutor.ScalarString(
                    connection,
                    "SELECT project_id FROM episodes WHERE id = $id",
                    ("$id", episodeId))
                ?? throw new InvalidOperationException(
                    $"Missing Episode '{episodeId}'.");
            var production = GetProjectSettings(connection, projectId)
                .ShotManagerOutput;
            if (episode is null)
            {
                _context.Execute(
                    connection,
                    transaction,
                    "UPDATE episodes SET shot_manager_association_state = 'free', shot_manager_reference_production_id = '', shot_manager_episode_id = '', shot_manager_episode_order = NULL, shot_manager_episode_slug = '' WHERE id = $id",
                    ("$id", episodeId));
            }
            else
            {
                if (!production.Enabled)
                    throw new InvalidOperationException(
                        "Associate the Project with Shot Manager before associating an Episode.");
                _context.Execute(
                    connection,
                    transaction,
                    "UPDATE episodes SET shot_manager_association_state = 'associated', shot_manager_reference_production_id = $productionId, shot_manager_episode_id = $episodeId, shot_manager_episode_order = $episodeOrder, shot_manager_episode_slug = $episodeSlug WHERE id = $id",
                    ("$id", episodeId),
                    ("$productionId", production.ProductionId),
                    ("$episodeId", episode.Id),
                    ("$episodeOrder", episode.Order),
                    ("$episodeSlug", episode.Slug));
            }
            _context.Execute(
                connection,
                transaction,
                "UPDATE shots SET shot_manager_association_state = 'free' WHERE episode_id = $episodeId",
                ("$episodeId", episodeId));
            transaction.Commit();
        }
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
        var slug = ProductionOutputContract.CreateEpisodeCode(
            GetProjectSettings(connection, projectId)
                .ProductionOutput.EpisodePrefix,
            sortOrder + 1);
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
        var slug = ProductionOutputContract.CreateEpisodeCode(
            GetProjectSettings(connection, source.ProjectId)
                .ProductionOutput.EpisodePrefix,
            sortOrder + 1);
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

    private static string RequireOutputMode(string value, string context)
    {
        if (value is not ("manual" or "shot_manager"))
        {
            throw new InvalidOperationException(
                $"{context}.mode must be 'manual' or 'shot_manager'.");
        }
        return value;
    }

    private static ShotManagerOutputSettings ResolveProductionAssociation(
        ShotManagerReadonlyProduction production,
        string workstreamName,
        string folderName)
    {
        var workstream = production.Workstreams.SingleOrDefault((candidate) =>
            candidate.Name.Equals(
                workstreamName,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Shot Manager Workstream '{workstreamName}' is not available.");
        var folder = workstream.Folders.SingleOrDefault((candidate) =>
            candidate.Name.Equals(
                folderName,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Shot Manager folder '{folderName}' is not available in Workstream '{workstream.Name}'.");
        return ShotManagerReadonlyContract.RequireSettings(
            new ShotManagerOutputSettings(
                true,
                production.ProductionId,
                production.ProductionSlug,
                production.SeasonSlug,
                workstream.Name,
                folder.Name,
                folder.Suffix),
            "Shot Manager Production association");
    }

    private void WriteProductionAssociation(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string projectId,
        ShotManagerOutputSettings association)
    {
        _context.Execute(
            connection,
            transaction,
            """
            UPDATE projects
            SET production_output_mode = $mode,
                shot_manager_production_id = $productionId,
                shot_manager_production_slug = $productionSlug,
                shot_manager_season_slug = $seasonSlug,
                shot_manager_workstream_name = $workstream,
                shot_manager_folder_name = $folder,
                shot_manager_folder_suffix = $suffix
            WHERE id = $id
            """,
            ("$id", projectId),
            ("$mode", association.Enabled ? "shot_manager" : "manual"),
            ("$productionId", association.ProductionId),
            ("$productionSlug", association.ProductionSlug),
            ("$seasonSlug", association.SeasonSlug),
            ("$workstream", association.WorkstreamName),
            ("$folder", association.FolderName),
            ("$suffix", association.FolderSuffix));
    }

    private void SetProjectDescendantsFree(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string projectId)
    {
        _context.Execute(
            connection,
            transaction,
            "UPDATE episodes SET shot_manager_association_state = 'free' WHERE project_id = $projectId",
            ("$projectId", projectId));
        _context.Execute(
            connection,
            transaction,
            "UPDATE shots SET shot_manager_association_state = 'free' WHERE episode_id IN (SELECT id FROM episodes WHERE project_id = $projectId)",
            ("$projectId", projectId));
    }

    private void RefreshEpisodeAssociations(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string projectId,
        ShotManagerReadonlyProduction production)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id, shot_manager_reference_production_id, shot_manager_episode_id FROM episodes WHERE project_id = $projectId";
        command.Parameters.AddWithValue("$projectId", projectId);
        var rows = new List<(string LocalId, string ProductionId, string EpisodeId)>();
        using (var reader = command.ExecuteReader())
            while (reader.Read())
                rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        foreach (var row in rows)
        {
            if (!row.ProductionId.Equals(production.ProductionId, StringComparison.Ordinal)
                || string.IsNullOrEmpty(row.EpisodeId)) continue;
            var external = production.Episodes.SingleOrDefault((candidate) =>
                candidate.Id.Equals(row.EpisodeId, StringComparison.Ordinal));
            _context.Execute(
                connection,
                transaction,
                external is null
                    ? "UPDATE episodes SET shot_manager_association_state = 'free' WHERE id = $id"
                    : "UPDATE episodes SET shot_manager_association_state = 'associated', shot_manager_episode_order = $episodeOrder, shot_manager_episode_slug = $episodeSlug WHERE id = $id",
                ("$id", row.LocalId),
                ("$episodeOrder", external?.Order),
                ("$episodeSlug", external?.Slug));
        }
    }

    private void RefreshShotAssociations(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string projectId,
        ShotManagerReadonlyProduction production)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT s.id, s.shot_manager_reference_production_id, s.shot_manager_shot_id, e.shot_manager_episode_id, e.shot_manager_association_state FROM shots s JOIN episodes e ON e.id = s.episode_id WHERE e.project_id = $projectId";
        command.Parameters.AddWithValue("$projectId", projectId);
        var rows = new List<(string LocalId, string ProductionId, string ShotId, string EpisodeId, string EpisodeState)>();
        using (var reader = command.ExecuteReader())
            while (reader.Read())
                rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        foreach (var row in rows)
        {
            if (!row.ProductionId.Equals(production.ProductionId, StringComparison.Ordinal)
                || string.IsNullOrEmpty(row.ShotId)) continue;
            var external = row.EpisodeState == "associated"
                ? production.Shots.SingleOrDefault((candidate) =>
                    candidate.Id.Equals(row.ShotId, StringComparison.Ordinal)
                    && candidate.EpisodeId.Equals(row.EpisodeId, StringComparison.Ordinal))
                : null;
            _context.Execute(
                connection,
                transaction,
                external is null
                    ? "UPDATE shots SET shot_manager_association_state = 'free' WHERE id = $id"
                    : "UPDATE shots SET shot_manager_association_state = 'associated', shot_manager_canonical_name = $canonicalName WHERE id = $id",
                ("$id", row.LocalId),
                ("$canonicalName", external?.CanonicalName));
        }
    }

}
