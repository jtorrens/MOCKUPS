using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public sealed record ModuleInstanceSlot(
        string Id,
        string Name,
        string ModuleName,
        int SortOrder,
        string TransitionType,
        int StoredDurationFrames);

    public ModuleInstanceSettings GetModuleInstanceSettings(string moduleInstanceId)
    {
        var record = _moduleInstanceRepository.Get(moduleInstanceId);
        return new ModuleInstanceSettings(
            record.ShotId, record.AppId, record.ModuleId, record.Name, record.Notes,
            record.SortOrder, record.DurationFrames, record.TransitionJson, record.ContentJson,
            record.BehaviorJson, record.AnimationJson, record.MetadataJson);
    }

    public string GetModuleInstanceModuleName(string moduleInstanceId)
    {
        var instance = _moduleInstanceRepository.Get(moduleInstanceId);
        return _appModuleRepository.GetModule(instance.ModuleId).Name;
    }

    public string GetModuleInstanceTransitionType(string moduleInstanceId)
    {
        var transition = ParseJsonObject(GetModuleInstanceSettings(moduleInstanceId).TransitionJson);
        return transition["type"]?.GetValue<string>() ?? "cut";
    }

    public string GetModuleInstanceRuntimePreviewJson(string moduleInstanceId)
    {
        var instance = GetModuleInstanceSettings(moduleInstanceId);
        var module = GetModuleInstanceVariantSettings(moduleInstanceId);
        var preview = RuntimeInputForwardingContract.EffectivePreview(
            ParseJsonObject(module.DesignPreviewJson),
            ParseJsonObject(module.ConfigJson));
        var runtime = ParseJsonObject(instance.ContentJson);
        foreach (var (key, value) in runtime)
        {
            if (key == "schemaVersion") continue;
            preview[key] = value?.DeepClone();
        }
        preview.Remove("testValues");
        return preview.ToJsonString();
    }

    public void UpdateModuleInstanceRuntimeValue(string moduleInstanceId, string jsonKey, JsonNode? value)
    {
        if (string.IsNullOrWhiteSpace(jsonKey)) throw new InvalidOperationException("Runtime input key cannot be empty.");
        var content = ParseJsonObject(GetModuleInstanceSettings(moduleInstanceId).ContentJson);
        content[jsonKey] = value?.DeepClone();
        SaveModuleInstanceRuntimeContent(moduleInstanceId, content);
    }

    public void UpdateModuleInstanceAnimationJson(string moduleInstanceId, string animationJson)
    {
        var animation = ParseJsonObject(animationJson);
        ValidateAnimationJson(animation, moduleInstanceId);
        using var connection = OpenConnection();
        _moduleInstanceRepository.UpdateAnimation(connection, moduleInstanceId, animation.ToJsonString());
        SynchronizeTimelineDurations(connection);
    }

    public void UpdateModuleInstanceRuntimeCollectionValue(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        string fieldJsonKey,
        JsonNode? value)
    {
        UpdateModuleInstanceRuntimeCollectionValues(
            moduleInstanceId,
            collectionJsonKey,
            itemId,
            new Dictionary<string, JsonNode?> { [fieldJsonKey] = value });
    }

    public void UpdateModuleInstanceRuntimeCollectionValues(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        IReadOnlyDictionary<string, JsonNode?> values)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException("A runtime collection update requires at least one explicit field.");
        }
        var content = ParseJsonObject(GetModuleInstanceSettings(moduleInstanceId).ContentJson);
        var item = (content[collectionJsonKey] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault((candidate) => candidate["id"]?.GetValue<string>() == itemId)
            ?? throw new InvalidOperationException($"Missing runtime collection item '{itemId}'.");
        foreach (var (fieldJsonKey, value) in values)
        {
            if (string.IsNullOrWhiteSpace(fieldJsonKey))
            {
                throw new InvalidOperationException("Runtime collection field keys cannot be empty.");
            }
            item[fieldJsonKey] = value?.DeepClone();
        }
        SaveModuleInstanceRuntimeContent(moduleInstanceId, content);
    }

    public void AddModuleInstanceRuntimeCollectionItem(string moduleInstanceId, string collectionJsonKey, JsonObject item)
    {
        var content = ParseJsonObject(GetModuleInstanceSettings(moduleInstanceId).ContentJson);
        var items = content[collectionJsonKey] as JsonArray ?? new JsonArray();
        content[collectionJsonKey] = items;
        items.Add(item.DeepClone());
        SaveModuleInstanceRuntimeContent(moduleInstanceId, content);
    }

    public void InsertModuleInstanceRuntimeCollectionItemAfter(
        string moduleInstanceId,
        string collectionJsonKey,
        string afterItemId,
        JsonObject item)
    {
        var content = ParseJsonObject(GetModuleInstanceSettings(moduleInstanceId).ContentJson);
        var items = content[collectionJsonKey] as JsonArray ?? new JsonArray();
        content[collectionJsonKey] = items;
        var currentIndex = -1;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index]?["id"]?.GetValue<string>() != afterItemId) continue;
            currentIndex = index;
            break;
        }
        items.Insert(currentIndex < 0 ? items.Count : currentIndex + 1, item.DeepClone());
        SaveModuleInstanceRuntimeContent(moduleInstanceId, content);
    }

    public void DuplicateModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        JsonObject duplicate,
        IReadOnlyDictionary<string, string> targetIdMappings)
    {
        var settings = GetModuleInstanceSettings(moduleInstanceId);
        var content = ParseJsonObject(settings.ContentJson);
        var items = content[collectionJsonKey] as JsonArray
            ?? throw new InvalidOperationException($"Missing runtime collection '{collectionJsonKey}'.");
        var currentIndex = -1;
        JsonObject? source = null;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index] is not JsonObject candidate
                || candidate["id"]?.GetValue<string>() != itemId) continue;
            currentIndex = index;
            source = candidate;
            break;
        }
        if (source is null) throw new InvalidOperationException($"Missing runtime collection item '{itemId}'.");
        items.Insert(currentIndex + 1, duplicate.DeepClone());

        var animation = ParseJsonObject(settings.AnimationJson);
        if (animation["tracks"] is JsonArray tracks)
        {
            foreach (var sourceTrack in tracks.OfType<JsonObject>()
                .Where((track) => targetIdMappings.ContainsKey(track["targetId"]?.GetValue<string>() ?? ""))
                .ToList())
            {
                var duplicateTrack = sourceTrack.DeepClone().AsObject();
                duplicateTrack["id"] = $"track_{Guid.NewGuid():N}";
                duplicateTrack["targetId"] = targetIdMappings[sourceTrack["targetId"]?.GetValue<string>() ?? ""];
                foreach (var keyframe in (duplicateTrack["keyframes"] as JsonArray)?.OfType<JsonObject>() ?? [])
                {
                    keyframe["id"] = $"keyframe_{Guid.NewGuid():N}";
                }
                tracks.Add(duplicateTrack);
            }
        }

        using var connection = OpenConnection();
        ValidateModuleInstanceRuntimeContent(connection, moduleInstanceId, content);
        _moduleInstanceRepository.UpdateContentAndAnimation(
            connection,
            moduleInstanceId,
            content.ToJsonString(),
            animation.ToJsonString());
        SynchronizeTimelineDurations(connection);
    }

    public void DeleteModuleInstanceRuntimeCollectionItem(string moduleInstanceId, string collectionJsonKey, string itemId)
    {
        var settings = GetModuleInstanceSettings(moduleInstanceId);
        var content = ParseJsonObject(settings.ContentJson);
        var items = content[collectionJsonKey] as JsonArray
            ?? throw new InvalidOperationException($"Missing runtime collection '{collectionJsonKey}'.");
        var item = items.OfType<JsonObject>().FirstOrDefault((candidate) => candidate["id"]?.GetValue<string>() == itemId)
            ?? throw new InvalidOperationException($"Missing runtime collection item '{itemId}'.");
        items.Remove(item);
        var removedTargetIds = CollectionTargetIds(item);
        var animation = ParseJsonObject(settings.AnimationJson);
        if (animation["tracks"] is JsonArray tracks)
        {
            foreach (var track in tracks.OfType<JsonObject>()
                .Where((candidate) => removedTargetIds.Contains(candidate["targetId"]?.GetValue<string>() ?? ""))
                .ToList())
            {
                tracks.Remove(track);
            }
        }
        using var connection = OpenConnection();
        ValidateModuleInstanceRuntimeContent(connection, moduleInstanceId, content);
        _moduleInstanceRepository.UpdateContentAndAnimation(
            connection,
            moduleInstanceId,
            content.ToJsonString(),
            animation.ToJsonString());
        SynchronizeTimelineDurations(connection);
    }

    private static HashSet<string> CollectionTargetIds(JsonNode root)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        void Visit(JsonNode? node)
        {
            if (node is JsonObject value)
            {
                if (value["id"] is JsonValue idValue
                    && idValue.TryGetValue<string>(out var id)
                    && !string.IsNullOrWhiteSpace(id))
                {
                    result.Add(id);
                }
                foreach (var child in value.Select((entry) => entry.Value)) Visit(child);
            }
            else if (node is JsonArray array)
            {
                foreach (var child in array) Visit(child);
            }
        }
        Visit(root);
        return result;
    }

    public void MoveModuleInstanceRuntimeCollectionItem(
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        int offset)
    {
        if (offset == 0) return;
        var content = ParseJsonObject(GetModuleInstanceSettings(moduleInstanceId).ContentJson);
        var items = content[collectionJsonKey] as JsonArray
            ?? throw new InvalidOperationException($"Missing runtime collection '{collectionJsonKey}'.");
        var currentIndex = -1;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index]?["id"]?.GetValue<string>() != itemId) continue;
            currentIndex = index;
            break;
        }
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= items.Count) return;
        var item = items[currentIndex];
        items.RemoveAt(currentIndex);
        items.Insert(targetIndex, item);
        SaveModuleInstanceRuntimeContent(moduleInstanceId, content);
    }

    private void SaveModuleInstanceRuntimeContent(string moduleInstanceId, JsonObject content)
    {
        using var connection = OpenConnection();
        ValidateModuleInstanceRuntimeContent(connection, moduleInstanceId, content);
        _moduleInstanceRepository.UpdateContent(connection, moduleInstanceId, content.ToJsonString());
        SynchronizeTimelineDurations(connection);
    }

    public IReadOnlyList<ModuleInstanceSlot> GetShotModuleInstanceSlots(string shotId)
    {
        using var connection = OpenConnection();
        var modules = _appModuleRepository.QueryModules(connection)
            .ToDictionary((module) => module.Id, (module) => module.Name, StringComparer.Ordinal);
        return _moduleInstanceRepository.QueryByShot(connection, shotId)
            .Select((instance) => new ModuleInstanceSlot(
                instance.Id,
                instance.Name,
                modules.TryGetValue(instance.ModuleId, out var moduleName)
                    ? moduleName
                    : throw new InvalidOperationException($"Missing module '{instance.ModuleId}'."),
                instance.SortOrder,
                ParseJsonObject(instance.TransitionJson)["type"]?.GetValue<string>() ?? "cut",
                instance.DurationFrames))
            .ToList();
    }

    public IReadOnlyList<ShotModuleChoice> GetAvailableShotModules(string shotId)
    {
        using var connection = OpenConnection();
        var shot = _shotRepository.Get(connection, shotId);
        var apps = _appModuleRepository.QueryApps(connection)
            .Where((app) => app.ProjectId == shot.ProjectId)
            .OrderBy((app) => app.SortOrder)
            .ThenBy((app) => app.Name)
            .ToDictionary((app) => app.Id, StringComparer.Ordinal);
        return _appModuleRepository.QueryModules(connection)
            .Where((module) => apps.ContainsKey(module.AppId))
            .OrderBy((module) => apps[module.AppId].SortOrder)
            .ThenBy((module) => apps[module.AppId].Name)
            .ThenBy((module) => module.SortOrder)
            .ThenBy((module) => module.Name)
            .Select((module) => new ShotModuleChoice(
                module.Id,
                module.Name,
                apps[module.AppId].Name,
                module.AppId,
                module.RecordClassId))
            .ToList();
    }

    public ProjectTreeNode AddModuleInstance(ProjectTreeNode shot, ShotModuleInstanceDraft draft)
    {
        if (shot.Kind != ProjectTreeNodeKind.Shot)
            throw new InvalidOperationException("A module instance can only be added to a Shot.");
        var module = draft.Module;
        if (!VariantReferenceId.TryParse(draft.VariantReference, out var variantModuleId, out _)
            || !variantModuleId.Equals(module.Id, StringComparison.Ordinal)
            || GetModuleVariantOptions(module.Id).All((option) => option.Value != draft.VariantReference))
            throw new InvalidOperationException("The selected Variant does not belong to the selected Module.");
        var requestedName = draft.Name.Trim();
        if (requestedName.Length == 0)
            throw new InvalidOperationException("A Module Instance name is required.");
        var initialDuration = RuntimeDurationContract.InitialDurationFrames(GetModuleSettings(module.Id).DesignPreviewJson);
        using var connection = OpenConnection();
        _moduleInstanceThemeContextService.RequireShotContext(connection, shot.Id);
        var index = _moduleInstanceRepository.NextSortOrder(connection, shot.Id);
        var id = $"module_instance_{Guid.NewGuid():N}";
        var name = _moduleInstanceRepository.UniqueName(connection, shot.Id, requestedName);
        _moduleInstanceRepository.Insert(
            connection,
            new ModuleInstanceRecord(
                id,
                shot.Id,
                module.AppId,
                module.Id,
                name,
                $"{module.Name} module instance.",
                index,
                initialDuration,
                "{\"type\":\"cut\"}",
                "{}",
                "{}",
                DefaultModuleAnimationJson(),
                new JsonObject
            {
                ["moduleVariantReference"] = draft.VariantReference,
            }.ToJsonString()));
        ReconcileModuleInstanceRuntimePayload(connection, id);
        SynchronizeTimelineDurations(connection);
        var duration = _moduleInstanceRepository.Get(connection, id).DurationFrames;
        return new ProjectTreeNode(
            ProjectTreeNodeKind.ModuleInstance,
            id,
            name,
            $"{module.Name} · {duration} frames · Cut",
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ModuleInstance),
            shot);
    }

    public ProjectTreeNode RenameModuleInstance(ProjectTreeNode node, string name)
    {
        if (node.Kind != ProjectTreeNodeKind.ModuleInstance)
            throw new InvalidOperationException("Only a Module Instance can be renamed here.");
        var requestedName = name.Trim();
        if (requestedName.Length == 0)
            throw new InvalidOperationException("A Module Instance name is required.");
        using var connection = OpenConnection();
        _moduleInstanceRepository.Rename(connection, node.Id, requestedName);
        return new ProjectTreeNode(
            ProjectTreeNodeKind.ModuleInstance,
            node.Id,
            requestedName,
            node.Notes,
            node.RecordClassId,
            node.Parent);
    }

    public void MoveModuleInstance(string moduleInstanceId, int offset)
    {
        if (offset == 0) return;

        var current = GetModuleInstanceSettings(moduleInstanceId);
        var slots = GetShotModuleInstanceSlots(current.ShotId).ToList();
        var currentIndex = slots.FindIndex((slot) => slot.Id == moduleInstanceId);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= slots.Count) return;

        var currentSlot = slots[currentIndex];
        var targetSlot = slots[targetIndex];
        using var connection = OpenConnection();
        _moduleInstanceRepository.SwapSortOrder(
            connection,
            currentSlot.Id,
            currentSlot.SortOrder,
            targetSlot.Id,
            targetSlot.SortOrder);
    }

    public void UpdateModuleInstanceField(string moduleInstanceId, string fieldId, string value)
    {
        switch (fieldId)
        {
            case "moduleInstance.variant":
                UpdateModuleInstanceVariant(moduleInstanceId, value);
                return;
            case "moduleInstance.durationFrames":
                if (RuntimeDurationContract.Policy(GetModuleInstanceEffectiveContractJson(moduleInstanceId))
                    != RuntimeDurationPolicy.Explicit)
                    throw new InvalidOperationException("Calculated Screen duration cannot be edited.");
                using (var connection = OpenConnection())
                {
                    _moduleInstanceRepository.UpdateDuration(
                        connection,
                        moduleInstanceId,
                        Math.Max(1, NumericText.Int32(value, 1)));
                    SynchronizeTimelineDurations(connection);
                }
                return;
            default:
                throw new InvalidOperationException($"Unknown module instance field '{fieldId}'.");
        }
    }

    private static string DefaultModuleAnimationJson()
    {
        return new JsonObject
        {
            ["schemaVersion"] = 2,
            ["tracks"] = new JsonArray(),
        }.ToJsonString();
    }

}
