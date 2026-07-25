using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class ShotManagerIntegrationRepository : IShotManagerIntegrationRepository
{
    private readonly SqliteProjectContext _context;

    public ShotManagerIntegrationRepository(SqliteProjectContext context)
    {
        _context = context;
    }

    public ShotManagerProjectAssociationRecord? GetAssociation(string projectId)
    {
        using var connection = _context.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT project_id, production_id, production_name, season_id,
                   season_code, season_name, updated_at
            FROM shot_manager_project_associations
            WHERE project_id = $projectId
            """;
        command.Parameters.AddWithValue("$projectId", projectId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadAssociation(reader) : null;
    }

    public ShotManagerEpisodeBindingRecord? GetEpisodeBinding(string episodeId)
    {
        using var connection = _context.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT episode_id, project_id, external_episode_id,
                   episode_number, episode_code, updated_at
            FROM shot_manager_episode_bindings
            WHERE episode_id = $episodeId
            """;
        command.Parameters.AddWithValue("$episodeId", episodeId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadEpisodeBinding(reader) : null;
    }

    public ShotManagerShotStructureRecord? GetShotStructure(string shotId)
    {
        using var connection = _context.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT shot_id, plan_version, production_id, season_id, episode_id,
                   shot_number, shot_code, full_name, structure_json, created_at
            FROM shot_manager_shot_structures
            WHERE shot_id = $shotId
            """;
        command.Parameters.AddWithValue("$shotId", shotId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadShotStructure(reader) : null;
    }

    public int SuggestShotNumber(string episodeId)
    {
        using var connection = _context.OpenConnection();
        var maximum = SqliteCommandExecutor.ScalarLong(
            connection,
            """
            SELECT COALESCE(MAX(structure.shot_number), 0)
            FROM shot_manager_shot_structures structure
            JOIN shots ON shots.id = structure.shot_id
            WHERE shots.episode_id = $episodeId
            """,
            ("$episodeId", episodeId));
        return maximum <= 0
            ? 10
            : checked((int)((maximum / 10 + 1) * 10));
    }

    public IReadOnlyList<ShotManagerLocalEpisodeRecord> LoadLocalEpisodes(string projectId)
    {
        using var connection = _context.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.id, e.project_id, e.name, e.slug, e.notes, e.sort_order,
                   b.episode_id, b.project_id, b.external_episode_id,
                   b.episode_number, b.episode_code, b.updated_at,
                   EXISTS(SELECT 1 FROM shots s WHERE s.episode_id = e.id)
            FROM episodes e
            LEFT JOIN shot_manager_episode_bindings b ON b.episode_id = e.id
            WHERE e.project_id = $projectId
            ORDER BY e.sort_order, e.name, e.id
            """;
        command.Parameters.AddWithValue("$projectId", projectId);
        using var reader = command.ExecuteReader();
        var rows = new List<ShotManagerLocalEpisodeRecord>();
        while (reader.Read())
        {
            var episode = new EpisodeRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                SqliteCommandExecutor.ReadString(reader, 3),
                SqliteCommandExecutor.ReadString(reader, 4),
                reader.GetInt32(5));
            var binding = reader.IsDBNull(6)
                ? null
                : new ShotManagerEpisodeBindingRecord(
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.GetString(8),
                    reader.GetInt32(9),
                    reader.GetString(10),
                    reader.GetString(11));
            rows.Add(new ShotManagerLocalEpisodeRecord(
                episode,
                binding,
                reader.GetInt64(12) != 0));
        }
        return rows;
    }

    public void ApplyAssociation(ShotManagerAssociationWritePlan plan)
    {
        ValidateAssociation(plan.Association);
        if (plan.Upserts.Select((upsert) => upsert.Episode.Id)
                .Distinct(StringComparer.Ordinal).Count() != plan.Upserts.Count
            || plan.DeleteEpisodeIds.Distinct(StringComparer.Ordinal).Count()
                != plan.DeleteEpisodeIds.Count
            || plan.Upserts.Any((upsert) =>
                plan.DeleteEpisodeIds.Contains(
                    upsert.Episode.Id,
                    StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "A Shot Manager Episode synchronization plan is ambiguous.");
        }
        using var connection = _context.OpenConnection();
        if (SqliteCommandExecutor.ScalarLong(
            connection,
            "SELECT COUNT(*) FROM projects WHERE id = $projectId",
            ("$projectId", plan.Association.ProjectId)) != 1)
        {
            throw new InvalidOperationException(
                $"Missing Project '{plan.Association.ProjectId}'.");
        }
        foreach (var upsert in plan.Upserts)
        {
            if (!upsert.Episode.ProjectId.Equals(
                plan.Association.ProjectId,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Episode '{upsert.Episode.Id}' does not belong to Project '{plan.Association.ProjectId}'.");
            }
            ValidateEpisodeWrite(upsert);
            if (SqliteCommandExecutor.ScalarLong(
                connection,
                """
                SELECT COUNT(*)
                FROM episodes
                WHERE id = $episodeId AND project_id <> $projectId
                """,
                ("$episodeId", upsert.Episode.Id),
                ("$projectId", plan.Association.ProjectId)) != 0)
            {
                throw new InvalidOperationException(
                    $"Episode '{upsert.Episode.Id}' belongs to another Project.");
            }
        }

        lock (SqliteProjectContext.WriteGate)
        {
            using var transaction = connection.BeginTransaction();
            SqliteCommandExecutor.Execute(
                connection,
                transaction,
                """
                INSERT INTO shot_manager_project_associations (
                  project_id, production_id, production_name, season_id,
                  season_code, season_name, updated_at)
                VALUES (
                  $projectId, $productionId, $productionName, $seasonId,
                  $seasonCode, $seasonName, $updatedAt)
                ON CONFLICT(project_id) DO UPDATE SET
                  production_id = excluded.production_id,
                  production_name = excluded.production_name,
                  season_id = excluded.season_id,
                  season_code = excluded.season_code,
                  season_name = excluded.season_name,
                  updated_at = excluded.updated_at
                """,
                ("$projectId", plan.Association.ProjectId),
                ("$productionId", plan.Association.ProductionId),
                ("$productionName", plan.Association.ProductionName),
                ("$seasonId", plan.Association.SeasonId),
                ("$seasonCode", plan.Association.SeasonCode),
                ("$seasonName", plan.Association.SeasonName),
                ("$updatedAt", plan.Association.UpdatedAt));
            SqliteCommandExecutor.Execute(
                connection,
                transaction,
                "DELETE FROM shot_manager_episode_bindings WHERE project_id = $projectId",
                ("$projectId", plan.Association.ProjectId));
            foreach (var episodeId in plan.DeleteEpisodeIds)
            {
                using var delete = connection.CreateCommand();
                delete.Transaction = transaction;
                delete.CommandText = """
                    DELETE FROM episodes
                    WHERE id = $episodeId
                      AND project_id = $projectId
                      AND NOT EXISTS(
                        SELECT 1 FROM shots WHERE shots.episode_id = episodes.id)
                    """;
                delete.Parameters.AddWithValue("$episodeId", episodeId);
                delete.Parameters.AddWithValue(
                    "$projectId",
                    plan.Association.ProjectId);
                if (delete.ExecuteNonQuery() != 1)
                {
                    throw new InvalidOperationException(
                        $"Governed Episode '{episodeId}' could not be removed because its current state changed.");
                }
            }
            foreach (var upsert in plan.Upserts)
            {
                SqliteCommandExecutor.Execute(
                    connection,
                    transaction,
                    """
                    INSERT INTO episodes (
                      id, project_id, name, slug, notes, sort_order, metadata_json)
                    VALUES (
                      $id, $projectId, $name, $slug, $notes, $sortOrder, '{}')
                    ON CONFLICT(id) DO UPDATE SET
                      name = excluded.name,
                      slug = excluded.slug,
                      sort_order = excluded.sort_order
                    """,
                    ("$id", upsert.Episode.Id),
                    ("$projectId", upsert.Episode.ProjectId),
                    ("$name", upsert.Episode.Name),
                    ("$slug", upsert.Episode.Slug),
                    ("$notes", upsert.Episode.Notes),
                    ("$sortOrder", upsert.Episode.SortOrder));
                SqliteCommandExecutor.Execute(
                    connection,
                    transaction,
                    """
                    INSERT INTO shot_manager_episode_bindings (
                      episode_id, project_id, external_episode_id,
                      episode_number, episode_code, updated_at)
                    VALUES (
                      $episodeId, $projectId, $externalEpisodeId,
                      $episodeNumber, $episodeCode, $updatedAt)
                    """,
                    ("$episodeId", upsert.Episode.Id),
                    ("$projectId", plan.Association.ProjectId),
                    ("$externalEpisodeId", upsert.ExternalEpisodeId),
                    ("$episodeNumber", upsert.EpisodeNumber),
                    ("$episodeCode", upsert.EpisodeCode),
                    ("$updatedAt", plan.Association.UpdatedAt));
            }
            transaction.Commit();
        }
    }

    public void Disconnect(string projectId)
    {
        using var connection = _context.OpenConnection();
        SqliteCommandExecutor.Execute(
            connection,
            "DELETE FROM shot_manager_project_associations WHERE project_id = $projectId",
            ("$projectId", projectId));
    }

    public void ValidateGovernedShotContext(
        SqliteConnection connection,
        string episodeId,
        string productionId,
        string seasonId,
        string externalEpisodeId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM shot_manager_episode_bindings b
            JOIN shot_manager_project_associations a ON a.project_id = b.project_id
            JOIN episodes e ON e.id = b.episode_id AND e.project_id = b.project_id
            WHERE b.episode_id = $episodeId
              AND a.production_id = $productionId
              AND a.season_id = $seasonId
              AND b.external_episode_id = $externalEpisodeId
            """;
        command.Parameters.AddWithValue("$episodeId", episodeId);
        command.Parameters.AddWithValue("$productionId", productionId);
        command.Parameters.AddWithValue("$seasonId", seasonId);
        command.Parameters.AddWithValue("$externalEpisodeId", externalEpisodeId);
        if (Convert.ToInt64(command.ExecuteScalar()) != 1)
        {
            throw new InvalidOperationException(
                "The Shot Manager plan does not match the exact governed Project and Episode.");
        }
    }

    public void InsertShotStructure(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ShotManagerShotStructureRecord record)
    {
        ValidateShotStructure(record);
        SqliteCommandExecutor.Execute(
            connection,
            transaction,
            """
            INSERT INTO shot_manager_shot_structures (
              shot_id, plan_version, production_id, season_id, episode_id,
              shot_number, shot_code, full_name, structure_json, created_at)
            VALUES (
              $shotId, $planVersion, $productionId, $seasonId, $episodeId,
              $shotNumber, $shotCode, $fullName, $structureJson, $createdAt)
            """,
            ("$shotId", record.ShotId),
            ("$planVersion", record.PlanVersion),
            ("$productionId", record.ProductionId),
            ("$seasonId", record.SeasonId),
            ("$episodeId", record.EpisodeId),
            ("$shotNumber", record.ShotNumber),
            ("$shotCode", record.ShotCode),
            ("$fullName", record.FullName),
            ("$structureJson", record.StructureJson),
            ("$createdAt", record.CreatedAt));
    }

    private static ShotManagerProjectAssociationRecord ReadAssociation(
        SqliteDataReader reader)
    {
        var record = new ShotManagerProjectAssociationRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetString(6));
        ValidateAssociation(record);
        return record;
    }

    private static ShotManagerEpisodeBindingRecord ReadEpisodeBinding(
        SqliteDataReader reader)
    {
        var record = new ShotManagerEpisodeBindingRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetString(4),
            reader.GetString(5));
        if (string.IsNullOrWhiteSpace(record.EpisodeId)
            || string.IsNullOrWhiteSpace(record.ProjectId)
            || string.IsNullOrWhiteSpace(record.ExternalEpisodeId)
            || string.IsNullOrWhiteSpace(record.EpisodeCode)
            || record.EpisodeNumber <= 0
            || string.IsNullOrWhiteSpace(record.UpdatedAt))
        {
            throw new InvalidOperationException(
                "A current Shot Manager Episode binding is incomplete.");
        }
        return record;
    }

    private static ShotManagerShotStructureRecord ReadShotStructure(
        SqliteDataReader reader)
    {
        var record = new ShotManagerShotStructureRecord(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetString(9));
        ValidateShotStructure(record);
        return record;
    }

    private static void ValidateAssociation(
        ShotManagerProjectAssociationRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.ProjectId)
            || string.IsNullOrWhiteSpace(record.ProductionId)
            || string.IsNullOrWhiteSpace(record.ProductionName)
            || string.IsNullOrWhiteSpace(record.SeasonId)
            || string.IsNullOrWhiteSpace(record.SeasonCode)
            || string.IsNullOrWhiteSpace(record.UpdatedAt))
        {
            throw new InvalidOperationException(
                "A current Shot Manager Project association is incomplete.");
        }
    }

    private static void ValidateEpisodeWrite(ShotManagerEpisodeWrite write)
    {
        if (string.IsNullOrWhiteSpace(write.Episode.Id)
            || string.IsNullOrWhiteSpace(write.Episode.Name)
            || string.IsNullOrWhiteSpace(write.ExternalEpisodeId)
            || string.IsNullOrWhiteSpace(write.EpisodeCode)
            || write.EpisodeNumber <= 0)
        {
            throw new InvalidOperationException(
                "A current Shot Manager Episode write is incomplete.");
        }
    }

    private static void ValidateShotStructure(
        ShotManagerShotStructureRecord record)
    {
        if (record.PlanVersion != 1
            || record.ShotNumber <= 0
            || string.IsNullOrWhiteSpace(record.ShotId)
            || string.IsNullOrWhiteSpace(record.ProductionId)
            || string.IsNullOrWhiteSpace(record.SeasonId)
            || string.IsNullOrWhiteSpace(record.EpisodeId)
            || string.IsNullOrWhiteSpace(record.ShotCode)
            || string.IsNullOrWhiteSpace(record.FullName)
            || string.IsNullOrWhiteSpace(record.CreatedAt))
        {
            throw new InvalidOperationException(
                "A current Shot Manager Shot structure is incomplete.");
        }
        ShotManagerPortableStructure.Parse(
            record.StructureJson,
            $"Shot Manager Shot '{record.ShotId}' structure_json");
    }
}
