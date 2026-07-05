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

    private static void SeedPaletteColorsIfEmpty(SqliteConnection connection)
    {
        var projectIds = QueryProjectRows(connection).Select((project) => project.Id).ToList();
        foreach (var projectId in projectIds)
        {
            if (ScalarLong(connection, "SELECT COUNT(*) FROM palette_colors WHERE project_id = $projectId", ("$projectId", projectId)) > 0)
            {
                continue;
            }

            foreach (var seed in PaletteSeedRows)
            {
                Execute(
                    connection,
                    """
                    INSERT INTO palette_colors (id, project_id, token, value_hex, metadata_json, is_neutral)
                    VALUES ($id, $projectId, $token, $valueHex, $metadataJson, $isNeutral)
                    """,
                    ("$id", $"palette_{projectId}_{seed.Token}"),
                    ("$projectId", projectId),
                    ("$token", seed.Token),
                    ("$valueHex", seed.ValueHex),
                    ("$metadataJson", seed.MetadataJson),
                    ("$isNeutral", seed.IsNeutral ? 1 : 0));
            }
        }
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
        var metadataJson = select.ExecuteScalar() as string ?? "{}";

        var metadata = new Dictionary<string, object?>();
        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                metadata[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number when property.Value.TryGetInt64(out var number) => number,
                    JsonValueKind.String => property.Value.GetString(),
                    _ => property.Value.GetRawText(),
                };
            }
        }
        catch (JsonException)
        {
            // Invalid metadata is replaced by the field being edited. This is project data,
            // but the table is internal to the spike and metadata must remain valid JSON.
        }

        metadata[key] = value;
        Execute(
            connection,
            "UPDATE palette_colors SET metadata_json = $metadataJson WHERE id = $id",
            ("$id", colorId),
            ("$metadataJson", JsonSerializer.Serialize(metadata)));
    }

    private static readonly PaletteSeedRow[] PaletteSeedRows =
    [
        new("aqua_green", "#79D2B0", false, """{"note":"Production palette primitive color."}"""),
        new("blue", "#007AFF", false, """{"source":"ios_seed_theme","note":"Primitive color seeded from the original iOS theme values."}"""),
        new("blue_bright", "#0A84FF", false, """{"source":"ios_seed_theme","note":"Primitive color seeded from the original iOS theme values."}"""),
        new("debug_red", "#FF00FF", false, """{"source":"debug_sentinel","protected":true,"hiddenFromPickers":true,"note":"Protected sentinel color for unresolved theme/component color decisions."}"""),
        new("gray_000", "#000000", true, """{"note":"Neutral palette color."}"""),
        new("gray_010", "#1A1A1A", true, """{"note":"Neutral palette color."}"""),
        new("gray_020", "#333333", true, """{"note":"Neutral palette color."}"""),
        new("gray_030", "#4D4D4D", true, """{"note":"Neutral palette color."}"""),
        new("gray_040", "#666666", true, """{"note":"Neutral palette color."}"""),
        new("gray_050", "#808080", true, """{"note":"Neutral palette color."}"""),
        new("gray_060", "#999999", true, """{"note":"Neutral palette color."}"""),
        new("gray_070", "#B3B3B3", true, """{"note":"Neutral palette color."}"""),
        new("gray_080", "#CCCCCC", true, """{"note":"Neutral palette color."}"""),
        new("gray_090", "#E6E6E6", true, """{"note":"Neutral palette color."}"""),
        new("gray_100", "#FFFFFF", true, """{"note":"Neutral palette color."}"""),
        new("pastel_coral", "#FF8A80", false, """{"note":"Actor differentiator palette color."}"""),
        new("pastel_lavender", "#B39DDB", false, """{"note":"Actor differentiator palette color."}"""),
        new("pastel_mint", "#66D9A3", false, """{"note":"Actor differentiator palette color."}"""),
        new("pastel_orange", "#FFB74D", false, """{"note":"Actor differentiator palette color."}"""),
        new("pastel_sky", "#64B5F6", false, """{"note":"Actor differentiator palette color."}"""),
        new("pastel_yellow", "#FFF176", false, """{"note":"Actor differentiator palette color."}"""),
        new("purple", "#6750A4", false, """{"source":"android_seed_theme","note":"Primitive color seeded from the Android theme values."}"""),
        new("purple_tint", "#D0BCFF", false, """{"source":"android_seed_theme","note":"Primitive color seeded from the Android theme values."}"""),
        new("red", "#DF2020", false, """{"note":"Production palette primitive color."}"""),
    ];
}
