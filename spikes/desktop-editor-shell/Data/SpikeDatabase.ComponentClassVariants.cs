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
        bool IsLocked,
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
            throw new InvalidOperationException($"Invalid component variant node id '{node.Id}'.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(settings.MetadataJson) ? "{}" : settings.MetadataJson);
            var presets = EnsurePresetArray(metadata);
            var source = FindPreset(presets, presetId)
                ?? throw new InvalidOperationException($"Missing component variant '{presetId}'.");
            var sourceName = JsonPath.String(source, "name", presetId);
            var copyName = $"{sourceName} copy";
            var copyId = UniquePresetId(presets, copyName);
            presets.Add(new JsonObject
            {
                ["id"] = copyId,
                ["name"] = copyName,
                ["protected"] = false,
                ["locked"] = false,
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
                "Component variant",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ComponentPreset),
                node.Parent);
        }
    }

    public ProjectTreeNode SaveComponentPreset(ProjectTreeNode sourceNode, string name)
    {
        if (sourceNode.Kind is not ProjectTreeNodeKind.ComponentPreset)
        {
            throw new InvalidOperationException("Component variants can only be saved from an active selected variant.");
        }

        var presetName = name.Trim();
        if (string.IsNullOrWhiteSpace(presetName))
        {
            throw new InvalidOperationException("Variant name cannot be empty.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            if (!TryParseComponentPresetNodeId(sourceNode.Id, out var componentClassId, out _))
            {
                throw new InvalidOperationException($"Invalid component variant node id '{sourceNode.Id}'.");
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
                ["locked"] = false,
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
                "Component variant",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ComponentPreset),
                sourceNode.Parent);
        }
    }

    private void DeleteComponentPreset(ProjectTreeNode node)
    {
        if (!TryParseComponentPresetNodeId(node.Id, out var componentClassId, out var presetId))
        {
            throw new InvalidOperationException($"Invalid component variant node id '{node.Id}'.");
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
                    throw new InvalidOperationException("Protected component variants cannot be deleted.");
                }

                if (ComponentPresetIsLocked(preset))
                {
                    throw new InvalidOperationException("Locked component variants cannot be deleted.");
                }

                var usages = GetComponentPresetReferenceUsages(connection, node);
                if (usages.Count > 0)
                {
                    throw new InvalidOperationException($"This component variant is still used and cannot be deleted.\n\n{string.Join(Environment.NewLine, usages.Take(12))}");
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

        throw new InvalidOperationException($"Missing component variant '{presetId}'.");
    }

    public ProjectTreeNode RenameComponentPreset(ProjectTreeNode node, string name)
    {
        if (!TryParseComponentPresetNodeId(node.Id, out var componentClassId, out var presetId))
        {
            throw new InvalidOperationException($"Invalid component variant node id '{node.Id}'.");
        }

        var nextName = name.Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            throw new InvalidOperationException("Variant name cannot be empty.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(settings.MetadataJson) ? "{}" : settings.MetadataJson);
            var presets = EnsurePresetArray(metadata);
            var preset = FindPreset(presets, presetId)
                ?? throw new InvalidOperationException($"Missing component variant '{presetId}'.");
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
            isProtected: node.IsProtected,
            isLocked: node.IsLocked);
    }

    public ProjectTreeNode ToggleComponentPresetLock(ProjectTreeNode node)
    {
        if (!TryParseComponentPresetNodeId(node.Id, out var componentClassId, out var presetId))
        {
            throw new InvalidOperationException($"Invalid component variant node id '{node.Id}'.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(settings.MetadataJson) ? "{}" : settings.MetadataJson);
            var presets = EnsurePresetArray(metadata);
            var preset = FindPreset(presets, presetId)
                ?? throw new InvalidOperationException($"Missing component variant '{presetId}'.");
            var nextLocked = !ComponentPresetIsLocked(preset);
            preset["locked"] = nextLocked;
            Execute(
                connection,
                "UPDATE component_classes SET metadata_json = $metadataJson WHERE id = $id",
                ("$id", componentClassId),
                ("$metadataJson", metadata.ToJsonString()));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.ComponentPreset,
                node.Id,
                node.Name,
                node.Notes,
                node.RecordClassId,
                node.Parent,
                isUsed: node.IsUsed,
                isProtected: node.IsProtected,
                isLocked: nextLocked);
        }
    }

    public void ReplaceComponentPresetConfig(ProjectTreeNode node, string configJson)
    {
        if (!TryParseComponentPresetNodeId(node.Id, out var componentClassId, out var presetId))
        {
            throw new InvalidOperationException($"Invalid component variant node id '{node.Id}'.");
        }

        var nextConfig = JsonNode.Parse(configJson) as JsonObject
            ?? throw new InvalidOperationException("Variant snapshot is not a valid object.");

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(settings.MetadataJson) ? "{}" : settings.MetadataJson);
            var presets = EnsurePresetArray(metadata);
            var preset = FindPreset(presets, presetId)
                ?? throw new InvalidOperationException($"Missing component variant '{presetId}'.");
            if (ComponentPresetIsLocked(preset))
            {
                throw new InvalidOperationException($"Component variant '{presetId}' is locked.");
            }

            preset["config"] = nextConfig;
            Execute(
                connection,
                "UPDATE component_classes SET metadata_json = $metadataJson WHERE id = $id",
                ("$id", componentClassId),
                ("$metadataJson", metadata.ToJsonString()));
        }
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
                    "Component Variant",
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


}
