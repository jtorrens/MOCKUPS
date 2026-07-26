using Mockups.DesktopEditorShell.Data;
using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal enum RuntimeInputDesignPreviewOwnerKind
{
    None,
    Module,
    ComponentClass,
}

internal sealed record RuntimeInputOwnerDocumentSource(
    string ConfigJson,
    string RuntimePreviewJson,
    bool IsInstance,
    RuntimeInputDesignPreviewOwnerKind DesignPreviewOwnerKind,
    string DesignPreviewOwnerId);

internal sealed record RuntimeComponentVariantSelectionSource(
    string ProjectId,
    string ComponentType,
    string RecordClassId,
    string ConfigJson);

internal sealed class RuntimeInputOwnerDocumentStore
{
    private readonly SpikeDatabase _database;

    public RuntimeInputOwnerDocumentStore(SpikeDatabase database)
    {
        _database = database;
    }

    public RuntimeInputOwnerDocumentSource Load(ProjectTreeNode node)
    {
        if (node.Kind == ProjectTreeNodeKind.Module)
        {
            var settings = _database.GetModuleSettings(node.Id);
            return new RuntimeInputOwnerDocumentSource(
                settings.ConfigJson,
                settings.DesignPreviewJson,
                false,
                RuntimeInputDesignPreviewOwnerKind.Module,
                node.Id);
        }

        if (node.Kind == ProjectTreeNodeKind.ModuleVariant)
        {
            var settings = _database.GetModuleVariantSettings(node);
            var moduleId = node.Parent?.Id
                ?? throw new InvalidOperationException("Module variant has no parent module.");
            return new RuntimeInputOwnerDocumentSource(
                settings.ConfigJson,
                settings.DesignPreviewJson,
                false,
                RuntimeInputDesignPreviewOwnerKind.Module,
                moduleId);
        }

        if (node.Kind == ProjectTreeNodeKind.ComponentVariant && node.Parent is not null)
        {
            var settings = _database.GetComponentVariantSettings(node);
            return new RuntimeInputOwnerDocumentSource(
                settings.ConfigJson,
                settings.DesignPreviewJson,
                false,
                RuntimeInputDesignPreviewOwnerKind.ComponentClass,
                node.Parent.Id);
        }

        if (node.Kind == ProjectTreeNodeKind.ModuleInstance)
        {
            var module = _database.GetModuleInstanceVariantSettings(node.Id);
            return new RuntimeInputOwnerDocumentSource(
                module.ConfigJson,
                _database.GetModuleInstanceRuntimePreviewJson(node.Id),
                true,
                RuntimeInputDesignPreviewOwnerKind.None,
                "");
        }

        throw new InvalidOperationException($"Runtime inputs are not supported by '{node.Kind}'.");
    }

    public void SaveDesignPreviewJson(RuntimeInputOwnerDocumentSource source, string designPreviewJson)
    {
        switch (source.DesignPreviewOwnerKind)
        {
            case RuntimeInputDesignPreviewOwnerKind.Module:
                _database.UpdateModuleDesignPreviewJson(source.DesignPreviewOwnerId, designPreviewJson);
                return;
            case RuntimeInputDesignPreviewOwnerKind.ComponentClass:
                _database.UpdateComponentClassDesignPreviewJson(source.DesignPreviewOwnerId, designPreviewJson);
                return;
            default:
                throw new InvalidOperationException("A Module Instance has no isolated Design Preview document.");
        }
    }

    public JsonObject ComponentVariantRuntimeInputs(string variantReference)
    {
        return _database.GetComponentVariantRuntimeInputs(variantReference);
    }

    public RuntimeComponentVariantSelectionSource ComponentVariantSelection(string variantReference)
    {
        var selected = _database.GetComponentVariantSelectionSettings(variantReference);
        return new RuntimeComponentVariantSelectionSource(
            selected.ProjectId,
            selected.ComponentType,
            selected.RecordClassId,
            selected.ConfigJson);
    }
}
