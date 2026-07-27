using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class SqliteProjectEngineModuleInstanceTestExtensions
{
    internal static ModuleInstanceSettings GetModuleInstanceSettings(
        this SqliteProjectEngine engine,
        string moduleInstanceId) =>
        engine.Production.GetModuleInstanceSettings(moduleInstanceId);

    internal static string GetModuleInstanceModuleName(
        this SqliteProjectEngine engine,
        string moduleInstanceId) =>
        engine.Production.GetModuleInstanceModuleName(moduleInstanceId);

    internal static string GetModuleInstanceTransitionType(
        this SqliteProjectEngine engine,
        string moduleInstanceId) =>
        engine.Production.GetModuleInstanceTransitionType(
            moduleInstanceId);

    internal static string GetModuleInstanceRuntimePreviewJson(
        this SqliteProjectEngine engine,
        string moduleInstanceId) =>
        engine.Production.GetModuleInstanceRuntimePreviewJson(
            moduleInstanceId);

    internal static void UpdateModuleInstanceRuntimeValue(
        this SqliteProjectEngine engine,
        string moduleInstanceId,
        string jsonKey,
        JsonNode? value) =>
        engine.RuntimeInputInstances.UpdateModuleInstanceRuntimeValue(
            moduleInstanceId,
            jsonKey,
            value);

    internal static void UpdateModuleInstanceAnimationJson(
        this SqliteProjectEngine engine,
        string moduleInstanceId,
        string animationJson) =>
        engine.Production.UpdateModuleInstanceAnimationJson(
            moduleInstanceId,
            animationJson);

    internal static void UpdateModuleInstanceRuntimeCollectionValue(
        this SqliteProjectEngine engine,
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        string fieldJsonKey,
        JsonNode? value) =>
        engine.RuntimeInputInstances
            .UpdateModuleInstanceRuntimeCollectionValue(
                moduleInstanceId,
                collectionJsonKey,
                itemId,
                fieldJsonKey,
                value);

    internal static void UpdateModuleInstanceRuntimeCollectionValues(
        this SqliteProjectEngine engine,
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        IReadOnlyDictionary<string, JsonNode?> values) =>
        engine.RuntimeInputInstances
            .UpdateModuleInstanceRuntimeCollectionValues(
                moduleInstanceId,
                collectionJsonKey,
                itemId,
                values);

    internal static void AddModuleInstanceRuntimeCollectionItem(
        this SqliteProjectEngine engine,
        string moduleInstanceId,
        string collectionJsonKey,
        JsonObject item) =>
        engine.RuntimeInputInstances
            .AddModuleInstanceRuntimeCollectionItem(
                moduleInstanceId,
                collectionJsonKey,
                item);

    internal static void InsertModuleInstanceRuntimeCollectionItemAfter(
        this SqliteProjectEngine engine,
        string moduleInstanceId,
        string collectionJsonKey,
        string afterItemId,
        JsonObject item) =>
        engine.RuntimeInputInstances
            .InsertModuleInstanceRuntimeCollectionItemAfter(
                moduleInstanceId,
                collectionJsonKey,
                afterItemId,
                item);

    internal static void DuplicateModuleInstanceRuntimeCollectionItem(
        this SqliteProjectEngine engine,
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        JsonObject duplicate,
        IReadOnlyDictionary<string, string> targetIdMappings) =>
        engine.RuntimeInputInstances
            .DuplicateModuleInstanceRuntimeCollectionItem(
                moduleInstanceId,
                collectionJsonKey,
                itemId,
                duplicate,
                targetIdMappings);

    internal static void DeleteModuleInstanceRuntimeCollectionItem(
        this SqliteProjectEngine engine,
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId) =>
        engine.RuntimeInputInstances
            .DeleteModuleInstanceRuntimeCollectionItem(
                moduleInstanceId,
                collectionJsonKey,
                itemId);

    internal static void MoveModuleInstanceRuntimeCollectionItem(
        this SqliteProjectEngine engine,
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        int offset) =>
        engine.RuntimeInputInstances
            .MoveModuleInstanceRuntimeCollectionItem(
                moduleInstanceId,
                collectionJsonKey,
                itemId,
                offset);

    internal static IReadOnlyList<ModuleInstanceSlot>
        GetShotModuleInstanceSlots(
            this SqliteProjectEngine engine,
            string shotId) =>
        engine.Production.GetShotModuleInstanceSlots(shotId);

    internal static IReadOnlyList<ShotModuleChoice>
        GetAvailableShotModules(
            this SqliteProjectEngine engine,
            string shotId) =>
        engine.ModuleInstanceCollection.GetAvailableShotModules(shotId);

    internal static ProjectTreeNode AddModuleInstance(
        this SqliteProjectEngine engine,
        ProjectTreeNode shot,
        ShotModuleInstanceDraft draft) =>
        engine.ModuleInstanceCollection.AddModuleInstance(
            shot,
            draft);

    internal static ProjectTreeNode RenameModuleInstance(
        this SqliteProjectEngine engine,
        ProjectTreeNode node,
        string name) =>
        engine.CoreFields.RenameDirectNode(node, name);

    internal static void MoveModuleInstance(
        this SqliteProjectEngine engine,
        string moduleInstanceId,
        int offset) =>
        engine.ModuleInstanceCollection.MoveModuleInstance(
            moduleInstanceId,
            offset);

    internal static void UpdateModuleInstanceField(
        this SqliteProjectEngine engine,
        string moduleInstanceId,
        string fieldId,
        string value) =>
        engine.RecordFields.UpdateModuleInstanceField(
            moduleInstanceId,
            fieldId,
            value);
}
