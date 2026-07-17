using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class RenderPresetRepository : IRenderPresetRepository
{
    private readonly SqliteProjectContext _context;

    public RenderPresetRepository(SqliteProjectContext context)
    {
        _context = context;
    }

    public RenderPresetSettings GetSettings(string renderPresetId)
    {
        using var connection = _context.OpenConnection();
        return GetSettings(connection, renderPresetId);
    }

    public void UpdateField(string renderPresetId, string fieldId, string value)
    {
        using var connection = _context.OpenConnection();
        if (fieldId == "renderPreset.export.ffmpegArgs")
        {
            var settings = GetSettings(connection, renderPresetId);
            var export = JsonPath.ParseRequiredObject(settings.ExportJson, $"Render Preset '{renderPresetId}' export_json");
            JsonPath.Set(export, ["ffmpegArgs"], JsonValue.Create(value)!);
            SqliteCommandExecutor.Execute(
                connection,
                "UPDATE render_presets SET export_json = $value WHERE id = $id",
                ("$id", renderPresetId),
                ("$value", export.ToJsonString()));
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
        if (fieldId is "renderPreset.codec" or "renderPreset.color" or "renderPreset.quality" or "renderPreset.export")
        {
            JsonPath.ParseRequiredObject(value, $"Render Preset '{renderPresetId}' {fieldId}");
        }

        SqliteCommandExecutor.Execute(
            connection,
            $"UPDATE render_presets SET {column} = $value WHERE id = $id",
            ("$id", renderPresetId),
            ("$value", nextValue));
    }

    public IReadOnlyList<RenderPresetOption> GetOptions(string projectId)
    {
        using var connection = _context.OpenConnection();
        return QueryAll(connection)
            .Where((preset) => preset.ProjectId == projectId)
            .OrderBy((preset) => preset.Name)
            .Select((preset) => new RenderPresetOption(preset.Id, preset.Name))
            .ToList();
    }

    public IReadOnlyList<RenderPresetRecord> QueryAll(SqliteConnection connection)
    {
        var rows = new List<RenderPresetRecord>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, width, height, fps, format, codec_json, color_json, quality_json, export_json, metadata_json FROM render_presets ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new RenderPresetRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                SqliteCommandExecutor.ReadString(reader, 6),
                SqliteCommandExecutor.ReadString(reader, 7),
                SqliteCommandExecutor.ReadString(reader, 8),
                SqliteCommandExecutor.ReadString(reader, 9),
                SqliteCommandExecutor.ReadString(reader, 10),
                SqliteCommandExecutor.ReadString(reader, 11)));
        }

        return rows;
    }

    public RenderPresetRecord Create(SqliteConnection connection, string projectId)
    {
        var index = SqliteCommandExecutor.ScalarLong(
            connection,
            "SELECT COUNT(*) FROM render_presets WHERE project_id = $projectId",
            ("$projectId", projectId)) + 1;
        var id = $"render_preset_{Guid.NewGuid():N}";
        var name = $"Render Preset {index}";
        var codecJson = DefaultCodecJson();
        var colorJson = DefaultColorJson();
        var qualityJson = DefaultQualityJson();
        var exportJson = DefaultExportJson();
        SqliteCommandExecutor.Execute(
            connection,
            """
            INSERT INTO render_presets (id, project_id, name, width, height, fps, format, codec_json, color_json, quality_json, export_json)
            VALUES ($id, $projectId, $name, 1, 1, 1, 'mov', $codecJson, $colorJson, $qualityJson, $exportJson)
            """,
            ("$id", id),
            ("$projectId", projectId),
            ("$name", name),
            ("$codecJson", codecJson),
            ("$colorJson", colorJson),
            ("$qualityJson", qualityJson),
            ("$exportJson", exportJson));

        return new RenderPresetRecord(
            id, projectId, name, 1, 1, 1, "mov", codecJson, colorJson, qualityJson, exportJson, "{}");
    }

    public RenderPresetRecord Duplicate(SqliteConnection connection, string sourceId, string copyName)
    {
        var source = QueryAll(connection).SingleOrDefault((preset) => preset.Id == sourceId)
            ?? throw new InvalidOperationException($"Missing render preset '{sourceId}'.");
        var id = $"render_preset_{Guid.NewGuid():N}";
        SqliteCommandExecutor.Execute(
            connection,
            """
            INSERT INTO render_presets (id, project_id, name, width, height, fps, format, codec_json, color_json, quality_json, export_json, metadata_json)
            VALUES ($id, $projectId, $name, $width, $height, $fps, $format, $codecJson, $colorJson, $qualityJson, $exportJson, $metadataJson)
            """,
            ("$id", id),
            ("$projectId", source.ProjectId),
            ("$name", copyName),
            ("$width", source.Width),
            ("$height", source.Height),
            ("$fps", source.Fps),
            ("$format", source.Format),
            ("$codecJson", source.CodecJson),
            ("$colorJson", source.ColorJson),
            ("$qualityJson", source.QualityJson),
            ("$exportJson", source.ExportJson),
            ("$metadataJson", source.MetadataJson));

        return source with { Id = id, Name = copyName };
    }

    public void Delete(SqliteConnection connection, string renderPresetId)
    {
        SqliteCommandExecutor.Execute(connection, "DELETE FROM render_presets WHERE id = $id", ("$id", renderPresetId));
    }

    public void Rename(SqliteConnection connection, string renderPresetId, string name)
    {
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE render_presets SET name = $name WHERE id = $id",
            ("$id", renderPresetId),
            ("$name", name));
    }

    private static RenderPresetSettings GetSettings(SqliteConnection connection, string renderPresetId)
    {
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
            SqliteCommandExecutor.ReadString(reader, 5),
            SqliteCommandExecutor.ReadString(reader, 6),
            SqliteCommandExecutor.ReadString(reader, 7),
            SqliteCommandExecutor.ReadString(reader, 8),
            SqliteCommandExecutor.ReadString(reader, 9),
            SqliteCommandExecutor.ReadString(reader, 10));
    }

    private static string DefaultCodecJson(string codec = "prores_422_hq")
    {
        return new JsonObject { ["codec"] = codec }.ToJsonString();
    }

    private static string DefaultColorJson(string format = "mov", string codec = "prores_422_hq")
    {
        var isImage = format == "image";
        var hasAlpha = codec is "prores_4444" or "prores_4444_alpha" or "prores_4444_xq" or "png" or "exr";
        return new JsonObject
        {
            ["colorSpace"] = codec == "exr" ? "linear" : isImage ? "srgb" : "rec709",
            ["alpha"] = hasAlpha,
        }.ToJsonString();
    }

    private static string DefaultQualityJson(string codec = "prores_422_hq")
    {
        return new JsonObject { ["profile"] = codec }.ToJsonString();
    }

    private static string DefaultExportJson(string format = "mov", string codec = "prores_422_hq")
    {
        var isImage = format == "image";
        return new JsonObject
        {
            ["extension"] = isImage ? codec : "mov",
            ["sequence"] = isImage,
            ["ffmpegArgs"] = FfmpegArgs(format, codec),
        }.ToJsonString();
    }

    private static string FfmpegArgs(string format, string codec)
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
