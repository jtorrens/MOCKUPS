using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProductionOwner
{
    public string GetModuleInstanceRuntimePreviewJson(
        string moduleInstanceId)
    {
        var instance = GetModuleInstanceSettings(moduleInstanceId);
        var module = GetModuleInstanceVariantSettings(moduleInstanceId);
        var preview = RuntimeInputForwardingContract.EffectivePreview(
            ParseJsonObject(module.DesignPreviewJson),
            ParseJsonObject(module.ConfigJson));
        var runtime = ParseJsonObject(instance.ContentJson);
        foreach (var (key, value) in runtime)
        {
            if (key == "schemaVersion")
            {
                continue;
            }

            preview[key] = value?.DeepClone();
        }

        preview.Remove("testValues");
        return preview.ToJsonString();
    }

    internal void UpdateModuleInstanceRuntimeValue(
        SqliteConnection connection,
        string moduleInstanceId,
        string jsonKey,
        JsonNode? value,
        IReadOnlySet<string> projectActorIds)
    {
        var content = ParseJsonObject(
            _moduleInstanceRepository
                .Get(connection, moduleInstanceId)
                .ContentJson);
        _ = RequireDeclaredRuntimeInput(
            connection,
            moduleInstanceId,
            jsonKey,
            value);
        content[jsonKey] = value?.DeepClone();
        SaveModuleInstanceRuntimeContent(
            connection,
            moduleInstanceId,
            content,
            projectActorIds);
    }

    internal void UpdateModuleInstanceAnimationJson(
        SqliteConnection connection,
        string moduleInstanceId,
        string animationJson)
    {
        var animation = ModuleInstanceAnimationDocumentContract.Parse(
            animationJson,
            $"Module Instance '{moduleInstanceId}' animation_json");
        _moduleInstanceRepository.UpdateAnimation(
            connection,
            moduleInstanceId,
            animation.ToJsonString());
        SynchronizeTimelineDurations(connection);
    }

    internal void UpdateModuleInstanceRuntimeCollectionValues(
        SqliteConnection connection,
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        IReadOnlyDictionary<string, JsonNode?> values,
        IReadOnlySet<string> projectActorIds)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                "A runtime collection update requires at least one explicit field.");
        }

        var content = ParseJsonObject(
            _moduleInstanceRepository
                .Get(connection, moduleInstanceId)
                .ContentJson);
        var item = RequireDeclaredRuntimeCollection(
                connection,
                moduleInstanceId,
                collectionJsonKey,
                content)
            .OfType<JsonObject>()
            .FirstOrDefault((candidate) =>
                candidate["id"]?.GetValue<string>() == itemId)
            ?? throw new InvalidOperationException(
                $"Missing runtime collection item '{itemId}'.");
        foreach (var (fieldJsonKey, value) in values)
        {
            if (string.IsNullOrWhiteSpace(fieldJsonKey))
            {
                throw new InvalidOperationException(
                    "Runtime collection field keys cannot be empty.");
            }

            RequireDeclaredRuntimeCollectionField(
                connection,
                moduleInstanceId,
                collectionJsonKey,
                fieldJsonKey,
                value);
            item[fieldJsonKey] = value?.DeepClone();
        }

        SaveModuleInstanceRuntimeContent(
            connection,
            moduleInstanceId,
            content,
            projectActorIds);
    }

    internal void AddModuleInstanceRuntimeCollectionItem(
        SqliteConnection connection,
        string moduleInstanceId,
        string collectionJsonKey,
        JsonObject item,
        IReadOnlySet<string> projectActorIds)
    {
        var content = ParseJsonObject(
            _moduleInstanceRepository
                .Get(connection, moduleInstanceId)
                .ContentJson);
        var items = RequireDeclaredRuntimeCollection(
            connection,
            moduleInstanceId,
            collectionJsonKey,
            content);
        RuntimeCollectionDocumentContract.RequireNewItem(
            items,
            item,
            $"runtime collection '{collectionJsonKey}'");
        items.Add(item.DeepClone());
        SaveModuleInstanceRuntimeContent(
            connection,
            moduleInstanceId,
            content,
            projectActorIds);
    }

    internal void InsertModuleInstanceRuntimeCollectionItemAfter(
        SqliteConnection connection,
        string moduleInstanceId,
        string collectionJsonKey,
        string afterItemId,
        JsonObject item,
        IReadOnlySet<string> projectActorIds)
    {
        var content = ParseJsonObject(
            _moduleInstanceRepository
                .Get(connection, moduleInstanceId)
                .ContentJson);
        var items = RequireDeclaredRuntimeCollection(
            connection,
            moduleInstanceId,
            collectionJsonKey,
            content);
        RuntimeCollectionDocumentContract.RequireNewItem(
            items,
            item,
            $"runtime collection '{collectionJsonKey}'");
        var currentIndex = IndexOfRuntimeItem(items, afterItemId);
        if (currentIndex < 0)
        {
            throw new InvalidOperationException(
                $"Missing runtime collection item '{afterItemId}'.");
        }

        items.Insert(currentIndex + 1, item.DeepClone());
        SaveModuleInstanceRuntimeContent(
            connection,
            moduleInstanceId,
            content,
            projectActorIds);
    }

    internal void DuplicateModuleInstanceRuntimeCollectionItem(
        SqliteConnection connection,
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        JsonObject duplicate,
        IReadOnlyDictionary<string, string> targetIdMappings,
        IReadOnlySet<string> projectActorIds)
    {
        var settings = _moduleInstanceRepository.Get(
            connection,
            moduleInstanceId);
        var content = ParseJsonObject(settings.ContentJson);
        var items = RequireDeclaredRuntimeCollection(
            connection,
            moduleInstanceId,
            collectionJsonKey,
            content);
        RuntimeCollectionDocumentContract.RequireNewItem(
            items,
            duplicate,
            $"runtime collection '{collectionJsonKey}'");
        var currentIndex = IndexOfRuntimeItem(items, itemId);
        if (currentIndex < 0)
        {
            throw new InvalidOperationException(
                $"Missing runtime collection item '{itemId}'.");
        }

        items.Insert(currentIndex + 1, duplicate.DeepClone());
        var animation = ParseJsonObject(settings.AnimationJson);
        if (animation["tracks"] is JsonArray tracks)
        {
            foreach (var sourceTrack in tracks
                         .OfType<JsonObject>()
                         .Where((track) =>
                             targetIdMappings.ContainsKey(
                                 track["targetId"]?.GetValue<string>()
                                 ?? ""))
                         .ToList())
            {
                var duplicateTrack =
                    sourceTrack.DeepClone().AsObject();
                duplicateTrack["id"] = $"track_{Guid.NewGuid():N}";
                duplicateTrack["targetId"] =
                    targetIdMappings[
                        sourceTrack["targetId"]?.GetValue<string>()
                        ?? ""];
                foreach (var keyframe in
                         (duplicateTrack["keyframes"] as JsonArray)
                         ?.OfType<JsonObject>() ?? [])
                {
                    keyframe["id"] =
                        $"keyframe_{Guid.NewGuid():N}";
                }

                tracks.Add(duplicateTrack);
            }
        }

        ValidateModuleInstanceRuntimeContent(
            connection,
            moduleInstanceId,
            content,
            projectActorIds);
        ModuleInstanceAnimationDocumentContract.Validate(
            animation,
            $"Module Instance '{moduleInstanceId}' animation_json");
        _moduleInstanceRepository.UpdateContentAndAnimation(
            connection,
            moduleInstanceId,
            content.ToJsonString(),
            animation.ToJsonString());
        SynchronizeTimelineDurations(connection);
    }

    internal void DeleteModuleInstanceRuntimeCollectionItem(
        SqliteConnection connection,
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        IReadOnlySet<string> projectActorIds)
    {
        var settings = _moduleInstanceRepository.Get(
            connection,
            moduleInstanceId);
        var content = ParseJsonObject(settings.ContentJson);
        var items = RequireDeclaredRuntimeCollection(
            connection,
            moduleInstanceId,
            collectionJsonKey,
            content);
        var item = items
            .OfType<JsonObject>()
            .FirstOrDefault((candidate) =>
                candidate["id"]?.GetValue<string>() == itemId)
            ?? throw new InvalidOperationException(
                $"Missing runtime collection item '{itemId}'.");
        items.Remove(item);
        var removedTargetIds = CollectionTargetIds(item);
        var animation = ParseJsonObject(settings.AnimationJson);
        if (animation["tracks"] is JsonArray tracks)
        {
            foreach (var track in tracks
                         .OfType<JsonObject>()
                         .Where((candidate) =>
                             removedTargetIds.Contains(
                                 candidate["targetId"]
                                     ?.GetValue<string>() ?? ""))
                         .ToList())
            {
                tracks.Remove(track);
            }
        }

        ValidateModuleInstanceRuntimeContent(
            connection,
            moduleInstanceId,
            content,
            projectActorIds);
        ModuleInstanceAnimationDocumentContract.Validate(
            animation,
            $"Module Instance '{moduleInstanceId}' animation_json");
        _moduleInstanceRepository.UpdateContentAndAnimation(
            connection,
            moduleInstanceId,
            content.ToJsonString(),
            animation.ToJsonString());
        SynchronizeTimelineDurations(connection);
    }

    internal void MoveModuleInstanceRuntimeCollectionItem(
        SqliteConnection connection,
        string moduleInstanceId,
        string collectionJsonKey,
        string itemId,
        int offset,
        IReadOnlySet<string> projectActorIds)
    {
        if (offset == 0)
        {
            return;
        }

        var content = ParseJsonObject(
            _moduleInstanceRepository
                .Get(connection, moduleInstanceId)
                .ContentJson);
        var items = RequireDeclaredRuntimeCollection(
            connection,
            moduleInstanceId,
            collectionJsonKey,
            content);
        var currentIndex = IndexOfRuntimeItem(items, itemId);
        if (currentIndex < 0)
        {
            throw new InvalidOperationException(
                $"Missing runtime collection item '{itemId}'.");
        }

        var targetIndex = currentIndex + offset;
        if (targetIndex < 0 || targetIndex >= items.Count)
        {
            return;
        }

        var item = items[currentIndex];
        items.RemoveAt(currentIndex);
        items.Insert(targetIndex, item);
        SaveModuleInstanceRuntimeContent(
            connection,
            moduleInstanceId,
            content,
            projectActorIds);
    }

    internal void UpdateModuleInstanceVariant(
        SqliteConnection connection,
        string moduleInstanceId,
        string reference,
        IReadOnlySet<string> projectActorIds)
    {
        var instance = _moduleInstanceRepository.Get(
            connection,
            moduleInstanceId);
        if (!VariantReferenceId.TryParse(
                reference,
                out var moduleId,
                out var variantId)
            || !moduleId.Equals(
                instance.ModuleId,
                StringComparison.Ordinal)
            || _moduleVariantCatalog.GetModuleVariants(moduleId)
                .All((variant) => variant.Id != variantId))
        {
            throw new InvalidOperationException(
                $"Invalid module variant reference '{reference}'.");
        }

        var metadata = ParseJsonObject(instance.MetadataJson);
        metadata["moduleVariantReference"] = reference;
        var contract = ResolveModuleInstanceContract(
            moduleId,
            metadata.ToJsonString());
        var content =
            RuntimeInputDocumentContract.CreateContentForContract(
                ParseJsonObject(instance.ContentJson),
                contract);
        var animation =
            RuntimeInputDocumentContract.RemoveOrphanedAnimationTracks(
                ParseJsonObject(instance.AnimationJson),
                contract,
                content);
        ValidateModuleInstanceRuntimeContent(
            connection,
            moduleInstanceId,
            content,
            projectActorIds);
        ModuleInstanceAnimationDocumentContract.Validate(
            animation,
            $"Module Instance '{moduleInstanceId}' animation_json");
        _moduleInstanceRepository.UpdateVariantDocuments(
            connection,
            moduleInstanceId,
            metadata.ToJsonString(),
            content.ToJsonString(),
            animation.ToJsonString());
        ReconcileModuleInstanceRuntimePayload(
            connection,
            moduleInstanceId,
            projectActorIds);
        SynchronizeTimelineDurations(connection);
    }

    internal void ReconcileModuleInstanceRuntimePayload(
        SqliteConnection connection,
        string moduleInstanceId,
        IReadOnlySet<string> projectActorIds)
    {
        var instance = _moduleInstanceRepository.Get(
            connection,
            moduleInstanceId);
        var original = instance.ContentJson;
        var content = ParseJsonObject(original);
        var contract = ResolveModuleInstanceContract(
            instance.ModuleId,
            instance.MetadataJson);
        foreach (var input in
                 RuntimeInputDocumentContract.DefinitionObjects(
                     contract,
                     "inputs",
                     $"Module Instance '{moduleInstanceId}' effective Runtime contract"))
        {
            var inputId = JsonPath.RequiredString(
                input,
                "id",
                "Runtime Input definition");
            var jsonKey = JsonPath.RequiredString(
                input,
                "jsonKey",
                $"Runtime Input '{inputId}'");
            if (!RuntimeInputDocumentContract.IsRuntimeDefinition(input))
            {
                content.Remove(jsonKey);
                continue;
            }

            if (!content.TryGetPropertyValue(
                    jsonKey,
                    out var currentValue))
            {
                content[jsonKey] =
                    RuntimeInputValueKindContract.CreateDefaultValue(
                        input,
                        $"Runtime Input '{inputId}'");
                continue;
            }

            RuntimeInputValueKindContract.ValidateRuntimeValue(
                input,
                currentValue,
                $"Module Instance '{moduleInstanceId}' Runtime Input '{inputId}'");
        }

        foreach (var collection in
                 RuntimeInputDocumentContract.DefinitionObjects(
                     contract,
                     "collections",
                     $"Module Instance '{moduleInstanceId}' effective Runtime contract"))
        {
            var storageKey =
                RuntimeInputDocumentContract.CollectionStorageKey(
                    collection);
            var projected =
                collection.ContainsKey("storageCollectionJsonKey");
            var items = projected
                ? RuntimeInputDocumentContract
                    .ReconcileProjectedCollection(
                        RuntimeInputDocumentContract.OptionalCollection(
                            content,
                            storageKey,
                            $"Module Instance '{moduleInstanceId}' content_json"),
                        RuntimeInputDocumentContract.OptionalCollection(
                            contract,
                            JsonPath.RequiredString(
                                collection,
                                "jsonKey",
                                "Runtime collection definition"),
                            $"Module Instance '{moduleInstanceId}' effective Runtime contract"))
                : RuntimeInputDocumentContract.OptionalCollection(
                      content,
                      storageKey,
                      $"Module Instance '{moduleInstanceId}' content_json")
                  ?? new JsonArray();
            content[storageKey] = items;
            RuntimeCollectionDocumentContract.Validate(
                items,
                $"Module Instance '{moduleInstanceId}' runtime collection '{storageKey}'");
            var fields =
                RuntimeInputDocumentContract.DefinitionObjects(
                    collection,
                    "fields",
                    $"Runtime collection '{storageKey}'",
                    required: true);
            for (var itemIndex = 0;
                 itemIndex < items.Count;
                 itemIndex++)
            {
                var item = items[itemIndex] as JsonObject
                    ?? throw new InvalidOperationException(
                        $"Runtime collection '{storageKey}' item at index {itemIndex} must be an object.");
                foreach (var field in fields)
                {
                    if (!RuntimeInputDocumentContract
                            .IsRuntimeDefinition(field))
                    {
                        continue;
                    }

                    var fieldId = JsonPath.RequiredString(
                        field,
                        "id",
                        $"Runtime collection '{storageKey}' field");
                    var jsonKey = JsonPath.RequiredString(
                        field,
                        "jsonKey",
                        $"Runtime collection '{storageKey}' field '{fieldId}'");
                    if (!item.TryGetPropertyValue(
                            jsonKey,
                            out var currentValue))
                    {
                        item[jsonKey] =
                            RuntimeInputValueKindContract
                                .CreateDefaultValue(
                                    field,
                                    $"Runtime collection field '{fieldId}'");
                        continue;
                    }

                    RuntimeInputValueKindContract.ValidateRuntimeValue(
                        field,
                        currentValue,
                        $"Runtime collection '{storageKey}' item field '{fieldId}'");
                }
            }
        }

        var next = content.ToJsonString();
        if (next == original)
        {
            return;
        }

        ValidateModuleInstanceRuntimeContent(
            connection,
            moduleInstanceId,
            content,
            projectActorIds);
        _moduleInstanceRepository.UpdateContent(
            connection,
            moduleInstanceId,
            next);
    }

    internal void UpdateModuleInstanceField(
        SqliteConnection connection,
        string moduleInstanceId,
        string fieldId,
        string value,
        IReadOnlySet<string> projectActorIds)
    {
        switch (fieldId)
        {
            case "moduleInstance.variant":
                UpdateModuleInstanceVariant(
                    connection,
                    moduleInstanceId,
                    value,
                    projectActorIds);
                return;
            case "moduleInstance.durationFrames":
                if (RuntimeDurationContract.Policy(
                        GetModuleInstanceEffectiveContractJson(
                            moduleInstanceId))
                    != RuntimeDurationPolicy.Explicit)
                {
                    throw new InvalidOperationException(
                        "Calculated Screen duration cannot be edited.");
                }

                _moduleInstanceRepository.UpdateDuration(
                    connection,
                    moduleInstanceId,
                    Math.Max(1, NumericText.Int32(value, 1)));
                SynchronizeTimelineDurations(connection);
                return;
            default:
                throw new InvalidOperationException(
                    $"Unknown module instance field '{fieldId}'.");
        }
    }

    internal ProjectTreeNode AddModuleInstance(
        SqliteConnection connection,
        ProjectTreeNode shot,
        ShotModuleInstanceDraft draft,
        IReadOnlySet<string> projectActorIds)
    {
        if (shot.Kind != ProjectTreeNodeKind.Shot)
        {
            throw new InvalidOperationException(
                "A module instance can only be added to a Shot.");
        }

        var module = draft.Module;
        if (!VariantReferenceId.TryParse(
                draft.VariantReference,
                out var variantModuleId,
                out var variantId)
            || !variantModuleId.Equals(
                module.Id,
                StringComparison.Ordinal)
            || _moduleVariantCatalog.GetModuleVariants(module.Id)
                .All((variant) => variant.Id != variantId))
        {
            throw new InvalidOperationException(
                "The selected Variant does not belong to the selected Module.");
        }

        var requestedName = draft.Name.Trim();
        if (requestedName.Length == 0)
        {
            throw new InvalidOperationException(
                "A Module Instance name is required.");
        }

        var moduleSettings =
            _moduleVariantCatalog.GetModuleSettings(module.Id);
        var initialDuration =
            RuntimeDurationContract.InitialDurationFrames(
                moduleSettings.DesignPreviewJson);
        _moduleInstanceThemeContextService.RequireShotContext(
            connection,
            shot.Id);
        var index = _moduleInstanceRepository.NextSortOrder(
            connection,
            shot.Id);
        var id = $"module_instance_{Guid.NewGuid():N}";
        var name = _moduleInstanceRepository.UniqueName(
            connection,
            shot.Id,
            requestedName);
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
                    ["moduleVariantReference"] =
                        draft.VariantReference,
                }.ToJsonString()));
        ReconcileModuleInstanceRuntimePayload(
            connection,
            id,
            projectActorIds);
        SynchronizeTimelineDurations(connection);
        var duration = _moduleInstanceRepository
            .Get(connection, id)
            .DurationFrames;
        return new ProjectTreeNode(
            ProjectTreeNodeKind.ModuleInstance,
            id,
            name,
            $"{module.Name} · {duration} frames · Cut",
            ProjectTreeNode.DefaultRecordClassId(
                ProjectTreeNodeKind.ModuleInstance),
            shot);
    }

    private void SaveModuleInstanceRuntimeContent(
        SqliteConnection connection,
        string moduleInstanceId,
        JsonObject content,
        IReadOnlySet<string> projectActorIds)
    {
        ValidateModuleInstanceRuntimeContent(
            connection,
            moduleInstanceId,
            content,
            projectActorIds);
        _moduleInstanceRepository.UpdateContent(
            connection,
            moduleInstanceId,
            content.ToJsonString());
        SynchronizeTimelineDurations(connection);
    }

    internal void ValidateModuleInstanceRuntimeContent(
        SqliteConnection connection,
        string moduleInstanceId,
        JsonObject content,
        IReadOnlySet<string> projectActorIds)
    {
        var instance = _moduleInstanceRepository.Get(
            connection,
            moduleInstanceId);
        var module =
            _moduleVariantCatalog.GetModuleSettings(instance.ModuleId);
        var contract = ResolveModuleInstanceContract(
            instance.ModuleId,
            instance.MetadataJson);
        RuntimeInputDocumentContract.ValidateCurrentCollections(
            contract,
            content,
            $"Module Instance '{moduleInstanceId}' content_json");
        RuntimeInputDocumentContract.ValidateCurrentValues(
            contract,
            content,
            $"Module Instance '{moduleInstanceId}' content_json");
        ModuleRuntimeDocumentContracts.ValidateCurrent(
            module.RecordClassId,
            $"Module Instance '{moduleInstanceId}' content_json",
            content,
            projectActorIds);
    }

    private JsonArray RequireDeclaredRuntimeCollection(
        SqliteConnection connection,
        string moduleInstanceId,
        string collectionJsonKey,
        JsonObject content)
    {
        if (string.IsNullOrWhiteSpace(collectionJsonKey))
        {
            throw new InvalidOperationException(
                "Runtime collection key cannot be empty.");
        }

        var contract = ModuleInstanceRuntimeContract(
            connection,
            moduleInstanceId);
        var matches =
            RuntimeInputDocumentContract.DefinitionObjects(
                    contract,
                    "collections",
                    $"Module Instance '{moduleInstanceId}' Runtime contract")
                .Where((collection) =>
                    RuntimeInputDocumentContract.CollectionStorageKey(
                        collection) == collectionJsonKey)
                .ToList();
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Module Instance '{moduleInstanceId}' has no unique declared runtime collection '{collectionJsonKey}'.");
        }

        var items = RuntimeInputDocumentContract.RequiredCollection(
            content,
            collectionJsonKey,
            $"Module Instance '{moduleInstanceId}' content_json");
        RuntimeCollectionDocumentContract.Validate(
            items,
            $"Module Instance '{moduleInstanceId}' runtime collection '{collectionJsonKey}'");
        return items;
    }

    private JsonObject RequireDeclaredRuntimeInput(
        SqliteConnection connection,
        string moduleInstanceId,
        string jsonKey,
        JsonNode? value)
    {
        if (string.IsNullOrWhiteSpace(jsonKey))
        {
            throw new InvalidOperationException(
                "Runtime input key cannot be empty.");
        }

        var contract = ModuleInstanceRuntimeContract(
            connection,
            moduleInstanceId);
        var matches =
            RuntimeInputDocumentContract.DefinitionObjects(
                    contract,
                    "inputs",
                    $"Module Instance '{moduleInstanceId}' Runtime contract")
                .Where(
                    RuntimeInputDocumentContract
                        .IsRuntimeDefinition)
                .Where((input) =>
                    JsonPath.RequiredString(
                        input,
                        "jsonKey",
                        "Runtime Input definition") == jsonKey)
                .ToList();
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Module Instance '{moduleInstanceId}' has no unique declared runtime input '{jsonKey}'.");
        }

        RuntimeInputValueKindContract.ValidateRuntimeValue(
            matches[0],
            value,
            $"Module Instance '{moduleInstanceId}' runtime input '{jsonKey}'");
        return matches[0];
    }

    private void RequireDeclaredRuntimeCollectionField(
        SqliteConnection connection,
        string moduleInstanceId,
        string collectionJsonKey,
        string fieldJsonKey,
        JsonNode? value)
    {
        var contract = ModuleInstanceRuntimeContract(
            connection,
            moduleInstanceId);
        var collectionMatches =
            RuntimeInputDocumentContract.DefinitionObjects(
                    contract,
                    "collections",
                    $"Module Instance '{moduleInstanceId}' Runtime contract")
                .Where((collection) =>
                    RuntimeInputDocumentContract.CollectionStorageKey(
                        collection) == collectionJsonKey)
                .ToList();
        if (collectionMatches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Module Instance '{moduleInstanceId}' has no unique declared runtime collection '{collectionJsonKey}'.");
        }

        var fieldMatches =
            RuntimeInputDocumentContract.DefinitionObjects(
                    collectionMatches[0],
                    "fields",
                    $"Runtime collection '{collectionJsonKey}'",
                    required: true)
                .Where(
                    RuntimeInputDocumentContract
                        .IsRuntimeDefinition)
                .Where((field) =>
                    JsonPath.RequiredString(
                        field,
                        "jsonKey",
                        "Runtime collection field") == fieldJsonKey)
                .ToList();
        if (fieldMatches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Runtime collection '{collectionJsonKey}' has no unique declared runtime field '{fieldJsonKey}'.");
        }

        RuntimeInputValueKindContract.ValidateRuntimeValue(
            fieldMatches[0],
            value,
            $"Module Instance '{moduleInstanceId}' runtime collection '{collectionJsonKey}' field '{fieldJsonKey}'");
    }

    private JsonObject ModuleInstanceRuntimeContract(
        SqliteConnection connection,
        string moduleInstanceId)
    {
        var instance = _moduleInstanceRepository.Get(
            connection,
            moduleInstanceId);
        return ResolveModuleInstanceContract(
            instance.ModuleId,
            instance.MetadataJson);
    }

    private static int IndexOfRuntimeItem(
        JsonArray items,
        string itemId)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index]?["id"]?.GetValue<string>() == itemId)
            {
                return index;
            }
        }

        return -1;
    }

    private static HashSet<string> CollectionTargetIds(
        JsonNode root)
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

                foreach (var child in
                         value.Select((entry) => entry.Value))
                {
                    Visit(child);
                }
            }
            else if (node is JsonArray array)
            {
                foreach (var child in array)
                {
                    Visit(child);
                }
            }
        }

        Visit(root);
        return result;
    }

    private static string DefaultModuleAnimationJson() =>
        new JsonObject
        {
            ["schemaVersion"] = 2,
            ["tracks"] = new JsonArray(),
        }.ToJsonString();
}
