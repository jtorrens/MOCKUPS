using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private const string DefaultModuleVariantId = "default";
    private const string ModuleVariantSeparator = "::variant::";

    public sealed record ModuleVariant(
        string Id,
        string Name,
        bool IsProtected,
        bool IsLocked,
        string ConfigJson);

    internal static string ModuleVariantNodeId(string moduleId, string variantId) =>
        $"{moduleId}{ModuleVariantSeparator}{variantId}";

    internal static bool TryParseModuleVariantNodeId(string nodeId, out string moduleId, out string variantId)
    {
        var separatorIndex = nodeId.IndexOf(ModuleVariantSeparator, StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex + ModuleVariantSeparator.Length >= nodeId.Length)
        {
            moduleId = "";
            variantId = "";
            return false;
        }

        moduleId = nodeId[..separatorIndex];
        variantId = nodeId[(separatorIndex + ModuleVariantSeparator.Length)..];
        return true;
    }

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

    public ModuleSettings GetModuleVariantSettings(ProjectTreeNode variantNode)
    {
        if (variantNode.Kind != ProjectTreeNodeKind.ModuleVariant
            || !TryParseModuleVariantNodeId(variantNode.Id, out var moduleId, out var variantId))
        {
            throw new InvalidOperationException($"Invalid module variant node id '{variantNode.Id}'.");
        }

        var settings = GetModuleSettings(moduleId);
        var variant = ModuleVariants(settings.MetadataJson)
            .FirstOrDefault((candidate) => candidate.Id.Equals(variantId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Missing module variant '{variantId}'.");
        return settings with { ConfigJson = variant.ConfigJson };
    }

    public ModuleSettings GetModuleInstanceVariantSettings(string moduleInstanceId)
    {
        var instance = GetModuleInstanceSettings(moduleInstanceId);
        var reference = GetModuleInstanceVariantReference(moduleInstanceId);
        if (!TryParseModuleVariantNodeId(reference, out var moduleId, out var variantId)
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
        if (!TryParseModuleVariantNodeId(reference, out var moduleId, out var variantId))
        {
            throw new InvalidOperationException($"Invalid module variant reference '{reference}'.");
        }

        return ModuleVariants(GetModuleSettings(moduleId).MetadataJson)
            .First((variant) => variant.Id.Equals(variantId, StringComparison.Ordinal)).Name;
    }

    public IReadOnlyList<FieldOption> GetModuleVariantOptions(string moduleId) =>
        ModuleVariants(GetModuleSettings(moduleId).MetadataJson)
            .Select((variant) => new FieldOption(ModuleVariantNodeId(moduleId, variant.Id), variant.Name))
            .ToList();

    private static JsonObject EffectiveModuleInstanceContract(
        string moduleId,
        string moduleMetadataJson,
        string instanceMetadataJson,
        string designPreviewJson)
    {
        var instanceMetadata = ParseJsonObject(instanceMetadataJson);
        var reference = instanceMetadata["moduleVariantReference"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Module instance has no explicit module variant reference.");
        if (!TryParseModuleVariantNodeId(reference, out var referencedModuleId, out var variantId)
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
        if (!TryParseModuleVariantNodeId(reference, out var moduleId, out var variantId)
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
        Execute(connection,
            "UPDATE module_instances SET metadata_json = $metadataJson, content_json = $contentJson, animation_json = $animationJson WHERE id = $id",
            ("$metadataJson", metadata.ToJsonString()),
            ("$contentJson", content.ToJsonString()),
            ("$animationJson", animation.ToJsonString()),
            ("$id", moduleInstanceId));
        ReconcileModuleInstanceRuntimePayload(connection, moduleInstanceId);
        SynchronizeTimelineDurations(connection);
    }

    private static JsonObject RuntimeContentForContract(JsonObject current, JsonObject contract)
    {
        var next = new JsonObject { ["schemaVersion"] = current["schemaVersion"]?.DeepClone() ?? JsonValue.Create(2) };
        foreach (var input in (contract["inputs"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            if (!RuntimeInputDefinition(input)) continue;
            var jsonKey = input["jsonKey"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(jsonKey)) continue;
            next[jsonKey] = current[jsonKey]?.DeepClone() ?? RuntimeDefaultValue(input);
        }
        foreach (var collection in (contract["collections"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var storageKey = RuntimeCollectionStorageKey(collection);
            if (string.IsNullOrWhiteSpace(storageKey)) continue;
            next[storageKey] = collection["storageCollectionJsonKey"] is JsonValue
                ? ReconcileProjectedRuntimeCollection(
                    current[storageKey] as JsonArray,
                    contract[collection["jsonKey"]?.GetValue<string>() ?? ""] as JsonArray)
                : current[storageKey]?.DeepClone() ?? new JsonArray();
        }
        return next;
    }

    private static JsonObject RemoveOrphanedAnimationTracks(JsonObject animation, JsonObject contract, JsonObject content)
    {
        var topLevelFields = (contract["inputs"] as JsonArray)?.OfType<JsonObject>()
            .Where(RuntimeInputDefinition)
            .Select((input) => input["id"]?.GetValue<string>() ?? "")
            .Where((id) => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal) ?? [];
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

    public ProjectTreeNode SaveModuleVariant(ProjectTreeNode sourceNode, string name)
    {
        if (sourceNode.Kind != ProjectTreeNodeKind.ModuleVariant
            || !TryParseModuleVariantNodeId(sourceNode.Id, out var moduleId, out _))
        {
            throw new InvalidOperationException("Module variants can only be saved from an active selected variant.");
        }

        var variantName = name.Trim();
        if (string.IsNullOrWhiteSpace(variantName)) throw new InvalidOperationException("Variant name cannot be empty.");
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetModuleVariantSettings(sourceNode);
            var module = GetModuleSettings(moduleId);
            var metadata = ParseJsonObject(module.MetadataJson);
            var variants = VariantEnvelopeContract.RequiredArray(metadata, "variants", $"Module '{moduleId}'");
            var variantId = UniqueModuleVariantId(variants, variantName);
            variants.Add(new JsonObject
            {
                ["id"] = variantId,
                ["name"] = variantName,
                ["protected"] = false,
                ["locked"] = false,
                ["config"] = ParseJsonObject(settings.ConfigJson),
            });
            Execute(connection, "UPDATE modules SET metadata_json = $metadataJson WHERE id = $id",
                ("$metadataJson", metadata.ToJsonString()), ("$id", moduleId));
            return new ProjectTreeNode(ProjectTreeNodeKind.ModuleVariant, ModuleVariantNodeId(moduleId, variantId),
                variantName, "Module variant", ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ModuleVariant), sourceNode.Parent);
        }
    }

    private ProjectTreeNode RenameModuleClass(ProjectTreeNode node, string name)
    {
        var nextName = name.Trim();
        if (string.IsNullOrWhiteSpace(nextName)) throw new InvalidOperationException("Module name cannot be empty.");
        using var connection = OpenConnection();
        Execute(connection, "UPDATE modules SET name = $name WHERE id = $id", ("$name", nextName), ("$id", node.Id));
        return new ProjectTreeNode(ProjectTreeNodeKind.Module, node.Id, nextName, node.Notes,
            node.RecordClassId, node.Parent, isUsed: node.IsUsed, isProtected: node.IsProtected, isLocked: node.IsLocked);
    }

    public ProjectTreeNode RenameModuleVariant(ProjectTreeNode node, string name) =>
        UpdateModuleVariantMetadata(node, (variant) =>
        {
            variant["name"] = name.Trim();
        }, name.Trim());

    public void DeleteModuleVariant(ProjectTreeNode node)
    {
        if (!TryParseModuleVariantNodeId(node.Id, out var moduleId, out var variantId))
            throw new InvalidOperationException($"Invalid module variant '{node.Id}'.");
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var module = GetModuleSettings(moduleId);
            var metadata = ParseJsonObject(module.MetadataJson);
            var variants = VariantEnvelopeContract.RequiredArray(metadata, "variants", $"Module '{moduleId}'");
            var variant = FindModuleVariant(metadata, node.Id);
            if (JsonBool(variant, ["protected"])) throw new InvalidOperationException("Protected module variants cannot be deleted.");
            if (JsonBool(variant, ["locked"])) throw new InvalidOperationException("Locked module variants cannot be deleted.");
            if (ScalarLong(connection,
                    "SELECT COUNT(*) FROM module_instances WHERE module_id = $moduleId AND json_extract(metadata_json, '$.moduleVariantReference') = $reference",
                    ("$moduleId", moduleId), ("$reference", node.Id)) > 0)
                throw new InvalidOperationException("This module variant is still used and cannot be deleted.");
            for (var index = 0; index < variants.Count; index++)
            {
                if (variants[index] is JsonObject candidate && JsonPath.String(candidate, "id", "") == variantId)
                {
                    variants.RemoveAt(index);
                    break;
                }
            }
            Execute(connection, "UPDATE modules SET metadata_json = $metadataJson WHERE id = $id",
                ("$metadataJson", metadata.ToJsonString()), ("$id", moduleId));
        }
    }

    public ProjectTreeNode ToggleModuleVariantLock(ProjectTreeNode node) =>
        UpdateModuleVariantMetadata(node, (variant) => variant["locked"] = !JsonBool(variant, ["locked"]), node.Name);

    public void ReplaceModuleVariantConfig(ProjectTreeNode node, string configJson)
    {
        var config = ParseJsonObject(configJson);
        UpdateModuleVariantMetadata(node, (variant) => variant["config"] = config, node.Name);
    }

    public void UpdateModuleVariantField(ProjectTreeNode node, string fieldId, string value)
    {
        if (!TryParseModuleVariantNodeId(node.Id, out var moduleId, out _))
            throw new InvalidOperationException($"Invalid module variant '{node.Id}'.");
        if (fieldId is "module.sortOrder" or "module.metadata" or "module.recordClassId")
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
            if (JsonBool(variant, ["locked"])) throw new InvalidOperationException($"Module variant '{node.Name}' is locked.");
            var config = variant["config"] as JsonObject ?? throw new InvalidOperationException("Module variant has no config.");
            UpdateModuleConfigFieldValue(connection, module.ProjectId, config, fieldId, value);
            variant["config"] = config;
            Execute(connection, "UPDATE modules SET metadata_json = $metadataJson WHERE id = $id",
                ("$metadataJson", metadata.ToJsonString()), ("$id", moduleId));
        }
    }

    public string GetModuleVariantConfigFieldValue(ProjectTreeNode node, string fieldId) =>
        ModuleConfigFieldValue(GetModuleVariantSettings(node).ConfigJson, fieldId);

    private ProjectTreeNode UpdateModuleVariantMetadata(ProjectTreeNode node, Action<JsonObject> update, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Variant name cannot be empty.");
        if (!TryParseModuleVariantNodeId(node.Id, out var moduleId, out _))
            throw new InvalidOperationException($"Invalid module variant '{node.Id}'.");
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var module = GetModuleSettings(moduleId);
            var metadata = ParseJsonObject(module.MetadataJson);
            var variant = FindModuleVariant(metadata, node.Id);
            update(variant);
            Execute(connection, "UPDATE modules SET metadata_json = $metadataJson WHERE id = $id",
                ("$metadataJson", metadata.ToJsonString()), ("$id", moduleId));
            return new ProjectTreeNode(ProjectTreeNodeKind.ModuleVariant, node.Id,
                JsonPath.String(variant, "name", name), node.Notes, node.RecordClassId, node.Parent,
                isUsed: node.IsUsed, isProtected: JsonBool(variant, ["protected"]), isLocked: JsonBool(variant, ["locked"]));
        }
    }

    private static JsonObject FindModuleVariant(JsonObject metadata, string nodeId)
    {
        if (!TryParseModuleVariantNodeId(nodeId, out var moduleId, out var variantId))
            throw new InvalidOperationException($"Invalid module variant '{nodeId}'.");
        var variants = VariantEnvelopeContract.RequiredArray(metadata, "variants", $"Module '{moduleId}'");
        return variants.OfType<JsonObject>().FirstOrDefault((variant) => JsonPath.String(variant, "id", "") == variantId)
            ?? throw new InvalidOperationException($"Missing module variant '{variantId}'.");
    }

    private static string UniqueModuleVariantId(JsonArray variants, string name)
    {
        var baseId = new string(name.Trim().ToLowerInvariant().Select((c) => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
        if (string.IsNullOrWhiteSpace(baseId)) baseId = "variant";
        var ids = variants.OfType<JsonObject>().Select((variant) => JsonPath.String(variant, "id", "")).ToHashSet(StringComparer.Ordinal);
        var candidate = baseId;
        for (var suffix = 2; ids.Contains(candidate); suffix++) candidate = $"{baseId}_{suffix}";
        return candidate;
    }
}
