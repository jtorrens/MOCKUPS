using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    public ComponentClassSettings GetComponentClassSettings(
        string componentClassId) =>
        ComponentClassSettingsFrom(
            _componentClassRepository.Get(componentClassId));

    internal ComponentClassSettings GetComponentClassSettings(
        SqliteConnection connection,
        string componentClassId) =>
        ComponentClassSettingsFrom(
            _componentClassRepository.Get(
                connection,
                componentClassId));

    public ComponentClassSettings GetComponentVariantSettings(
        ProjectTreeNode variantNode)
    {
        using var connection = OpenConnection();
        return GetComponentVariantSettings(connection, variantNode);
    }

    internal ComponentClassSettings GetComponentVariantSettings(
        SqliteConnection connection,
        ProjectTreeNode variantNode)
    {
        if (variantNode.Kind != ProjectTreeNodeKind.ComponentVariant
            || !VariantReferenceId.TryParse(
                variantNode.Id,
                out var componentClassId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid component variant node id '{variantNode.Id}'.");
        }

        var settings = GetComponentClassSettings(
            connection,
            componentClassId);
        var metadata = ParseJsonObject(settings.MetadataJson);
        var variants = VariantEnvelopeContract.RequiredArray(
            metadata,
            "variants",
            $"Component class '{componentClassId}'");
        var variant = VariantEnvelopeContract.FindSource(
            variants,
            variantId)
            ?? throw new InvalidOperationException(
                $"Missing component variant '{variantId}'.");
        if (variant["config"] is not JsonObject configObject)
        {
            throw new InvalidOperationException(
                $"Component variant '{variantId}' has no config.");
        }

        var variantName = JsonPath.String(
            variant,
            "name",
            variantId);
        return settings with
        {
            Name = string.IsNullOrWhiteSpace(variantName)
                ? settings.Name
                : $"{settings.Name} · {variantName}",
            ConfigJson = configObject.ToJsonString(),
        };
    }

    public void UpdateComponentClassDesignPreviewJson(
        string componentClassId,
        string designPreviewJson) =>
        _componentClassRepository.UpdateDesignPreview(
            componentClassId,
            designPreviewJson);

    internal static IReadOnlyList<ComponentClassVariant>
        ComponentClassVariants(
            string metadataJson,
            string owner = "Component class metadata")
    {
        var metadata = ParseJsonObject(metadataJson);
        return VariantEnvelopeContract.Read(
                metadata,
                "variants",
                owner)
            .Select((variant) => new ComponentClassVariant(
                variant.Id,
                variant.Name,
                variant.IsProtected,
                variant.IsLocked,
                variant.Config.ToJsonString()))
            .OrderBy(
                (variant) => variant.Id.Equals(
                    VariantEnvelopeContract.DefaultId,
                    StringComparison.Ordinal)
                        ? 0
                        : 1)
            .ThenBy(
                (variant) => variant.Name,
                StringComparer.Ordinal)
            .ToList();
    }

    internal static string DefaultComponentVariantConfigJson(
        string metadataJson,
        string owner) =>
        ComponentClassVariants(metadataJson, owner)
            .Single(
                (variant) => variant.Id.Equals(
                    VariantEnvelopeContract.DefaultId,
                    StringComparison.Ordinal))
            .ConfigJson;

    public ProjectTreeNode RenameComponentClass(
        ProjectTreeNode node,
        string name)
    {
        var nextName = name.Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            throw new InvalidOperationException(
                "Component class name cannot be empty.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            _componentClassRepository.Rename(
                connection,
                node.Id,
                nextName);
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

    public ProjectTreeNode DuplicateComponentVariant(
        ProjectTreeNode node)
    {
        if (!VariantReferenceId.TryParse(
                node.Id,
                out var componentClassId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid component variant node id '{node.Id}'.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(
                connection,
                componentClassId);
            var metadata = ParseJsonObject(settings.MetadataJson);
            var variants = VariantEnvelopeContract.RequiredArray(
                metadata,
                "variants",
                $"Component class '{componentClassId}'");
            var source = VariantEnvelopeContract.FindSource(
                variants,
                variantId)
                ?? throw new InvalidOperationException(
                    $"Missing component variant '{variantId}'.");
            var sourceName = JsonPath.String(
                source,
                "name",
                variantId);
            var copyName = $"{sourceName} copy";
            var copyId = VariantEnvelopeContract.UniqueId(
                variants,
                copyName);
            var copyConfig = (source["config"] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Component Variant '{variantId}' has no config snapshot."))
                .DeepClone()
                .AsObject();
            variants.Add(VariantEnvelopeContract.CreateSource(
                copyId,
                copyName,
                copyConfig));
            _componentClassRepository.UpdateMetadata(
                connection,
                componentClassId,
                metadata.ToJsonString());

            return new ProjectTreeNode(
                ProjectTreeNodeKind.ComponentVariant,
                VariantReferenceId.Format(
                    componentClassId,
                    copyId),
                copyName,
                "Component variant",
                ProjectTreeNode.DefaultRecordClassId(
                    ProjectTreeNodeKind.ComponentVariant),
                node.Parent);
        }
    }

    public ProjectTreeNode SaveComponentVariant(
        ProjectTreeNode sourceNode,
        string name)
    {
        if (sourceNode.Kind is not ProjectTreeNodeKind.ComponentVariant)
        {
            throw new InvalidOperationException(
                "Component variants can only be saved from an active selected variant.");
        }

        var variantName = name.Trim();
        if (string.IsNullOrWhiteSpace(variantName))
        {
            throw new InvalidOperationException(
                "Variant name cannot be empty.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            if (!VariantReferenceId.TryParse(
                    sourceNode.Id,
                    out var componentClassId,
                    out _))
            {
                throw new InvalidOperationException(
                    $"Invalid component variant node id '{sourceNode.Id}'.");
            }

            var sourceConfig = ParseJsonObject(
                GetComponentVariantSettings(
                    connection,
                    sourceNode).ConfigJson);
            var settings = GetComponentClassSettings(
                connection,
                componentClassId);
            var metadata = ParseJsonObject(settings.MetadataJson);
            var variants = VariantEnvelopeContract.RequiredArray(
                metadata,
                "variants",
                $"Component class '{componentClassId}'");
            var variantId = VariantEnvelopeContract.UniqueId(
                variants,
                variantName);
            variants.Add(VariantEnvelopeContract.CreateSource(
                variantId,
                variantName,
                sourceConfig));
            _componentClassRepository.UpdateMetadata(
                connection,
                componentClassId,
                metadata.ToJsonString());

            return new ProjectTreeNode(
                ProjectTreeNodeKind.ComponentVariant,
                VariantReferenceId.Format(
                    componentClassId,
                    variantId),
                variantName,
                "Component variant",
                ProjectTreeNode.DefaultRecordClassId(
                    ProjectTreeNodeKind.ComponentVariant),
                sourceNode.Parent);
        }
    }

    public ProjectTreeNode RenameComponentVariant(
        ProjectTreeNode node,
        string name)
    {
        if (!VariantReferenceId.TryParse(
                node.Id,
                out var componentClassId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid component variant node id '{node.Id}'.");
        }

        var nextName = name.Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            throw new InvalidOperationException(
                "Variant name cannot be empty.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(
                connection,
                componentClassId);
            var metadata = ParseJsonObject(settings.MetadataJson);
            var variants = VariantEnvelopeContract.RequiredArray(
                metadata,
                "variants",
                $"Component class '{componentClassId}'");
            var variant = VariantEnvelopeContract.FindSource(
                variants,
                variantId)
                ?? throw new InvalidOperationException(
                    $"Missing component variant '{variantId}'.");
            variant["name"] = nextName;
            _componentClassRepository.UpdateMetadata(
                connection,
                componentClassId,
                metadata.ToJsonString());
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

    public ProjectTreeNode ToggleComponentVariantLock(
        ProjectTreeNode node)
    {
        if (!VariantReferenceId.TryParse(
                node.Id,
                out var componentClassId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid component variant node id '{node.Id}'.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(
                connection,
                componentClassId);
            var metadata = ParseJsonObject(settings.MetadataJson);
            var variants = VariantEnvelopeContract.RequiredArray(
                metadata,
                "variants",
                $"Component class '{componentClassId}'");
            var variant = VariantEnvelopeContract.FindSource(
                variants,
                variantId)
                ?? throw new InvalidOperationException(
                    $"Missing component variant '{variantId}'.");
            if (variantId.Equals(
                    VariantEnvelopeContract.DefaultId,
                    StringComparison.Ordinal))
            {
                var sessionLocked =
                    ToggleDefaultVariantSessionLock(
                        componentClassId,
                        variantId);
                return new ProjectTreeNode(
                    ProjectTreeNodeKind.ComponentVariant,
                    node.Id,
                    node.Name,
                    node.Notes,
                    node.RecordClassId,
                    node.Parent,
                    isUsed: node.IsUsed,
                    isProtected: node.IsProtected,
                    isLocked: sessionLocked);
            }

            var nextLocked = !JsonBool(variant, ["locked"]);
            variant["locked"] = nextLocked;
            _componentClassRepository.UpdateMetadata(
                connection,
                componentClassId,
                metadata.ToJsonString());

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

    internal void RequireComponentVariantDeleteAllowed(
        SqliteConnection connection,
        ProjectTreeNode node)
    {
        if (!VariantReferenceId.TryParse(
                node.Id,
                out var componentClassId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid component variant node id '{node.Id}'.");
        }

        var settings = GetComponentClassSettings(
            connection,
            componentClassId);
        var metadata = ParseJsonObject(settings.MetadataJson);
        var variants = VariantEnvelopeContract.RequiredArray(
            metadata,
            "variants",
            $"Component class '{componentClassId}'");
        var variant = VariantEnvelopeContract.FindSource(
            variants,
            variantId)
            ?? throw new InvalidOperationException(
                $"Missing component variant '{variantId}'.");
        if (JsonBool(variant, ["protected"]))
        {
            throw new InvalidOperationException(
                "Protected component variants cannot be deleted.");
        }

        if (JsonBool(variant, ["locked"]))
        {
            throw new InvalidOperationException(
                "Locked component variants cannot be deleted.");
        }
    }

    internal void DeleteComponentVariant(
        SqliteConnection connection,
        ProjectTreeNode node)
    {
        RequireComponentVariantDeleteAllowed(connection, node);
        VariantReferenceId.TryParse(
            node.Id,
            out var componentClassId,
            out var variantId);
        var settings = GetComponentClassSettings(
            connection,
            componentClassId);
        var metadata = ParseJsonObject(settings.MetadataJson);
        var variants = VariantEnvelopeContract.RequiredArray(
            metadata,
            "variants",
            $"Component class '{componentClassId}'");
        for (var index = 0; index < variants.Count; index++)
        {
            if (variants[index] is not JsonObject variant
                || !JsonPath.String(variant, "id", "").Equals(
                    variantId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            variants.RemoveAt(index);
            _componentClassRepository.UpdateMetadata(
                connection,
                componentClassId,
                metadata.ToJsonString());
            return;
        }

        throw new InvalidOperationException(
            $"Missing component variant '{variantId}'.");
    }

    private static ComponentClassSettings ComponentClassSettingsFrom(
        ComponentClassDefinitionRecord record) =>
        new(
            record.ProjectId,
            record.ComponentType,
            record.RecordClassId,
            record.Name,
            record.Notes,
            DefaultComponentVariantConfigJson(
                record.MetadataJson,
                $"Component class '{record.Id}'"),
            record.DesignPreviewJson,
            record.MetadataJson);
}
