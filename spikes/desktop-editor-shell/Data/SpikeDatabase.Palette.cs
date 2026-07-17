using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public PaletteColorSettings GetPaletteColorSettings(string colorId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT token, value_hex, metadata_json, is_neutral FROM palette_colors WHERE id = $id";
        command.Parameters.AddWithValue("$id", colorId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing palette color '{colorId}'.");
        }

        var metadataJson = ReadString(reader, 2);
        return new PaletteColorSettings(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(3) != 0,
            MetadataString(metadataJson, "source"),
            MetadataBool(metadataJson, "protected"),
            MetadataBool(metadataJson, "hiddenFromPickers"),
            MetadataString(metadataJson, "note"));
    }

    public void UpdatePaletteColorField(string colorId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        switch (fieldId)
        {
            case "palette.token":
                Execute(connection, "UPDATE palette_colors SET token = $value WHERE id = $id", ("$id", colorId), ("$value", value));
                return;
            case "palette.valueHex":
                Execute(connection, "UPDATE palette_colors SET value_hex = $value WHERE id = $id", ("$id", colorId), ("$value", ColorValue.NormalizeHex(value)));
                return;
            case "palette.isNeutral":
                Execute(connection, "UPDATE palette_colors SET is_neutral = $value WHERE id = $id", ("$id", colorId), ("$value", StringToBool(value) ? 1 : 0));
                return;
            case "palette.source":
                UpdatePaletteMetadata(connection, colorId, "source", value);
                return;
            case "palette.protected":
                UpdatePaletteMetadata(connection, colorId, "protected", StringToBool(value));
                return;
            case "palette.hiddenFromPickers":
                UpdatePaletteMetadata(connection, colorId, "hiddenFromPickers", StringToBool(value));
                return;
            case "palette.note":
                UpdatePaletteMetadata(connection, colorId, "note", value);
                return;
            default:
                throw new InvalidOperationException($"Unknown palette field '{fieldId}'.");
        }
    }

    public IReadOnlyList<FieldOption> GetPaletteColorOptions(string projectId)
    {
        using var connection = OpenConnection();
        return QueryPaletteColorRows(connection)
            .Where((color) => color.ProjectId == projectId)
            .OrderBy((color) => color.Token)
            .Select((color) => new FieldOption(color.Token, color.Token, color.ValueHex, color.IsNeutral))
            .ToList();
    }

    public IReadOnlyDictionary<string, string> GetPaletteColorMap(string projectId)
    {
        using var connection = OpenConnection();
        return QueryPaletteColorRows(connection)
            .Where((color) => color.ProjectId == projectId)
            .GroupBy((color) => color.Token, StringComparer.Ordinal)
            .ToDictionary(
                (group) => group.Key,
                (group) => group.First().ValueHex,
                StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, bool> GetPaletteNeutralMap(string projectId)
    {
        using var connection = OpenConnection();
        return QueryPaletteColorRows(connection)
            .Where((color) => color.ProjectId == projectId)
            .GroupBy((color) => color.Token, StringComparer.Ordinal)
            .ToDictionary(
                (group) => group.Key,
                (group) => group.First().IsNeutral,
                StringComparer.Ordinal);
    }

    private static List<PaletteColorRow> QueryPaletteColorRows(SqliteConnection connection)
    {
        var rows = new List<PaletteColorRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, token, value_hex, metadata_json, is_neutral FROM palette_colors ORDER BY token";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var metadataJson = ReadString(reader, 4);
            rows.Add(new PaletteColorRow(
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

    private static void UpdatePaletteMetadata(SqliteConnection connection, string colorId, string key, object value)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT metadata_json FROM palette_colors WHERE id = $id";
        select.Parameters.AddWithValue("$id", colorId);
        var metadataJson = select.ExecuteScalar() as string
            ?? throw new InvalidOperationException($"Missing palette color '{colorId}'.");
        var metadata = ParseJsonObject(metadataJson);
        metadata[key] = JsonSerializer.SerializeToNode(value);
        Execute(
            connection,
            "UPDATE palette_colors SET metadata_json = $metadataJson WHERE id = $id",
            ("$id", colorId),
            ("$metadataJson", metadata.ToJsonString()));
    }

}
