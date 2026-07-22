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
    private const string DefaultComponentVariantId = "default";

    public sealed record EmbeddedComponentUsage(
        string ParentComponentClassId,
        string ParentComponentName,
        string ParentComponentType,
        string SlotFieldId,
        string SlotLabel,
        bool HasOverrides,
        string SourceNodeId = "");

    public sealed record ComponentVariantReferenceUsage(
        string SourceKind,
        string SourceName,
        string Detail,
        string TargetNodeId,
        EmbeddedComponentUsage? EmbeddedUsage);

    public sealed record ComponentClassVariant(
        string Id,
        string Name,
        bool IsProtected,
        bool IsLocked,
        string ConfigJson);

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
            _componentClassRepository.Rename(connection, node.Id, nextName);
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

    private ProjectTreeNode DuplicateComponentVariant(ProjectTreeNode node)
    {
        if (!VariantReferenceId.TryParse(node.Id, out var componentClassId, out var variantId))
        {
            throw new InvalidOperationException($"Invalid component variant node id '{node.Id}'.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var metadata = ParseJsonObject(settings.MetadataJson);
            var variants = VariantEnvelopeContract.RequiredArray(metadata, "variants", $"Component class '{componentClassId}'");
            var source = FindVariant(variants, variantId)
                ?? throw new InvalidOperationException($"Missing component variant '{variantId}'.");
            var sourceName = JsonPath.String(source, "name", variantId);
            var copyName = $"{sourceName} copy";
            var copyId = UniqueVariantId(variants, copyName);
            variants.Add(new JsonObject
            {
                ["id"] = copyId,
                ["name"] = copyName,
                ["protected"] = false,
                ["locked"] = false,
                ["config"] = (source["config"] as JsonObject
                    ?? throw new InvalidOperationException($"Component Variant '{variantId}' has no config snapshot.")).DeepClone(),
            });
            _componentClassRepository.UpdateMetadata(connection, componentClassId, metadata.ToJsonString());

            return new ProjectTreeNode(
                ProjectTreeNodeKind.ComponentVariant,
                VariantReferenceId.Format(componentClassId, copyId),
                copyName,
                "Component variant",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ComponentVariant),
                node.Parent);
        }
    }

    public ProjectTreeNode SaveComponentVariant(ProjectTreeNode sourceNode, string name)
    {
        if (sourceNode.Kind is not ProjectTreeNodeKind.ComponentVariant)
        {
            throw new InvalidOperationException("Component variants can only be saved from an active selected variant.");
        }

        var variantName = name.Trim();
        if (string.IsNullOrWhiteSpace(variantName))
        {
            throw new InvalidOperationException("Variant name cannot be empty.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            if (!VariantReferenceId.TryParse(sourceNode.Id, out var componentClassId, out _))
            {
                throw new InvalidOperationException($"Invalid component variant node id '{sourceNode.Id}'.");
            }

            var sourceConfigJson = GetComponentVariantSettings(connection, sourceNode).ConfigJson;
            var sourceConfig = ParseJsonObject(sourceConfigJson);
            var settings = GetComponentClassSettings(connection, componentClassId);
            var metadata = ParseJsonObject(settings.MetadataJson);
            var variants = VariantEnvelopeContract.RequiredArray(metadata, "variants", $"Component class '{componentClassId}'");
            var variantId = UniqueVariantId(variants, variantName);
            variants.Add(new JsonObject
            {
                ["id"] = variantId,
                ["name"] = variantName,
                ["protected"] = false,
                ["locked"] = false,
                ["config"] = sourceConfig,
            });
            _componentClassRepository.UpdateMetadata(connection, componentClassId, metadata.ToJsonString());

            return new ProjectTreeNode(
                ProjectTreeNodeKind.ComponentVariant,
                VariantReferenceId.Format(componentClassId, variantId),
                variantName,
                "Component variant",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ComponentVariant),
                sourceNode.Parent);
        }
    }

    private void DeleteComponentVariant(ProjectTreeNode node)
    {
        if (!VariantReferenceId.TryParse(node.Id, out var componentClassId, out var variantId))
        {
            throw new InvalidOperationException($"Invalid component variant node id '{node.Id}'.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var metadata = ParseJsonObject(settings.MetadataJson);
            var variants = VariantEnvelopeContract.RequiredArray(metadata, "variants", $"Component class '{componentClassId}'");
            for (var index = 0; index < variants.Count; index++)
            {
                if (variants[index] is not JsonObject variant
                    || !JsonPath.String(variant, "id", "").Equals(variantId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (JsonBool(variant, ["protected"]))
                {
                    throw new InvalidOperationException("Protected component variants cannot be deleted.");
                }

                if (JsonBool(variant, ["locked"]))
                {
                    throw new InvalidOperationException("Locked component variants cannot be deleted.");
                }

                var usages = GetReferenceUsages(connection, node.Kind, node.Id);
                if (usages.Count > 0)
                {
                    throw new InvalidOperationException($"This component variant is still used and cannot be deleted.\n\n{string.Join(Environment.NewLine, usages.Take(12))}");
                }

                variants.RemoveAt(index);
                _componentClassRepository.UpdateMetadata(connection, componentClassId, metadata.ToJsonString());
                return;
            }
        }

        throw new InvalidOperationException($"Missing component variant '{variantId}'.");
    }

    public ProjectTreeNode RenameComponentVariant(ProjectTreeNode node, string name)
    {
        if (!VariantReferenceId.TryParse(node.Id, out var componentClassId, out var variantId))
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
            var variants = VariantEnvelopeContract.RequiredArray(metadata, "variants", $"Component class '{componentClassId}'");
            var variant = FindVariant(variants, variantId)
                ?? throw new InvalidOperationException($"Missing component variant '{variantId}'.");
            variant["name"] = nextName;
            _componentClassRepository.UpdateMetadata(connection, componentClassId, metadata.ToJsonString());
        }

        return new ProjectTreeNode(
            ProjectTreeNodeKind.ComponentVariant,
            node.Id,
            nextName,
            node.Notes,
            node.RecordClassId,
            node.Parent,
            isUsed: node.IsUsed,
            isProtected: node.IsProtected,
            isLocked: node.IsLocked);
    }

    public ProjectTreeNode ToggleComponentVariantLock(ProjectTreeNode node)
    {
        if (!VariantReferenceId.TryParse(node.Id, out var componentClassId, out var variantId))
        {
            throw new InvalidOperationException($"Invalid component variant node id '{node.Id}'.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var metadata = ParseJsonObject(settings.MetadataJson);
            var variants = VariantEnvelopeContract.RequiredArray(metadata, "variants", $"Component class '{componentClassId}'");
            var variant = FindVariant(variants, variantId)
                ?? throw new InvalidOperationException($"Missing component variant '{variantId}'.");
            var nextLocked = !JsonBool(variant, ["locked"]);
            variant["locked"] = nextLocked;
            _componentClassRepository.UpdateMetadata(connection, componentClassId, metadata.ToJsonString());

            return new ProjectTreeNode(
                ProjectTreeNodeKind.ComponentVariant,
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

    public void ReplaceComponentVariantConfig(ProjectTreeNode node, string configJson)
    {
        if (!VariantReferenceId.TryParse(node.Id, out var componentClassId, out var variantId))
        {
            throw new InvalidOperationException($"Invalid component variant node id '{node.Id}'.");
        }

        var nextConfig = ParseJsonObject(configJson);

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            CurrentComponentConfigContract.Validate(
                settings.ComponentType,
                nextConfig,
                $"Component class '{componentClassId}' Variant '{variantId}' config");
            var metadata = ParseJsonObject(settings.MetadataJson);
            var variants = VariantEnvelopeContract.RequiredArray(metadata, "variants", $"Component class '{componentClassId}'");
            var variant = FindVariant(variants, variantId)
                ?? throw new InvalidOperationException($"Missing component variant '{variantId}'.");
            if (JsonBool(variant, ["locked"]))
            {
                throw new InvalidOperationException($"Component variant '{variantId}' is locked.");
            }

            variant["config"] = nextConfig;
            _componentClassRepository.UpdateMetadata(connection, componentClassId, metadata.ToJsonString());
        }
    }

    public IReadOnlyList<ComponentVariantReferenceUsage> GetComponentVariantReferenceUsageDetails(ProjectTreeNode node)
    {
        return _referenceUsageService.GetUsages(node.Kind, node.Id)
            .Select((usage) => new ComponentVariantReferenceUsage(
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

    private static JsonObject? FindVariant(JsonArray variants, string variantId) =>
        variants
            .OfType<JsonObject>()
            .FirstOrDefault((variant) => JsonPath.String(variant, "id", "").Equals(variantId, StringComparison.Ordinal));

    private static string UniqueVariantId(JsonArray variants, string name)
    {
        var baseId = new string(name
                .Trim()
                .ToLowerInvariant()
                .Select((character) => char.IsLetterOrDigit(character) ? character : '_')
                .ToArray())
            .Trim('_');
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = "variant";
        }

        var existing = variants
            .OfType<JsonObject>()
            .Select((variant) => JsonPath.String(variant, "id", ""))
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
