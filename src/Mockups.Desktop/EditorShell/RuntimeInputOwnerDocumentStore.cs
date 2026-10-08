using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

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

internal sealed class RuntimeInputOwnerDocumentStore
{
    private readonly IRuntimeInputOwnerStore _database;
    private readonly IModuleInstanceTimelineStore _timeline;
    private readonly EditorOperationCoordinator _operations;

    public RuntimeInputOwnerDocumentStore(
        IRuntimeInputOwnerStore database,
        IModuleInstanceTimelineStore timeline,
        EditorOperationCoordinator operations)
    {
        _database = database;
        _timeline = timeline;
        _operations = operations;
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
            if (!VariantReferenceId.TryParse(
                    node.Id,
                    out var moduleId,
                    out _))
            {
                throw new InvalidOperationException(
                    $"Invalid Module Variant reference '{node.Id}'.");
            }
            var settings = _database.GetModuleVariantSettings(node);
            return new RuntimeInputOwnerDocumentSource(
                settings.ConfigJson,
                settings.DesignPreviewJson,
                false,
                RuntimeInputDesignPreviewOwnerKind.Module,
                moduleId);
        }

        if (node.Kind == ProjectTreeNodeKind.ComponentVariant)
        {
            if (!VariantReferenceId.TryParse(
                    node.Id,
                    out var componentClassId,
                    out _))
            {
                throw new InvalidOperationException(
                    $"Invalid Component Variant reference '{node.Id}'.");
            }
            var settings = _database.GetComponentVariantSettings(node);
            return new RuntimeInputOwnerDocumentSource(
                settings.ConfigJson,
                settings.DesignPreviewJson,
                false,
                RuntimeInputDesignPreviewOwnerKind.ComponentClass,
                componentClassId);
        }

        if (node.Kind == ProjectTreeNodeKind.ModuleInstance)
        {
            var module =
                _timeline.GetModuleInstanceVariantSettings(node.Id);
            return new RuntimeInputOwnerDocumentSource(
                module.ConfigJson,
                _timeline.GetModuleInstanceRuntimePreviewJson(node.Id),
                true,
                RuntimeInputDesignPreviewOwnerKind.None,
                "");
        }

        throw new InvalidOperationException($"Runtime inputs are not supported by '{node.Kind}'.");
    }

    public Task ReplaceDesignPreviewAsync(
        RuntimeInputOwnerDocumentSource source,
        string designPreviewJson)
    {
        var replacement = new DesignPreviewDocumentReplacement(source.RuntimePreviewJson, designPreviewJson);
        return _operations.ExecuteAsync(() =>
        {
            switch (source.DesignPreviewOwnerKind)
            {
                case RuntimeInputDesignPreviewOwnerKind.Module:
                    _database.UpdateModuleDesignPreviewJson(source.DesignPreviewOwnerId, replacement);
                    break;
                case RuntimeInputDesignPreviewOwnerKind.ComponentClass:
                    _database.UpdateComponentClassDesignPreviewJson(source.DesignPreviewOwnerId, replacement);
                    break;
                default:
                    throw new InvalidOperationException("A Production Screen cannot replace Design defaults.");
            }
        });
    }

    public Task PromoteDefaultsAsync(RuntimeInputOwnerDocumentSource source, JsonObject values)
    {
        var candidate = values.DeepClone().AsObject();
        var config = DesignPreviewTestValues.Parse(source.ConfigJson);
        DesignPreviewTestValues.PromoteToDefaults(candidate,
            RuntimeInputDefinitionReader.ReadInputs(candidate, config),
            RuntimeInputDefinitionReader.ReadCollections(candidate, config));
        return ReplaceDesignPreviewAsync(source, candidate.ToJsonString());
    }

}
