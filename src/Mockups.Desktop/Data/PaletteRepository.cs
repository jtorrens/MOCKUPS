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

    public PaletteColorSettings GetSettings(string colorId)
    {
        using var connection = _context.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT token, value_hex, metadata_json, is_neutral FROM palette_colors WHERE id = $id";
        command.Parameters.AddWithValue("$id", colorId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing palette color '{colorId}'.");
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

    public void UpdateField(string colorId, string fieldId, string value)
    {
        using var connection = _context.OpenConnection();
        switch (fieldId)
        {
            case "palette.token":
                SqliteCommandExecutor.Execute(connection, "UPDATE palette_colors SET token = $value WHERE id = $id", ("$id", colorId), ("$value", value));
                return;
            case "palette.valueHex":
                SqliteCommandExecutor.Execute(connection, "UPDATE palette_colors SET value_hex = $value WHERE id = $id", ("$id", colorId), ("$value", ColorValue.NormalizeHex(value)));
                return;
            case "palette.isNeutral":
                SqliteCommandExecutor.Execute(connection, "UPDATE palette_colors SET is_neutral = $value WHERE id = $id", ("$id", colorId), ("$value", BooleanText.ParseRequired(value, fieldId) ? 1 : 0));
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
                throw new InvalidOperationException($"Unknown palette field '{fieldId}'.");
        }
    }

    public IReadOnlyList<PaletteColorOption> GetOptions(string projectId)
    {
        using var connection = _context.OpenConnection();
        return QueryAll(connection)
            .Where((color) => color.ProjectId == projectId)
            .OrderBy((color) => color.Token)
            .Select((color) => new PaletteColorOption(color.Token, color.Token, color.ValueHex, color.IsNeutral))
            .ToList();
    }

    public IReadOnlyDictionary<string, string> GetColorMap(string projectId)
    {
        using var connection = _context.OpenConnection();
        return QueryAll(connection)
            .Where((color) => color.ProjectId == projectId)
            .GroupBy((color) => color.Token, StringComparer.Ordinal)
            .ToDictionary(
                (group) => group.Key,
                (group) => group.First().ValueHex,
                StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, bool> GetNeutralMap(string projectId)
    {
        using var connection = _context.OpenConnection();
        return QueryAll(connection)
            .Where((color) => color.ProjectId == projectId)
            .GroupBy((color) => color.Token, StringComparer.Ordinal)
            .ToDictionary(
                (group) => group.Key,
                (group) => group.First().IsNeutral,
                StringComparer.Ordinal);
    }

    public IReadOnlyList<PaletteColorRecord> QueryAll(SqliteConnection connection)
    {
        var rows = new List<PaletteColorRecord>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, token, value_hex, metadata_json, is_neutral FROM palette_colors ORDER BY token";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var metadataJson = SqliteCommandExecutor.ReadString(reader, 4);
            rows.Add(new PaletteColorRecord(
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

    public PaletteColorRecord Create(SqliteConnection connection, string projectId)
    {
        var id = $"palette_{Guid.NewGuid():N}";
        var token = $"color_{SqliteCommandExecutor.ScalarLong(connection, "SELECT COUNT(*) FROM palette_colors WHERE project_id = $projectId", ("$projectId", projectId)) + 1}";
        const string valueHex = "#808080";
        const string note = "Project palette primitive color.";
        var metadataJson = new JsonObject { ["note"] = note }.ToJsonString();
        SqliteCommandExecutor.Execute(
            connection,
            """
            INSERT INTO palette_colors (id, project_id, token, value_hex, metadata_json, is_neutral)
            VALUES ($id, $projectId, $token, $valueHex, $metadataJson, 1)
            """,
            ("$id", id),
            ("$projectId", projectId),
            ("$token", token),
            ("$valueHex", valueHex),
            ("$metadataJson", metadataJson));

        return new PaletteColorRecord(id, projectId, token, valueHex, note, true, metadataJson);
    }

    public PaletteColorRecord Duplicate(SqliteConnection connection, string sourceId)
    {
        var source = QueryAll(connection).SingleOrDefault((color) => color.Id == sourceId)
            ?? throw new InvalidOperationException($"Missing palette color '{sourceId}'.");
        var copy = source with
        {
            Id = $"palette_{Guid.NewGuid():N}",
            Token = $"{source.Token}_copy",
        };
        SqliteCommandExecutor.Execute(
            connection,
            """
            INSERT INTO palette_colors (id, project_id, token, value_hex, metadata_json, is_neutral)
            VALUES ($id, $projectId, $token, $valueHex, $metadataJson, $isNeutral)
            """,
            ("$id", copy.Id),
            ("$projectId", copy.ProjectId),
            ("$token", copy.Token),
            ("$valueHex", copy.ValueHex),
            ("$metadataJson", copy.MetadataJson),
            ("$isNeutral", copy.IsNeutral ? 1 : 0));
        return copy;
    }

    public void Delete(SqliteConnection connection, string colorId)
    {
        SqliteCommandExecutor.Execute(connection, "DELETE FROM palette_colors WHERE id = $id", ("$id", colorId));
    }

    public void UpdateNode(SqliteConnection connection, string colorId, string token, string note)
    {
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE palette_colors SET token = $token WHERE id = $id",
            ("$id", colorId),
            ("$token", token));
        UpdateMetadata(connection, colorId, "note", note);
    }

    private static void UpdateMetadata(SqliteConnection connection, string colorId, string key, object value)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT metadata_json FROM palette_colors WHERE id = $id";
        select.Parameters.AddWithValue("$id", colorId);
        var metadataJson = select.ExecuteScalar() as string
            ?? throw new InvalidOperationException($"Missing palette color '{colorId}'.");
        var metadata = JsonPath.ParseRequiredObject(metadataJson, $"Palette color '{colorId}' metadata_json");
        metadata[key] = JsonSerializer.SerializeToNode(value);
        SqliteCommandExecutor.Execute(
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
