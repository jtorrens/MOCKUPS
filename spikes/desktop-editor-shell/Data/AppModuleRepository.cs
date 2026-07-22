using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class AppModuleRepository : IAppModuleRepository
{
    private readonly SqliteProjectContext _context;

    public AppModuleRepository(SqliteProjectContext context)
    {
        _context = context;
    }

    public AppDefinitionRecord GetApp(string appId)
    {
        using var connection = _context.OpenConnection();
        return GetApp(connection, appId);
    }

    public AppDefinitionRecord GetApp(SqliteConnection connection, string appId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, record_class_id, name, bundle_key, app_type, notes, sort_order, config_json, metadata_json FROM apps WHERE id = $id";
        command.Parameters.AddWithValue("$id", appId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing app '{appId}'.");
        }

        return ReadApp(reader);
    }

    public ModuleDefinitionRecord GetModule(string moduleId)
    {
        using var connection = _context.OpenConnection();
        return GetModule(connection, moduleId);
    }

    public ModuleDefinitionRecord GetModule(SqliteConnection connection, string moduleId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT m.id, m.app_id, a.project_id, m.record_class_id, m.name, m.notes, m.sort_order,
                   m.config_json, m.design_preview_json, m.metadata_json
            FROM modules m
            JOIN apps a ON a.id = m.app_id
            WHERE m.id = $id
            """;
        command.Parameters.AddWithValue("$id", moduleId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing module '{moduleId}'.");
        }

        return ReadModule(reader);
    }

    public AppDefinitionRecord GetModuleApp(string moduleId)
    {
        using var connection = _context.OpenConnection();
        var module = GetModule(connection, moduleId);
        return GetApp(connection, module.AppId);
    }

    public IReadOnlyList<AppDefinitionRecord> QueryApps(SqliteConnection connection)
    {
        var rows = new List<AppDefinitionRecord>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, record_class_id, name, bundle_key, app_type, notes, sort_order, config_json, metadata_json FROM apps ORDER BY sort_order, name, id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(ReadApp(reader));
        }

        return rows;
    }

    public IReadOnlyList<ModuleDefinitionRecord> QueryModules(SqliteConnection connection)
    {
        var rows = new List<ModuleDefinitionRecord>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT m.id, m.app_id, a.project_id, m.record_class_id, m.name, m.notes, m.sort_order,
                   m.config_json, m.design_preview_json, m.metadata_json
            FROM modules m
            JOIN apps a ON a.id = m.app_id
            ORDER BY m.sort_order, m.name, m.id
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(ReadModule(reader));
        }

        return rows;
    }

    public void UpdateAppDirectField(SqliteConnection connection, string appId, string fieldId, string value)
    {
        var column = fieldId switch
        {
            "app.bundleKey" => "bundle_key",
            "app.appType" => "app_type",
            "app.config" => "config_json",
            "app.metadata" => "metadata_json",
            _ => throw new InvalidOperationException($"Unknown direct app field '{fieldId}'."),
        };
        if (fieldId is "app.config" or "app.metadata")
        {
            JsonPath.ParseRequiredObject(value, $"App '{appId}' {column}");
        }
        SqliteCommandExecutor.Execute(
            connection,
            $"UPDATE apps SET {column} = $value WHERE id = $id",
            ("$id", appId),
            ("$value", value));
    }

    public void UpdateAppConfig(SqliteConnection connection, string appId, string configJson)
    {
        JsonPath.ParseRequiredObject(configJson, $"App '{appId}' config_json");
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE apps SET config_json = $configJson WHERE id = $id",
            ("$id", appId),
            ("$configJson", configJson));
    }

    public void UpdateAppMetadata(SqliteConnection connection, string appId, string metadataJson)
    {
        JsonPath.ParseRequiredObject(metadataJson, $"App '{appId}' metadata_json");
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE apps SET metadata_json = $metadataJson WHERE id = $id",
            ("$id", appId),
            ("$metadataJson", metadataJson));
    }

    public void UpdateModuleSortOrder(SqliteConnection connection, string moduleId, int sortOrder)
    {
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE modules SET sort_order = $value WHERE id = $id",
            ("$id", moduleId),
            ("$value", sortOrder));
    }

    public void UpdateModuleConfig(SqliteConnection connection, string moduleId, string configJson)
    {
        JsonPath.ParseRequiredObject(configJson, $"Module '{moduleId}' config_json");
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE modules SET config_json = $configJson WHERE id = $id",
            ("$id", moduleId),
            ("$configJson", configJson));
    }

    public void UpdateModuleDesignPreview(string moduleId, string designPreviewJson)
    {
        JsonPath.ParseRequiredObject(designPreviewJson, $"Module '{moduleId}' design_preview_json");
        using var connection = _context.OpenConnection();
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE modules SET design_preview_json = $json WHERE id = $id",
            ("$id", moduleId),
            ("$json", designPreviewJson));
    }

    public void UpdateModuleMetadata(SqliteConnection connection, string moduleId, string metadataJson)
    {
        ValidateModuleMetadata(metadataJson, moduleId);
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE modules SET metadata_json = $metadataJson WHERE id = $id",
            ("$id", moduleId),
            ("$metadataJson", metadataJson));
    }

    public void RenameApp(SqliteConnection connection, string appId, string name)
    {
        SqliteCommandExecutor.Execute(connection, "UPDATE apps SET name = $name WHERE id = $id", ("$id", appId), ("$name", name));
    }

    public void RenameModule(SqliteConnection connection, string moduleId, string name)
    {
        SqliteCommandExecutor.Execute(connection, "UPDATE modules SET name = $name WHERE id = $id", ("$id", moduleId), ("$name", name));
    }

    public void UpdateAppNode(SqliteConnection connection, string appId, string name, string notes)
    {
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE apps SET name = $name, notes = $notes WHERE id = $id",
            ("$id", appId),
            ("$name", name),
            ("$notes", notes));
    }

    public void UpdateModuleNode(SqliteConnection connection, string moduleId, string name, string notes)
    {
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE modules SET name = $name, notes = $notes WHERE id = $id",
            ("$id", moduleId),
            ("$name", name),
            ("$notes", notes));
    }

    private static AppDefinitionRecord ReadApp(SqliteDataReader reader)
    {
        var record = new AppDefinitionRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            SqliteCommandExecutor.ReadString(reader, 4),
            SqliteCommandExecutor.ReadString(reader, 5),
            SqliteCommandExecutor.ReadString(reader, 6),
            reader.GetInt32(7),
            SqliteCommandExecutor.ReadString(reader, 8),
            SqliteCommandExecutor.ReadString(reader, 9));
        JsonPath.ParseRequiredObject(record.ConfigJson, $"App '{record.Id}' config_json");
        JsonPath.ParseRequiredObject(record.MetadataJson, $"App '{record.Id}' metadata_json");
        return record;
    }

    private static ModuleDefinitionRecord ReadModule(SqliteDataReader reader)
    {
        var record = new ModuleDefinitionRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            SqliteCommandExecutor.ReadString(reader, 5),
            reader.GetInt32(6),
            SqliteCommandExecutor.ReadString(reader, 7),
            SqliteCommandExecutor.ReadString(reader, 8),
            SqliteCommandExecutor.ReadString(reader, 9));
        JsonPath.ParseRequiredObject(record.ConfigJson, $"Module '{record.Id}' config_json");
        JsonPath.ParseRequiredObject(record.DesignPreviewJson, $"Module '{record.Id}' design_preview_json");
        ValidateModuleMetadata(record.MetadataJson, record.Id);
        return record;
    }

    private static void ValidateModuleMetadata(string metadataJson, string moduleId)
    {
        var metadata = JsonPath.ParseRequiredObject(metadataJson, $"Module '{moduleId}' metadata_json");
        var variants = VariantEnvelopeContract.Read(metadata, "variants", $"Module '{moduleId}'");
        if (variants.All((variant) => variant.Id != VariantEnvelopeContract.DefaultId))
        {
            throw new InvalidOperationException($"Module '{moduleId}' has no explicit default Variant.");
        }
    }
}
