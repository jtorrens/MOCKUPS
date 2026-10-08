using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Linq;
using System.Text.Json;
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

    public ComponentOverrideFieldOwner OverrideOwner(
        ProjectTreeNode node,
        RuntimeInputInstanceDocumentStore instances,
        ComponentPreviewInputDataSource previewData,
        Func<ProjectTreeNode, ComponentPreviewTransientState> capture,
        Action<ProjectTreeNode, string, string, bool> publish)
    {
        (JsonObject Preview, JsonObject Config) Current()
        {
            var source = Load(node);
            var config = DesignPreviewTestValues.Parse(source.ConfigJson);
            var preview = DesignPreviewTestValues.Parse(source.RuntimePreviewJson);
            if (!source.IsInstance)
                preview = ComponentPreviewTransientValues.Apply(preview, config, capture(node),
                    previewData.ComponentVariantConfig, previewData.ComponentVariantRuntimeContract);
            return (preview, config);
        }
        static (string JsonKey, bool IsCollection) ResolveField(
            string fieldId, (JsonObject Preview, JsonObject Config) current)
        {
            var collection = RuntimeInputDefinitionReader.ReadCollections(current.Preview, current.Config, includeHidden: true)
                .SingleOrDefault(item => item.Id == fieldId);
            if (collection is not null) return (collection.StorageJsonKey, true);
            var input = RuntimeInputDefinitionReader.ReadInputs(current.Preview, current.Config)
                .Single(item => item.Id == fieldId);
            return (input.JsonKey, false);
        }
        string Read(string fieldId)
        {
            var current = Current();
            var field = ResolveField(fieldId, current);
            return current.Preview[field.JsonKey]?.ToJsonString()
                ?? throw new InvalidOperationException($"Missing Runtime Override field '{fieldId}'.");
        }
        (string JsonKey, bool IsCollection) Field(string fieldId) => ResolveField(fieldId, Current());
        var identity = JsonSerializer.Serialize(new { node.Kind, node.Id, Scope = "runtime" });
        return node.Kind == ProjectTreeNodeKind.ModuleInstance
            ? instances.OverrideOwner(identity, node.Id, Read, Field)
            : new ComponentOverrideFieldOwner(identity, Read, (address, json) =>
            {
                var field = Field(address.FieldId);
                publish(node, field.JsonKey, json, field.IsCollection);
            });
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
