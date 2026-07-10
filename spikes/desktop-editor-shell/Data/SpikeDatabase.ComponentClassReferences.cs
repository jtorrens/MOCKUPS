using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{

    public IReadOnlyList<EmbeddedComponentUsage> GetEmbeddedComponentUsages(
        string projectId,
        string componentType,
        string? excludedComponentClassId = null)
    {
        using var connection = OpenConnection();
        var rows = QueryComponentClassRows(connection)
            .Where((row) => row.ProjectId.Equals(projectId, StringComparison.Ordinal))
            .Where((row) => !row.Id.Equals(excludedComponentClassId, StringComparison.Ordinal))
            .ToList();
        var usages = new List<EmbeddedComponentUsage>();
        foreach (var row in rows)
        {
            AddEmbeddedComponentUsages(
                usages,
                row,
                row.Name,
                row.Id,
                row.ConfigJson,
                componentType);
            foreach (var preset in ComponentClassPresets(row.MetadataJson))
            {
                AddEmbeddedComponentUsages(
                    usages,
                    row,
                    $"{row.Name} · {preset.Name}",
                    ComponentPresetNodeId(row.Id, preset.Id),
                    preset.ConfigJson,
                    componentType);
            }
        }

        return usages
            .OrderBy((usage) => usage.ParentComponentType)
            .ThenBy((usage) => usage.ParentComponentName)
            .ThenBy((usage) => usage.SlotLabel)
            .ToList();
    }

    private static void AddEmbeddedComponentUsages(
        ICollection<EmbeddedComponentUsage> usages,
        ComponentClassRow row,
        string sourceName,
        string sourceNodeId,
        string configJson,
        string componentType)
    {
        var config = ParseJsonObject(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
        foreach (var slot in EmbeddedComponentSlotCatalog.All()
                     .Where((candidate) => candidate.EmbeddedComponentType.Equals(componentType, StringComparison.Ordinal)))
        {
            if (JsonPath.Get(config, slot.SlotPath) is not JsonObject)
            {
                continue;
            }

            usages.Add(new EmbeddedComponentUsage(
                row.Id,
                sourceName,
                row.ComponentType,
                slot.FieldId,
                slot.Label,
                EmbeddedComponentHasOverrides(configJson, slot),
                sourceNodeId));
        }
    }

    public string GetEmbeddedComponentPresetName(
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        if (slots.Count == 0)
        {
            return "";
        }

        using var connection = OpenConnection();
        var settings = GetComponentClassSettings(connection, componentClassId);
        var ownerConfig = ParseJsonObject(string.IsNullOrWhiteSpace(settings.ConfigJson) ? "{}" : settings.ConfigJson);
        return GetEmbeddedComponentPresetName(connection, settings, ownerConfig, slots);
    }

    public string GetEmbeddedComponentPresetName(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        if (slots.Count == 0)
        {
            return "";
        }

        using var connection = OpenConnection();
        var settings = ownerNode.Kind switch
        {
            ProjectTreeNodeKind.ComponentClass => GetComponentClassSettings(connection, ownerNode.Id),
            ProjectTreeNodeKind.ComponentPreset => GetComponentPresetSettings(connection, ownerNode),
            _ => throw new InvalidOperationException($"Embedded component variants are not supported for '{ownerNode.Kind}'."),
        };
        var ownerConfig = ParseJsonObject(string.IsNullOrWhiteSpace(settings.ConfigJson) ? "{}" : settings.ConfigJson);
        return GetEmbeddedComponentPresetName(connection, settings, ownerConfig, slots);
    }

    private static string GetEmbeddedComponentPresetName(
        SqliteConnection connection,
        ComponentClassSettings settings,
        JsonObject ownerConfig,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        JsonObject? currentContainer = ownerConfig;
        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];
            var slotNode = currentContainer is null
                ? null
                : JsonPath.Get(currentContainer, slot.SlotPath) as JsonObject;
            var presetId = JsonPath.String(slotNode ?? [], "presetId", DefaultComponentPresetId);
            if (index == slots.Count - 1)
            {
                return ComponentPresetName(connection, settings.ProjectId, slot.EmbeddedComponentType, presetId);
            }

            var child = ParseJsonObject(GetComponentClassPresetConfigJson(
                connection,
                settings.ProjectId,
                slot.EmbeddedComponentType,
                presetId));
            if (slotNode?["overrides"] is JsonObject overrides)
            {
                MergeOverride(child, overrides);
            }

            currentContainer = child;
        }

        return "";
    }

    public string GetComponentClassBaseConfigsJson(string projectId)
    {
        using var connection = OpenConnection();
        var configs = new JsonObject();
        var presets = new JsonObject();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, component_type, config_json, metadata_json
            FROM component_classes
            WHERE project_id = $projectId
            ORDER BY CASE WHEN id = 'component_' || $projectId || '_' || component_type THEN 0 ELSE 1 END, name
            """;
        command.Parameters.AddWithValue("$projectId", projectId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var componentClassId = reader.GetString(0);
            var componentType = reader.GetString(1);
            var classConfigJson = ReadString(reader, 2);
            var metadataJson = ReadString(reader, 3);
            var row = new ComponentClassRow(
                componentClassId,
                projectId,
                componentType,
                "",
                "",
                "",
                classConfigJson,
                "",
                metadataJson);
            AddComponentPresetConfigs(connection, presets, row);
            if (configs.ContainsKey(componentType))
            {
                continue;
            }

            var defaultConfig = ParseJsonObject(DefaultComponentPresetConfigJson(
                classConfigJson,
                metadataJson));
            ValidateEmbeddedSlotPresetReferences(connection, projectId, defaultConfig);
            configs[componentType] = defaultConfig;
        }

        configs["presets"] = presets;
        return configs.ToJsonString();
    }

    public string ValidateComponentPresetReferencesForPreview(string projectId, string configJson)
    {
        using var connection = OpenConnection();
        var config = ParseJsonObject(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
        ValidateEmbeddedSlotPresetReferences(connection, projectId, config);
        return config.ToJsonString();
    }

    private static void AddComponentPresetConfigs(SqliteConnection connection, JsonObject target, ComponentClassRow row)
    {
        foreach (var preset in ComponentClassPresetsOrDefault(row))
        {
            var config = ParseJsonObject(
                string.IsNullOrWhiteSpace(preset.ConfigJson) || preset.ConfigJson == "{}"
                    ? row.ConfigJson
                    : preset.ConfigJson);
            ValidateEmbeddedSlotPresetReferences(connection, row.ProjectId, config);
            target[ComponentPresetNodeId(row.Id, preset.Id)] = config;
        }
    }

    private static void ValidateEmbeddedSlotPresetReferences(
        SqliteConnection connection,
        string projectId,
        JsonObject config)
    {
        var componentRows = QueryComponentClassRows(connection)
            .Where((row) => row.ProjectId.Equals(projectId, StringComparison.Ordinal))
            .ToList();

        foreach (var slot in EmbeddedComponentSlotCatalog.All())
        {
            if (JsonPath.Get(config, slot.SlotPath) is not JsonObject slotNode)
            {
                continue;
            }

            var reference = JsonPath.String(slotNode, "presetId", "");
            if (!TryParseComponentPresetNodeId(reference, out var componentClassId, out var presetId))
            {
                throw new InvalidOperationException(
                    $"Embedded component slot '{slot.FieldId}' must use a full component variant reference.");
            }

            var componentClass = componentRows.FirstOrDefault((row) =>
                row.Id.Equals(componentClassId, StringComparison.Ordinal)
                && row.ComponentType.Equals(slot.EmbeddedComponentType, StringComparison.Ordinal));
            if (componentClass is null)
            {
                throw new InvalidOperationException(
                    $"Embedded component slot '{slot.FieldId}' references missing {slot.EmbeddedComponentType} class '{componentClassId}'.");
            }

            if (!ComponentClassPresetsOrDefault(componentClass)
                    .Any((preset) => preset.Id.Equals(presetId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Embedded component slot '{slot.FieldId}' references missing variant '{presetId}' on '{componentClassId}'.");
            }
        }
    }

    public IReadOnlyList<FieldOption> GetComponentClassOptionsByType(
        string projectId,
        string componentType,
        bool includeNone = false)
    {
        using var connection = OpenConnection();
        var options = QueryComponentClassRows(connection)
            .Where((row) => row.ProjectId.Equals(projectId, StringComparison.Ordinal))
            .Where((row) => row.ComponentType.Equals(componentType, StringComparison.Ordinal))
            .OrderBy((row) => row.Id.Equals($"component_{projectId}_{componentType}", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy((row) => row.Name, StringComparer.Ordinal)
            .Select((row) => new FieldOption(row.Id, row.Name))
            .ToList();
        if (includeNone)
        {
            options.Insert(0, new FieldOption("", "None"));
        }

        return options;
    }

    public IReadOnlyList<FieldOption> GetComponentPresetReferenceOptionsByType(
        string projectId,
        string componentType,
        bool includeNone = false)
    {
        using var connection = OpenConnection();
        var rows = ComponentClassRowsByType(connection, projectId, componentType);
        var showClassName = rows.Count > 1;
        var options = rows
            .SelectMany((row) => ComponentClassPresetsOrDefault(row)
                .Select((preset) => new FieldOption(
                    ComponentPresetNodeId(row.Id, preset.Id),
                    showClassName ? $"{row.Name} · {preset.Name}" : preset.Name)))
            .ToList();
        if (includeNone)
        {
            options.Insert(0, new FieldOption("", "None"));
        }

        return options;
    }

    public string NormalizeComponentPresetReferenceValue(
        string projectId,
        string componentType,
        string currentValue)
    {
        using var connection = OpenConnection();
        return NormalizeComponentPresetReference(connection, projectId, componentType, currentValue);
    }

    private static string FirstComponentClassIdByType(SqliteConnection connection, string projectId, string componentType)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id
            FROM component_classes
            WHERE project_id = $projectId
              AND component_type = $componentType
            ORDER BY CASE WHEN id = 'component_' || $projectId || '_' || $componentType THEN 0 ELSE 1 END, name
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$projectId", projectId);
        command.Parameters.AddWithValue("$componentType", componentType);
        return command.ExecuteScalar() as string ?? "";
    }

    private static string NormalizeComponentPresetReference(
        SqliteConnection connection,
        string projectId,
        string componentType,
        string currentValue)
    {
        var rows = ComponentClassRowsByType(connection, projectId, componentType);
        if (rows.Count == 0)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(currentValue)
            && TryParseComponentPresetNodeId(currentValue, out var componentClassId, out var presetId))
        {
            var row = rows.FirstOrDefault((candidate) => candidate.Id.Equals(componentClassId, StringComparison.Ordinal));
            if (row is not null && ComponentClassPresetsOrDefault(row).Any((preset) => preset.Id.Equals(presetId, StringComparison.Ordinal)))
            {
                return currentValue;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentValue))
        {
            var rowByClassId = rows.FirstOrDefault((candidate) => candidate.Id.Equals(currentValue, StringComparison.Ordinal));
            if (rowByClassId is not null)
            {
                return ComponentPresetNodeId(rowByClassId.Id, PreferredPresetId(rowByClassId));
            }

            var rowByPresetId = rows.FirstOrDefault((candidate) =>
                ComponentClassPresetsOrDefault(candidate).Any((preset) => preset.Id.Equals(currentValue, StringComparison.Ordinal)));
            if (rowByPresetId is not null)
            {
                return ComponentPresetNodeId(rowByPresetId.Id, currentValue);
            }

            var normalizedLookup = ComponentPresetLookupKey(currentValue);
            foreach (var row in rows)
            {
                var preset = ComponentClassPresetsOrDefault(row)
                    .FirstOrDefault((candidate) => ComponentPresetMatchesLookup(candidate, normalizedLookup));
                if (preset is not null)
                {
                    return ComponentPresetNodeId(row.Id, preset.Id);
                }
            }
        }

        var first = rows[0];
        return ComponentPresetNodeId(first.Id, PreferredPresetId(first));
    }

    private static bool ComponentPresetMatchesLookup(ComponentClassPreset preset, string normalizedLookup)
    {
        if (string.IsNullOrWhiteSpace(normalizedLookup))
        {
            return false;
        }

        return ComponentPresetLookupKey(preset.Id).Equals(normalizedLookup, StringComparison.Ordinal)
            || ComponentPresetLookupKey(preset.Name).Equals(normalizedLookup, StringComparison.Ordinal)
            || ComponentPresetLookupKey(preset.Name).EndsWith(normalizedLookup, StringComparison.Ordinal);
    }

    private static string ComponentPresetLookupKey(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static List<ComponentClassRow> ComponentClassRowsByType(
        SqliteConnection connection,
        string projectId,
        string componentType)
    {
        return QueryComponentClassRows(connection)
            .Where((row) => row.ProjectId.Equals(projectId, StringComparison.Ordinal))
            .Where((row) => row.ComponentType.Equals(componentType, StringComparison.Ordinal))
            .OrderBy((row) => row.Id.Equals($"component_{projectId}_{componentType}", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy((row) => row.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<ComponentClassPreset> ComponentClassPresetsOrDefault(ComponentClassRow row)
    {
        var presets = ComponentClassPresets(row.MetadataJson);
        return presets.Count > 0
            ? presets
            : [new ComponentClassPreset(DefaultComponentPresetId, "Default", true, true, row.ConfigJson)];
    }

    private static string PreferredPresetId(ComponentClassRow row)
    {
        var presets = ComponentClassPresetsOrDefault(row);
        return presets.FirstOrDefault((preset) => preset.Id.Equals(DefaultComponentPresetId, StringComparison.Ordinal))?.Id
            ?? presets[0].Id;
    }

    private static string GetComponentClassPresetConfigJson(
        SqliteConnection connection,
        string projectId,
        string componentType,
        string presetReference)
    {
        if (TryParseComponentPresetNodeId(presetReference, out var componentClassId, out var referencedPresetId))
        {
            var referencedRow = QueryComponentClassRows(connection)
                .FirstOrDefault((row) =>
                    row.ProjectId.Equals(projectId, StringComparison.Ordinal)
                    && row.Id.Equals(componentClassId, StringComparison.Ordinal)
                    && row.ComponentType.Equals(componentType, StringComparison.Ordinal));
            if (referencedRow is null)
            {
                throw new InvalidOperationException($"Missing component variant reference '{presetReference}'.");
            }

            return ComponentPresetConfigJson(referencedRow, referencedPresetId);
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT config_json, metadata_json
            FROM component_classes
            WHERE project_id = $projectId
              AND component_type = $componentType
            ORDER BY CASE WHEN id = 'component_' || $projectId || '_' || component_type THEN 0 ELSE 1 END, name
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$projectId", projectId);
        command.Parameters.AddWithValue("$componentType", componentType);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing base component class '{componentType}' for project '{projectId}'.");
        }

        var classConfigJson = ReadString(reader, 0);
        var metadataJson = ReadString(reader, 1);
        return ComponentPresetConfigJson(classConfigJson, metadataJson, presetReference);
    }

    private static string ComponentPresetConfigJson(ComponentClassRow row, string presetId)
    {
        return ComponentPresetConfigJson(row.ConfigJson, row.MetadataJson, presetId);
    }

    private static string ComponentPresetConfigJson(string classConfigJson, string metadataJson, string presetId)
    {
        var preset = ComponentClassPresets(metadataJson)
            .FirstOrDefault((candidate) => candidate.Id.Equals(presetId, StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(preset?.ConfigJson) || preset.ConfigJson == "{}"
            ? classConfigJson
            : preset.ConfigJson;
    }

    private IReadOnlyList<FieldOption> EmbeddedComponentOptions(string projectId, string recordClassId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM component_classes
            WHERE project_id = $projectId
              AND record_class_id = $recordClassId
            ORDER BY CASE WHEN id = 'component_' || $projectId || '_' || component_type THEN 0 ELSE 1 END, name
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$projectId", projectId);
        command.Parameters.AddWithValue("$recordClassId", recordClassId);
        var name = command.ExecuteScalar() as string;
        return [new FieldOption(recordClassId, string.IsNullOrWhiteSpace(name) ? recordClassId : name)];
    }

    private IReadOnlyList<FieldOption> ComponentPresetOptions(string projectId, string componentType)
    {
        return GetComponentPresetReferenceOptionsByType(projectId, componentType);
    }

    private static string ComponentPresetName(
        SqliteConnection connection,
        string projectId,
        string componentType,
        string presetReference)
    {
        if (TryParseComponentPresetNodeId(presetReference, out var componentClassId, out var referencedPresetId))
        {
            var row = QueryComponentClassRows(connection)
                .FirstOrDefault((candidate) =>
                    candidate.ProjectId.Equals(projectId, StringComparison.Ordinal)
                    && candidate.Id.Equals(componentClassId, StringComparison.Ordinal)
                    && candidate.ComponentType.Equals(componentType, StringComparison.Ordinal));
            if (row is null)
            {
                return presetReference;
            }

            var referencedPreset = ComponentClassPresetsOrDefault(row)
                .FirstOrDefault((candidate) => candidate.Id.Equals(referencedPresetId, StringComparison.Ordinal));
            var presetName = string.IsNullOrWhiteSpace(referencedPreset?.Name) ? referencedPresetId : referencedPreset.Name;
            return $"{row.Name} · {presetName}";
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT metadata_json
            FROM component_classes
            WHERE project_id = $projectId
              AND component_type = $componentType
            ORDER BY CASE WHEN id = 'component_' || $projectId || '_' || component_type THEN 0 ELSE 1 END, name
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$projectId", projectId);
        command.Parameters.AddWithValue("$componentType", componentType);
        var metadataJson = command.ExecuteScalar() as string ?? "";
        var preset = ComponentClassPresets(metadataJson)
            .FirstOrDefault((candidate) => candidate.Id.Equals(presetReference, StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(preset?.Name) ? presetReference : preset.Name;
    }

    private IReadOnlyList<FieldOption>? ComponentClassFieldOptions(
        string projectId,
        ComponentClassFieldDescriptor descriptor)
    {
        return descriptor.ValueKind switch
        {
            ValueKind.EmbeddedComponent => EmbeddedComponentOptions(projectId, descriptor.DefaultValue),
            ValueKind.ComponentPreset when EmbeddedComponentSlotCatalog.TryGet(descriptor.Id, out var slot)
                => ComponentPresetOptions(projectId, slot.EmbeddedComponentType),
            ValueKind.ComponentPreset when !string.IsNullOrWhiteSpace(descriptor.ComponentPresetType)
                => ComponentPresetOptions(projectId, descriptor.ComponentPresetType),
            ValueKind.OptionToken when !string.IsNullOrWhiteSpace(descriptor.ComponentPresetType)
                => ComponentPresetOptions(projectId, descriptor.ComponentPresetType),
            ValueKind.OptionToken when EmbeddedComponentPresetType(descriptor.Id) is { } componentType
                => ComponentPresetOptions(projectId, componentType),
            ValueKind.PaletteColorToken or ValueKind.PaletteColorPair or ValueKind.PaletteColorAlphaPair
                => GetPaletteColorOptions(projectId),
            ValueKind.TypographyStyle
                => [new FieldOption("theme", "Theme"), .. GetProductionFontOptions(projectId, "text")],
            _ => descriptor.Options,
        };
    }

    private static string? EmbeddedComponentPresetType(string fieldId)
    {
        if (!fieldId.EndsWith(".presetId", StringComparison.Ordinal))
        {
            return null;
        }

        var slotEditorFieldId = string.Concat(fieldId.AsSpan(0, fieldId.Length - ".presetId".Length), ".editor");
        return EmbeddedComponentSlotCatalog.TryGet(slotEditorFieldId, out var slot)
            ? slot.EmbeddedComponentType
            : null;
    }

    private static bool EmbeddedComponentHasOverrides(
        string configJson,
        EmbeddedComponentSlotDefinition slot)
    {
        var config = ParseJsonObject(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
        var overrides = EmbeddedOverrides(config, slot, createIfMissing: false);
        return overrides is not null && HasEffectiveJsonValue(overrides);
    }

    private static bool HasEffectiveJsonValue(JsonObject value)
    {
        foreach (var child in value)
        {
            if (child.Value is JsonObject childObject)
            {
                if (HasEffectiveJsonValue(childObject))
                {
                    return true;
                }

                continue;
            }

            if (child.Value is not null)
            {
                return true;
            }
        }

        return false;
    }

}
