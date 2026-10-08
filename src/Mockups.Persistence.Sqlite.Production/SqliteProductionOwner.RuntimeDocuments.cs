using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProductionOwner
{
    internal void ReconcileVariantRuntimePayloads(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyDictionary<string, JsonObject> previousContracts)
    {
        var shots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, previousContract) in previousContracts)
        {
            var instance = _moduleInstanceRepository.Get(connection, id);
            var contract = ResolveModuleInstanceContract(connection, instance.ModuleId, instance.MetadataJson);
            var content = RuntimeInputDocumentContract.ReconcileContentForContract(
                ParseJsonObject(instance.ContentJson), previousContract, contract);
            var runtime = PrepareModuleInstanceRuntimeDocument(connection, id, content);
            content = RuntimeInputDocumentContract.CreateContentForContract(runtime, runtime);
            var animation = RuntimeInputDocumentContract.RemoveOrphanedAnimationTracks(
                ParseJsonObject(instance.AnimationJson), runtime, runtime);
            _moduleInstanceRepository.UpdateContentAndAnimation(connection, id,
                content.ToJsonString(), animation.ToJsonString(), transaction);
            shots.Add(instance.ShotId);
        }
        CompleteScreenWrite(connection, transaction, shots);
    }

    public string GetModuleInstanceRuntimePreviewJson(
        string moduleInstanceId)
    {
        using var connection = OpenConnection();
        var instance = _moduleInstanceRepository.Get(connection, moduleInstanceId);
        var preview = PrepareModuleInstanceRuntimeDocument(connection, moduleInstanceId, ParseJsonObject(instance.ContentJson));
        preview.Remove("testValues");
        return preview.ToJsonString();
    }

    private JsonObject PrepareModuleInstanceRuntimeDocument(
        SqliteConnection connection, string moduleInstanceId, JsonObject content)
    {
        var module = GetModuleInstanceVariantSettings(connection, moduleInstanceId);
        var config = ParseJsonObject(module.ConfigJson);
        return RuntimePreviewDocumentContract.PrepareRuntime(
            ParseJsonObject(module.DesignPreviewJson),
            config,
            content,
            reference => _componentVariantConfigCatalog.GetComponentVariantConfig(connection, reference),
            reference => _componentVariantConfigCatalog.GetComponentVariantRuntimeContract(connection, reference));
    }

    internal void UpdateModuleInstanceRuntimeValue(
        SqliteConnection connection,
        string moduleInstanceId,
        string jsonKey,
        JsonNode? value)
    {
        CommitModuleInstanceWrite(connection, moduleInstanceId, (transaction, instance) =>
        {
            var content = ParseJsonObject(instance.ContentJson);
            var contract = ResolveModuleInstanceContract(
                connection,
                instance.ModuleId,
                instance.MetadataJson);
            var shot = _shotRepository.Get(connection, instance.ShotId);
            var project = _projectEpisodeRepository.GetProjectSettings(
                connection,
                shot.ProjectId);
            content = RuntimeInputDocumentContract.UpdateValue(
                    contract,
                    content,
                    ParseJsonObject(instance.AnimationJson),
                    jsonKey,
                    value,
                    ParseJsonObject(
                        _moduleInstanceThemeContextService.GetTokensJson(
                            connection,
                            moduleInstanceId)),
                    shot.FpsOverride ?? project.DefaultFps);
            _moduleInstanceRepository.UpdateContentAndAnimation(
                connection, moduleInstanceId, content.ToJsonString(), instance.AnimationJson, transaction);
            return true;
        });
    }

    internal void UpdateModuleInstanceAnimationJson(
        SqliteConnection connection,
        string moduleInstanceId,
        string animationJson)
    {
        CommitModuleInstanceWrite(connection, moduleInstanceId, (transaction, instance) =>
        {
            var animation = ModuleInstanceAnimationDocumentContract.Parse(
                animationJson, $"Module Instance '{moduleInstanceId}' animation_json");
            _moduleInstanceRepository.UpdateContentAndAnimation(
                connection, moduleInstanceId, instance.ContentJson, animation.ToJsonString(), transaction);
            return true;
        });
    }

    internal void UpdateModuleInstanceRuntimeCollectionValues(
        SqliteConnection connection,
        string moduleInstanceId,
        StructuredCollectionAddress address,
        string itemId,
        IReadOnlyDictionary<string, JsonNode?> values)
    {
        CommitModuleInstanceWrite(connection, moduleInstanceId, (transaction, instance) =>
        {
            var content = ParseJsonObject(instance.ContentJson);
            var rootDefinition = RequireDeclaredRuntimeCollectionDefinition(
                connection,
                moduleInstanceId,
                address.RootStorageJsonKey,
                content);
            var nextContent = StructuredCollectionMutationEngine.UpdateValues(
                content,
                rootDefinition,
                address,
                itemId,
                values,
                reference => _componentVariantConfigCatalog.GetComponentVariantConfig(connection, reference),
                reference => _componentVariantConfigCatalog.GetComponentVariantRuntimeContract(connection, reference));

            _moduleInstanceRepository.UpdateContentAndAnimation(
                connection, moduleInstanceId, nextContent.ToJsonString(), instance.AnimationJson, transaction);
            return true;
        });
    }

    internal StructuredCollectionMutationResult MutateModuleInstanceStructuredCollection(
        SqliteConnection connection,
        string moduleInstanceId,
        StructuredCollectionMutation mutation)
    {
        return CommitModuleInstanceWrite(connection, moduleInstanceId, (transaction, settings) =>
        {
            var content = ParseJsonObject(settings.ContentJson);
            var rootDefinition = RequireDeclaredRuntimeCollectionDefinition(
                connection,
                moduleInstanceId,
                mutation.Address.RootStorageJsonKey,
                content);
            var animation = ParseJsonObject(settings.AnimationJson);
            var result = StructuredCollectionMutationEngine.Apply(
                content,
                animation,
                rootDefinition,
                mutation);

            _moduleInstanceRepository.UpdateContentAndAnimation(
                connection,
                moduleInstanceId,
                result.Content.ToJsonString(),
                result.Animation.ToJsonString(),
                transaction);
            return result;
        });
    }

    internal void UpdateModuleInstanceVariant(
        SqliteConnection connection,
        string moduleInstanceId,
        string reference)
    {
        CommitModuleInstanceWrite(connection, moduleInstanceId, (transaction, instance) =>
        {
            if (!VariantReferenceId.TryParse(reference, out var moduleId, out var variantId)
                || moduleId != instance.ModuleId
                || _moduleVariantCatalog.GetModuleVariants(connection, moduleId).All(variant => variant.Id != variantId))
                throw new InvalidOperationException($"Invalid module variant reference '{reference}'.");

            var previousContract = ResolveModuleInstanceContract(connection, moduleId, instance.MetadataJson);
            var metadata = ParseJsonObject(instance.MetadataJson);
            metadata["moduleVariantReference"] = reference;
            var contract = ResolveModuleInstanceContract(connection, moduleId, metadata.ToJsonString());
            var content = RuntimeInputDocumentContract.ReconcileContentForContract(
                ParseJsonObject(instance.ContentJson), previousContract, contract);
            var animation = RuntimeInputDocumentContract.RemoveOrphanedAnimationTracks(
                ParseJsonObject(instance.AnimationJson), contract, content);
            _moduleInstanceRepository.UpdateVariantDocuments(
                connection, moduleInstanceId, metadata.ToJsonString(), content.ToJsonString(), animation.ToJsonString(), transaction);
            return true;
        });
    }

    internal void UpdateModuleInstanceField(
        SqliteConnection connection,
        string moduleInstanceId,
        string fieldId,
        string value)
    {
        if (fieldId == "moduleInstance.variant")
        {
            UpdateModuleInstanceVariant(connection, moduleInstanceId, value);
            return;
        }
        CommitModuleInstanceWrite(connection, moduleInstanceId, (transaction, instance) =>
        {
            switch (fieldId)
            {
                case "moduleInstance.startFrame":
                    _moduleInstanceRepository.UpdateStartFrame(connection, moduleInstanceId,
                        NumericText.Int32(value, 0), transaction);
                    break;
                case "moduleInstance.themeId":
                    var shot = _shotRepository.Get(connection, instance.ShotId);
                    ProjectReferenceIntegrity.RequireSameProjectReference(connection, shot.ProjectId,
                        ProjectReferenceKind.Theme, value, $"Screen '{moduleInstanceId}' Theme", required: true);
                    _moduleInstanceRepository.UpdateTheme(connection, moduleInstanceId, value, transaction);
                    break;
                case "moduleInstance.durationFrames":
                    if (RuntimeDurationContract.ParsePolicy(instance.DurationPolicy) != RuntimeDurationPolicy.Explicit)
                        throw new InvalidOperationException("Calculated Screen duration cannot be edited.");
                    _moduleInstanceRepository.UpdateDuration(connection, moduleInstanceId,
                        Math.Max(1, NumericText.Int32(value, 1)), transaction);
                    break;
                case "moduleInstance.durationPolicy":
                    var contract = ResolveModuleInstanceContract(connection, instance.ModuleId, instance.MetadataJson);
                    var policy = RuntimeDurationContract.RequireAllowedPolicy(contract, value);
                    _moduleInstanceRepository.UpdateDurationPolicy(connection, moduleInstanceId,
                        RuntimeDurationContract.FormatPolicy(policy), transaction);
                    break;
                case "moduleInstance.actionDelayFrames":
                    _moduleInstanceRepository.UpdateActionDelay(connection, moduleInstanceId,
                        Math.Max(0, NumericText.Int32(value, 0)), transaction);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown module instance field '{fieldId}'.");
            }
            return true;
        });
    }

    public void UpdateModuleInstanceDeviceOverrides(
        string moduleInstanceId,
        string overridesJson)
    {
        using var connection = OpenConnection();
        CommitModuleInstanceWrite(connection, moduleInstanceId, (transaction, instance) =>
        {
            _moduleInstanceRepository.UpdateDeviceOverrides(connection, moduleInstanceId, overridesJson, transaction);
            return true;
        });
    }

    internal RecordCreationDefinition PrepareModuleInstanceCreation(
        SqliteConnection connection,
        ProjectTreeNode shot,
        ShotModuleInstanceDraft draft,
        IReadOnlyList<FieldOption> actorOptions)
    {
        RequireModuleInstanceSelection(shot, draft);
        var metadata = ModuleInstanceMetadata(draft);
        var contract = ResolveModuleInstanceContract(
            connection,
            draft.Module.Id,
            metadata.ToJsonString());
        var content = RuntimeInputDocumentContract.CreateContentForContract(
            new JsonObject(),
            contract);
        var moduleSettings = _moduleVariantCatalog.GetModuleSettings(
            draft.Module.Id);
        var config = ParseJsonObject(moduleSettings.ConfigJson);
        var runtime = RuntimePreviewDocumentContract.PrepareRuntime(
            ParseJsonObject(moduleSettings.DesignPreviewJson),
            config,
            content,
            reference => _componentVariantConfigCatalog.GetComponentVariantConfig(connection, reference),
            reference => _componentVariantConfigCatalog.GetComponentVariantRuntimeContract(connection, reference));
        return ProductionRuntimeCreationContract.Prepare(
            ModuleInstanceCreationDefinitionId(draft),
            content,
            runtime,
            config,
            actorOptions);
    }

    internal ProjectTreeNode AddModuleInstance(
        SqliteConnection connection,
        ProjectTreeNode shot,
        ShotModuleInstanceCreationDraft creation,
        IReadOnlyList<FieldOption> actorOptions)
    {
        var createdId = CommitScreenWrite(connection, transaction =>
        {
            var draft = creation.Selection;
            RequireModuleInstanceSelection(shot, draft);
            var module = draft.Module;
            var requestedName = draft.Name.Trim();
            var moduleSettings =
                _moduleVariantCatalog.GetModuleSettings(module.Id);
            var initialDuration =
                RuntimeDurationContract.InitialDurationFrames(
                    moduleSettings.DesignPreviewJson);
            var initialDurationPolicy = RuntimeDurationContract.FormatPolicy(
                RuntimeDurationContract.Policy(
                    moduleSettings.DesignPreviewJson));
            var initialThemeId = _moduleInstanceThemeContextService.GetInitialThemeId(
                connection,
                shot.Id);
            var metadata = ModuleInstanceMetadata(draft);
            var contract = ResolveModuleInstanceContract(
                connection,
                module.Id,
                metadata.ToJsonString());
            var initialContent =
                RuntimeInputDocumentContract.CreateContentForContract(
                    new JsonObject(),
                    contract);
            var moduleConfig = ParseJsonObject(moduleSettings.ConfigJson);
            var initialRuntime = RuntimePreviewDocumentContract.PrepareRuntime(
                ParseJsonObject(moduleSettings.DesignPreviewJson),
                moduleConfig,
                initialContent,
                reference => _componentVariantConfigCatalog.GetComponentVariantConfig(connection, reference),
                reference => _componentVariantConfigCatalog.GetComponentVariantRuntimeContract(connection, reference));
            var content = ProductionRuntimeCreationContract.Complete(
                ModuleInstanceCreationDefinitionId(draft),
                initialContent,
                initialRuntime,
                moduleConfig,
                actorOptions,
                creation.RuntimeValues);
            var index = _moduleInstanceRepository.NextSortOrder(
                connection,
                shot.Id);
            var shotSettings = _shotRepository.Get(connection, shot.Id);
            var transitionFrames =
                ScreenTimelineTiming.EffectiveTransitionDurationFrames(
                    shotSettings.TransitionJson,
                    shotSettings.TransitionDurationFrames);
            var startFrame = _moduleInstanceRepository.QueryByShot(connection, shot.Id)
                .Select((screen) => screen.StartFrame
                    + EffectiveScreenDurationFrames(
                        screen,
                        shotSettings)
                    - transitionFrames)
                .DefaultIfEmpty(-transitionFrames)
                .Max();
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
                    startFrame,
                    initialDuration,
                    initialDurationPolicy,
                    0,
                    "{}",
                    initialThemeId,
                    content.ToJsonString(),
                    "{}",
                    DefaultModuleAnimationJson(),
                    metadata.ToJsonString()),
                transaction);
            return (id, (IReadOnlyList<string>)[shot.Id]);
        });
        var created = _moduleInstanceRepository.Get(connection, createdId);
        return new ProjectTreeNode(
            ProjectTreeNodeKind.ModuleInstance, created.Id, created.Name,
            $"{creation.Selection.Module.Name} · {created.DurationFrames} frames · None",
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ModuleInstance), shot);
    }

    private void RequireModuleInstanceSelection(
        ProjectTreeNode shot,
        ShotModuleInstanceDraft draft)
    {
        if (shot.Kind != ProjectTreeNodeKind.Shot)
        {
            throw new InvalidOperationException(
                "A module instance can only be added to a Shot.");
        }
        if (!VariantReferenceId.TryParse(
                draft.VariantReference,
                out var variantModuleId,
                out var variantId)
            || !variantModuleId.Equals(draft.Module.Id, StringComparison.Ordinal)
            || _moduleVariantCatalog.GetModuleVariants(draft.Module.Id)
                .All((variant) => variant.Id != variantId))
        {
            throw new InvalidOperationException(
                "The selected Variant does not belong to the selected Module.");
        }
        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            throw new InvalidOperationException(
                "A Module Instance name is required.");
        }
    }

    private static JsonObject ModuleInstanceMetadata(ShotModuleInstanceDraft draft) =>
        new()
        {
            ["moduleVariantReference"] = draft.VariantReference,
        };

    private static string ModuleInstanceCreationDefinitionId(ShotModuleInstanceDraft draft) =>
        $"screen-runtime:{draft.Module.Id}:{draft.VariantReference}";

    internal JsonObject ValidateModuleInstanceRuntimeContent(
        SqliteConnection connection,
        string moduleInstanceId,
        JsonObject content,
        IReadOnlySet<string> projectActorIds)
    {
        var instance = _moduleInstanceRepository.Get(
            connection,
            moduleInstanceId);
        var module = GetModuleInstanceVariantSettings(connection, moduleInstanceId);
        var contract = ResolveModuleInstanceContract(
            connection,
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
        var config = ParseJsonObject(module.ConfigJson);
        var effectiveRuntime = PrepareModuleInstanceRuntimeDocument(connection, moduleInstanceId, content);
        ProductionRuntimeFixtureIsolationContract.Validate(
            effectiveRuntime,
            config,
            $"Module Instance '{moduleInstanceId}'");
        return effectiveRuntime;
    }

    internal void ValidateModuleInstanceDocuments(
        SqliteConnection connection,
        string moduleInstanceId,
        JsonObject content,
        JsonObject animation,
        IReadOnlySet<string> projectActorIds)
    {
        var runtime = ValidateModuleInstanceRuntimeContent(
            connection, moduleInstanceId, content, projectActorIds);
        RuntimeInputAnimationValueContract.Validate(
            runtime,
            animation,
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["actors"] = projectActorIds,
            },
            $"Module Instance '{moduleInstanceId}' animation_json");
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

    private RuntimeInputCollectionDefinition
        RequireDeclaredRuntimeCollectionDefinition(
            SqliteConnection connection,
            string moduleInstanceId,
            string collectionJsonKey,
            JsonObject content)
    {
        _ = RequireDeclaredRuntimeCollection(
            connection,
            moduleInstanceId,
            collectionJsonKey,
            content);
        var contract = ModuleInstanceRuntimeContract(
            connection,
            moduleInstanceId);
        var matches = RuntimeInputDefinitionReader.ReadCollections(
                contract,
                new JsonObject(),
                includeHidden: true)
            .Where((collection) =>
                collection.StorageJsonKey.Equals(
                    collectionJsonKey,
                    StringComparison.Ordinal))
            .ToList();
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Module Instance '{moduleInstanceId}' has no unique structured collection definition '{collectionJsonKey}'.");
        }
        return matches[0];
    }

    private JsonObject ModuleInstanceRuntimeContract(
        SqliteConnection connection,
        string moduleInstanceId)
    {
        var instance = _moduleInstanceRepository.Get(
            connection,
            moduleInstanceId);
        return ResolveModuleInstanceContract(
            connection,
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

    private static string DefaultModuleAnimationJson() =>
        new JsonObject
        {
            ["schemaVersion"] = 2,
            ["tracks"] = new JsonArray(),
        }.ToJsonString();
}
