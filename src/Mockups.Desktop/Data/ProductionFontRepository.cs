using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class ProductionFontRepository : IProductionFontRepository
{
    private readonly SqliteProjectContext _context;

    public ProductionFontRepository(SqliteProjectContext context)
    {
        _context = context;
    }

    public ProductionFontRecord Get(string fontId)
    {
        using var connection = _context.OpenConnection();
        return Get(connection, fontId);
    }

    public ProductionFontRecord Get(SqliteConnection connection, string fontId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, family_name, category, source_directory, files_json, metadata_json FROM production_fonts WHERE id = $id";
        command.Parameters.AddWithValue("$id", fontId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing production font '{fontId}'.");
        }

        return ReadRecord(reader);
    }

    public IReadOnlyList<ProductionFontRecord> QueryAll(SqliteConnection connection)
    {
        var rows = new List<ProductionFontRecord>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, family_name, category, source_directory, files_json, metadata_json FROM production_fonts ORDER BY family_name, id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(ReadRecord(reader));
        }

        return rows;
    }

    public void UpdateField(string fontId, string fieldId, string value)
    {
        var column = fieldId switch
        {
            "font.family" => "family_name",
            "font.category" => "category",
            _ => throw new InvalidOperationException($"Unknown editable production font field '{fieldId}'."),
        };
        using var connection = _context.OpenConnection();
        SqliteCommandExecutor.Execute(
            connection,
            $"UPDATE production_fonts SET {column} = $value WHERE id = $id",
            ("$id", fontId),
            ("$value", value));
    }

    public ProductionFontRecord UpsertImported(
        SqliteConnection connection,
        string projectId,
        string familyName,
        string category,
        string sourceDirectory,
        string filesJson)
    {
        ProductionFontFilesContract.ParseRequired(
            filesJson,
            $"Production Font '{familyName}' files_json");
        using var existing = connection.CreateCommand();
        existing.CommandText = "SELECT id FROM production_fonts WHERE project_id = $projectId AND family_name = $familyName";
        existing.Parameters.AddWithValue("$projectId", projectId);
        existing.Parameters.AddWithValue("$familyName", familyName);
        var existingId = existing.ExecuteScalar() as string ?? "";
        var id = string.IsNullOrWhiteSpace(existingId) ? $"font_{Guid.NewGuid():N}" : existingId;
        if (string.IsNullOrWhiteSpace(existingId))
        {
            SqliteCommandExecutor.Execute(
                connection,
                """
                INSERT INTO production_fonts (id, project_id, family_name, category, source_directory, files_json, metadata_json)
                VALUES ($id, $projectId, $familyName, $category, $sourceDirectory, $filesJson, '{}')
                """,
                ("$id", id),
                ("$projectId", projectId),
                ("$familyName", familyName),
                ("$category", category),
                ("$sourceDirectory", sourceDirectory),
                ("$filesJson", filesJson));
        }
        else
        {
            SqliteCommandExecutor.Execute(
                connection,
                """
                UPDATE production_fonts
                SET category = $category,
                    source_directory = $sourceDirectory,
                    files_json = $filesJson
                WHERE id = $id
                """,
                ("$id", id),
                ("$category", category),
                ("$sourceDirectory", sourceDirectory),
                ("$filesJson", filesJson));
        }

        return Get(connection, id);
    }

    public void Delete(SqliteConnection connection, string fontId)
    {
        SqliteCommandExecutor.Execute(connection, "DELETE FROM production_fonts WHERE id = $id", ("$id", fontId));
    }

    public void Rename(SqliteConnection connection, string fontId, string name)
    {
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE production_fonts SET family_name = $name WHERE id = $id",
            ("$id", fontId),
            ("$name", name));
    }

    private static ProductionFontRecord ReadRecord(SqliteDataReader reader)
    {
        var record = new ProductionFontRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            SqliteCommandExecutor.ReadString(reader, 3),
            SqliteCommandExecutor.ReadString(reader, 4),
            SqliteCommandExecutor.ReadString(reader, 5),
            SqliteCommandExecutor.ReadString(reader, 6));
        ProductionFontFilesContract.ParseRequired(
            record.FilesJson,
            $"Production Font '{record.Id}' files_json");
        JsonPath.ParseRequiredObject(record.MetadataJson, $"Production Font '{record.Id}' metadata_json");
        return record;
    }
}
