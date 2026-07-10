using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mockups.DesktopEditorShell.Data;

internal static class SchemaV1DatabaseConsolidator
{
    private static readonly string[] CanonicalTables =
    [
        "projects", "episodes", "shots", "apps", "modules", "module_instances",
        "palette_colors", "devices", "actors", "production_fonts", "icon_themes",
        "render_presets", "component_classes", "themes", "editor_layouts",
    ];

    private static readonly (string Table, string Column)[] JsonColumns =
    [
        ("projects", "metadata_json"),
        ("episodes", "metadata_json"),
        ("shots", "canvas_json"), ("shots", "metadata_json"),
        ("apps", "config_json"), ("apps", "metadata_json"), ("modules", "metadata_json"),
        ("module_instances", "transition_json"), ("module_instances", "content_json"),
        ("module_instances", "behavior_json"), ("module_instances", "animation_json"), ("module_instances", "metadata_json"),
        ("palette_colors", "metadata_json"), ("devices", "metrics_json"), ("actors", "metadata_json"),
        ("production_fonts", "files_json"), ("production_fonts", "metadata_json"),
        ("icon_themes", "mapping_json"), ("icon_themes", "metadata_json"),
        ("render_presets", "codec_json"), ("render_presets", "color_json"), ("render_presets", "quality_json"),
        ("render_presets", "export_json"), ("render_presets", "metadata_json"),
        ("component_classes", "config_json"), ("component_classes", "design_preview_json"), ("component_classes", "metadata_json"),
        ("themes", "tokens_json"), ("themes", "metadata_json"), ("editor_layouts", "layout_json"),
    ];

    public static bool TryRun(string[] args)
    {
        if (args.Contains("--validate-schema-v1", StringComparer.Ordinal))
        {
            var path = OptionValue(args, "--source") ?? SpikeDatabase.DefaultDatabasePath();
            _ = new SpikeDatabase(path);
            Console.WriteLine($"Schema v1 database validated: {Path.GetFullPath(path)}");
            return true;
        }

        if (!args.Contains("--create-schema-v1", StringComparer.Ordinal)) return false;

        var sourcePath = OptionValue(args, "--source") ?? SpikeDatabase.DefaultDatabasePath();
        var outputPath = OptionValue(args, "--output") ?? DefaultCandidatePath(sourcePath);
        Console.WriteLine(Create(sourcePath, outputPath));
        return true;
    }

    private static string DefaultCandidatePath(string sourcePath)
    {
        return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourcePath))!, "desktop-editor-spike.schema-v1.sqlite");
    }

    private static string Create(string sourcePath, string outputPath)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        outputPath = Path.GetFullPath(outputPath);
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Source desktop DB does not exist.", sourcePath);
        if (File.Exists(outputPath)) throw new InvalidOperationException($"Output already exists and will not be overwritten: {outputPath}");

        var temporaryPath = outputPath + ".tmp";
        if (File.Exists(temporaryPath)) throw new InvalidOperationException($"Temporary output already exists: {temporaryPath}");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        try
        {
            using var connection = Open(temporaryPath);
            Execute(connection, "ATTACH DATABASE $sourcePath AS source", ("$sourcePath", sourcePath));
            ValidateSourceTables(connection);
            ExecuteScript(connection, SchemaV1Sql());
            CopyCanonicalData(connection, sourcePath);
            var report = ValidateTarget(connection, sourcePath, outputPath);
            Execute(connection, "DETACH DATABASE source");
            File.Move(temporaryPath, outputPath);
            return report;
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    private static SqliteConnection Open(string databasePath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString());
        connection.Open();
        Execute(connection, "PRAGMA foreign_keys = ON");
        return connection;
    }

    private static void ValidateSourceTables(SqliteConnection connection)
    {
        var sourceTables = TableNames(connection, "source");
        var missing = CanonicalTables.Where((table) => !sourceTables.Contains(table)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"The source DB is not the accepted pre-v1 shape. Missing tables: {string.Join(", ", missing)}.");
        }
    }

    private static string SchemaV1Sql()
    {
        return SpikeDatabase.SchemaSql;
    }

    private static void CopyCanonicalData(SqliteConnection connection, string sourcePath)
    {
        Execute(connection, "BEGIN IMMEDIATE");
        try
        {
            Copy(connection, "projects", "id, name, slug, default_fps, notes, media_root, metadata_json");
            NormalizeProjectMediaRoots(connection, sourcePath);
            Copy(connection, "episodes", "id, project_id, name, slug, notes, sort_order, metadata_json");
            Execute(connection, """
                INSERT INTO shots (id, episode_id, name, slug, version, notes, sort_order, fps_override, duration_frames, owner_actor_id, render_preset_id, canvas_json, metadata_json)
                SELECT s.id, s.episode_id, s.name, s.slug, s.version, s.notes, s.sort_order,
                       CASE WHEN s.fps = p.default_fps THEN NULL ELSE s.fps END,
                       s.duration_frames, s.owner_actor_id, s.render_preset_id, s.canvas_json, s.metadata_json
                FROM source.shots s
                JOIN source.episodes e ON e.id = s.episode_id
                JOIN source.projects p ON p.id = e.project_id
                """);
            Copy(connection, "apps", "id, project_id, record_class_id, name, bundle_key, app_type, notes, sort_order, config_json, metadata_json");
            Copy(connection, "modules", "id, app_id, record_class_id, name, notes, sort_order, config_json, design_preview_json, metadata_json");
            Copy(connection, "module_instances", "id, shot_id, app_id, module_id, name, notes, sort_order, duration_frames, transition_json, content_json, behavior_json, animation_json, metadata_json");
            Copy(connection, "palette_colors", "id, project_id, token, value_hex, metadata_json, is_neutral");
            Copy(connection, "devices", "id, project_id, name, manufacturer, model, os_family, metrics_json");
            Copy(connection, "actors", "id, project_id, display_name, short_name, default_device_id, default_theme_id, metadata_json");
            Copy(connection, "production_fonts", "id, project_id, family_name, category, source_directory, files_json, metadata_json");
            Copy(connection, "icon_themes", "id, project_id, name, asset_root, mapping_json, metadata_json");
            Copy(connection, "render_presets", "id, project_id, name, width, height, fps, format, codec_json, color_json, quality_json, export_json, metadata_json");
            Copy(connection, "component_classes", "id, project_id, component_type, record_class_id, name, notes, config_json, design_preview_json, metadata_json");
            Copy(connection, "themes", "id, project_id, name, family, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, metadata_json");
            Copy(connection, "editor_layouts", "record_class_id, layout_json");
            SpikeDatabase.SeedEditorLayouts(connection);
            Execute(connection, "COMMIT");
        }
        catch
        {
            Execute(connection, "ROLLBACK");
            throw;
        }
    }

    private static void NormalizeProjectMediaRoots(SqliteConnection connection, string sourcePath)
    {
        var projectRoot = Directory.GetParent(Path.GetDirectoryName(sourcePath)!)!.FullName;
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT id, media_root FROM projects";
        using var reader = query.ExecuteReader();
        var updates = new List<(string Id, string MediaRoot)>();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var mediaRoot = reader.IsDBNull(1) ? "" : reader.GetString(1);
            if (string.IsNullOrWhiteSpace(mediaRoot) || !Path.IsPathFullyQualified(mediaRoot)) continue;

            var relative = Path.GetRelativePath(projectRoot, Path.GetFullPath(mediaRoot));
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(relative)) continue;
            updates.Add((id, relative.Replace(Path.DirectorySeparatorChar, '/')));
        }

        foreach (var (id, mediaRoot) in updates)
        {
            Execute(connection, "UPDATE projects SET media_root = $mediaRoot WHERE id = $id", ("$mediaRoot", mediaRoot), ("$id", id));
        }
    }

    private static void Copy(SqliteConnection connection, string table, string columns)
    {
        Execute(connection, $"INSERT INTO {table} ({columns}) SELECT {columns} FROM source.{table}");
    }

    private static string ValidateTarget(SqliteConnection connection, string sourcePath, string outputPath)
    {
        var targetTables = TableNames(connection, "main");
        var unexpected = targetTables.Except(CanonicalTables, StringComparer.Ordinal).ToArray();
        var missing = CanonicalTables.Except(targetTables, StringComparer.Ordinal).ToArray();
        if (unexpected.Length > 0 || missing.Length > 0)
        {
            throw new InvalidOperationException($"Schema v1 table validation failed. Missing: {string.Join(", ", missing)}. Unexpected: {string.Join(", ", unexpected)}.");
        }

        if (ScalarLong(connection, "PRAGMA user_version") != 1) throw new InvalidOperationException("Schema v1 user_version was not written as 1.");
        foreach (var table in CanonicalTables)
        {
            var sourceCount = ScalarLong(connection, $"SELECT COUNT(*) FROM source.{table}");
            var targetCount = ScalarLong(connection, $"SELECT COUNT(*) FROM {table}");
            if (sourceCount != targetCount) throw new InvalidOperationException($"Row count mismatch for {table}: source {sourceCount}, target {targetCount}.");
        }

        ValidateJsonColumns(connection);
        ValidateReferences(connection);
        ValidateForeignKeys(connection);
        ValidateAssetRoots(connection, outputPath);

        var report = new StringBuilder();
        report.AppendLine("Schema v1 candidate validated.");
        report.AppendLine($"Source: {sourcePath}");
        report.AppendLine($"Output: {outputPath}");
        report.AppendLine("Schema: user_version 1");
        foreach (var table in CanonicalTables)
        {
            report.AppendLine($"- {table}: {ScalarLong(connection, $"SELECT COUNT(*) FROM {table}")}");
        }

        return report.ToString();
    }

    private static void ValidateJsonColumns(SqliteConnection connection)
    {
        foreach (var (table, column) in JsonColumns)
        {
            var invalid = ScalarLong(connection, $"SELECT COUNT(*) FROM {table} WHERE json_valid({column}) = 0");
            if (invalid > 0) throw new InvalidOperationException($"Invalid JSON: {table}.{column} has {invalid} invalid rows.");
        }
    }

    private static void ValidateReferences(SqliteConnection connection)
    {
        RequireNoRows(connection, "SELECT 1 FROM shots s LEFT JOIN episodes e ON e.id = s.episode_id WHERE e.id IS NULL", "shot without episode");
        RequireNoRows(connection, "SELECT 1 FROM apps a LEFT JOIN projects p ON p.id = a.project_id WHERE p.id IS NULL", "app without project");
        RequireNoRows(connection, "SELECT 1 FROM modules m LEFT JOIN apps a ON a.id = m.app_id WHERE a.id IS NULL", "module without app");
        RequireNoRows(connection, "SELECT 1 FROM module_instances mi LEFT JOIN shots s ON s.id = mi.shot_id WHERE s.id IS NULL", "module instance without shot");
        RequireNoRows(connection, "SELECT 1 FROM module_instances mi JOIN modules m ON m.id = mi.module_id WHERE mi.app_id <> m.app_id", "module instance app/module mismatch");
        RequireNoRows(connection, "SELECT 1 FROM actors a LEFT JOIN devices d ON d.id = a.default_device_id WHERE a.default_device_id <> '' AND d.id IS NULL", "actor default device missing");
        RequireNoRows(connection, "SELECT 1 FROM actors a LEFT JOIN themes t ON t.id = a.default_theme_id WHERE a.default_theme_id <> '' AND t.id IS NULL", "actor default theme missing");
        RequireNoRows(connection, "SELECT 1 FROM shots s LEFT JOIN actors a ON a.id = s.owner_actor_id WHERE s.owner_actor_id <> '' AND a.id IS NULL", "shot owner actor missing");
    }

    private static void ValidateForeignKeys(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check";
        using var reader = command.ExecuteReader();
        if (reader.Read()) throw new InvalidOperationException($"Foreign key validation failed for {reader.GetString(0)} row {reader.GetInt64(1)}.");
    }

    private static void ValidateAssetRoots(SqliteConnection connection, string outputPath)
    {
        var projectRoot = Directory.GetParent(Path.GetDirectoryName(outputPath)!)!.FullName;
        using var projects = connection.CreateCommand();
        projects.CommandText = "SELECT id, media_root FROM projects";
        using var reader = projects.ExecuteReader();
        var mediaRoots = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            var mediaRoot = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var resolved = Path.IsPathFullyQualified(mediaRoot)
                ? mediaRoot
                : Path.GetFullPath(Path.Combine(projectRoot, mediaRoot));
            if (!Directory.Exists(resolved)) throw new InvalidOperationException($"Project media root is missing: {resolved}");
            mediaRoots.Add(reader.GetString(0), resolved);
        }

        ValidateAssetDirectories(connection, "icon_themes", "asset_root", mediaRoots);
        ValidateAssetDirectories(connection, "production_fonts", "source_directory", mediaRoots);
    }

    private static void ValidateAssetDirectories(
        SqliteConnection connection,
        string table,
        string column,
        IReadOnlyDictionary<string, string> mediaRoots)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT project_id, {column} FROM {table}";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var projectId = reader.GetString(0);
            var value = reader.IsDBNull(1) ? "" : reader.GetString(1);
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (!mediaRoots.TryGetValue(projectId, out var mediaRoot)) throw new InvalidOperationException($"Missing media root for project {projectId}.");
            var resolved = Path.IsPathFullyQualified(value) ? value : Path.GetFullPath(Path.Combine(mediaRoot, value));
            if (!Directory.Exists(resolved)) throw new InvalidOperationException($"Missing asset directory for {table}.{column}: {resolved}");
        }
    }

    private static void RequireNoRows(SqliteConnection connection, string sql, string description)
    {
        if (ScalarLong(connection, $"SELECT COUNT(*) FROM ({sql})") > 0) throw new InvalidOperationException($"Schema v1 reference validation failed: {description}.");
    }

    private static HashSet<string> TableNames(SqliteConnection connection, string schema)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT name FROM {schema}.sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        using var reader = command.ExecuteReader();
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read()) names.Add(reader.GetString(0));
        return names;
    }

    private static long ScalarLong(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static void Execute(SqliteConnection connection, string sql, params (string Key, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (key, value) in parameters) command.Parameters.AddWithValue(key, value);
        command.ExecuteNonQuery();
    }

    private static void ExecuteScript(SqliteConnection connection, string script)
    {
        using var command = connection.CreateCommand();
        command.CommandText = script;
        command.ExecuteNonQuery();
    }

    private static string? OptionValue(string[] args, string option)
    {
        var index = Array.FindIndex(args, (value) => string.Equals(value, option, StringComparison.Ordinal));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
