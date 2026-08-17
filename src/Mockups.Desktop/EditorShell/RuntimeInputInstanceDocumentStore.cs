using Mockups.DesktopEditorShell.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RuntimeInputInstanceDocumentStore
{
    private readonly IRuntimeInputInstanceStore _database;
    private readonly ModuleInstanceAnimationDocumentStore _animationDocuments;
    private readonly EditorOperationCoordinator _operations;

    public RuntimeInputInstanceDocumentStore(
        IRuntimeInputInstanceStore database,
        IModuleInstanceAnimationStore animation,
        IModuleInstanceTimelineStore timeline,
        IModuleInstanceThemeTokenQuery themeTokens,
        EditorOperationCoordinator operations)
    {
        _database = database;
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

    public Task UpdateRuntimeValueAsync(
        string moduleInstanceId,
        string jsonKey,
        JsonNode? value)
    {
        var valueSnapshot = value?.DeepClone();
        return _operations.ExecuteAsync(
            () => _database.UpdateModuleInstanceRuntimeValue(
                moduleInstanceId,
                jsonKey,
                valueSnapshot));
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

    public Task UpdateCollectionValueAsync(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        string fieldJsonKey,
        JsonNode? value)
    {
        var valueSnapshot = value?.DeepClone();
        return _operations.ExecuteAsync(
            () => _database.UpdateModuleInstanceRuntimeCollectionValue(
                moduleInstanceId,
                collectionJsonKey,
                itemId,
                fieldJsonKey,
                valueSnapshot));
    }

    public Task UpdateCollectionValuesAsync(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        IReadOnlyDictionary<string, JsonNode?> values)
    {
        var valuesSnapshot = new Dictionary<string, JsonNode?>();
        foreach (var (key, value) in values)
        {
            valuesSnapshot[key] = value?.DeepClone();
        }

        return _operations.ExecuteAsync(
            () => _database.UpdateModuleInstanceRuntimeCollectionValues(
                moduleInstanceId,
                collectionJsonKey,
                itemId,
                valuesSnapshot));
    }

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
