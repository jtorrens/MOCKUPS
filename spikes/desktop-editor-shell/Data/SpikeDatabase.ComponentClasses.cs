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
        bool HasOverrides,
        string SourceNodeId = "");

    public sealed record ComponentPresetReferenceUsage(
        string SourceKind,
        string SourceName,
        string Detail,
        string TargetNodeId,
        EmbeddedComponentUsage? EmbeddedUsage);

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

    private ProjectTreeNode RenameComponentClass(ProjectTreeNode node, string name)
    {
        var nextName = name.Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            throw new InvalidOperationException("Component class name cannot be empty.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            Execute(
                connection,
                "UPDATE component_classes SET name = $name WHERE id = $id",
                ("$id", node.Id),
                ("$name", nextName));
        }

        return new ProjectTreeNode(
            ProjectTreeNodeKind.ComponentClass,
            node.Id,
            nextName,
            node.Notes,
            node.RecordClassId,
            node.Parent,
            node.ColorHex,
            node.IsUsed,
            node.IsProtected);
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

    public ProjectTreeNode SaveComponentPreset(ProjectTreeNode sourceNode, string name)
    {
        if (sourceNode.Kind is not ProjectTreeNodeKind.ComponentPreset)
        {
            throw new InvalidOperationException("Component presets can only be saved from an active selected preset.");
        }

        var presetName = name.Trim();
        if (string.IsNullOrWhiteSpace(presetName))
        {
            throw new InvalidOperationException("Preset name cannot be empty.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            if (!TryParseComponentPresetNodeId(sourceNode.Id, out var componentClassId, out _))
            {
                throw new InvalidOperationException($"Invalid component preset node id '{sourceNode.Id}'.");
            }

            var sourceConfigJson = GetComponentPresetSettings(connection, sourceNode).ConfigJson;
            var settings = GetComponentClassSettings(connection, componentClassId);
            var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(settings.MetadataJson) ? "{}" : settings.MetadataJson);
            var presets = EnsurePresetArray(metadata);
            var presetId = UniquePresetId(presets, presetName);
            presets.Add(new JsonObject
            {
                ["id"] = presetId,
                ["name"] = presetName,
                ["protected"] = false,
                ["config"] = JsonNode.Parse(sourceConfigJson),
            });
            Execute(
                connection,
                "UPDATE component_classes SET metadata_json = $metadataJson WHERE id = $id",
                ("$id", componentClassId),
                ("$metadataJson", metadata.ToJsonString()));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.ComponentPreset,
                ComponentPresetNodeId(componentClassId, presetId),
                presetName,
                "Component preset",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ComponentPreset),
                sourceNode.Parent);
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

    public IReadOnlyList<ComponentPresetReferenceUsage> GetComponentPresetReferenceUsageDetails(ProjectTreeNode node)
    {
        using var connection = OpenConnection();
        return GetComponentPresetReferenceUsageDetails(connection, node);
    }

    private static IReadOnlyList<string> GetComponentPresetReferenceUsages(SqliteConnection connection, ProjectTreeNode node)
    {
        return GetComponentPresetReferenceUsageDetails(connection, node)
            .Select((usage) => $"{usage.SourceKind}: {usage.SourceName}{(string.IsNullOrWhiteSpace(usage.Detail) ? "" : $" · {usage.Detail}")}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy((usage) => usage, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<ComponentPresetReferenceUsage> GetComponentPresetReferenceUsageDetails(SqliteConnection connection, ProjectTreeNode node)
    {
        if (!TryParseComponentPresetNodeId(node.Id, out var componentClassId, out var presetId))
        {
            return [];
        }

        var owner = GetComponentClassSettings(connection, componentClassId);
        var usages = new List<ComponentPresetReferenceUsage>();
        foreach (var row in QueryComponentClassRows(connection).Where((candidate) => candidate.ProjectId.Equals(owner.ProjectId, StringComparison.Ordinal)))
        {
            AddComponentPresetEmbeddedReferenceUsage(
                usages,
                row,
                "Component Class",
                row.Name,
                row.Id,
                row.ConfigJson,
                componentClassId,
                owner.ComponentType,
                presetId);

            foreach (var preset in ComponentClassPresets(row.MetadataJson))
            {
                AddComponentPresetEmbeddedReferenceUsage(
                    usages,
                    row,
                    "Component Preset",
                    $"{row.Name} · {preset.Name}",
                    ComponentPresetNodeId(row.Id, preset.Id),
                    preset.ConfigJson,
                    componentClassId,
                    owner.ComponentType,
                    presetId);
            }
        }

        foreach (var theme in QueryThemeRows(connection).Where((candidate) => candidate.ProjectId.Equals(owner.ProjectId, StringComparison.Ordinal)))
        {
            if (owner.ComponentType.Equals("status_bar", StringComparison.Ordinal)
                && theme.StatusBarId.Equals(node.Id, StringComparison.Ordinal))
            {
                usages.Add(new ComponentPresetReferenceUsage(
                    "Theme",
                    theme.Name,
                    "Status Bar",
                    theme.Id,
                    null));
            }

            if (owner.ComponentType.Equals("navigation_bar", StringComparison.Ordinal)
                && theme.NavigationBarId.Equals(node.Id, StringComparison.Ordinal))
            {
                usages.Add(new ComponentPresetReferenceUsage(
                    "Theme",
                    theme.Name,
                    "Navigation Bar",
                    theme.Id,
                    null));
            }
        }

        return usages
            .OrderBy((usage) => usage.SourceKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy((usage) => usage.SourceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy((usage) => usage.Detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddComponentPresetEmbeddedReferenceUsage(
        ICollection<ComponentPresetReferenceUsage> usages,
        ComponentClassRow row,
        string sourceKind,
        string sourceName,
        string sourceNodeId,
        string configJson,
        string componentClassId,
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

            if (!SlotPresetMatches(slotNode, componentClassId, presetId))
            {
                continue;
            }

            var embeddedUsage = new EmbeddedComponentUsage(
                row.Id,
                row.Name,
                row.ComponentType,
                slot.FieldId,
                slot.Label,
                EmbeddedComponentHasOverrides(configJson, slot),
                sourceNodeId);
            usages.Add(new ComponentPresetReferenceUsage(
                sourceKind,
                sourceName,
                embeddedUsage.HasOverrides ? $"{slot.Label} · overrides" : slot.Label,
                sourceNodeId,
                embeddedUsage));
        }
    }

    private static bool ComponentPresetIsUsedByConfig(
        string configJson,
        string componentClassId,
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

            if (SlotPresetMatches(slotNode, componentClassId, presetId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SlotPresetMatches(JsonObject slotNode, string componentClassId, string presetId)
    {
        var value = JsonPath.String(slotNode, "presetId", DefaultComponentPresetId);
        if (TryParseComponentPresetNodeId(value, out var referencedClassId, out var referencedPresetId))
        {
            return referencedClassId.Equals(componentClassId, StringComparison.Ordinal)
                && referencedPresetId.Equals(presetId, StringComparison.Ordinal);
        }

        return value.Equals(presetId, StringComparison.Ordinal);
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

    public ComponentClassSettings GetComponentPresetSettings(ProjectTreeNode presetNode)
    {
        using var connection = OpenConnection();
        return GetComponentPresetSettings(connection, presetNode);
    }

    private static ComponentClassSettings GetComponentPresetSettings(SqliteConnection connection, ProjectTreeNode presetNode)
    {
        if (presetNode.Kind != ProjectTreeNodeKind.ComponentPreset
            || !TryParseComponentPresetNodeId(presetNode.Id, out var componentClassId, out var presetId))
        {
            throw new InvalidOperationException($"Invalid component preset node id '{presetNode.Id}'.");
        }

        var settings = GetComponentClassSettings(connection, componentClassId);
        var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(settings.MetadataJson) ? "{}" : settings.MetadataJson);
        if (metadata["presets"] is not JsonArray presets)
        {
            throw new InvalidOperationException($"Component class '{componentClassId}' has no presets.");
        }

        var preset = FindPreset(presets, presetId)
            ?? throw new InvalidOperationException($"Missing component preset '{presetId}'.");
        if (preset["config"] is not JsonObject configObject)
        {
            throw new InvalidOperationException($"Component preset '{presetId}' has no config.");
        }

        var config = configObject.ToJsonString();
        var presetName = JsonPath.String(preset, "name", presetId);

        return settings with
        {
            Name = string.IsNullOrWhiteSpace(presetName)
                ? settings.Name
                : $"{settings.Name} · {presetName}",
            ConfigJson = config,
        };
    }

    private static JsonObject ComponentPresetConfigForUpdate(
        SqliteConnection connection,
        ProjectTreeNode presetNode,
        out string componentClassId,
        out JsonObject metadata)
    {
        if (presetNode.Kind != ProjectTreeNodeKind.ComponentPreset
            || !TryParseComponentPresetNodeId(presetNode.Id, out componentClassId, out var presetId))
        {
            throw new InvalidOperationException($"Invalid component preset node id '{presetNode.Id}'.");
        }

        var settings = GetComponentClassSettings(connection, componentClassId);
        metadata = ParseJsonObject(string.IsNullOrWhiteSpace(settings.MetadataJson) ? "{}" : settings.MetadataJson);
        if (metadata["presets"] is not JsonArray presets)
        {
            throw new InvalidOperationException($"Component class '{componentClassId}' has no presets.");
        }

        var preset = FindPreset(presets, presetId)
            ?? throw new InvalidOperationException($"Missing component preset '{presetId}'.");
        if (preset["config"] is not JsonObject config)
        {
            throw new InvalidOperationException($"Component preset '{presetId}' has no config.");
        }

        return config;
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
            _ => throw new InvalidOperationException($"Embedded component presets are not supported for '{ownerNode.Kind}'."),
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
            NormalizeEmbeddedSlotPresetIds(connection, projectId, defaultConfig);
            configs[componentType] = defaultConfig;
        }

        configs["presets"] = presets;
        return configs.ToJsonString();
    }

    public string NormalizeComponentConfigJsonForPreview(string projectId, string configJson)
    {
        using var connection = OpenConnection();
        var config = ParseJsonObject(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
        NormalizeEmbeddedSlotPresetIds(connection, projectId, config);
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
            NormalizeEmbeddedSlotPresetIds(connection, row.ProjectId, config);
            target[ComponentPresetNodeId(row.Id, preset.Id)] = config;
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
            : [new ComponentClassPreset(DefaultComponentPresetId, "Default", true, row.ConfigJson)];
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
                throw new InvalidOperationException($"Missing component preset reference '{presetReference}'.");
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
        var isHighlighted = descriptor.ValueKind is ValueKind.EmbeddedComponent or ValueKind.ComponentPreset
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

    public FieldValue CreateComponentPresetFieldValue(ProjectTreeNode presetNode, string fieldId)
    {
        var settings = GetComponentPresetSettings(presetNode);
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var value = fieldId == "component.type"
            ? ComponentTypeLabel(settings.ComponentType)
            : ComponentConfigFieldValue(settings.ConfigJson, descriptor);
        var options = ComponentClassFieldOptions(settings.ProjectId, descriptor);
        var isHighlighted = descriptor.ValueKind is ValueKind.EmbeddedComponent or ValueKind.ComponentPreset
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

    public void UpdateComponentPresetField(ProjectTreeNode presetNode, string fieldId, string value)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        if (!descriptor.IsEditable || descriptor.JsonPath.Length == 0)
        {
            return;
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var config = ComponentPresetConfigForUpdate(connection, presetNode, out var componentClassId, out var metadata);
            SetJsonValue(config, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value));
            Execute(
                connection,
                "UPDATE component_classes SET metadata_json = $metadataJson WHERE id = $id",
                ("$id", componentClassId),
                ("$metadataJson", metadata.ToJsonString()));
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
        var isHighlighted = descriptor.ValueKind is ValueKind.EmbeddedComponent or ValueKind.ComponentPreset
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

    public FieldValue CreateEmbeddedComponentFieldValue(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId)
    {
        if (ownerNode.Kind == ProjectTreeNodeKind.ComponentClass)
        {
            return CreateEmbeddedComponentFieldValue(ownerNode.Id, slots, embeddedFieldId);
        }

        if (ownerNode.Kind != ProjectTreeNodeKind.ComponentPreset)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' is not supported for '{ownerNode.Kind}'.");
        }

        if (slots.Count == 0)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        var settings = GetComponentPresetSettings(ownerNode);
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
        var isHighlighted = descriptor.ValueKind is ValueKind.EmbeddedComponent or ValueKind.ComponentPreset
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

    public void UpdateEmbeddedComponentField(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value)
    {
        if (ownerNode.Kind == ProjectTreeNodeKind.ComponentClass)
        {
            UpdateEmbeddedComponentField(ownerNode.Id, slots, embeddedFieldId, value);
            return;
        }

        if (ownerNode.Kind != ProjectTreeNodeKind.ComponentPreset)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' is not supported for '{ownerNode.Kind}'.");
        }

        if (slots.Count == 0)
        {
            throw new InvalidOperationException($"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        var descriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var config = ComponentPresetConfigForUpdate(connection, ownerNode, out var componentClassId, out var metadata);
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
                "UPDATE component_classes SET metadata_json = $metadataJson WHERE id = $id",
                ("$id", componentClassId),
                ("$metadataJson", metadata.ToJsonString()));
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
            var configChanged = NormalizeComponentConfigDefaults(
                connection,
                row.ProjectId,
                row.ComponentType,
                config,
                defaults);

            var designPreview = ParseJsonObject(string.IsNullOrWhiteSpace(row.DesignPreviewJson) ? "{}" : row.DesignPreviewJson);
            var designPreviewDefaults = ParseJsonObject(DefaultComponentDesignPreviewJson(row.ComponentType));
            var designPreviewChanged = JsonPath.MergeMissing(designPreview, designPreviewDefaults);
            designPreviewChanged |= EnsureComponentInputs(designPreview, designPreviewDefaults);
            designPreviewChanged |= EnsureComponentDesignPreviewText(row.ComponentType, designPreview);
            designPreviewChanged |= EnsureButtonIconPreviewSize(row.ComponentType, designPreview);

            var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(row.MetadataJson) ? "{}" : row.MetadataJson);
            var metadataChanged = EnsureDefaultComponentPreset(metadata, config);
            metadataChanged |= NormalizeComponentPresetConfigs(
                connection,
                row.ProjectId,
                row.ComponentType,
                metadata,
                defaults);

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

    private static bool NormalizeComponentPresetConfigs(
        SqliteConnection connection,
        string projectId,
        string componentType,
        JsonObject metadata,
        JsonObject defaults)
    {
        var changed = false;
        foreach (var preset in EnsurePresetArray(metadata).OfType<JsonObject>())
        {
            JsonObject presetConfig;
            if (preset["config"] is JsonObject configObject)
            {
                presetConfig = configObject;
            }
            else if (preset["configJson"] is JsonValue configValue
                && configValue.TryGetValue<string>(out var configJson)
                && !string.IsNullOrWhiteSpace(configJson))
            {
                presetConfig = ParseJsonObject(configJson);
                preset["config"] = presetConfig;
                changed = true;
            }
            else
            {
                presetConfig = ParseJsonObject(defaults.ToJsonString());
                preset["config"] = presetConfig;
                changed = true;
            }

            if (!NormalizeComponentConfigDefaults(connection, projectId, componentType, presetConfig, defaults))
            {
                continue;
            }

            changed = true;
        }

        return changed;
    }

    private static bool NormalizeComponentConfigDefaults(
        SqliteConnection connection,
        string projectId,
        string componentType,
        JsonObject config,
        JsonObject defaults)
    {
        var changed = NormalizeAvatarLabelPlacement(componentType, config);
        changed |= NormalizeButtonIconLabelSlot(componentType, config);
        changed |= NormalizeAudioEmbeddedSlots(componentType, config);
        changed |= NormalizeSurfaceSlots(componentType, config);
        changed |= NormalizeEmbeddedSlotPresetIds(connection, projectId, config);
        changed |= JsonPath.MergeMissing(config, defaults);
        changed |= NormalizeReliefIntensity(config, "reliefTopIntensity");
        changed |= NormalizeReliefIntensity(config, "reliefBottomIntensity");
        return changed;
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

    private static bool NormalizeEmbeddedSlotPresetIds(SqliteConnection connection, string projectId, JsonObject config)
    {
        var changed = false;
        foreach (var slot in EmbeddedComponentSlotCatalog.All())
        {
            if (JsonPath.Get(config, slot.SlotPath) is not JsonObject slotNode)
            {
                continue;
            }

            var currentValue = JsonPath.String(slotNode, "presetId", "");
            var normalizedValue = NormalizeComponentPresetReference(
                connection,
                projectId,
                slot.EmbeddedComponentType,
                currentValue);
            if (currentValue.Equals(normalizedValue, StringComparison.Ordinal))
            {
                continue;
            }

            slotNode["presetId"] = normalizedValue;
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeSurfaceSlots(string componentType, JsonObject config)
    {
        var (ownerPath, preferredPresetName) = componentType switch
        {
            "label" => (new[] { "label" }, "Label"),
            "buttonIcon" => (new[] { "buttonIcon" }, "IconButton"),
            "textInputBar" => (new[] { "textInput" }, "InputBox"),
            "audio" => (new[] { "audio" }, DefaultComponentPresetId),
            "video" => (new[] { "video" }, DefaultComponentPresetId),
            _ => (Array.Empty<string>(), ""),
        };
        if (ownerPath.Length == 0 || JsonPath.Get(config, ownerPath) is not JsonObject owner)
        {
            return false;
        }

        var changed = false;
        if (owner["surfaceSlot"] is not JsonObject surfaceSlot)
        {
            owner["surfaceSlot"] = ComponentSurfaceSlot(preferredPresetName);
            return true;
        }

        if (string.IsNullOrWhiteSpace(JsonPath.String(surfaceSlot, "presetId", "")))
        {
            surfaceSlot["presetId"] = preferredPresetName;
            changed = true;
        }

        if (surfaceSlot["overrides"] is not JsonObject)
        {
            surfaceSlot["overrides"] = new JsonObject();
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
            if (index < slots.Count - 1 && overrides is not null)
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

}
