using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

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

    public ModuleSettings GetModuleInstanceVariantSettings(string moduleInstanceId)
    {
        var instance = GetModuleInstanceSettings(moduleInstanceId);
        var reference = GetModuleInstanceVariantReference(moduleInstanceId);
        if (!VariantReferenceId.TryParse(reference, out var moduleId, out var variantId)
            || !moduleId.Equals(instance.ModuleId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Module instance '{moduleInstanceId}' has an invalid module variant reference.");
        }

        var settings = GetModuleSettings(moduleId);
        var variant = ModuleVariants(settings.MetadataJson)
            .FirstOrDefault((candidate) => candidate.Id.Equals(variantId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Missing module variant '{reference}'.");
        return settings with { ConfigJson = variant.ConfigJson };
    }

    public string GetModuleInstanceEffectiveContractJson(string moduleInstanceId)
    {
        var instance = GetModuleInstanceSettings(moduleInstanceId);
        var module = GetModuleSettings(instance.ModuleId);
        return EffectiveModuleInstanceContract(
            instance.ModuleId,
            module.MetadataJson,
            instance.MetadataJson,
            module.DesignPreviewJson).ToJsonString();
    }

    public string GetModuleInstanceVariantReference(string moduleInstanceId)
    {
        var instance = GetModuleInstanceSettings(moduleInstanceId);
        var metadata = ParseJsonObject(instance.MetadataJson);
        return metadata["moduleVariantReference"]?.GetValue<string>()
            ?? throw new InvalidOperationException($"Module instance '{moduleInstanceId}' has no explicit module variant reference.");
    }

    public string GetModuleInstanceVariantName(string moduleInstanceId)
    {
        var reference = GetModuleInstanceVariantReference(moduleInstanceId);
        if (!VariantReferenceId.TryParse(reference, out var moduleId, out var variantId))
        {
            throw new InvalidOperationException($"Invalid module variant reference '{reference}'.");
        }

        return ModuleVariants(GetModuleSettings(moduleId).MetadataJson)
            .First((variant) => variant.Id.Equals(variantId, StringComparison.Ordinal)).Name;
    }

    public IReadOnlyList<FieldOption> GetModuleVariantOptions(
        string moduleId) =>
        _designOwner.GetModuleVariantOptions(moduleId);

    private static JsonObject EffectiveModuleInstanceContract(
        string moduleId,
        string moduleMetadataJson,
        string instanceMetadataJson,
        string designPreviewJson)
    {
        var instanceMetadata = ParseJsonObject(instanceMetadataJson);
        var reference = instanceMetadata["moduleVariantReference"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Module instance has no explicit module variant reference.");
        if (!VariantReferenceId.TryParse(reference, out var referencedModuleId, out var variantId)
            || referencedModuleId != moduleId)
            throw new InvalidOperationException($"Invalid module variant reference '{reference}'.");
        var variant = ModuleVariants(moduleMetadataJson)
            .FirstOrDefault((candidate) => candidate.Id == variantId)
            ?? throw new InvalidOperationException($"Missing module variant '{reference}'.");
        return RuntimeInputForwardingContract.EffectivePreview(
            ParseJsonObject(designPreviewJson),
            ParseJsonObject(variant.ConfigJson));
    }

    public void UpdateModuleInstanceVariant(string moduleInstanceId, string reference)
    {
        var instance = GetModuleInstanceSettings(moduleInstanceId);
        if (!VariantReferenceId.TryParse(reference, out var moduleId, out var variantId)
            || !moduleId.Equals(instance.ModuleId, StringComparison.Ordinal)
            || ModuleVariants(GetModuleSettings(moduleId).MetadataJson).All((variant) => variant.Id != variantId))
        {
            throw new InvalidOperationException($"Invalid module variant reference '{reference}'.");
        }

        var metadata = ParseJsonObject(instance.MetadataJson);
        metadata["moduleVariantReference"] = reference;
        var module = GetModuleSettings(moduleId);
        var contract = EffectiveModuleInstanceContract(
            moduleId, module.MetadataJson, metadata.ToJsonString(), module.DesignPreviewJson);
        var content = RuntimeContentForContract(ParseJsonObject(instance.ContentJson), contract);
        var animation = RemoveOrphanedAnimationTracks(ParseJsonObject(instance.AnimationJson), contract, content);
        using var connection = OpenConnection();
        ValidateModuleInstanceRuntimeContent(connection, moduleInstanceId, content);
        ModuleInstanceAnimationDocumentContract.Validate(
            animation,
            $"Module Instance '{moduleInstanceId}' animation_json");
        _productionOwner.ModuleInstanceRepository.UpdateVariantDocuments(
            connection,
            moduleInstanceId,
            metadata.ToJsonString(),
            content.ToJsonString(),
            animation.ToJsonString());
        ReconcileModuleInstanceRuntimePayload(connection, moduleInstanceId);
        SynchronizeTimelineDurations(connection);
    }

    private static JsonObject RuntimeContentForContract(JsonObject current, JsonObject contract)
    {
        var next = new JsonObject { ["schemaVersion"] = current["schemaVersion"]?.DeepClone() ?? JsonValue.Create(2) };
        foreach (var input in RuntimeDefinitionObjects(
                     contract,
                     "inputs",
                     "Effective Module Runtime contract"))
        {
            if (!RuntimeInputDefinition(input)) continue;
            var inputId = JsonPath.RequiredString(input, "id", "Runtime Input definition");
            var jsonKey = JsonPath.RequiredString(input, "jsonKey", $"Runtime Input '{inputId}'");
            if (current.TryGetPropertyValue(jsonKey, out var currentValue))
            {
                RuntimeInputValueKindContract.ValidateRuntimeValue(
                    input,
                    currentValue,
                    $"Current Runtime Input '{inputId}'");
                next[jsonKey] = currentValue!.DeepClone();
            }
            else
            {
                next[jsonKey] = RuntimeInputValueKindContract.CreateDefaultValue(
                    input,
                    $"Runtime Input '{inputId}'");
            }
        }
        foreach (var collection in RuntimeDefinitionObjects(
                     contract,
                     "collections",
                     "Effective Module Runtime contract"))
        {
            var storageKey = RuntimeCollectionStorageKey(collection);
            next[storageKey] = collection.ContainsKey("storageCollectionJsonKey")
                ? ReconcileProjectedRuntimeCollection(
                    OptionalRuntimeCollection(current, storageKey, "Current Module Instance content"),
                    OptionalRuntimeCollection(
                        contract,
                        JsonPath.RequiredString(collection, "jsonKey", "Runtime collection definition"),
                        "Effective Module Runtime contract"))
                : (OptionalRuntimeCollection(current, storageKey, "Current Module Instance content")
                    ?? new JsonArray()).DeepClone();
        }
        return next;
    }

    private static JsonObject RemoveOrphanedAnimationTracks(JsonObject animation, JsonObject contract, JsonObject content)
    {
        var topLevelFields = RuntimeDefinitionObjects(
                contract,
                "inputs",
                "Effective Module Runtime contract")
            .Where(RuntimeInputDefinition)
            .Select((input) => JsonPath.RequiredString(input, "id", "Runtime Input definition"))
            .ToHashSet(StringComparer.Ordinal);
        var targetIds = new HashSet<string>(StringComparer.Ordinal);
        CollectObjectIds(content, targetIds);
        if (animation["tracks"] is JsonArray tracks)
        {
            for (var index = tracks.Count - 1; index >= 0; index--)
            {
                if (tracks[index] is not JsonObject track) continue;
                var targetId = track["targetId"]?.GetValue<string>() ?? "";
                var fieldId = track["fieldId"]?.GetValue<string>() ?? "";
                if ((!string.IsNullOrWhiteSpace(targetId) && !targetIds.Contains(targetId))
                    || (string.IsNullOrWhiteSpace(targetId) && !topLevelFields.Contains(fieldId)))
                    tracks.RemoveAt(index);
            }
        }
        return animation;
    }

    private static void CollectObjectIds(JsonNode? node, ISet<string> ids)
    {
        if (node is JsonObject obj)
        {
            if (obj["id"]?.GetValue<string>() is { Length: > 0 } id) ids.Add(id);
            foreach (var child in obj.Select((entry) => entry.Value)) CollectObjectIds(child, ids);
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array) CollectObjectIds(child, ids);
        }
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
