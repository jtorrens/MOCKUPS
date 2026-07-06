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
    private const string DefaultComponentPresetId = "default";

    public sealed record EmbeddedComponentUsage(
        string ParentComponentClassId,
        string ParentComponentName,
        string ParentComponentType,
        string SlotFieldId,
        string SlotLabel,
        bool HasOverrides);

    public sealed record ComponentClassPreset(
        string Id,
        string Name,
        bool IsProtected,
        string ConfigJson);

    private static string ComponentPresetNodeId(string componentClassId, string presetId) =>
        $"{componentClassId}::preset::{presetId}";

    private static bool TryParseComponentPresetNodeId(string nodeId, out string componentClassId, out string presetId)
    {
        const string separator = "::preset::";
        var separatorIndex = nodeId.IndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex + separator.Length >= nodeId.Length)
        {
            componentClassId = "";
            presetId = "";
            return false;
        }

        componentClassId = nodeId[..separatorIndex];
        presetId = nodeId[(separatorIndex + separator.Length)..];
        return true;
    }

    private ProjectTreeNode DuplicateComponentPreset(ProjectTreeNode node)
    {
        if (!TryParseComponentPresetNodeId(node.Id, out var componentClassId, out var presetId))
        {
            throw new InvalidOperationException($"Invalid component preset node id '{node.Id}'.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(settings.MetadataJson) ? "{}" : settings.MetadataJson);
            var presets = EnsurePresetArray(metadata);
            var source = FindPreset(presets, presetId)
                ?? throw new InvalidOperationException($"Missing component preset '{presetId}'.");
            var sourceName = JsonPath.String(source, "name", presetId);
            var copyName = $"{sourceName} copy";
            var copyId = UniquePresetId(presets, copyName);
            presets.Add(new JsonObject
            {
                ["id"] = copyId,
                ["name"] = copyName,
                ["protected"] = false,
                ["config"] = (source["config"] as JsonObject)?.DeepClone() ?? ParseJsonObject(settings.ConfigJson),
            });
            Execute(
                connection,
                "UPDATE component_classes SET metadata_json = $metadataJson WHERE id = $id",
                ("$id", componentClassId),
                ("$metadataJson", metadata.ToJsonString()));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.ComponentPreset,
                ComponentPresetNodeId(componentClassId, copyId),
                copyName,
                "Component preset",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ComponentPreset),
                node.Parent);
        }
    }

    public ProjectTreeNode SaveComponentPreset(ProjectTreeNode componentClassNode, string name)
    {
        if (componentClassNode.Kind != ProjectTreeNodeKind.ComponentClass)
        {
            throw new InvalidOperationException("Component presets can only be saved from a component class.");
        }

        var presetName = name.Trim();
        if (string.IsNullOrWhiteSpace(presetName))
        {
            throw new InvalidOperationException("Preset name cannot be empty.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassNode.Id);
            var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(settings.MetadataJson) ? "{}" : settings.MetadataJson);
            var presets = EnsurePresetArray(metadata);
            var presetId = UniquePresetId(presets, presetName);
            presets.Add(new JsonObject
            {
                ["id"] = presetId,
                ["name"] = presetName,
                ["protected"] = false,
                ["config"] = JsonNode.Parse(settings.ConfigJson),
            });
            Execute(
                connection,
                "UPDATE component_classes SET metadata_json = $metadataJson WHERE id = $id",
                ("$id", componentClassNode.Id),
                ("$metadataJson", metadata.ToJsonString()));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.ComponentPreset,
                ComponentPresetNodeId(componentClassNode.Id, presetId),
                presetName,
                "Component preset",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ComponentPreset),
                componentClassNode);
        }
    }

    private void DeleteComponentPreset(ProjectTreeNode node)
    {
        if (!TryParseComponentPresetNodeId(node.Id, out var componentClassId, out var presetId))
        {
            throw new InvalidOperationException($"Invalid component preset node id '{node.Id}'.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(settings.MetadataJson) ? "{}" : settings.MetadataJson);
            var presets = EnsurePresetArray(metadata);
            for (var index = 0; index < presets.Count; index++)
            {
                if (presets[index] is not JsonObject preset
                    || !JsonPath.String(preset, "id", "").Equals(presetId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (JsonBool(preset, ["protected"]))
                {
                    throw new InvalidOperationException("Protected component presets cannot be deleted.");
                }

                var usages = GetComponentPresetReferenceUsages(connection, node);
                if (usages.Count > 0)
                {
                    throw new InvalidOperationException($"This component preset is still used and cannot be deleted.\n\n{string.Join(Environment.NewLine, usages.Take(12))}");
                }

                presets.RemoveAt(index);
                Execute(
                    connection,
                    "UPDATE component_classes SET metadata_json = $metadataJson WHERE id = $id",
                    ("$id", componentClassId),
                    ("$metadataJson", metadata.ToJsonString()));
                return;
            }
        }

        throw new InvalidOperationException($"Missing component preset '{presetId}'.");
    }

    public ProjectTreeNode RenameComponentPreset(ProjectTreeNode node, string name)
    {
        if (!TryParseComponentPresetNodeId(node.Id, out var componentClassId, out var presetId))
        {
            throw new InvalidOperationException($"Invalid component preset node id '{node.Id}'.");
        }

        var nextName = name.Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            throw new InvalidOperationException("Preset name cannot be empty.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(settings.MetadataJson) ? "{}" : settings.MetadataJson);
            var presets = EnsurePresetArray(metadata);
            var preset = FindPreset(presets, presetId)
                ?? throw new InvalidOperationException($"Missing component preset '{presetId}'.");
            preset["name"] = nextName;
            Execute(
                connection,
                "UPDATE component_classes SET metadata_json = $metadataJson WHERE id = $id",
                ("$id", componentClassId),
                ("$metadataJson", metadata.ToJsonString()));
        }

        return new ProjectTreeNode(
            ProjectTreeNodeKind.ComponentPreset,
            node.Id,
            nextName,
            node.Notes,
            node.RecordClassId,
            node.Parent,
            isUsed: node.IsUsed,
            isProtected: node.IsProtected);
    }

    private static IReadOnlyList<string> GetComponentPresetReferenceUsages(SqliteConnection connection, ProjectTreeNode node)
    {
        if (!TryParseComponentPresetNodeId(node.Id, out var componentClassId, out var presetId))
        {
            return [];
        }

        var owner = GetComponentClassSettings(connection, componentClassId);
        var usages = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in QueryComponentClassRows(connection)
                     .Where((row) => row.ProjectId.Equals(owner.ProjectId, StringComparison.Ordinal)))
        {
            if (ComponentPresetIsUsedByConfig(row.ConfigJson, owner.ComponentType, presetId))
            {
                usages.Add($"Component Class: {row.Name}");
            }
        }

        return usages.ToList();
    }

    private static bool ComponentPresetIsUsedByConfig(
        string configJson,
        string componentType,
        string presetId)
    {
        var config = ParseJsonObject(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
        foreach (var slot in EmbeddedComponentSlotCatalog.All())
        {
            if (!slot.EmbeddedComponentType.Equals(componentType, StringComparison.Ordinal))
            {
                continue;
            }

            if (JsonPath.Get(config, slot.SlotPath) is not JsonObject slotNode)
            {
                continue;
            }

            if (JsonPath.String(slotNode, "presetId", DefaultComponentPresetId).Equals(presetId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static JsonArray EnsurePresetArray(JsonObject metadata)
    {
        if (metadata["presets"] is JsonArray presets)
        {
            return presets;
        }

        presets = [];
        metadata["presets"] = presets;
        return presets;
    }

    private static JsonObject? FindPreset(JsonArray presets, string presetId) =>
        presets
            .OfType<JsonObject>()
            .FirstOrDefault((preset) => JsonPath.String(preset, "id", "").Equals(presetId, StringComparison.Ordinal));

    private static string UniquePresetId(JsonArray presets, string name)
    {
        var baseId = new string(name
                .Trim()
                .ToLowerInvariant()
                .Select((character) => char.IsLetterOrDigit(character) ? character : '_')
                .ToArray())
            .Trim('_');
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = "preset";
        }

        var existing = presets
            .OfType<JsonObject>()
            .Select((preset) => JsonPath.String(preset, "id", ""))
            .ToHashSet(StringComparer.Ordinal);
        var candidate = baseId;
        var suffix = 2;
        while (existing.Contains(candidate))
        {
            candidate = $"{baseId}_{suffix}";
            suffix++;
        }

        return candidate;
    }

    public ComponentClassSettings GetComponentClassSettings(string componentClassId)
    {
        using var connection = OpenConnection();
        return GetComponentClassSettings(connection, componentClassId);
    }

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
            var config = ParseJsonObject(string.IsNullOrWhiteSpace(row.ConfigJson) ? "{}" : row.ConfigJson);
            foreach (var slot in EmbeddedComponentSlotCatalog.All()
                         .Where((candidate) => candidate.EmbeddedComponentType.Equals(componentType, StringComparison.Ordinal)))
            {
                if (JsonPath.Get(config, slot.SlotPath) is not JsonObject)
                {
                    continue;
                }

                usages.Add(new EmbeddedComponentUsage(
                    row.Id,
                    row.Name,
                    row.ComponentType,
                    slot.FieldId,
                    slot.Label,
                    EmbeddedComponentHasOverrides(row.ConfigJson, slot)));
            }
        }

        return usages
            .OrderBy((usage) => usage.ParentComponentType)
            .ThenBy((usage) => usage.ParentComponentName)
            .ThenBy((usage) => usage.SlotLabel)
            .ToList();
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
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT component_type, config_json, metadata_json
            FROM component_classes
            WHERE project_id = $projectId
            ORDER BY CASE WHEN id = 'component_' || $projectId || '_' || component_type THEN 0 ELSE 1 END, name
            """;
        command.Parameters.AddWithValue("$projectId", projectId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var componentType = reader.GetString(0);
            if (configs.ContainsKey(componentType))
            {
                continue;
            }

            configs[componentType] = ParseJsonObject(DefaultComponentPresetConfigJson(
                ReadString(reader, 1),
                ReadString(reader, 2)));
        }

        return configs.ToJsonString();
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

    private static string GetComponentClassPresetConfigJson(
        SqliteConnection connection,
        string projectId,
        string componentType,
        string presetId)
    {
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
        using var connection = OpenConnection();
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
        var options = ComponentClassPresets(metadataJson)
            .Select((preset) => new FieldOption(preset.Id, preset.Name))
            .ToList();
        if (options.Count == 0)
        {
            options.Add(new FieldOption(DefaultComponentPresetId, "Default"));
        }

        return options;
    }

    private static string ComponentPresetName(
        SqliteConnection connection,
        string projectId,
        string componentType,
        string presetId)
    {
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
            .FirstOrDefault((candidate) => candidate.Id.Equals(presetId, StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(preset?.Name) ? presetId : preset.Name;
    }

    private IReadOnlyList<FieldOption>? ComponentClassFieldOptions(
        string projectId,
        ComponentClassFieldDescriptor descriptor)
    {
        return descriptor.ValueKind switch
        {
            ValueKind.EmbeddedComponent => EmbeddedComponentOptions(projectId, descriptor.DefaultValue),
            ValueKind.OptionToken when EmbeddedComponentPresetType(descriptor.Id) is { } componentType
                => ComponentPresetOptions(projectId, componentType),
            ValueKind.PaletteColorToken or ValueKind.PaletteColorPair or ValueKind.PaletteColorAlphaPair
                => GetPaletteColorOptions(projectId),
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

    private static ComponentClassSettings GetComponentClassSettings(SqliteConnection connection, string componentClassId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT project_id, component_type, record_class_id, name, notes, config_json, design_preview_json, metadata_json
            FROM component_classes
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", componentClassId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing component class '{componentClassId}'.");
        }

        return new ComponentClassSettings(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            ReadString(reader, 4),
            ReadString(reader, 5),
            ReadString(reader, 6),
            ReadString(reader, 7));
    }

    public FieldValue CreateComponentClassFieldValue(string componentClassId, string fieldId)
    {
        var settings = GetComponentClassSettings(componentClassId);
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var value = fieldId == "component.type"
            ? ComponentTypeLabel(settings.ComponentType)
            : ComponentConfigFieldValue(settings.ConfigJson, descriptor);
        var options = ComponentClassFieldOptions(settings.ProjectId, descriptor);
        var isHighlighted = descriptor.ValueKind == ValueKind.EmbeddedComponent
            && EmbeddedComponentSlotCatalog.TryGet(fieldId, out var slot)
            && EmbeddedComponentHasOverrides(settings.ConfigJson, slot);

        return new FieldValue(
            new FieldDefinition(
                descriptor.Id,
                descriptor.Label,
                descriptor.ValueKind,
                descriptor.IsEditable,
                descriptor.DefaultValue,
                Options: options,
                PairLabels: descriptor.PairLabels,
                Number: descriptor.Number),
            value,
            IsHighlighted: isHighlighted);
    }

    public void UpdateComponentClassField(string componentClassId, string fieldId, string value)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        if (!descriptor.IsEditable || descriptor.JsonPath.Length == 0)
        {
            return;
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var config = ParseJsonObject(string.IsNullOrWhiteSpace(settings.ConfigJson) ? "{}" : settings.ConfigJson);
            SetJsonValue(config, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value));
            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson WHERE id = $id",
                ("$id", componentClassId),
                ("$configJson", config.ToJsonString()));
        }
    }

    public FieldValue CreateEmbeddedComponentFieldValue(
        string componentClassId,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId)
    {
        var slot = EmbeddedComponentSlotCatalog.Get(slotFieldId);
        if (!slot.EmbeddedComponentType.Equals(embeddedComponentType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Embedded component '{embeddedComponentType}' is not supported for slot '{slotFieldId}'.");
        }

        var settings = GetComponentClassSettings(componentClassId);
        var descriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
        using var connection = OpenConnection();
        var config = ParseJsonObject(string.IsNullOrWhiteSpace(settings.ConfigJson) ? "{}" : settings.ConfigJson);
        var inheritedConfigJson = EffectiveEmbeddedBaseConfig(connection, settings.ProjectId, config, [slot]).ToJsonString();
        var inheritedValue = ComponentConfigFieldValue(inheritedConfigJson, descriptor);
        var overrides = EmbeddedOverrides(config, slot, createIfMissing: false);
        var hasOverride = overrides is not null && GetJsonValue(overrides, descriptor.JsonPath) is not null;
        var localValue = hasOverride && overrides is not null
            ? ComponentConfigFieldValue(overrides.ToJsonString(), descriptor)
            : inheritedValue;

        return new FieldValue(
            new FieldDefinition(
                descriptor.Id,
                descriptor.Label,
                descriptor.ValueKind,
                descriptor.IsEditable,
                descriptor.DefaultValue,
                CanInherit: true,
                InheritedValue: inheritedValue,
                Options: ComponentClassFieldOptions(settings.ProjectId, descriptor),
                PairLabels: descriptor.PairLabels,
                Number: descriptor.Number),
            localValue,
            IsInherited: !hasOverride);
    }

    public FieldValue CreateEmbeddedComponentFieldValue(
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId)
    {
        if (slots.Count == 0)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        var settings = GetComponentClassSettings(componentClassId);
        var descriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
        using var connection = OpenConnection();
        var config = ParseJsonObject(string.IsNullOrWhiteSpace(settings.ConfigJson) ? "{}" : settings.ConfigJson);
        var inheritedConfig = EffectiveEmbeddedBaseConfig(connection, settings.ProjectId, config, slots);
        var inheritedValue = ComponentConfigFieldValue(inheritedConfig.ToJsonString(), descriptor);
        var overrides = EmbeddedOverrides(config, slots, createIfMissing: false);
        var hasOverride = overrides is not null && GetJsonValue(overrides, descriptor.JsonPath) is not null;
        var localValue = hasOverride && overrides is not null
            ? ComponentConfigFieldValue(overrides.ToJsonString(), descriptor)
            : inheritedValue;
        var options = ComponentClassFieldOptions(settings.ProjectId, descriptor);
        var isHighlighted = descriptor.ValueKind == ValueKind.EmbeddedComponent
            && EmbeddedComponentSlotCatalog.TryGet(embeddedFieldId, out var nestedSlot)
            && EmbeddedComponentHasOverrides(config, [.. slots, nestedSlot]);

        return new FieldValue(
            new FieldDefinition(
                descriptor.Id,
                descriptor.Label,
                descriptor.ValueKind,
                descriptor.IsEditable,
                descriptor.DefaultValue,
                CanInherit: true,
                InheritedValue: inheritedValue,
                Options: options,
                PairLabels: descriptor.PairLabels,
                Number: descriptor.Number),
            localValue,
            IsInherited: !hasOverride,
            IsHighlighted: isHighlighted);
    }

    public void UpdateEmbeddedComponentField(
        string componentClassId,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId,
        string value)
    {
        var slot = EmbeddedComponentSlotCatalog.Get(slotFieldId);
        if (!slot.EmbeddedComponentType.Equals(embeddedComponentType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Embedded component '{embeddedComponentType}' is not supported for slot '{slotFieldId}'.");
        }

        var descriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var config = ParseJsonObject(string.IsNullOrWhiteSpace(settings.ConfigJson) ? "{}" : settings.ConfigJson);
            var overrides = EmbeddedOverrides(config, slot, createIfMissing: true)
                ?? throw new InvalidOperationException($"Missing embedded override slot '{slotFieldId}'.");

            if (value.Equals("inherited", StringComparison.Ordinal))
            {
                RemoveJsonValue(overrides, descriptor.JsonPath);
            }
            else
            {
                SetJsonValue(overrides, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value));
            }

            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson WHERE id = $id",
                ("$id", componentClassId),
                ("$configJson", config.ToJsonString()));
        }
    }

    public void UpdateEmbeddedComponentField(
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value)
    {
        if (slots.Count == 0)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        var descriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var config = ParseJsonObject(string.IsNullOrWhiteSpace(settings.ConfigJson) ? "{}" : settings.ConfigJson);
            var overrides = EmbeddedOverrides(config, slots, createIfMissing: true)
                ?? throw new InvalidOperationException($"Missing embedded override slot '{slots[^1].FieldId}'.");

            if (value.Equals("inherited", StringComparison.Ordinal))
            {
                RemoveJsonValue(overrides, descriptor.JsonPath);
            }
            else
            {
                SetJsonValue(overrides, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value));
            }

            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson WHERE id = $id",
                ("$id", componentClassId),
                ("$configJson", config.ToJsonString()));
        }
    }

    private static void SeedComponentClassesIfEmpty(SqliteConnection connection)
    {
        var projectIds = QueryProjectRows(connection).Select((project) => project.Id).ToList();
        foreach (var projectId in projectIds)
        {
            foreach (var seed in ComponentSeedRows)
            {
                if (ScalarLong(
                        connection,
                        "SELECT COUNT(*) FROM component_classes WHERE project_id = $projectId AND component_type = $componentType",
                        ("$projectId", projectId),
                        ("$componentType", seed.ComponentType)) > 0)
                {
                    continue;
                }

                Execute(
                    connection,
                    """
                    INSERT INTO component_classes (id, project_id, component_type, record_class_id, name, notes, config_json, design_preview_json, metadata_json)
                    VALUES ($id, $projectId, $componentType, $recordClassId, $name, $notes, $configJson, $designPreviewJson, $metadataJson)
                    """,
                    ("$id", $"component_{projectId}_{seed.ComponentType}"),
                    ("$projectId", projectId),
                    ("$componentType", seed.ComponentType),
                    ("$recordClassId", seed.RecordClassId),
                    ("$name", seed.Name),
                    ("$notes", ComponentTypeLabel(seed.ComponentType)),
                    ("$configJson", seed.ConfigJson),
                    ("$designPreviewJson", seed.DesignPreviewJson),
                    ("$metadataJson", seed.MetadataJson));
            }
        }
    }

    private static void EnsureComponentClassConfigDefaults(SqliteConnection connection)
    {
        foreach (var row in QueryComponentClassRows(connection))
        {
            var config = ParseJsonObject(string.IsNullOrWhiteSpace(row.ConfigJson) ? "{}" : row.ConfigJson);
            var defaults = ParseJsonObject(DefaultComponentClassConfigJson(row.ComponentType));
            var configChanged = NormalizeAvatarLabelPlacement(row.ComponentType, config);
            configChanged |= NormalizeButtonIconLabelSlot(row.ComponentType, config);
            configChanged |= NormalizeAudioEmbeddedSlots(row.ComponentType, config);
            configChanged |= NormalizeEmbeddedSlotPresetIds(config);
            configChanged |= JsonPath.MergeMissing(config, defaults);
            configChanged |= NormalizeReliefIntensity(config, "reliefTopIntensity");
            configChanged |= NormalizeReliefIntensity(config, "reliefBottomIntensity");

            var designPreview = ParseJsonObject(string.IsNullOrWhiteSpace(row.DesignPreviewJson) ? "{}" : row.DesignPreviewJson);
            var designPreviewDefaults = ParseJsonObject(DefaultComponentDesignPreviewJson(row.ComponentType));
            var designPreviewChanged = JsonPath.MergeMissing(designPreview, designPreviewDefaults);
            designPreviewChanged |= EnsureComponentInputs(designPreview, designPreviewDefaults);
            designPreviewChanged |= EnsureComponentDesignPreviewText(row.ComponentType, designPreview);
            designPreviewChanged |= EnsureButtonIconPreviewSize(row.ComponentType, designPreview);

            var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(row.MetadataJson) ? "{}" : row.MetadataJson);
            var metadataChanged = EnsureDefaultComponentPreset(metadata, config);

            if (!configChanged && !designPreviewChanged && !metadataChanged)
            {
                continue;
            }

            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson, design_preview_json = $designPreviewJson, metadata_json = $metadataJson WHERE id = $id",
                ("$id", row.Id),
                ("$configJson", config.ToJsonString()),
                ("$designPreviewJson", designPreview.ToJsonString()),
                ("$metadataJson", metadata.ToJsonString()));
        }
    }

    private static bool EnsureDefaultComponentPreset(JsonObject metadata, JsonObject config)
    {
        if (metadata["presets"] is not JsonArray presets)
        {
            presets = [];
            metadata["presets"] = presets;
        }

        foreach (var presetNode in presets.OfType<JsonObject>())
        {
            if (!JsonPath.String(presetNode, "id", "").Equals(DefaultComponentPresetId, StringComparison.Ordinal))
            {
                continue;
            }

            var changed = false;
            if (!JsonPath.String(presetNode, "name", "").Equals("Default", StringComparison.Ordinal))
            {
                presetNode["name"] = "Default";
                changed = true;
            }

            if (!JsonBool(presetNode, ["protected"]))
            {
                presetNode["protected"] = true;
                changed = true;
            }

            if (presetNode["config"] is not JsonObject)
            {
                presetNode["config"] = JsonNode.Parse(config.ToJsonString());
                changed = true;
            }

            return changed;
        }

        presets.Insert(0, new JsonObject
        {
            ["id"] = DefaultComponentPresetId,
            ["name"] = "Default",
            ["protected"] = true,
            ["config"] = JsonNode.Parse(config.ToJsonString()),
        });
        return true;
    }

    private static IReadOnlyList<ComponentClassPreset> ComponentClassPresets(string metadataJson)
    {
        var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson);
        if (metadata["presets"] is not JsonArray presets)
        {
            return [];
        }

        return presets
            .OfType<JsonObject>()
            .Select((preset) =>
            {
                var id = JsonPath.String(preset, "id", "");
                var name = JsonPath.String(preset, "name", id);
                var config = preset["config"] is JsonObject configObject ? configObject.ToJsonString() : "{}";
                return new ComponentClassPreset(
                    id,
                    string.IsNullOrWhiteSpace(name) ? id : name,
                    JsonBool(preset, ["protected"]),
                    config);
            })
            .Where((preset) => !string.IsNullOrWhiteSpace(preset.Id))
            .OrderBy((preset) => preset.Id.Equals(DefaultComponentPresetId, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy((preset) => preset.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static string DefaultComponentPresetConfigJson(string classConfigJson, string metadataJson)
    {
        var defaultPreset = ComponentClassPresets(metadataJson)
            .FirstOrDefault((preset) => preset.Id.Equals(DefaultComponentPresetId, StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(defaultPreset?.ConfigJson) || defaultPreset.ConfigJson == "{}"
            ? classConfigJson
            : defaultPreset.ConfigJson;
    }

    private static bool NormalizeAvatarLabelPlacement(string componentType, JsonObject config)
    {
        if (componentType != "avatar")
        {
            return false;
        }

        var labelSlot = JsonPath.Get(config, ["avatar", "labelSlot"]) as JsonObject;
        if (labelSlot is null || labelSlot["placement"] is not null)
        {
            return false;
        }

        var position = JsonPath.String(labelSlot, "position", "bottom");
        var gap = JsonPath.Number(labelSlot, "gap", 4);
        labelSlot["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromLegacyPosition(position, gap).ToJsonString());
        if (labelSlot["presetId"] is null)
        {
            labelSlot["presetId"] = DefaultComponentPresetId;
        }

        return true;
    }

    private static bool NormalizeButtonIconLabelSlot(string componentType, JsonObject config)
    {
        if (componentType != "buttonIcon")
        {
            return false;
        }

        var buttonIcon = JsonPath.Get(config, ["buttonIcon"]) as JsonObject;
        if (buttonIcon is null || buttonIcon["labelSlot"] is not null)
        {
            return false;
        }

        var labelEnabled = JsonBool(buttonIcon, ["labelEnabled"]);
        var labelPosition = JsonPath.String(buttonIcon, "labelPosition", "bottom");
        var labelPadding = JsonPath.Number(buttonIcon, "labelPadding", 3);
        buttonIcon["labelSlot"] = new JsonObject
        {
            ["showLabel"] = labelEnabled,
            ["showSubtext"] = false,
            ["presetId"] = DefaultComponentPresetId,
            ["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromLegacyPosition(labelPosition, labelPadding).ToJsonString()),
            ["overrides"] = new JsonObject(),
        };
        return true;
    }

    private static bool NormalizeAudioEmbeddedSlots(string componentType, JsonObject config)
    {
        if (componentType != "audio")
        {
            return false;
        }

        var audio = JsonPath.Get(config, ["audio"]) as JsonObject;
        if (audio is null)
        {
            return false;
        }

        var changed = false;
        if (audio["padding"] is null)
        {
            audio["padding"] = "10|8";
            changed = true;
        }

        if (audio["playIconPadding"] is null)
        {
            audio["playIconPadding"] = 9;
            changed = true;
        }

        changed |= NormalizeNumber(audio, "playCircleSize", 32, minimum: 8);
        changed |= NormalizeNumber(audio, "textSize", 11, minimum: 6);
        changed |= NormalizeNumber(audio, "waveformBarCount", 28, minimum: 4);
        changed |= NormalizeNumber(audio, "waveformGap", 2, minimum: 0);
        changed |= NormalizeNumber(audio, "waveformMinHeight", 4, minimum: 1);
        changed |= NormalizeNumber(audio, "waveformMaxHeight", 22, minimum: 2);
        changed |= NormalizeNumber(audio, "progressKnobSize", 9, minimum: 4);

        if (audio["waveformBarWidth"] is null)
        {
            audio["waveformBarWidth"] = 3;
            changed = true;
        }
        else
        {
            changed |= NormalizeNumber(audio, "waveformBarWidth", 3, minimum: 1);
        }

        if (audio["progressKnobSize"] is null && audio["knobSize"] is not null)
        {
            audio["progressKnobSize"] = audio["knobSize"]?.DeepClone();
            changed = true;
        }

        if (audio["avatarSlot"] is null)
        {
            var avatarPosition = JsonPath.String(audio, "avatarPosition", "right");
            var avatarSize = JsonPath.Get(audio, ["avatarSize"]);
            var avatarOverrides = new JsonObject();
            if (avatarSize is not null)
            {
                JsonPath.Set(avatarOverrides, ["avatar", "defaultSize"], avatarSize.DeepClone());
            }

            audio["avatarSlot"] = new JsonObject
            {
                ["showAvatar"] = true,
                ["presetId"] = DefaultComponentPresetId,
                ["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromLegacyPosition(avatarPosition, 4).ToJsonString()),
                ["overrides"] = avatarOverrides,
            };
            changed = true;
        }

        var badgeSlot = audio["badgeSlot"] as JsonObject;
        if (badgeSlot is null)
        {
            badgeSlot = new JsonObject
            {
                ["showBadge"] = false,
                ["iconToken"] = "media_mic",
                ["backgroundColor"] = "blue",
                ["iconColor"] = "gray_100",
                ["presetId"] = DefaultComponentPresetId,
                ["placement"] = JsonNode.Parse("""{"mode":"center","alignX":1,"alignY":1,"offsetX":0,"offsetY":0}"""),
                ["overrides"] = new JsonObject
                {
                    ["buttonIcon"] = new JsonObject
                    {
                        ["size"] = 16,
                        ["iconPadding"] = 3,
                    },
                },
            };
            audio["badgeSlot"] = badgeSlot;
            changed = true;
        }

        if (badgeSlot["backgroundColor"] is null)
        {
            badgeSlot["backgroundColor"] = "blue";
            changed = true;
        }

        if (badgeSlot["iconToken"] is null)
        {
            badgeSlot["iconToken"] = "media_mic";
            changed = true;
        }

        if (badgeSlot["iconColor"] is null)
        {
            badgeSlot["iconColor"] = "gray_100";
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeEmbeddedSlotPresetIds(JsonObject config)
    {
        var changed = false;
        foreach (var slot in EmbeddedComponentSlotCatalog.All())
        {
            if (JsonPath.Get(config, slot.SlotPath) is not JsonObject slotNode)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(JsonPath.String(slotNode, "presetId", "")))
            {
                continue;
            }

            slotNode["presetId"] = DefaultComponentPresetId;
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeNumber(
        JsonObject owner,
        string property,
        double replacement,
        double minimum)
    {
        var value = owner[property];
        if (value is not JsonValue jsonValue
            || !jsonValue.TryGetValue<double>(out var number)
            || !double.IsFinite(number)
            || number < minimum)
        {
            owner[property] = replacement;
            return true;
        }

        return false;
    }

    private static bool NormalizeReliefIntensity(JsonObject config, string key)
    {
        var style = JsonPath.Get(config, ["style"]) as JsonObject;
        if (style is null)
        {
            return false;
        }

        var value = JsonPath.Number(style, key, 0);
        if (Math.Abs(value) <= 1)
        {
            return false;
        }

        style[key] = JsonValue.Create(Math.Clamp(value / 100, -1, 1));
        return true;
    }

    private static bool EnsureComponentDesignPreviewText(string componentType, JsonObject designPreview)
    {
        if (componentType != "avatar")
        {
            return false;
        }

        if (JsonPath.String(designPreview, "sampleSubtext", "").Trim().Length > 0)
        {
            return false;
        }

        designPreview["sampleSubtext"] = "Subtitle";
        return true;
    }

    private static bool EnsureComponentInputs(
        JsonObject designPreview,
        JsonObject designPreviewDefaults)
    {
        if (designPreviewDefaults["inputs"] is not JsonArray defaultInputs)
        {
            return false;
        }

        if (designPreview["inputs"] is not JsonArray inputs)
        {
            designPreview["inputs"] = JsonNode.Parse(defaultInputs.ToJsonString());
            return true;
        }

        var existingIds = inputs
            .OfType<JsonObject>()
            .Select((input) => JsonPath.String(input, "id", ""))
            .Where((id) => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var changed = false;
        foreach (var defaultInput in defaultInputs.OfType<JsonObject>())
        {
            var id = JsonPath.String(defaultInput, "id", "");
            if (string.IsNullOrWhiteSpace(id) || existingIds.Contains(id))
            {
                continue;
            }

            inputs.Add(JsonNode.Parse(defaultInput.ToJsonString()));
            existingIds.Add(id);
            changed = true;
        }

        return changed;
    }

    private static bool EnsureButtonIconPreviewSize(string componentType, JsonObject designPreview)
    {
        if (componentType != "buttonIcon")
        {
            return false;
        }

        var sampleSize = JsonPath.Number(designPreview, "sampleSize", 0);
        if (sampleSize > 0 && sampleSize <= 96)
        {
            return false;
        }

        designPreview["sampleSize"] = 48;
        return true;
    }

    private static List<ComponentClassRow> QueryComponentClassRows(SqliteConnection connection)
    {
        var rows = new List<ComponentClassRow>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, project_id, component_type, record_class_id, name, notes, config_json, design_preview_json, metadata_json
            FROM component_classes
            ORDER BY component_type, name
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ComponentClassRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                ReadString(reader, 5),
                ReadString(reader, 6),
                ReadString(reader, 7),
                ReadString(reader, 8)));
        }

        return rows;
    }

    private static void EnsureComponentClassColumns(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "component_classes", "notes", "TEXT NOT NULL DEFAULT ''");
    }

    private static string ComponentConfigFieldValue(string configJson, ComponentClassFieldDescriptor descriptor)
    {
        if (descriptor.ValueKind == ValueKind.EmbeddedComponent)
        {
            return descriptor.DefaultValue;
        }

        var config = ParseJsonObject(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
        var node = GetJsonValue(config, descriptor.JsonPath);
        if (node is null)
        {
            return descriptor.DefaultValue;
        }

        return descriptor.ValueKind switch
        {
            ValueKind.Boolean => BoolToString(node is JsonValue value && value.TryGetValue<bool>(out var boolean) && boolean),
            ValueKind.Integer => JsonNumberString(config, descriptor.JsonPath, descriptor.DefaultValue),
            ValueKind.Decimal => JsonNumberString(config, descriptor.JsonPath, descriptor.DefaultValue),
            ValueKind.Alpha => JsonNumberString(config, descriptor.JsonPath, descriptor.DefaultValue),
            ValueKind.IntegerPair => node is JsonValue pairValue && pairValue.TryGetValue<string>(out var pairText)
                ? pairText
                : descriptor.DefaultValue,
            ValueKind.AlignmentPlacement => node is JsonObject
                ? node.ToJsonString()
                : descriptor.DefaultValue,
            ValueKind.IconSlots => node.ToJsonString(),
            _ => node is JsonValue stringValue && stringValue.TryGetValue<string>(out var text)
                ? text
                : node.ToJsonString().Trim('"'),
        };
    }

    private static JsonNode ComponentConfigJsonValue(ValueKind valueKind, string value)
    {
        return valueKind switch
        {
            ValueKind.Boolean => JsonValue.Create(StringToBool(value))!,
            ValueKind.Integer => NumberNode(value),
            ValueKind.Decimal => NumberNode(value),
            ValueKind.Alpha => NumberNode(value),
            ValueKind.AlignmentPlacement => JsonNode.Parse(value)
                ?? throw new InvalidOperationException("Alignment placement value must be valid JSON."),
            ValueKind.IconSlots => JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? ComponentClassFieldCatalog.EmptyIconSlots : value)
                ?? JsonNode.Parse(ComponentClassFieldCatalog.EmptyIconSlots)!,
            _ => JsonValue.Create(value)!,
        };
    }

    private static JsonObject? EmbeddedOverrides(JsonObject config, EmbeddedComponentSlotDefinition slot, bool createIfMissing)
    {
        var slotNode = JsonPath.Get(config, slot.SlotPath) as JsonObject;
        if (slotNode is null)
        {
            if (!createIfMissing) return null;

            slotNode = [];
            JsonPath.Set(config, slot.SlotPath, slotNode);
        }

        if (slotNode["overrides"] is JsonObject overrides)
        {
            return overrides;
        }

        if (!createIfMissing) return null;

        overrides = [];
        slotNode["overrides"] = overrides;
        return overrides;
    }

    private static JsonObject? EmbeddedOverrides(
        JsonObject config,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        bool createIfMissing)
    {
        JsonObject? currentConfig = config;
        JsonObject? overrides = null;
        foreach (var slot in slots)
        {
            if (currentConfig is null)
            {
                return null;
            }

            overrides = EmbeddedOverrides(currentConfig, slot, createIfMissing);
            currentConfig = overrides;
        }

        return overrides;
    }

    private static bool EmbeddedComponentHasOverrides(
        JsonObject config,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        var overrides = EmbeddedOverrides(config, slots, createIfMissing: false);
        return overrides is not null && HasEffectiveJsonValue(overrides);
    }

    private static JsonObject EffectiveEmbeddedBaseConfig(
        SqliteConnection connection,
        string projectId,
        JsonObject ownerConfig,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        JsonObject? currentContainer = ownerConfig;
        JsonObject? current = null;
        for (var index = 0; index < slots.Count; index++)
        {
            var slotNode = currentContainer is null
                ? null
                : JsonPath.Get(currentContainer, slots[index].SlotPath) as JsonObject;
            var presetId = JsonPath.String(slotNode ?? [], "presetId", DefaultComponentPresetId);
            var child = ParseJsonObject(GetComponentClassPresetConfigJson(
                connection,
                projectId,
                slots[index].EmbeddedComponentType,
                presetId));
            var overrides = slotNode?["overrides"] as JsonObject;
            if (overrides is not null)
            {
                MergeOverride(child, overrides);
            }

            current = child;
            currentContainer = current;
        }

        return current ?? [];
    }

    private static void MergeOverride(JsonObject target, JsonObject overrides)
    {
        foreach (var pair in overrides)
        {
            if (pair.Value is JsonObject overrideObject
                && target[pair.Key] is JsonObject targetObject)
            {
                MergeOverride(targetObject, overrideObject);
                continue;
            }

            target[pair.Key] = pair.Value?.DeepClone();
        }
    }

    private static string ComponentTypeLabel(string componentType)
    {
        return componentType switch
        {
            "avatar" => "Avatar component",
            "status_bar" => "Status bar component",
            "navigation_bar" => "Navigation bar component",
            "textInputBar" => "Text input bar component",
            "keyboard" => "Keyboard component",
            "buttonIcon" => "Button icon component",
            "label" => "Label component",
            "audio" => "Audio component",
            "video" => "Video component",
            _ => componentType,
        };
    }

    private static ComponentSeedRow NewComponentSeed(string componentType, string recordClassId, string name)
    {
        return new ComponentSeedRow(
            componentType,
            recordClassId,
            name,
            DefaultComponentClassConfigJson(componentType),
            DefaultComponentDesignPreviewJson(componentType),
            JsonSerializer.Serialize(new { note = "Seeded reusable component class." }));
    }

    private static string DefaultComponentClassConfigJson(string componentType)
    {
        var config = new JsonObject
        {
            ["style"] = new JsonObject
            {
                ["shadowEnabled"] = false,
                ["reliefEnabled"] = false,
                ["borderWidth"] = 0,
                ["borderColorToken"] = "theme.borders.primary",
                ["cornerRadiusToken"] = componentType == "avatar" ? "theme.radii.avatar" : "theme.radii.surface",
                ["reliefAngle"] = -45,
                ["reliefExtent"] = 1,
                ["reliefSpread"] = 0,
                ["reliefTopIntensity"] = 0.12,
                ["reliefBottomIntensity"] = -0.1,
            },
        };

        switch (componentType)
        {
            case "avatar":
                config["avatar"] = new JsonObject
                {
                    ["defaultSize"] = 48,
                    ["cornerRadiusToken"] = "theme.radii.avatar",
                    ["labelSlot"] = new JsonObject
                    {
                        ["showLabel"] = false,
                        ["showSubtext"] = false,
                        ["presetId"] = DefaultComponentPresetId,
                        ["placement"] = JsonNode.Parse(AlignmentPlacementValue.Default.ToJsonString()),
                        ["overrides"] = new JsonObject(),
                    },
                };
                break;
            case "textInputBar":
                config["textInput"] = new JsonObject
                {
                    ["height"] = 44,
                    ["placeholder"] = "Message",
                    ["idleTextColorToken"] = "theme.colors.textSecondary",
                    ["cursorColorToken"] = "theme.cursor.color",
                    ["cursorWidth"] = 2,
                    ["cursorBlinkFrames"] = 18,
                };
                break;
            case "keyboard":
                config["keyboard"] = new JsonObject
                {
                    ["keyPadding"] = 4,
                    ["keyCornerRadius"] = 6,
                    ["keyShadowEnabled"] = false,
                    ["pressedEffect"] = "popup",
                    ["specialKeyTextScale"] = "0.65",
                    ["emojiScale"] = "1.2",
                    ["bottomIconSlots"] = JsonNode.Parse(ComponentClassFieldCatalog.EmptyIconSlots),
                };
                break;
            case "buttonIcon":
                config["buttonIcon"] = new JsonObject
                {
                    ["size"] = 48,
                    ["iconToken"] = "media_mic",
                    ["iconPadding"] = 6,
                    ["backgroundColorToken"] = "theme.colors.button",
                    ["backgroundAlpha"] = 1,
                    ["iconColorToken"] = "theme.colors.icon",
                    ["labelSlot"] = new JsonObject
                    {
                        ["showLabel"] = false,
                        ["showSubtext"] = false,
                        ["presetId"] = DefaultComponentPresetId,
                        ["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromLegacyPosition("bottom", 3).ToJsonString()),
                        ["overrides"] = new JsonObject(),
                    },
                };
                break;
            case "label":
                config["label"] = new JsonObject
                {
                    ["dimensionMode"] = "content",
                    ["size"] = "120|32",
                    ["padding"] = "8|4",
                    ["backgroundColorToken"] = "theme.colors.background",
                    ["alpha"] = 1,
                    ["textColorToken"] = "theme.colors.textPrimary",
                    ["textSizeToken"] = "theme.typography.sizes.s",
                    ["textStyle"] = "normal",
                    ["textAlign"] = "center",
                    ["textGap"] = 2,
                    ["subtextColorToken"] = "theme.colors.textSecondary",
                    ["subtextSizeToken"] = "theme.typography.sizes.xs",
                    ["subtextStyle"] = "normal",
                };
                break;
            case "audio":
                config["audio"] = new JsonObject
                {
                    ["padding"] = "10|8",
                    ["backgroundColorToken"] = "theme.colors.surface",
                    ["backgroundAlpha"] = 1,
                    ["textSize"] = 11,
                    ["textColorToken"] = "theme.icons.secondary",
                    ["playCircleSize"] = 32,
                    ["playIconPadding"] = 9,
                    ["playColorToken"] = "theme.icons.accent",
                    ["playIconColorToken"] = "theme.icons.secondary",
                    ["waveformColorToken"] = "theme.icons.primary",
                    ["waveformPlayedColorToken"] = "theme.icons.accent",
                    ["waveformBarCount"] = 28,
                    ["waveformBarWidth"] = 3,
                    ["waveformGap"] = 2,
                    ["waveformMinHeight"] = 4,
                    ["waveformMaxHeight"] = 22,
                    ["progressKnobSize"] = 9,
                    ["avatarSlot"] = new JsonObject
                    {
                        ["showAvatar"] = true,
                        ["presetId"] = DefaultComponentPresetId,
                        ["placement"] = JsonNode.Parse(AlignmentPlacementValue.FromLegacyPosition("left", 8).ToJsonString()),
                        ["overrides"] = new JsonObject
                        {
                            ["avatar"] = new JsonObject
                            {
                                ["defaultSize"] = 38,
                            },
                        },
                    },
                    ["badgeSlot"] = new JsonObject
                    {
                        ["showBadge"] = false,
                        ["iconToken"] = "media_mic",
                        ["backgroundColor"] = "blue",
                        ["iconColor"] = "gray_100",
                        ["presetId"] = DefaultComponentPresetId,
                        ["placement"] = JsonNode.Parse("""{"mode":"center","alignX":1,"alignY":1,"offsetX":0,"offsetY":0}"""),
                        ["overrides"] = new JsonObject
                        {
                            ["buttonIcon"] = new JsonObject
                            {
                                ["size"] = 16,
                                ["iconPadding"] = 3,
                            },
                        },
                    },
                };
                break;
            case "status_bar":
                return DefaultStatusBarConfigJson();
            case "navigation_bar":
                return DefaultNavigationBarConfigJson();
            case "video":
                config["video"] = new JsonObject
                {
                    ["statusVisible"] = true,
                    ["statusHeight"] = 24,
                    ["statusIconSlots"] = JsonNode.Parse("""{"left":["app_camera"],"center":[],"right":[]}"""),
                    ["playOverlayVisible"] = true,
                    ["playColorToken"] = "theme.icons.accent",
                };
                break;
        }

        return config.ToJsonString();
    }

    private static string DefaultComponentDesignPreviewJson(string componentType)
    {
        var preview = new JsonObject
        {
            ["componentType"] = componentType,
            ["sampleText"] = componentType switch
            {
                "label" => "Alex",
                "textInputBar" => "Message",
                "audio" => "0:05",
                "video" => "0:12",
                _ => "Sample",
            },
            ["sampleSubtext"] = componentType is "label" or "avatar" or "buttonIcon" ? "Subtitle" : "",
            ["sampleSize"] = componentType == "buttonIcon" ? 48 : 256,
            ["inputs"] = ComponentInputsForComponent(componentType),
        };
        if (componentType == "audio")
        {
            preview["animation"] = new JsonObject
            {
                ["playInputId"] = "isPlaying",
                ["durationInputId"] = "durationSeconds",
                ["timeJsonKey"] = "currentTimeSeconds",
            };
        }

        return preview.ToJsonString();
    }

    private static JsonArray ComponentInputsForComponent(string componentType)
    {
        return componentType switch
        {
            "label" =>
            [
                ComponentInput("sampleText", "Text", "sampleText", "text", "Sample"),
                ComponentInput("sampleSubtext", "Subtext", "sampleSubtext", "text", "Subtitle"),
            ],
            "avatar" =>
            [
                ComponentInput("actorId", "Actor", "actorId", "actor", ""),
                ComponentInput("sampleSubtext", "Subtitle", "sampleSubtext", "text", "Subtitle"),
            ],
            "buttonIcon" =>
            [
                ComponentInput("iconToken", "Icon", "iconToken", "icon", "media_mic"),
                ComponentInput("sampleText", "Text", "sampleText", "text", "Action"),
                ComponentInput("sampleSubtext", "Subtext", "sampleSubtext", "text", "Subtitle"),
            ],
            "audio" =>
            [
                ComponentInput("isPlaying", "Playing", "isPlaying", "boolean", "false"),
                ComponentInput("durationSeconds", "Duration", "durationSeconds", "number", "65", minimum: 1, maximum: 86400, increment: 1),
                ComponentInput("actorId", "Actor", "actorId", "actor", ""),
            ],
            _ => [],
        };
    }

    private static JsonObject ComponentInput(
        string id,
        string label,
        string jsonKey,
        string kind,
        string defaultValue,
        decimal minimum = 0,
        decimal maximum = 9999,
        decimal increment = 1)
    {
        return new JsonObject
        {
            ["id"] = id,
            ["label"] = label,
            ["jsonKey"] = jsonKey,
            ["kind"] = kind,
            ["defaultValue"] = defaultValue,
            ["minimum"] = minimum,
            ["maximum"] = maximum,
            ["increment"] = increment,
        };
    }

    private static readonly ComponentSeedRow[] ComponentSeedRows =
    [
        NewComponentSeed("avatar", "component.avatar", "Default Avatar"),
        NewComponentSeed("status_bar", "component.status_bar", "Default Status Bar"),
        NewComponentSeed("navigation_bar", "component.navigation_bar", "Default Navigation Bar"),
        NewComponentSeed("textInputBar", "component.text_input_bar", "Default Text Input Bar"),
        NewComponentSeed("keyboard", "component.keyboard", "Default Keyboard"),
        NewComponentSeed("buttonIcon", "component.button_icon", "Default Button Icon"),
        NewComponentSeed("label", "component.label", "Default Label"),
        NewComponentSeed("audio", "component.audio", "Default Audio"),
        NewComponentSeed("video", "component.video", "Default Video"),
    ];
}
