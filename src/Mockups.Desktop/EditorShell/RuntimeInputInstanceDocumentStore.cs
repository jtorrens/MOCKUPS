using Mockups.DesktopEditorShell.Data;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RuntimeInputInstanceDocumentStore
{
    private readonly IRuntimeInputInstanceStore _database;
    private readonly ModuleInstanceAnimationDocumentStore _animationDocuments;
    private readonly EditorOperationCoordinator _operations;
    private readonly IModuleInstanceTimelineStore _timeline;

    public RuntimeInputInstanceDocumentStore(
        IRuntimeInputInstanceStore database,
        IModuleInstanceAnimationStore animation,
        IModuleInstanceTimelineStore timeline,
        IModuleInstanceThemeTokenQuery themeTokens,
        EditorOperationCoordinator operations)
    {
        _database = database;
        _timeline = timeline;
        _operations = operations;
        _animationDocuments = new ModuleInstanceAnimationDocumentStore(
            animation,
            timeline,
            themeTokens,
            new ModuleInstanceTimelineDataSource(
                timeline,
                themeTokens),
            operations);
    }

    public Task<RuntimeInputCommittedDocument> UpdateRuntimeValueAsync(
        string moduleInstanceId,
        string jsonKey,
        JsonNode? value)
    {
        var valueSnapshot = value?.DeepClone();
        return _operations.ExecuteAsync(
            () =>
            {
                _database.UpdateModuleInstanceRuntimeValue(
                    moduleInstanceId,
                    jsonKey,
                    valueSnapshot);
                return ReadConfirmed(moduleInstanceId);
            });
    }

    public Task<StructuredCollectionMutationResult> MutateStructuredCollectionAsync(
        string moduleInstanceId,
        StructuredCollectionMutation mutation)
    {
        var mutationSnapshot = StructuredCollectionMutationEngine.Snapshot(mutation);
        return _operations.ExecuteAsync(
            () => _database.MutateModuleInstanceStructuredCollection(
                moduleInstanceId,
                mutationSnapshot));
    }

    public Task<RuntimeInputCommittedDocument> UpdateCollectionValueAsync(
        string moduleInstanceId,
        StructuredCollectionAddress address,
        string itemId,
        string fieldJsonKey,
        JsonNode? value)
    {
        return UpdateCollectionValuesAsync(moduleInstanceId, address, itemId,
            new Dictionary<string, JsonNode?> { [fieldJsonKey] = value });
    }

    public Task<RuntimeInputCommittedDocument> UpdateCollectionValuesAsync(
        string moduleInstanceId,
        StructuredCollectionAddress address,
        string itemId,
        IReadOnlyDictionary<string, JsonNode?> values)
    {
        var valuesSnapshot = new Dictionary<string, JsonNode?>();
        foreach (var (key, value) in values)
        {
            valuesSnapshot[key] = value?.DeepClone();
        }

        return _operations.ExecuteAsync(
            () =>
            {
                _database.UpdateModuleInstanceRuntimeCollectionValues(
                    moduleInstanceId,
                    address,
                    itemId,
                    valuesSnapshot);
                return ReadConfirmed(moduleInstanceId);
            });
    }

    private RuntimeInputCommittedDocument ReadConfirmed(string id) => new(
        id, _timeline.GetModuleInstanceRuntimePreviewJson(id));

    public ComponentOverrideFieldOwner OverrideOwner(
        string identity, string instanceId, Func<string, string> read,
        Func<string, (string JsonKey, bool IsCollection)> field) => new(
        identity, read, (address, json) =>
        {
            var root = field(address.FieldId);
            var value = JsonNode.Parse(json);
            if (!root.IsCollection)
            {
                _database.UpdateModuleInstanceRuntimeValue(instanceId, root.JsonKey, value);
                return;
            }
            var itemId = address.Path[0].ItemId;
            var item = value!.AsArray().Select(item => item as JsonObject
                ?? throw new InvalidOperationException("A Runtime Override collection item must be an object."))
                .Single(item => item["id"]!.GetValue<string>() == itemId);
            var key = address.Path.Count == 1 ? address.OverridesKey : address.Path[1].Property;
            _database.UpdateModuleInstanceRuntimeCollectionValue(instanceId,
                StructuredCollectionAddress.Root(root.JsonKey), itemId, key, item[key]);
        });

    public Task<ModuleInstanceAnimationSnapshot>
        ExecuteAnimationMutationAsync(
        string moduleInstanceId,
        System.Func<ModuleInstanceAnimationDocument, bool>
            mutation)
    {
        return _animationDocuments.ExecuteMutationAsync(
            moduleInstanceId,
            mutation);
    }
}
