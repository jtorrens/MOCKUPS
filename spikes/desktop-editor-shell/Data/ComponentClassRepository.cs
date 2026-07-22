using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class ComponentClassRepository : IComponentClassRepository
{
    private readonly SqliteProjectContext _context;

    public ComponentClassRepository(SqliteProjectContext context)
    {
        _context = context;
    }

    public ComponentClassDefinitionRecord Get(string componentClassId)
    {
        using var connection = _context.OpenConnection();
        return Get(connection, componentClassId);
    }

    public ComponentClassDefinitionRecord Get(SqliteConnection connection, string componentClassId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, project_id, component_type, record_class_id, name, notes,
                   config_json, design_preview_json, metadata_json
            FROM component_classes
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", componentClassId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing component class '{componentClassId}'.");
        }

        return Read(reader);
    }

    public IReadOnlyList<ComponentClassDefinitionRecord> QueryAll(SqliteConnection connection)
    {
        var rows = new List<ComponentClassDefinitionRecord>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, project_id, component_type, record_class_id, name, notes,
                   config_json, design_preview_json, metadata_json
            FROM component_classes
            ORDER BY component_type, name, id
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(Read(reader));
        }

        return rows;
    }

    public IReadOnlyList<ComponentClassDefinitionRecord> QueryByProject(
        SqliteConnection connection,
        string projectId)
    {
        var rows = new List<ComponentClassDefinitionRecord>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, project_id, component_type, record_class_id, name, notes,
                   config_json, design_preview_json, metadata_json
            FROM component_classes
            WHERE project_id = $projectId
            ORDER BY CASE WHEN id = 'component_' || $projectId || '_' || component_type THEN 0 ELSE 1 END,
                     name,
                     id
            """;
        command.Parameters.AddWithValue("$projectId", projectId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(Read(reader));
        }

        return rows;
    }

    public void UpdateDesignPreview(string componentClassId, string designPreviewJson)
    {
        JsonPath.ParseRequiredObject(designPreviewJson, $"Component class '{componentClassId}' design_preview_json");
        using var connection = _context.OpenConnection();
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE component_classes SET design_preview_json = $json WHERE id = $id",
            ("$json", designPreviewJson),
            ("$id", componentClassId));
    }

    public void UpdateConfigAndMetadata(
        SqliteConnection connection,
        string componentClassId,
        string configJson,
        string metadataJson)
    {
        var current = Get(connection, componentClassId);
        var config = JsonPath.ParseRequiredObject(configJson, $"Component class '{componentClassId}' config_json");
        var metadata = ValidateMetadata(metadataJson, componentClassId);
        CurrentComponentConfigContract.Validate(
            current.ComponentType,
            config,
            $"Component class '{componentClassId}' config_json");
        ValidateVariantConfigs(current.ComponentType, metadata, componentClassId);
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE component_classes SET config_json = $configJson, metadata_json = $metadataJson WHERE id = $id",
            ("$id", componentClassId),
            ("$configJson", configJson),
            ("$metadataJson", metadataJson));
    }

    public void UpdateMetadata(SqliteConnection connection, string componentClassId, string metadataJson)
    {
        var current = Get(connection, componentClassId);
        var metadata = ValidateMetadata(metadataJson, componentClassId);
        ValidateVariantConfigs(current.ComponentType, metadata, componentClassId);
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE component_classes SET metadata_json = $metadataJson WHERE id = $id",
            ("$id", componentClassId),
            ("$metadataJson", metadataJson));
    }

    public void Rename(SqliteConnection connection, string componentClassId, string name)
    {
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE component_classes SET name = $name WHERE id = $id",
            ("$id", componentClassId),
            ("$name", name));
    }

    public void UpdateNode(SqliteConnection connection, string componentClassId, string name, string notes)
    {
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE component_classes SET name = $name, notes = $notes WHERE id = $id",
            ("$id", componentClassId),
            ("$name", name),
            ("$notes", notes));
    }

    private static ComponentClassDefinitionRecord Read(SqliteDataReader reader)
    {
        var record = new ComponentClassDefinitionRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            SqliteCommandExecutor.ReadString(reader, 5),
            SqliteCommandExecutor.ReadString(reader, 6),
            SqliteCommandExecutor.ReadString(reader, 7),
            SqliteCommandExecutor.ReadString(reader, 8));
        var config = JsonPath.ParseRequiredObject(record.ConfigJson, $"Component class '{record.Id}' config_json");
        JsonPath.ParseRequiredObject(record.DesignPreviewJson, $"Component class '{record.Id}' design_preview_json");
        var metadata = ValidateMetadata(record.MetadataJson, record.Id);
        CurrentComponentConfigContract.Validate(
            record.ComponentType,
            config,
            $"Component class '{record.Id}' config_json");
        ValidateVariantConfigs(record.ComponentType, metadata, record.Id);
        return record;
    }

    private static void ValidateVariantConfigs(string componentType, JsonObject metadata, string componentClassId)
    {
        foreach (var variant in VariantEnvelopeContract.Read(metadata, "variants", $"Component class '{componentClassId}'"))
        {
            CurrentComponentConfigContract.Validate(
                componentType,
                variant.Config,
                $"Component class '{componentClassId}' Variant '{variant.Id}' config");
        }
    }

    private static JsonObject ValidateMetadata(string metadataJson, string componentClassId)
    {
        var metadata = JsonPath.ParseRequiredObject(metadataJson, $"Component class '{componentClassId}' metadata_json");
        var variants = VariantEnvelopeContract.Read(metadata, "variants", $"Component class '{componentClassId}'");
        if (variants.All((variant) => variant.Id != "default"))
        {
            throw new InvalidOperationException($"Component class '{componentClassId}' has no explicit default Variant.");
        }

        return metadata;
    }
}
