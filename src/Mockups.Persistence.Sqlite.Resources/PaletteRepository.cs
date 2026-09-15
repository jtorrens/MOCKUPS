using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class PaletteRepository : IPaletteRepository
{
    private readonly SqliteProjectContext _context;

    public PaletteRepository(SqliteProjectContext context)
    {
        _context = context;
    }

    public PaletteColorSettings GetSystemSettings(string colorId)
    {
        using var connection = _context.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT token, default_value_hex, metadata_json, is_neutral
            FROM palette_colors
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", colorId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing System Palette color '{colorId}'.");
        }

        var metadataJson = SqliteCommandExecutor.ReadString(reader, 2);
        return new PaletteColorSettings(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(3) != 0,
            MetadataString(metadataJson, "source"),
            MetadataBool(metadataJson, "protected"),
            MetadataBool(metadataJson, "hiddenFromPickers"),
            MetadataString(metadataJson, "note"));
    }

    public ProductionPaletteColorSettings GetProductionSettings(
        string projectId,
        string colorId)
    {
        using var connection = _context.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT c.token, v.value_hex
            FROM palette_colors c
            JOIN production_palette_values v ON v.palette_color_id = c.id
            WHERE c.id = $id AND v.project_id = $projectId
            """;
        command.Parameters.AddWithValue("$id", colorId);
        command.Parameters.AddWithValue("$projectId", projectId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException(
                $"Missing Production Palette value for color '{colorId}' in Production '{projectId}'.");
        }
        return new ProductionPaletteColorSettings(colorId, reader.GetString(0), reader.GetString(1));
    }

    public void UpdateSystemField(string colorId, string fieldId, string value)
    {
        using var connection = _context.OpenConnection();
        switch (fieldId)
        {
            case "palette.token":
                var token = value.Trim();
                if (string.IsNullOrWhiteSpace(token))
                    throw new InvalidOperationException("System Palette token cannot be empty.");
                _context.Execute(connection, "UPDATE palette_colors SET token = $value WHERE id = $id", ("$id", colorId), ("$value", token));
                return;
            case "palette.defaultValueHex":
                _context.Execute(connection, "UPDATE palette_colors SET default_value_hex = $value WHERE id = $id", ("$id", colorId), ("$value", HexColorText.Normalize(value)));
                return;
            case "palette.isNeutral":
                _context.Execute(connection, "UPDATE palette_colors SET is_neutral = $value WHERE id = $id", ("$id", colorId), ("$value", BooleanText.ParseRequired(value, fieldId) ? 1 : 0));
                return;
            case "palette.source":
                UpdateMetadata(connection, colorId, "source", value);
                return;
            case "palette.protected":
                UpdateMetadata(connection, colorId, "protected", BooleanText.ParseRequired(value, fieldId));
                return;
            case "palette.hiddenFromPickers":
                UpdateMetadata(connection, colorId, "hiddenFromPickers", BooleanText.ParseRequired(value, fieldId));
                return;
            case "palette.note":
                UpdateMetadata(connection, colorId, "note", value);
                return;
            default:
                throw new InvalidOperationException($"Unknown System Palette field '{fieldId}'.");
        }
    }

    public void UpdateProductionValue(string projectId, string colorId, string value)
    {
        using var connection = _context.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE production_palette_values SET value_hex = $value WHERE project_id = $projectId AND palette_color_id = $id";
        command.Parameters.AddWithValue("$projectId", projectId);
        command.Parameters.AddWithValue("$id", colorId);
        command.Parameters.AddWithValue("$value", HexColorText.Normalize(value));
        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException(
                $"Missing Production Palette value for color '{colorId}' in Production '{projectId}'.");
        }
    }

    public PaletteColorRecord RequireRecord(
        SqliteConnection connection,
        string colorId)
    {
        return QueryAll(connection).SingleOrDefault((color) => color.Id == colorId)
            ?? throw new InvalidOperationException($"Missing palette color '{colorId}'.");
    }

    public void RenameToken(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string colorId,
        string token)
    {
        _context.Execute(
            connection,
            transaction,
            "UPDATE palette_colors SET token = $token WHERE id = $id",
            ("$id", colorId),
            ("$token", token));
    }

    public IReadOnlyList<PaletteColorOption> GetOptions(string projectId)
    {
        using var connection = _context.OpenConnection();
        return QueryProject(connection, projectId)
            .OrderBy((color) => color.Token)
            .Select((color) => new PaletteColorOption(color.Id, color.Token, color.ValueHex, color.IsNeutral))
            .ToList();
    }

    public IReadOnlyDictionary<string, string> GetColorMap(string projectId)
    {
        using var connection = _context.OpenConnection();
        return QueryProject(connection, projectId)
            .GroupBy((color) => color.Id, StringComparer.Ordinal)
            .ToDictionary(
                (group) => group.Key,
                (group) => group.First().ValueHex,
                StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, bool> GetNeutralMap(string projectId)
    {
        using var connection = _context.OpenConnection();
        return QueryProject(connection, projectId)
            .GroupBy((color) => color.Id, StringComparer.Ordinal)
            .ToDictionary(
                (group) => group.Key,
                (group) => group.First().IsNeutral,
                StringComparer.Ordinal);
    }

    public IReadOnlyList<PaletteColorRecord> QueryAll(SqliteConnection connection)
    {
        var rows = new List<PaletteColorRecord>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, token, default_value_hex, metadata_json, is_neutral FROM palette_colors ORDER BY token";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var metadataJson = SqliteCommandExecutor.ReadString(reader, 3);
            rows.Add(new PaletteColorRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                MetadataString(metadataJson, "note"),
                reader.GetInt32(4) != 0,
                metadataJson));
        }

        return rows;
    }

    public IReadOnlyList<ProductionPaletteColorRecord> QueryAllProductionValues(SqliteConnection connection)
    {
        var rows = new List<ProductionPaletteColorRecord>();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT p.id, c.id, c.token, v.value_hex, c.metadata_json, c.is_neutral
            FROM projects p
            CROSS JOIN palette_colors c
            LEFT JOIN production_palette_values v
              ON v.project_id = p.id AND v.palette_color_id = c.id
            ORDER BY p.id, c.token
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.IsDBNull(3))
            {
                throw new InvalidOperationException(
                    $"Missing Production Palette value for color '{reader.GetString(1)}' in Production '{reader.GetString(0)}'.");
            }
            var metadataJson = SqliteCommandExecutor.ReadString(reader, 4);
            rows.Add(new ProductionPaletteColorRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                MetadataString(metadataJson, "note"),
                reader.GetInt32(5) != 0,
                metadataJson));
        }
        return rows;
    }

    private IReadOnlyList<ProductionPaletteColorRecord> QueryProject(
        SqliteConnection connection,
        string projectId)
    {
        return QueryAllProductionValues(connection)
            .Where((color) => color.ProjectId == projectId)
            .ToList();
    }

    public PaletteColorRecord CreateSystemColor(
        SqliteConnection connection,
        string token,
        string defaultValueHex)
    {
        var id = $"palette_{Guid.NewGuid():N}";
        token = token.Trim();
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("System Palette token cannot be empty.");
        var valueHex = HexColorText.Normalize(defaultValueHex);
        const string note = "System palette primitive color.";
        var metadataJson = new JsonObject { ["note"] = note }.ToJsonString();
        using var transaction = connection.BeginTransaction();
        _context.Execute(
            connection,
            transaction,
            "INSERT INTO palette_colors (id, token, default_value_hex, metadata_json, is_neutral) VALUES ($id, $token, $value, $metadata, 1)",
            ("$id", id), ("$token", token), ("$value", valueHex), ("$metadata", metadataJson));
        _context.Execute(
            connection,
            transaction,
            "INSERT INTO production_palette_values (project_id, palette_color_id, value_hex) SELECT id, $colorId, $value FROM projects",
            ("$colorId", id), ("$value", valueHex));
        transaction.Commit();
        return new PaletteColorRecord(id, token, valueHex, note, true, metadataJson);
    }

    public void CreateProductionValuesForProject(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string projectId)
    {
        _context.Execute(
            connection,
            transaction,
            """
            INSERT INTO production_palette_values (
              project_id,
              palette_color_id,
              value_hex)
            SELECT $projectId, id, default_value_hex
            FROM palette_colors
            """,
            ("$projectId", projectId));
    }

    public PaletteColorRecord DuplicateSystemColor(
        SqliteConnection connection,
        string sourceId)
    {
        var source = RequireRecord(connection, sourceId);
        using var transaction = connection.BeginTransaction();
        var copy = source with
        {
            Id = $"palette_{Guid.NewGuid():N}",
            Token = UniqueToken(connection, $"{source.Token}_copy"),
        };
        _context.Execute(
            connection,
            transaction,
            "INSERT INTO palette_colors (id, token, default_value_hex, metadata_json, is_neutral) VALUES ($id, $token, $value, $metadata, $neutral)",
            ("$id", copy.Id), ("$token", copy.Token), ("$value", copy.DefaultValueHex),
            ("$metadata", copy.MetadataJson), ("$neutral", copy.IsNeutral ? 1 : 0));
        _context.Execute(
            connection,
            transaction,
            "INSERT INTO production_palette_values (project_id, palette_color_id, value_hex) SELECT project_id, $copyId, value_hex FROM production_palette_values WHERE palette_color_id = $sourceId",
            ("$copyId", copy.Id), ("$sourceId", sourceId));
        transaction.Commit();
        return copy;
    }

    private static string UniqueToken(SqliteConnection connection, string candidate)
    {
        var token = candidate;
        for (var suffix = 2;
             SqliteCommandExecutor.ScalarLong(
                 connection,
                 "SELECT COUNT(*) FROM palette_colors WHERE token = $token",
                 ("$token", token)) != 0;
             suffix++)
        {
            token = $"{candidate}_{suffix}";
        }
        return token;
    }

    public void Delete(SqliteConnection connection, string colorId)
    {
        _context.Execute(connection, "DELETE FROM palette_colors WHERE id = $id", ("$id", colorId));
    }

    public void UpdateNode(SqliteConnection connection, string colorId, string token, string note)
    {
        _context.Execute(
            connection,
            "UPDATE palette_colors SET token = $token WHERE id = $id",
            ("$id", colorId),
            ("$token", token));
        UpdateMetadata(connection, colorId, "note", note);
    }

    private void UpdateMetadata(SqliteConnection connection, string colorId, string key, object value)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT metadata_json FROM palette_colors WHERE id = $id";
        select.Parameters.AddWithValue("$id", colorId);
        var metadataJson = select.ExecuteScalar() as string
            ?? throw new InvalidOperationException($"Missing palette color '{colorId}'.");
        var metadata = JsonPath.ParseRequiredObject(metadataJson, $"Palette color '{colorId}' metadata_json");
        metadata[key] = JsonSerializer.SerializeToNode(value);
        _context.Execute(
            connection,
            "UPDATE palette_colors SET metadata_json = $metadataJson WHERE id = $id",
            ("$id", colorId),
            ("$metadataJson", metadata.ToJsonString()));
    }

    private static string MetadataString(string metadataJson, string key)
    {
        return JsonPath.String(JsonPath.ParseRequiredObject(metadataJson, "Palette metadata_json"), [key]);
    }

    private static bool MetadataBool(string metadataJson, string key)
    {
        var metadata = JsonPath.ParseRequiredObject(metadataJson, "Palette metadata_json");
        if (metadata[key] is not JsonValue value)
        {
            return false;
        }

        if (value.TryGetValue<bool>(out var boolean))
        {
            return boolean;
        }

        throw new InvalidOperationException(
            $"Palette metadata_json '{key}' must be an explicit JSON boolean when present.");
    }
}
