using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
            var metadata = ParseJsonObject(settings.MetadataJson);
            var presets = VariantEnvelopeContract.RequiredArray(metadata, "presets", $"Component class '{componentClassId}'");
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
                ["config"] = (source["config"] as JsonObject
                    ?? throw new InvalidOperationException($"Component Variant '{presetId}' has no config snapshot.")).DeepClone(),
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
            var sourceConfig = ParseJsonObject(sourceConfigJson);
            var settings = GetComponentClassSettings(connection, componentClassId);
            var metadata = ParseJsonObject(settings.MetadataJson);
            var presets = VariantEnvelopeContract.RequiredArray(metadata, "presets", $"Component class '{componentClassId}'");
            var presetId = UniquePresetId(presets, presetName);
            presets.Add(new JsonObject
            {
                ["id"] = presetId,
                ["name"] = presetName,
                ["protected"] = false,
                ["locked"] = false,
                ["config"] = sourceConfig,
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
            var metadata = ParseJsonObject(settings.MetadataJson);
            var presets = VariantEnvelopeContract.RequiredArray(metadata, "presets", $"Component class '{componentClassId}'");
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

                if (JsonBool(preset, ["locked"]))
                {
                    throw new InvalidOperationException("Locked component variants cannot be deleted.");
                }

                var usages = GetReferenceUsages(connection, node.Kind, node.Id);
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
            var metadata = ParseJsonObject(settings.MetadataJson);
            var presets = VariantEnvelopeContract.RequiredArray(metadata, "presets", $"Component class '{componentClassId}'");
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
            var metadata = ParseJsonObject(settings.MetadataJson);
            var presets = VariantEnvelopeContract.RequiredArray(metadata, "presets", $"Component class '{componentClassId}'");
            var preset = FindPreset(presets, presetId)
                ?? throw new InvalidOperationException($"Missing component variant '{presetId}'.");
            var nextLocked = !JsonBool(preset, ["locked"]);
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

        var nextConfig = ParseJsonObject(configJson);

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var metadata = ParseJsonObject(settings.MetadataJson);
            var presets = VariantEnvelopeContract.RequiredArray(metadata, "presets", $"Component class '{componentClassId}'");
            var preset = FindPreset(presets, presetId)
                ?? throw new InvalidOperationException($"Missing component variant '{presetId}'.");
            if (JsonBool(preset, ["locked"]))
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
        return _referenceUsageService.GetUsages(node.Kind, node.Id)
            .Select((usage) => new ComponentPresetReferenceUsage(
                usage.SourceTypeLabel,
                usage.SourceName,
                usage.FieldLabel,
                usage.SourceNodeId,
                usage.EmbeddedContext is null ? null : ToEmbeddedComponentUsage(usage.EmbeddedContext)))
            .OrderBy((usage) => usage.SourceKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy((usage) => usage.SourceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy((usage) => usage.Detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static EmbeddedComponentUsage ToEmbeddedComponentUsage(ReferenceEmbeddedContext context) =>
        new(
            context.ParentComponentClassId,
            context.ParentComponentName,
            context.ParentComponentType,
            context.SlotFieldId,
            context.SlotLabel,
            context.HasOverrides,
            context.SourceNodeId);

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
