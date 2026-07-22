using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static readonly Version MinimumSafeSqliteVersion = new(3, 50, 2);

    private static readonly HashSet<string> CurrentComponentVariantReferenceKeys = new(StringComparer.Ordinal)
    {
        "variantReference",
        "buttonVariantReference",
        "bubbleVariant",
        "headerAvatarVariant",
        "keyboardVariant",
        "textInputBarVariant",
    };

    private static readonly (string Table, string Column, string RootKind)[] CurrentJsonColumns =
    [
        ("projects", "metadata_json", "object"),
        ("episodes", "metadata_json", "object"),
        ("shots", "canvas_json", "object"),
        ("shots", "metadata_json", "object"),
        ("apps", "config_json", "object"),
        ("apps", "metadata_json", "object"),
        ("modules", "config_json", "object"),
        ("modules", "design_preview_json", "object"),
        ("modules", "metadata_json", "object"),
        ("module_instances", "transition_json", "object"),
        ("module_instances", "content_json", "object"),
        ("module_instances", "behavior_json", "object"),
        ("module_instances", "animation_json", "object"),
        ("module_instances", "metadata_json", "object"),
        ("palette_colors", "metadata_json", "object"),
        ("devices", "metrics_json", "object"),
        ("actors", "metadata_json", "object"),
        ("production_fonts", "files_json", "array"),
        ("production_fonts", "metadata_json", "object"),
        ("icon_themes", "mapping_json", "object"),
        ("icon_themes", "metadata_json", "object"),
        ("render_presets", "codec_json", "object"),
        ("render_presets", "color_json", "object"),
        ("render_presets", "quality_json", "object"),
        ("render_presets", "export_json", "object"),
        ("render_presets", "metadata_json", "object"),
        ("component_classes", "config_json", "object"),
        ("component_classes", "design_preview_json", "object"),
        ("component_classes", "metadata_json", "object"),
        ("themes", "tokens_json", "object"),
        ("themes", "metadata_json", "object"),
        ("editor_layouts", "layout_json", "object"),
    ];

    private void ValidateSchemaV1(SqliteConnection connection)
    {
        ValidateSqliteRuntime(connection);
        ValidatePhysicalSchema(connection);
        ValidateCurrentJsonColumns(connection);
        ValidateCurrentEditorLayouts(connection);
        ValidateCurrentDefinitionLifecycle(connection);
        ValidateCurrentPreviewManifest(connection);
        ValidateCurrentRuntimeInputContracts(connection);
        ValidateCurrentReferences(connection);
        ValidateCurrentModuleRuntimeDocuments(connection);
        ValidateCurrentComponentVariants(connection);
        ValidateCurrentModuleVariantsAndAnimations(connection);
        ValidateForeignKeyIntegrity(connection);
    }

    private void ValidateCurrentPreviewManifest(SqliteConnection connection)
    {
        using (var components = connection.CreateCommand())
        {
            components.CommandText = "SELECT id, component_type FROM component_classes";
            using var reader = components.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0);
                var componentType = reader.GetString(1);
                if (!DesktopPreviewManifest.Components.ContainsKey(componentType))
                {
                    throw InvalidCurrentDatabase(
                        $"component class '{id}' uses undeclared preview type '{componentType}'");
                }
            }
        }

        using var modules = connection.CreateCommand();
        modules.CommandText = "SELECT id, record_class_id FROM modules";
        using var moduleReader = modules.ExecuteReader();
        while (moduleReader.Read())
        {
            var id = moduleReader.GetString(0);
            var moduleClass = moduleReader.GetString(1);
            if (!DesktopPreviewManifest.Modules.ContainsKey(moduleClass))
            {
                throw InvalidCurrentDatabase(
                    $"module '{id}' uses undeclared preview class '{moduleClass}'");
            }
        }
    }

    private void ValidateCurrentDefinitionLifecycle(SqliteConnection connection)
    {
        RequireNoRows(
            connection,
            "SELECT 1 FROM apps WHERE record_class_id = 'app.generic'",
            "retired generic App definition");
        RequireNoRows(
            connection,
            "SELECT 1 FROM editor_layouts WHERE record_class_id IN ('app.generic', 'module.generic')",
            "retired generic App or Module editor layout");
    }

    private void ValidateCurrentRuntimeInputContracts(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT 'component class', id, config_json, design_preview_json, metadata_json FROM component_classes
            UNION ALL
            SELECT 'module', id, config_json, design_preview_json, metadata_json FROM modules
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var owner = $"{reader.GetString(0)} '{reader.GetString(1)}'";
            for (var column = 2; column <= 4; column++)
            {
                var document = ParseRequiredObject(reader.GetString(column), $"{owner} runtime contract");
                ValidateRuntimeInputDefinitions(document, owner, "$");
            }
        }
    }

    private void ValidateRuntimeInputDefinitions(JsonNode? node, string owner, string path)
    {
        if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
            {
                ValidateRuntimeInputDefinitions(array[index], owner, $"{path}[{index}]");
            }
            return;
        }
        if (node is not JsonObject obj) return;

        if (obj["inputs"] is JsonArray inputs)
        {
            ValidateRuntimeInputDefinitionArray(inputs, owner, $"{path}.inputs");
        }
        if (obj["fields"] is JsonArray fields
            && obj["itemLabel"] is not null)
        {
            ValidateRuntimeInputDefinitionArray(fields, owner, $"{path}.fields");
        }
        if (obj.TryGetPropertyValue(RuntimeInputForwardingContract.StorageKey, out var forwardedNode))
        {
            var forwarded = forwardedNode as JsonObject
                ?? throw InvalidCurrentDatabase(
                    $"{owner} has a non-object forwarding envelope at {path}.{RuntimeInputForwardingContract.StorageKey}");
            foreach (var (key, definition) in forwarded)
            {
                ValidateRuntimeInputDefinition(
                    definition as JsonObject,
                    owner,
                    $"{path}.{RuntimeInputForwardingContract.StorageKey}.{key}");
            }
        }

        foreach (var (key, child) in obj)
        {
            ValidateRuntimeInputDefinitions(child, owner, $"{path}.{key}");
        }
    }

    private void ValidateRuntimeInputDefinitionArray(
        JsonArray definitions,
        string owner,
        string path)
    {
        for (var index = 0; index < definitions.Count; index++)
        {
            ValidateRuntimeInputDefinition(
                definitions[index] as JsonObject,
                owner,
                $"{path}[{index}]");
        }
    }

    private void ValidateRuntimeInputDefinition(
        JsonObject? definition,
        string owner,
        string path)
    {
        if (definition is null)
        {
            throw InvalidCurrentDatabase($"{owner} has a non-object runtime field at {path}");
        }
        var id = definition["id"]?.GetValue<string>() ?? "";
        var label = definition["label"]?.GetValue<string>() ?? "";
        var jsonKey = definition["jsonKey"]?.GetValue<string>() ?? "";
        var kind = definition["kind"]?.GetValue<string>() ?? "";
        var valueKind = definition["valueKind"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(id)
            || string.IsNullOrWhiteSpace(label)
            || string.IsNullOrWhiteSpace(jsonKey))
        {
            throw InvalidCurrentDatabase($"{owner} has an incomplete runtime field at {path}");
        }
        try
        {
            _ = RuntimeInputValueKindContract.RequireCompatible(
                kind,
                valueKind,
                $"{owner} runtime field '{id}' at {path}");
            _ = RuntimeInputValueKindContract.CreateDefaultValue(
                definition,
                $"{owner} runtime field '{id}' at {path}");
        }
        catch (InvalidOperationException exception)
        {
            throw InvalidCurrentDatabase(exception.Message);
        }
    }

    private static void ValidateSqliteRuntime(SqliteConnection connection)
    {
        var value = ScalarString(connection, "SELECT sqlite_version()") ?? "";
        if (!Version.TryParse(value, out var version))
        {
            throw new InvalidOperationException($"Unable to identify the SQLite runtime version '{value}'.");
        }

        if (version < MinimumSafeSqliteVersion)
        {
            throw new InvalidOperationException(
                $"SQLite runtime {version} is below the required safe version {MinimumSafeSqliteVersion}. Update the bundled SQLite dependency before opening project data.");
        }
    }

    private void ValidatePhysicalSchema(SqliteConnection connection)
    {
        using var expected = new SqliteConnection("Data Source=:memory:");
        expected.Open();
        ExecuteScript(expected, SchemaSql);

        if (ScalarLong(connection, "PRAGMA user_version") != ScalarLong(expected, "PRAGMA user_version"))
        {
            throw InvalidCurrentDatabase("user_version does not match the canonical schema");
        }

        var expectedEntries = ReadSchemaEntries(expected);
        var actualEntries = ReadSchemaEntries(connection);
        var expectedKeys = expectedEntries.Keys.ToHashSet(StringComparer.Ordinal);
        var actualKeys = actualEntries.Keys.ToHashSet(StringComparer.Ordinal);
        if (!expectedKeys.SetEquals(actualKeys))
        {
            var missing = expectedKeys.Except(actualKeys, StringComparer.Ordinal);
            var unexpected = actualKeys.Except(expectedKeys, StringComparer.Ordinal);
            throw InvalidCurrentDatabase(
                $"physical objects differ; missing [{string.Join(", ", missing)}], unexpected [{string.Join(", ", unexpected)}]");
        }

        foreach (var key in expectedKeys.OrderBy((value) => value, StringComparer.Ordinal))
        {
            if (!string.Equals(expectedEntries[key], actualEntries[key], StringComparison.Ordinal))
            {
                throw InvalidCurrentDatabase($"physical definition for '{key}' differs from the canonical schema");
            }
        }
    }

    private static Dictionary<string, string> ReadSchemaEntries(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT type, name, sql FROM sqlite_master WHERE sql IS NOT NULL AND name NOT LIKE 'sqlite_%' ORDER BY type, name";
        using var reader = command.ExecuteReader();
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            entries.Add(
                $"{reader.GetString(0)}:{reader.GetString(1)}",
                Regex.Replace(reader.GetString(2).Trim(), @"\s+", " "));
        }

        return entries;
    }

    private void ValidateCurrentJsonColumns(SqliteConnection connection)
    {
        foreach (var (table, column, rootKind) in CurrentJsonColumns)
        {
            var invalid = ScalarLong(
                connection,
                $"SELECT COUNT(*) FROM {table} WHERE json_valid({column}) = 0 OR CASE WHEN json_valid({column}) THEN json_type({column}) ELSE '' END <> $rootKind",
                ("$rootKind", rootKind));
            if (invalid > 0)
            {
                throw InvalidCurrentDatabase($"{table}.{column} has {invalid} invalid {rootKind} JSON value(s)");
            }
        }
    }

    private void ValidateCurrentEditorLayouts(SqliteConnection connection)
    {
        var invalidRoots = ScalarLong(
            connection,
            """
            SELECT COUNT(*)
            FROM editor_layouts
            WHERE COALESCE(json_type(layout_json, '$.cards'), '') <> 'array'
               OR EXISTS (
                   SELECT 1
                   FROM json_each(editor_layouts.layout_json)
                   WHERE json_each.key <> 'cards'
               )
            """);
        if (invalidRoots > 0)
        {
            throw InvalidCurrentDatabase(
                $"editor_layouts has {invalidRoots} document(s) outside the current cards-only root contract");
        }

        var retiredOrDerivedProperties = ScalarLong(
            connection,
            "SELECT COUNT(*) FROM editor_layouts, json_tree(editor_layouts.layout_json) WHERE json_tree.key IN ('VisibleGroups', 'VisibleFields', 'Entries', 'simplified', 'capturedSlots')");
        if (retiredOrDerivedProperties > 0)
        {
            throw InvalidCurrentDatabase(
                $"editor_layouts contains {retiredOrDerivedProperties} retired or derived presentation properties instead of current authored card metadata");
        }
    }

    private void ValidateCurrentReferences(SqliteConnection connection)
    {
        RequireNoRows(connection, "SELECT 1 FROM shots s LEFT JOIN episodes e ON e.id = s.episode_id WHERE e.id IS NULL", "shot without episode");
        RequireNoRows(connection, "SELECT 1 FROM apps a LEFT JOIN projects p ON p.id = a.project_id WHERE p.id IS NULL", "app without project");
        RequireNoRows(connection, "SELECT 1 FROM modules m LEFT JOIN apps a ON a.id = m.app_id WHERE a.id IS NULL", "module without app");
        RequireNoRows(connection, "SELECT 1 FROM module_instances mi LEFT JOIN shots s ON s.id = mi.shot_id WHERE s.id IS NULL", "module instance without shot");
        RequireNoRows(connection, "SELECT 1 FROM module_instances mi LEFT JOIN modules m ON m.id = mi.module_id WHERE m.id IS NULL", "module instance without module");
        RequireNoRows(connection, "SELECT 1 FROM module_instances mi JOIN modules m ON m.id = mi.module_id WHERE mi.app_id <> m.app_id", "module instance app/module mismatch");
        RequireNoRows(
            connection,
            """
            SELECT 1
            FROM module_instances mi
            JOIN shots s ON s.id = mi.shot_id
            LEFT JOIN actors actor ON actor.id = s.owner_actor_id
            LEFT JOIN themes t ON t.id = actor.default_theme_id
            WHERE s.owner_actor_id = '' OR actor.id IS NULL OR actor.default_theme_id = '' OR t.id IS NULL
            """,
            "module instance without explicit Shot owner Theme context");
        RequireNoRows(connection, "SELECT 1 FROM actors a LEFT JOIN devices d ON d.id = a.default_device_id WHERE a.default_device_id <> '' AND d.id IS NULL", "actor default device missing");
        RequireNoRows(
            connection,
            """
            SELECT 1
            FROM actors a
            LEFT JOIN themes t ON t.id = a.default_theme_id AND t.project_id = a.project_id
            WHERE a.default_theme_id <> '' AND t.id IS NULL
            """,
            "Actor default Theme missing or from another Project");
        RequireNoRows(
            connection,
            """
            SELECT 1
            FROM shots s
            JOIN episodes e ON e.id = s.episode_id
            LEFT JOIN actors a ON a.id = s.owner_actor_id AND a.project_id = e.project_id
            WHERE s.owner_actor_id = '' OR a.id IS NULL
            """,
            "Shot owner Actor missing or from another Project");
        RequireNoRows(connection, "SELECT 1 FROM shots s LEFT JOIN render_presets r ON r.id = s.render_preset_id WHERE s.render_preset_id <> '' AND r.id IS NULL", "shot render preset missing");
        RequireNoRows(connection, "SELECT 1 FROM themes t LEFT JOIN icon_themes i ON i.id = t.icon_theme_id WHERE t.icon_theme_id <> '' AND i.id IS NULL", "Theme icon theme missing");
    }

    private void ValidateCurrentComponentVariants(SqliteConnection connection)
    {
        var validReferences = new HashSet<string>(StringComparer.Ordinal);
        var documents = new List<(string Context, JsonNode Node)>();
        foreach (var row in QueryComponentClassRows(connection))
        {
            var variants = RequiredComponentClassVariants(row);
            foreach (var variant in variants)
            {
                validReferences.Add(VariantReferenceId.Format(row.Id, variant.Id));
                var variantConfig = ParseRequiredObject(variant.ConfigJson, $"component variant '{row.Id}::{variant.Id}'");
                ValidateEmbeddedSlotVariantReferences(connection, row.ProjectId, variantConfig);
                documents.Add(($"component variant '{row.Id}::{variant.Id}'", variantConfig));
            }

            var classConfig = ParseRequiredObject(row.ConfigJson, $"component class '{row.Id}' config_json");
            ValidateEmbeddedSlotVariantReferences(connection, row.ProjectId, classConfig);
            documents.Add(($"component class '{row.Id}' config_json", classConfig));
            documents.Add(($"component class '{row.Id}' design_preview_json", ParseRequiredObject(row.DesignPreviewJson, $"component class '{row.Id}' design_preview_json")));
            documents.Add(($"component class '{row.Id}' metadata_json", ParseRequiredObject(row.MetadataJson, $"component class '{row.Id}' metadata_json")));
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, record_class_id, config_json, design_preview_json, metadata_json FROM modules";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0);
                var recordClassId = reader.GetString(1);
                var config = ParseRequiredObject(reader.GetString(2), $"module '{id}' config_json");
                CurrentModuleConfigContract.Validate(recordClassId, config, $"Module '{id}' config_json");
                documents.Add(($"module '{id}' config_json", config));
                documents.Add(($"module '{id}' design_preview_json", ParseRequiredObject(reader.GetString(3), $"module '{id}' design_preview_json")));
                var metadata = ParseRequiredObject(reader.GetString(4), $"module '{id}' metadata_json");
                foreach (var variant in VariantEnvelopeContract.Read(metadata, "variants", $"Module '{id}'"))
                {
                    CurrentModuleConfigContract.Validate(
                        recordClassId,
                        variant.Config,
                        $"Module Variant '{id}::{variant.Id}' config");
                }
                documents.Add(($"module '{id}' metadata_json", metadata));
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, content_json, behavior_json, metadata_json FROM module_instances";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0);
                documents.Add(($"module instance '{id}' content_json", ParseRequiredObject(reader.GetString(1), $"module instance '{id}' content_json")));
                documents.Add(($"module instance '{id}' behavior_json", ParseRequiredObject(reader.GetString(2), $"module instance '{id}' behavior_json")));
                documents.Add(($"module instance '{id}' metadata_json", ParseRequiredObject(reader.GetString(3), $"module instance '{id}' metadata_json")));
            }
        }

        foreach (var (context, document) in documents)
        {
            ValidateFullComponentVariantReferences(document, context, validReferences);
        }
    }

    private void ValidateCurrentModuleVariantsAndAnimations(SqliteConnection connection)
    {
        var variantsByModule = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, metadata_json FROM modules";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var moduleId = reader.GetString(0);
                var variants = ModuleVariants(reader.GetString(1), $"Module '{moduleId}'");
                var ids = variants.Select((variant) => variant.Id).ToHashSet(StringComparer.Ordinal);
                if (ids.Count != variants.Count || !ids.Contains(VariantEnvelopeContract.DefaultId))
                {
                    throw InvalidCurrentDatabase($"module '{moduleId}' must have unique explicit Variants including Default");
                }

                variantsByModule.Add(moduleId, ids);
            }
        }

        using var instances = connection.CreateCommand();
        instances.CommandText = "SELECT id, module_id, metadata_json, animation_json FROM module_instances";
        using var instanceReader = instances.ExecuteReader();
        while (instanceReader.Read())
        {
            var instanceId = instanceReader.GetString(0);
            var moduleId = instanceReader.GetString(1);
            var metadata = ParseRequiredObject(instanceReader.GetString(2), $"module instance '{instanceId}' metadata_json");
            var reference = metadata["moduleVariantReference"]?.GetValue<string>() ?? "";
            if (!VariantReferenceId.TryParse(reference, out var referencedModuleId, out var variantId)
                || !referencedModuleId.Equals(moduleId, StringComparison.Ordinal)
                || !variantsByModule.TryGetValue(moduleId, out var variants)
                || !variants.Contains(variantId))
            {
                throw InvalidCurrentDatabase($"module instance '{instanceId}' has invalid explicit Variant reference '{reference}'");
            }

            ModuleInstanceAnimationDocumentContract.Parse(
                instanceReader.GetString(3),
                $"Module Instance '{instanceId}' animation_json");
        }
    }

    private void ValidateForeignKeyIntegrity(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check";
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            throw InvalidCurrentDatabase($"foreign key failure in '{reader.GetString(0)}' row {reader.GetInt64(1)}");
        }
    }

    private void RequireNoRows(SqliteConnection connection, string sql, string description)
    {
        if (ScalarLong(connection, $"SELECT COUNT(*) FROM ({sql})") > 0)
        {
            throw InvalidCurrentDatabase(description);
        }
    }

    private static JsonObject ParseRequiredObject(string json, string context)
    {
        return JsonPath.ParseRequiredObject(json, context);
    }

    private void ValidateFullComponentVariantReferences(JsonNode? node, string context, IReadOnlySet<string> validReferences)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var entry in obj)
                {
                    if (CurrentComponentVariantReferenceKeys.Contains(entry.Key)
                        && entry.Value is JsonValue referenceValue
                        && referenceValue.TryGetValue<string>(out var reference)
                        && !string.IsNullOrWhiteSpace(reference)
                        && (!IsCompleteComponentVariantReference(reference) || !validReferences.Contains(reference)))
                    {
                        throw InvalidCurrentDatabase($"{context} has invalid full component Variant reference '{reference}' in '{entry.Key}'");
                    }

                    ValidateFullComponentVariantReferences(entry.Value, context, validReferences);
                }
                break;
            case JsonArray array:
                foreach (var item in array) ValidateFullComponentVariantReferences(item, context, validReferences);
                break;
        }
    }

    private static bool IsCompleteComponentVariantReference(string value)
    {
        if (!VariantReferenceId.TryParse(value, out var componentClassId, out var variantId)) return false;
        return VariantReferenceId.Format(componentClassId, variantId).Equals(value, StringComparison.Ordinal)
            && componentClassId.All(IsStableReferenceCharacter)
            && variantId.All(IsStableReferenceCharacter);
    }

    private static bool IsStableReferenceCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '_' or '-' or '.';

    private InvalidOperationException InvalidCurrentDatabase(string detail) =>
        new($"Desktop database '{_context.DatabasePath}' does not satisfy the current persistence contract: {detail}. Run an explicit migration on a copy; startup will not repair it.");
}
