using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    internal static IReadOnlyList<ModuleVariant> ModuleVariants(
        string metadataJson,
        string owner = "Module metadata") =>
        SqliteDesignOwner.ModuleVariants(metadataJson, owner);

    public ModuleSettings GetModuleVariantSettings(
        ProjectTreeNode variantNode) =>
        _designOwner.GetModuleVariantSettings(variantNode);

    public ModuleSettings GetModuleInstanceVariantSettings(
        string moduleInstanceId) =>
        _productionOwner.GetModuleInstanceVariantSettings(
            moduleInstanceId);

    public string GetModuleInstanceEffectiveContractJson(
        string moduleInstanceId) =>
        _productionOwner.GetModuleInstanceEffectiveContractJson(
            moduleInstanceId);

    public string GetModuleInstanceVariantReference(
        string moduleInstanceId) =>
        _productionOwner.GetModuleInstanceVariantReference(
            moduleInstanceId);

    public string GetModuleInstanceVariantName(
        string moduleInstanceId) =>
        _productionOwner.GetModuleInstanceVariantName(
            moduleInstanceId);

    public IReadOnlyList<FieldOption> GetModuleVariantOptions(
        string moduleId) =>
        _designOwner.GetModuleVariantOptions(moduleId);

    public void UpdateModuleInstanceVariant(string moduleInstanceId, string reference)
    {
        using var connection = OpenConnection();
        _productionOwner.UpdateModuleInstanceVariant(
            connection,
            moduleInstanceId,
            reference,
            ModuleInstanceProjectActorIds(
                connection,
                moduleInstanceId));
    }

    public ProjectTreeNode SaveModuleVariant(
        ProjectTreeNode sourceNode,
        string name) =>
        _designOwner.SaveModuleVariant(sourceNode, name);

    private ProjectTreeNode RenameModuleClass(
        ProjectTreeNode node,
        string name) =>
        _designOwner.RenameModuleClass(node, name);

    public ProjectTreeNode RenameModuleVariant(
        ProjectTreeNode node,
        string name) =>
        _designOwner.RenameModuleVariant(node, name);

    public void DeleteModuleVariant(ProjectTreeNode node)
    {
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
            _designOwner.RequireModuleVariantDeleteAllowed(
                connection,
                node);
            if (_productionOwner.ModuleInstanceRepository
                    .CountVariantReferences(
                        connection,
                        moduleId,
                        node.Id) > 0)
            {
                throw new InvalidOperationException(
                    "This module variant is still used and cannot be deleted.");
            }

            _designOwner.DeleteModuleVariant(connection, node);
        }
    }

    public ProjectTreeNode ToggleModuleVariantLock(
        ProjectTreeNode node) =>
        _designOwner.ToggleModuleVariantLock(node);

    public void ReplaceModuleVariantConfig(
        ProjectTreeNode node,
        string configJson) =>
        _designOwner.ReplaceModuleVariantConfig(node, configJson);

    public void UpdateModuleVariantField(
        ProjectTreeNode node,
        string fieldId,
        string value) =>
        _designOwner.UpdateModuleVariantField(node, fieldId, value);

    public string GetModuleVariantConfigFieldValue(
        ProjectTreeNode node,
        string fieldId) =>
        _designOwner.GetModuleVariantConfigFieldValue(node, fieldId);
}
