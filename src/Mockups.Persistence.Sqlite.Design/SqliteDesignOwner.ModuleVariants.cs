using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    internal static IReadOnlyList<ModuleVariant> ModuleVariants(
        string metadataJson,
        string owner = "Module metadata")
    {
        var metadata = ParseJsonObject(metadataJson);
        return VariantEnvelopeContract.Read(metadata, "variants", owner)
            .Select((variant) => new ModuleVariant(
                variant.Id,
                variant.Name,
                variant.IsProtected,
                variant.IsLocked,
                variant.Config.ToJsonString()))
            .ToList();
    }

    public ModuleSettings GetModuleVariantSettings(
        ProjectTreeNode variantNode)
    {
        if (variantNode.Kind != ProjectTreeNodeKind.ModuleVariant
            || !VariantReferenceId.TryParse(
                variantNode.Id,
                out var moduleId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid module variant node id '{variantNode.Id}'.");
        }

        var settings = GetModuleSettings(moduleId);
        var variant = ModuleVariants(settings.MetadataJson)
            .FirstOrDefault(
                (candidate) => candidate.Id.Equals(
                    variantId,
                    StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Missing module variant '{variantId}'.");
        return settings with { ConfigJson = variant.ConfigJson };
    }

    public IReadOnlyList<FieldOption> GetModuleVariantOptions(
        string moduleId) =>
        ModuleVariants(GetModuleSettings(moduleId).MetadataJson)
            .Select((variant) => new FieldOption(
                VariantReferenceId.Format(moduleId, variant.Id),
                variant.Name))
            .ToList();

    public ProjectTreeNode SaveModuleVariant(
        ProjectTreeNode sourceNode,
        string name)
    {
        if (sourceNode.Kind != ProjectTreeNodeKind.ModuleVariant
            || !VariantReferenceId.TryParse(
                sourceNode.Id,
                out var moduleId,
                out _))
        {
            throw new InvalidOperationException(
                "Module variants can only be saved from an active selected variant.");
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
            var settings = GetModuleVariantSettings(sourceNode);
            var module = GetModuleSettings(moduleId);
            var metadata = ParseJsonObject(module.MetadataJson);
            var variants = VariantEnvelopeContract.RequiredArray(
                metadata,
                "variants",
                $"Module '{moduleId}'");
            var variantId = VariantEnvelopeContract.UniqueId(
                variants,
                variantName);
            variants.Add(VariantEnvelopeContract.CreateSource(
                variantId,
                variantName,
                ParseJsonObject(settings.ConfigJson)));
            _appModuleRepository.UpdateModuleMetadata(
                connection,
                moduleId,
                metadata.ToJsonString());
            return new ProjectTreeNode(
                ProjectTreeNodeKind.ModuleVariant,
                VariantReferenceId.Format(moduleId, variantId),
                variantName,
                "Module variant",
                ProjectTreeNode.DefaultRecordClassId(
                    ProjectTreeNodeKind.ModuleVariant),
                sourceNode.Parent);
        }
    }

    public ProjectTreeNode RenameModuleClass(
        ProjectTreeNode node,
        string name)
    {
        var nextName = name.Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            throw new InvalidOperationException(
                "Module name cannot be empty.");
        }

        using var connection = OpenConnection();
        _appModuleRepository.RenameModule(
            connection,
            node.Id,
            nextName);
        return new ProjectTreeNode(
            ProjectTreeNodeKind.Module,
            node.Id,
            nextName,
            node.Notes,
            node.RecordClassId,
            node.Parent,
            isUsed: node.IsUsed,
            isProtected: node.IsProtected,
            isLocked: node.IsLocked);
    }

    public ProjectTreeNode RenameModuleVariant(
        ProjectTreeNode node,
        string name) =>
        UpdateModuleVariantMetadata(
            node,
            (variant) => variant["name"] = name.Trim(),
            name.Trim());

    internal void DeleteModuleVariant(
        SqliteConnection connection,
        ProjectTreeNode node)
    {
        RequireModuleVariantDeleteAllowed(connection, node);
        if (!VariantReferenceId.TryParse(
                node.Id,
                out var moduleId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid module variant '{node.Id}'.");
        }

        var module = _appModuleRepository.GetModule(
            connection,
            moduleId);
        var metadata = ParseJsonObject(module.MetadataJson);
        var variants = VariantEnvelopeContract.RequiredArray(
            metadata,
            "variants",
            $"Module '{moduleId}'");
        FindModuleVariant(metadata, node.Id);
        for (var index = 0; index < variants.Count; index++)
        {
            if (variants[index] is JsonObject candidate
                && JsonPath.String(candidate, "id", "") == variantId)
            {
                variants.RemoveAt(index);
                break;
            }
        }

        _appModuleRepository.UpdateModuleMetadata(
            connection,
            moduleId,
            metadata.ToJsonString());
    }

    internal void RequireModuleVariantDeleteAllowed(
        SqliteConnection connection,
        ProjectTreeNode node)
    {
        if (!VariantReferenceId.TryParse(
                node.Id,
                out var moduleId,
                out _))
        {
            throw new InvalidOperationException(
                $"Invalid module variant '{node.Id}'.");
        }

        var module = _appModuleRepository.GetModule(
            connection,
            moduleId);
        var metadata = ParseJsonObject(module.MetadataJson);
        var variant = FindModuleVariant(metadata, node.Id);
        if (JsonBool(variant, ["protected"]))
        {
            throw new InvalidOperationException(
                "Protected module variants cannot be deleted.");
        }

        if (JsonBool(variant, ["locked"]))
        {
            throw new InvalidOperationException(
                "Locked module variants cannot be deleted.");
        }
    }

    public ProjectTreeNode ToggleModuleVariantLock(
        ProjectTreeNode node)
    {
        if (!VariantReferenceId.TryParse(
                node.Id,
                out var moduleId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid module variant '{node.Id}'.");
        }

        if (variantId.Equals(
                VariantEnvelopeContract.DefaultId,
                StringComparison.Ordinal))
        {
            var nextLocked = ToggleDefaultVariantSessionLock(
                moduleId,
                variantId);
            return new ProjectTreeNode(
                ProjectTreeNodeKind.ModuleVariant,
                node.Id,
                node.Name,
                node.Notes,
                node.RecordClassId,
                node.Parent,
                isUsed: node.IsUsed,
                isProtected: node.IsProtected,
                isLocked: nextLocked);
        }

        return UpdateModuleVariantMetadata(
            node,
            (variant) =>
                variant["locked"] = !JsonBool(variant, ["locked"]),
            node.Name);
    }

    public void ReplaceModuleVariantConfig(
        ProjectTreeNode node,
        string configJson)
    {
        var config = ParseJsonObject(configJson);
        UpdateModuleVariantConfig(
            node,
            (variant) => variant["config"] = config);
    }

    public void UpdateModuleVariantField(
        ProjectTreeNode node,
        string fieldId,
        string value)
    {
        if (!VariantReferenceId.TryParse(
                node.Id,
                out var moduleId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid module variant '{node.Id}'.");
        }

        if (fieldId is
            "module.sortOrder"
            or "module.metadata"
            or "module.recordClassId")
        {
            UpdateModuleField(moduleId, fieldId, value);
            return;
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var module = GetModuleSettings(moduleId);
            var metadata = ParseJsonObject(module.MetadataJson);
            var variant = FindModuleVariant(metadata, node.Id);
            if (IsVariantLockedForEditing(
                    moduleId,
                    variantId,
                    JsonBool(variant, ["locked"])))
            {
                throw new InvalidOperationException(
                    $"Module variant '{node.Name}' is locked.");
            }

            var config = variant["config"] as JsonObject
                ?? throw new InvalidOperationException(
                    "Module variant has no config.");
            UpdateModuleConfigFieldValue(
                connection,
                module.ProjectId,
                module.RecordClassId,
                config,
                fieldId,
                value);
            variant["config"] = config;
            _appModuleRepository.UpdateModuleMetadata(
                connection,
                moduleId,
                metadata.ToJsonString());
        }
    }

    public string GetModuleVariantConfigFieldValue(
        ProjectTreeNode node,
        string fieldId)
    {
        var settings = GetModuleVariantSettings(node);
        return ModuleConfigFieldValue(
            settings.RecordClassId,
            settings.ConfigJson,
            fieldId);
    }

    private ProjectTreeNode UpdateModuleVariantMetadata(
        ProjectTreeNode node,
        Action<JsonObject> update,
        string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(
                "Variant name cannot be empty.");
        }

        if (!VariantReferenceId.TryParse(
                node.Id,
                out var moduleId,
                out _))
        {
            throw new InvalidOperationException(
                $"Invalid module variant '{node.Id}'.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var module = GetModuleSettings(moduleId);
            var metadata = ParseJsonObject(module.MetadataJson);
            var variant = FindModuleVariant(metadata, node.Id);
            update(variant);
            _appModuleRepository.UpdateModuleMetadata(
                connection,
                moduleId,
                metadata.ToJsonString());
            return new ProjectTreeNode(
                ProjectTreeNodeKind.ModuleVariant,
                node.Id,
                JsonPath.String(variant, "name", name),
                node.Notes,
                node.RecordClassId,
                node.Parent,
                isUsed: node.IsUsed,
                isProtected: JsonBool(variant, ["protected"]),
                isLocked: JsonBool(variant, ["locked"]));
        }
    }

    private void UpdateModuleVariantConfig(
        ProjectTreeNode node,
        Action<JsonObject> update)
    {
        if (!VariantReferenceId.TryParse(
                node.Id,
                out var moduleId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid module variant '{node.Id}'.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var module = GetModuleSettings(moduleId);
            var metadata = ParseJsonObject(module.MetadataJson);
            var variant = FindModuleVariant(metadata, node.Id);
            if (IsVariantLockedForEditing(
                    moduleId,
                    variantId,
                    JsonBool(variant, ["locked"])))
            {
                throw new InvalidOperationException(
                    $"Module variant '{node.Name}' is locked.");
            }

            update(variant);
            _appModuleRepository.UpdateModuleMetadata(
                connection,
                moduleId,
                metadata.ToJsonString());
        }
    }

    private static JsonObject FindModuleVariant(
        JsonObject metadata,
        string nodeId)
    {
        if (!VariantReferenceId.TryParse(
                nodeId,
                out var moduleId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid module variant '{nodeId}'.");
        }

        var variants = VariantEnvelopeContract.RequiredArray(
            metadata,
            "variants",
            $"Module '{moduleId}'");
        return VariantEnvelopeContract.FindSource(variants, variantId)
            ?? throw new InvalidOperationException(
                $"Missing module variant '{variantId}'.");
    }
}
