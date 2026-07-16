using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public RenderPresetSettings GetRenderPresetSettings(string renderPresetId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT project_id, name, width, height, fps, format, codec_json, color_json, quality_json, export_json, metadata_json FROM render_presets WHERE id = $id";
        command.Parameters.AddWithValue("$id", renderPresetId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing render preset '{renderPresetId}'.");
        }

        return new RenderPresetSettings(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? 1080 : reader.GetInt32(2),
            reader.IsDBNull(3) ? 1920 : reader.GetInt32(3),
            reader.IsDBNull(4) ? 25 : reader.GetInt32(4),
            ReadString(reader, 5),
            ReadString(reader, 6),
            ReadString(reader, 7),
            ReadString(reader, 8),
            ReadString(reader, 9),
            ReadString(reader, 10));
    }

    public void UpdateRenderPresetField(string renderPresetId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        if (fieldId == "renderPreset.export.ffmpegArgs")
        {
            var settings = GetRenderPresetSettings(renderPresetId);
            var export = ParseJsonObject(string.IsNullOrWhiteSpace(settings.ExportJson) ? "{}" : settings.ExportJson);
            SetJsonValue(export, ["ffmpegArgs"], JsonValue.Create(value)!);
            Execute(connection, "UPDATE render_presets SET export_json = $value WHERE id = $id", ("$id", renderPresetId), ("$value", export.ToJsonString()));
            return;
        }

        var column = fieldId switch
        {
            "renderPreset.width" => "width",
            "renderPreset.height" => "height",
            "renderPreset.fps" => "fps",
            "renderPreset.format" => "format",
            "renderPreset.codec" => "codec_json",
            "renderPreset.color" => "color_json",
            "renderPreset.quality" => "quality_json",
            "renderPreset.export" => "export_json",
            _ => throw new InvalidOperationException($"Unknown render preset field '{fieldId}'."),
        };
        object nextValue = fieldId is "renderPreset.width" or "renderPreset.height" or "renderPreset.fps"
            ? NumericText.Int32(value, 0)
            : value;

        Execute(
            connection,
            $"UPDATE render_presets SET {column} = $value WHERE id = $id",
            ("$id", renderPresetId),
            ("$value", nextValue));
    }

    public IReadOnlyList<FieldOption> GetRenderPresetOptions(string projectId)
    {
        using var connection = OpenConnection();
        var options = QueryRenderPresetRows(connection)
            .Where((preset) => preset.ProjectId == projectId)
            .OrderBy((preset) => preset.Name)
            .Select((preset) => new FieldOption(preset.Id, preset.Name))
            .ToList();
        options.Insert(0, new FieldOption("", "None"));
        return options;
    }

    private static List<RenderPresetRow> QueryRenderPresetRows(SqliteConnection connection)
    {
        var rows = new List<RenderPresetRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, width, height, fps, format, codec_json, color_json, quality_json, export_json, metadata_json FROM render_presets ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new RenderPresetRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                ReadString(reader, 6),
                ReadString(reader, 7),
                ReadString(reader, 8),
                ReadString(reader, 9),
                ReadString(reader, 10),
                ReadString(reader, 11)));
        }

        return rows;
    }

    private static string DefaultRenderPresetCodecJson(string codec = "prores_422_hq")
    {
        return new JsonObject { ["codec"] = codec }.ToJsonString();
    }

    private static string DefaultRenderPresetColorJson(string format = "mov", string codec = "prores_422_hq")
    {
        var isImage = format == "image";
        var hasAlpha = codec is "prores_4444" or "prores_4444_alpha" or "prores_4444_xq" or "png" or "exr";
        return new JsonObject
        {
            ["colorSpace"] = codec == "exr" ? "linear" : isImage ? "srgb" : "rec709",
            ["alpha"] = hasAlpha,
        }.ToJsonString();
    }

    private static string DefaultRenderPresetQualityJson(string codec = "prores_422_hq")
    {
        return new JsonObject { ["profile"] = codec }.ToJsonString();
    }

    private static string DefaultRenderPresetExportJson(string format = "mov", string codec = "prores_422_hq")
    {
        var isImage = format == "image";
        return new JsonObject
        {
            ["extension"] = isImage ? codec : "mov",
            ["sequence"] = isImage,
            ["ffmpegArgs"] = FfmpegArgsForRenderPreset(format, codec),
        }.ToJsonString();
    }

    private static string FfmpegArgsForRenderPreset(string format, string codec)
    {
        if (format == "image")
        {
            return codec == "exr"
                ? "-compression zip -pix_fmt rgba64le"
                : "-compression_level 6 -pix_fmt rgba";
        }

        return codec switch
        {
            "prores_422_proxy" => "-c:v prores_ks -profile:v 0 -pix_fmt yuv422p10le",
            "prores_422_lt" => "-c:v prores_ks -profile:v 1 -pix_fmt yuv422p10le",
            "prores_422" => "-c:v prores_ks -profile:v 2 -pix_fmt yuv422p10le",
            "prores_422_hq" => "-c:v prores_ks -profile:v 3 -pix_fmt yuv422p10le",
            "prores_4444" or "prores_4444_alpha" => "-c:v prores_ks -profile:v 4 -pix_fmt yuva444p10le",
            "prores_4444_xq" => "-c:v prores_ks -profile:v 5 -pix_fmt yuva444p10le",
            "h264_low" => "-c:v libx264 -preset medium -crf 28 -pix_fmt yuv420p",
            "h264_medium" => "-c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p",
            "h264_high" => "-c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p",
            _ => "",
        };
    }

}
