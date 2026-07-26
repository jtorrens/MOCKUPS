using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class IconThemeRepository : IIconThemeRepository
{
    private readonly SqliteProjectContext _context;

    public IconThemeRepository(SqliteProjectContext context)
    {
        _context = context;
    }

    public IconThemeRecord Get(string iconThemeId)
    {
        using var connection = _context.OpenConnection();
        return Get(connection, iconThemeId);
    }

    public IconThemeRecord Get(SqliteConnection connection, string iconThemeId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, asset_root, mapping_json, metadata_json FROM icon_themes WHERE id = $id";
        command.Parameters.AddWithValue("$id", iconThemeId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing icon theme '{iconThemeId}'.");
        }

        return ReadRecord(reader);
    }

    public IReadOnlyList<IconThemeRecord> QueryAll(SqliteConnection connection)
    {
        var rows = new List<IconThemeRecord>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, asset_root, mapping_json, metadata_json FROM icon_themes ORDER BY name, id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(ReadRecord(reader));
        }

        return rows;
    }

    public IconThemeRecord UpsertDiscovered(
        SqliteConnection connection,
        string id,
        string projectId,
        string name,
        string assetRoot,
        string metadataJson)
    {
        JsonPath.ParseRequiredObject(metadataJson, $"Icon Theme '{id}' metadata_json");
        SqliteCommandExecutor.Execute(
            connection,
            """
            INSERT INTO icon_themes (id, project_id, name, asset_root, mapping_json, metadata_json)
            VALUES ($id, $projectId, $name, $assetRoot, '{}', $metadataJson)
            ON CONFLICT(project_id, name) DO UPDATE SET
              asset_root = excluded.asset_root,
              metadata_json = excluded.metadata_json
            """,
            ("$id", id),
            ("$projectId", projectId),
            ("$name", name),
            ("$assetRoot", assetRoot),
            ("$metadataJson", metadataJson));
        return QueryAll(connection)
            .Single((row) => row.ProjectId == projectId && row.Name == name);
    }

    public IconThemeRecord CreateDuplicate(
        SqliteConnection connection,
        string sourceId,
        string id,
        string name,
        string assetRoot,
        string metadataJson)
    {
        JsonPath.ParseRequiredObject(metadataJson, $"Icon Theme '{id}' metadata_json");
        var source = Get(connection, sourceId);
        SqliteCommandExecutor.Execute(
            connection,
            """
            INSERT INTO icon_themes (id, project_id, name, asset_root, mapping_json, metadata_json)
            VALUES ($id, $projectId, $name, $assetRoot, $mappingJson, $metadataJson)
            """,
            ("$id", id),
            ("$projectId", source.ProjectId),
            ("$name", name),
            ("$assetRoot", assetRoot),
            ("$mappingJson", source.MappingJson),
            ("$metadataJson", metadataJson));
        return Get(connection, id);
    }

    public void UpdateMapping(SqliteConnection connection, string iconThemeId, string mappingJson)
    {
        JsonPath.ParseRequiredObject(mappingJson, $"Icon Theme '{iconThemeId}' mapping_json");
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE icon_themes SET mapping_json = $mappingJson WHERE id = $id",
            ("$id", iconThemeId),
            ("$mappingJson", mappingJson));
    }

    public void UpdateIdentity(
        SqliteConnection connection,
        string iconThemeId,
        string name,
        string assetRoot,
        string metadataJson)
    {
        JsonPath.ParseRequiredObject(metadataJson, $"Icon Theme '{iconThemeId}' metadata_json");
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE icon_themes SET name = $name, asset_root = $assetRoot, metadata_json = $metadataJson WHERE id = $id",
            ("$id", iconThemeId),
            ("$name", name),
            ("$assetRoot", assetRoot),
            ("$metadataJson", metadataJson));
    }

    public void Delete(SqliteConnection connection, string iconThemeId)
    {
        SqliteCommandExecutor.Execute(connection, "DELETE FROM icon_themes WHERE id = $id", ("$id", iconThemeId));
    }

    private static IconThemeRecord ReadRecord(SqliteDataReader reader)
    {
        var record = new IconThemeRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            SqliteCommandExecutor.ReadString(reader, 3),
            SqliteCommandExecutor.ReadString(reader, 4),
            SqliteCommandExecutor.ReadString(reader, 5));
        JsonPath.ParseRequiredObject(record.MappingJson, $"Icon Theme '{record.Id}' mapping_json");
        JsonPath.ParseRequiredObject(record.MetadataJson, $"Icon Theme '{record.Id}' metadata_json");
        return record;
    }
}
