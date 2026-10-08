using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record DesignPreviewInputCapture(
    ComponentPreviewTransientState Transient,
    string InputSignature,
    IReadOnlyDictionary<string, string> ActionSignatures);

internal sealed record PreparedRuntimeValueEdit(
    string JsonKey, string Value, IReadOnlyDictionary<string, string> Collections);

internal sealed record PreparedDesignPreviewInputs(
    DesignPreviewPayload Payload,
    string ScopeKey,
    string InputSignature,
    string RuntimeJson,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<ComponentPreviewActionDefinition> Actions,
    IReadOnlyDictionary<string, string> ActionSignatures,
    bool ResetSession,
    IReadOnlyList<string> ResetActionIds);

/// <summary>Per-request preparation. Its persistence capabilities never enter the visual session.</summary>
internal sealed class DesignPreviewInputPreparer(
    IComponentPreviewInputRepository componentPreview,
    IDictionaryFieldContextRepository dictionary,
    IActorPreviewRepository actors,
    IProjectPathResolver projectPaths)
{
    public PreparedDesignPreviewInputs Prepare(
        DesignPreviewPayload payload, DesignPreviewInputCapture capture, string themeMode, string projectId)
    {
        var previewInputs = new ComponentPreviewInputDataSource(componentPreview, actors);
        var frameRate = previewInputs.ProjectDefaultFrameRate(projectId);
        payload = DesignPreviewPayloadLayers.MapPrimaryOwner(payload, current => current with { FrameRate = frameRate });
        var owner = DesignPreviewPayloadLayers.PrimaryOwner(payload);
        var scope = ComponentPreviewTransientValues.ScopeKey(owner);
        if (capture.Transient.ScopeKey != scope)
            throw new InvalidOperationException("Design Preview preparation requires the exact captured owner.");
        var work = new Preparation(
            previewInputs,
            new RuntimeInputOptionsDataSource(dictionary, actors),
            new ComponentPreviewRecordInputResolver(new ActorPreviewDataSource(actors), projectPaths),
            new NestedRuntimeRecordReferenceResolver(new ActorPreviewDataSource(actors), projectPaths),
            capture, owner.Kind is "componentClass" or "module");
        var resolved = DesignPreviewPayloadLayers.MapPrimaryOwner(
            payload, current => work.ApplyOwnerInputs(current, themeMode, projectId));
        return work.Result(resolved);
    }

    public JsonObject ApplyTransient(JsonObject preview, JsonObject config, ComponentPreviewTransientState state) =>
        ComponentPreviewTransientValues.Apply(preview, config, state,
            componentPreview.GetComponentVariantConfig,
            componentPreview.GetComponentVariantRuntimeContract);

    public JsonArray UpdateCollection(
        DesignPreviewPayload payload, ComponentPreviewTransientState state,
        StructuredCollectionAddress address, string itemId, IReadOnlyDictionary<string, JsonNode?> values)
    {
        var (content, collection) = PrepareCollection(payload, state, address);
        var updated = StructuredCollectionMutationEngine.UpdateValues(
            content, collection, address, itemId, values,
            componentPreview.GetComponentVariantConfig,
            componentPreview.GetComponentVariantRuntimeContract);
        return updated[collection.StorageJsonKey]!.DeepClone().AsArray();
    }

    public PreparedRuntimeValueEdit UpdateValue(
        DesignPreviewPayload payload, ComponentPreviewTransientState state, string jsonKey, string value)
    {
        payload = DesignPreviewPayloadLayers.PrimaryOwner(payload);
        var config = ParseJsonObject(payload.ConfigJson);
        var preview = ApplyTransient(ParseJsonObject(payload.RuntimeContractJson), config, state);
        var input = RuntimeInputDefinitionReader.ReadInputs(preview, config)
            .Single(candidate => candidate.Source == ComponentInputSource.Runtime && candidate.JsonKey == jsonKey);
        var frameRate = payload.Kind == "moduleInstance" ? payload.FrameRate
            : new ComponentPreviewInputDataSource(componentPreview, actors).ProjectDefaultFrameRate(payload.ProjectId);
        var updated = RuntimeInputDocumentContract.UpdateValue(preview, preview,
            new JsonObject { ["schemaVersion"] = 2, ["tracks"] = new JsonArray() },
            jsonKey, DesignPreviewTestValues.ValueNode(input, value), ParseJsonObject(payload.ThemeTokensJson), frameRate);
        var collections = RuntimeInputDefinitionReader.ReadCollections(preview, config, includeHidden: true)
            .Where(collection => !JsonNode.DeepEquals(preview[collection.StorageJsonKey], updated[collection.StorageJsonKey]))
            .ToFrozenDictionary(collection => collection.StorageJsonKey,
                collection => StructuredCollectionDocumentContract.StoredClone(
                    updated[collection.StorageJsonKey]!.AsArray(), collection, "Runtime value mutation").ToJsonString(),
                StringComparer.Ordinal);
        return new(jsonKey, value, collections);
    }

    public StructuredCollectionMutationResult MutateCollection(
        DesignPreviewPayload payload, ComponentPreviewTransientState state,
        StructuredCollectionMutation mutation)
    {
        var (content, collection) = PrepareCollection(payload, state, mutation.Address);
        return StructuredCollectionMutationEngine.Apply(content,
            new JsonObject { ["schemaVersion"] = 2, ["tracks"] = new JsonArray() },
            collection, mutation);
    }

    private (JsonObject Content, RuntimeInputCollectionDefinition Definition) PrepareCollection(
        DesignPreviewPayload payload, ComponentPreviewTransientState state,
        StructuredCollectionAddress address)
    {
        var config = ParseJsonObject(payload.ConfigJson);
        var preview = ApplyTransient(ParseJsonObject(payload.RuntimeContractJson), config, state);
        var collection = RuntimeInputDefinitionReader.ReadCollections(preview, config, includeHidden: true)
            .Single(candidate => candidate.StorageJsonKey == address.RootStorageJsonKey);
        var content = new JsonObject
        {
            [collection.StorageJsonKey] = StructuredCollectionDocumentContract.StoredClone(
                new JsonArray(DesignPreviewTestValues.CollectionItems(preview, collection)
                    .Select(item => (JsonNode?)item.DeepClone()).ToArray()),
                collection, "Design Test Values collection"),
        };
        return (content, collection);
    }

    private static JsonObject ParseJsonObject(string json) =>
        JsonPath.ParseRequiredObject(json, "Design Preview input preparation");

    private sealed class Preparation(
        ComponentPreviewInputDataSource _previewInputData,
        RuntimeInputOptionsDataSource _inputOptionsData,
        ComponentPreviewRecordInputResolver _recordInputResolver,
        NestedRuntimeRecordReferenceResolver _nestedRecordInputResolver,
        DesignPreviewInputCapture _capture,
        bool _allowSystemPreviewFixtures)
    {
        private readonly Dictionary<string, string> _values = new(_capture.Transient.Values, StringComparer.Ordinal);
        private string _scopeKey = _capture.Transient.ScopeKey;
        private IReadOnlyList<ComponentPreviewActionDefinition> _actions = [];
        private JsonObject _config = [];
        private JsonObject _runtimePreview = [];
        public string InputSignature { get; private set; } = "";
        public IReadOnlyDictionary<string, string> ActionSignatures { get; private set; } = FrozenDictionary<string, string>.Empty;
        public bool ResetSession { get; private set; }
        public List<string> ResetActionIds { get; } = [];

        public PreparedDesignPreviewInputs Result(DesignPreviewPayload payload) => new(
            payload, _scopeKey, InputSignature, _runtimePreview.ToJsonString(),
            _values.ToFrozenDictionary(StringComparer.Ordinal), Array.AsReadOnly(_actions.ToArray()),
            ActionSignatures, ResetSession, Array.AsReadOnly(ResetActionIds.ToArray()));

        public DesignPreviewPayload ApplyOwnerInputs(
            DesignPreviewPayload payload,
            string themeMode,
            string? projectId)
        {

            var config = ParseJsonObject(payload.ConfigJson);
            _config = config;
            var preview = ComponentPreviewTransientValues.Apply(
                ParseJsonObject(payload.RuntimeContractJson),
                config,
                _capture.Transient,
                _previewInputData.ComponentVariantConfig,
                _previewInputData.ComponentVariantRuntimeContract);
            _nestedRecordInputResolver.Resolve(
                config,
                themeMode,
                payload.PaletteColors,
                _allowSystemPreviewFixtures);
            _runtimePreview = preview;
            var inputs = RuntimeInputDefinitionReader.ReadInputs(preview, config);
            var collections = RuntimeInputDefinitionReader.ReadCollections(preview, config);
            _actions = ComponentPreviewActions.ReadWithEmbedded(
                preview,
                _previewInputData.ComponentVariantRuntimeContract);
            if (string.IsNullOrWhiteSpace(_scopeKey))
            {
                _scopeKey = ComponentPreviewTransientValues.ScopeKey(payload);
            }

            var signature = Signature(inputs, collections);
            if (_capture.InputSignature.Length > 0 && _capture.InputSignature != signature)
            {
                _values.Clear();
                ResetSession = true;
                preview = ComponentPreviewTransientValues.Apply(
                    ParseJsonObject(payload.RuntimeContractJson), config,
                    _capture.Transient with { Values = FrozenDictionary<string, string>.Empty },
                    _previewInputData.ComponentVariantConfig, _previewInputData.ComponentVariantRuntimeContract);
                _runtimePreview = preview;
            }
            InputSignature = signature;
            // Item membership and order are authoring values, not the Runtime
            // contract. Compare each complete action contract by its stable id.
            ActionSignatures = _actions.ToFrozenDictionary(action => action.Id,
                action => JsonSerializer.Serialize(action), StringComparer.Ordinal);
            foreach (var (id, previous) in _capture.ActionSignatures)
            {
                if (ActionSignatures.TryGetValue(id, out var current) && current == previous) continue;
                ResetActionIds.Add(id);
                foreach (var key in ComponentPreviewTransientValues.ActionKeys(_scopeKey, id)) _values.Remove(key);
            }
            foreach (var input in inputs)
            {
                EnsureValue(input, preview);
            }
            EnsureActionValues(preview);

            ValidateRecordReferenceValues(inputs);
            var effectiveProjectId = projectId;
            if (!string.IsNullOrWhiteSpace(effectiveProjectId))
            {
                EnsureComponentVariantReferenceValues(inputs, effectiveProjectId);
            }

            foreach (var input in inputs)
            {
                var value = Value(input);
                if (input.Kind == ComponentInputKind.RecordReference)
                {
                    ApplyRecordReferenceInput(preview, input, value, themeMode, payload.PaletteColors);
                    continue;
                }
                preview[input.JsonKey] = DesignPreviewTestValues.ValueNode(input, value);
            }
            foreach (var action in _actions.Where((action) => ComponentPreviewActions.IsApplicable(preview, action)))
            {
                if (action.IsCollectionItemAction
                    && !string.IsNullOrWhiteSpace(action.TargetInputId)
                    && _values.TryGetValue(ActionTargetStorageKey(action), out var targetValue))
                {
                    ComponentPreviewActions.SetStoredValue(preview, action, action.TargetInputId, targetValue);
                }
                ComponentPreviewActions.SetValue(preview, action, action.PlayInputId, IsPlaying(action));
                ComponentPreviewActions.SetValue(preview, action, action.TimeJsonKey, PlaybackTimeValue(action));
                if (!string.IsNullOrWhiteSpace(action.TargetFromJsonKey)
                    && _values.TryGetValue(ActionTargetFromKey(action), out var fromValue))
                {
                    ComponentPreviewActions.SetValue(preview, action, action.TargetFromJsonKey, fromValue);
                }
            }

            ReconcileRuntimeStructure(preview, config);
            foreach (var action in _actions.Where(action => ComponentPreviewActions.IsApplicable(preview, action)))
            {
                var time = ComponentPreviewActionRuntimeValue.NormalizedTime(
                    PlaybackTimeValue(action), action, preview,
                    PreviewPlaybackTiming.PreviewFrameRate(payload.FrameRate), payload.ThemeTokensJson);
                _values[ActionTimeKey(action)] = time.ToString(CultureInfo.InvariantCulture);
                ComponentPreviewActions.SetValue(preview, action, action.TimeJsonKey, time);
            }
            var runtimeContractJson = preview.ToJsonString();
            if (!string.IsNullOrWhiteSpace(effectiveProjectId))
            {
                ResolveCollectionRecordReferences(
                    preview,
                    config,
                    themeMode,
                    payload.PaletteColors);
            }
            _nestedRecordInputResolver.Resolve(
                preview,
                themeMode,
                payload.PaletteColors,
                _allowSystemPreviewFixtures);

            var preparedPreviewJson = preview.ToJsonString();
            var result = payload with
            {
                ConfigJson = config.ToJsonString(),
                DesignPreviewJson = preparedPreviewJson,
                RuntimeContractJson = runtimeContractJson,
                ProjectMediaFiles = PreviewMediaDirectoryCatalog.Resolve(
                    payload.ProjectMediaRoot,
                    preparedPreviewJson,
                    payload.SystemPreviewFixtureRoot),
            };
            var moduleFrameActions = _actions
                .Where((action) =>
                    action.DefinesModuleDuration
                    && ComponentPreviewActions.IsApplicable(preview, action))
                .ToList();
            if (moduleFrameActions.Count > 1)
            {
                throw new InvalidOperationException(
                    "Design Preview Runtime declares multiple actions that define the Module frame.");
            }
            return moduleFrameActions.Count == 0
                ? result
                : DesignPreviewPlaybackFrameProjection.Apply(
                    result,
                    moduleFrameActions[0],
                    PlaybackTimeValue(moduleFrameActions[0]));
        }

        private void ResolveCollectionRecordReferences(
            JsonObject preview,
            JsonObject config,
            string themeMode,
            IReadOnlyDictionary<string, string> paletteColors)
        {
            foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(preview, config))
            {
                foreach (var item in DesignPreviewTestValues.CurrentCollectionItems(preview, collection))
                {
                    ResolveRecordReferenceInputs(
                        item,
                        collection.Fields,
                        themeMode,
                        paletteColors,
                        _allowSystemPreviewFixtures);
                    if (collection.ComponentItems is not { } componentItems)
                    {
                        continue;
                    }
                    var variantReference = RuntimeComponentCollectionItemDocumentContract.RequireVariantReference(
                        item,
                        componentItems.DocumentKeys,
                        $"Design Preview collection '{collection.JsonKey}' item");
                    var componentInputs = RuntimeComponentCollectionItemDocumentContract.RequireInputs(
                        item,
                        componentItems.DocumentKeys,
                        $"Design Preview collection '{collection.JsonKey}' item");
                    if (variantReference.Length == 0) continue;

                    var componentConfig = _previewInputData.ComponentVariantConfig(variantReference);
                    ResolveRecordReferenceInputs(
                        componentInputs,
                        RuntimeInputDefinitionReader.ReadInputs(componentInputs, componentConfig),
                    themeMode,
                    paletteColors,
                    _allowSystemPreviewFixtures);
                }
            }
        }

        private void ResolveRecordReferenceInputs(
            JsonObject values,
            IReadOnlyList<ComponentInputDefinition> inputs,
            string themeMode,
            IReadOnlyDictionary<string, string> paletteColors,
            bool allowSystemPreviewFixtures)
        {
            _nestedRecordInputResolver.ResolveDeclaredValues(
                values,
                inputs,
                themeMode,
                paletteColors,
                allowSystemPreviewFixtures);
        }

        private void ReconcileRuntimeStructure(
            JsonObject preview,
            JsonObject config)
        {
            ComponentPreviewTransientValues.ReconcileRuntimeStructure(
                preview,
                config,
                _previewInputData.ComponentVariantConfig,
                _previewInputData.ComponentVariantRuntimeContract);
        }


        private void EnsureValue(ComponentInputDefinition input, JsonObject preview)
        {
            var key = StorageKey(input);
            if (_values.ContainsKey(key)) return;

            if (!preview.TryGetPropertyValue(input.JsonKey, out var stored))
            {
                _values[key] = input.DefaultValue;
                return;
            }
            if (stored is null)
            {
                if (input.AllowEmpty)
                {
                    _values[key] = "";
                    return;
                }
                throw new InvalidOperationException(
                    $"Design Preview Runtime value '{input.JsonKey}' cannot be null.");
            }
            _values[key] = RuntimeInputValueKindContract.CurrentStorageText(
                input.ValueKind,
                stored,
                $"Design Preview Runtime value '{input.JsonKey}'");
        }

        private void EnsureActionValues(JsonObject preview)
        {
            foreach (var action in _actions)
            {
                var stateKey = ActionStateKey(action);
                if (!_values.ContainsKey(stateKey))
                {
                    _values[stateKey] = ComponentPreviewActionRuntimeValue.BooleanOrDefault(
                        preview,
                        action,
                        action.PlayInputId,
                        absentValue: false)
                        ? "true"
                        : "false";
                }

                var timeKey = ActionTimeKey(action);
                if (!_values.ContainsKey(timeKey))
                {
                    _values[timeKey] = ComponentPreviewActionRuntimeValue.TimeOrDefault(
                            preview,
                            action,
                            absentValue: 0)
                        .ToString(CultureInfo.InvariantCulture);
                }
                if (action.IsCollectionItemAction
                    && !string.IsNullOrWhiteSpace(action.TargetInputId)
                    && !_values.ContainsKey(ActionTargetStorageKey(action)))
                {
                    _values[ActionTargetStorageKey(action)] = ComponentPreviewActions.Value(preview, action, action.TargetInputId) switch
                    {
                        JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var boolean) => boolean ? "true" : "false",
                        JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => text,
                        JsonValue jsonValue when jsonValue.TryGetValue<double>(out var number) => number.ToString(CultureInfo.InvariantCulture),
                        _ => "",
                    };
                }
            }
            NormalizeCollectionOptionActionTargets(preview);
        }

        private void NormalizeCollectionOptionActionTargets(JsonObject preview)
        {
            var collections = RuntimeInputDefinitionReader.ReadCollections(preview, _config)
                .ToDictionary((collection) => collection.JsonKey, StringComparer.Ordinal);
            foreach (var action in _actions.Where((candidate) =>
                         candidate.IsCollectionItemAction
                         && candidate.TargetMode == ComponentPreviewActionTargetMode.Option
                         && !string.IsNullOrWhiteSpace(candidate.TargetInputId)))
            {
                if (!collections.TryGetValue(action.CollectionJsonKey, out var collection)) continue;
                var input = collection.Fields.FirstOrDefault((field) =>
                    field.JsonKey.Equals(action.TargetInputId, StringComparison.Ordinal));
                if (input is null || string.IsNullOrWhiteSpace(input.OptionsSourceCollectionJsonKey)) continue;
                var item = DesignPreviewTestValues.CurrentCollectionItems(preview, collection)
                    .FirstOrDefault((candidate) =>
                        candidate["id"] is JsonValue value
                        && value.TryGetValue<string>(out var id)
                        && id.Equals(action.CollectionItemId, StringComparison.Ordinal));
                if (item is null)
                {
                    throw new InvalidOperationException(
                        $"Runtime action '{action.Id}' target item '{action.CollectionItemId}' does not exist.");
                }
                var validValues = (RuntimeInputDynamicOptions.ResolveForCollectionItem(
                        _inputOptionsData,
                        input,
                        preview,
                        collections,
                        collection,
                        item)
                        ?? throw new InvalidOperationException(
                            $"Runtime action '{action.Id}' has no declared option source."))
                    .Select((option) => option.Value)
                    .ToList();
                var targetKey = ActionTargetStorageKey(action);
                var current = _values.GetValueOrDefault(targetKey, "");
                if (validValues.Contains(current)) continue;

                ResetActionIds.Add(action.Id);
                var replacement = validValues.FirstOrDefault() ?? "";
                _values[targetKey] = replacement;
                _values[ActionTargetFromKey(action)] = replacement;
                _values[ActionStateKey(action)] = "false";
                _values[ActionTimeKey(action)] = "0";

            }
        }

        private void ValidateRecordReferenceValues(IReadOnlyList<ComponentInputDefinition> inputs)
        {
            foreach (var input in inputs.Where(input => input.Kind == ComponentInputKind.RecordReference))
            {
                if (!input.AllowEmpty && string.IsNullOrWhiteSpace(_values.GetValueOrDefault(StorageKey(input))))
                {
                    throw new InvalidOperationException(
                        $"Design Preview Runtime input '{input.Id}' requires an explicit record reference.");
                }
            }
        }

        private void EnsureComponentVariantReferenceValues(IReadOnlyList<ComponentInputDefinition> inputs, string projectId)
        {
            var variantInputs = inputs
                .Where((input) => input.Kind is ComponentInputKind.ComponentVariant or ComponentInputKind.ComponentVariantSlot)
                .ToList();
            if (variantInputs.Count == 0)
            {
                return;
            }

            foreach (var input in variantInputs)
            {
                var key = StorageKey(input);
                var storedValue = _values.GetValueOrDefault(key, input.DefaultValue);
                if (input.Kind == ComponentInputKind.ComponentVariantSlot)
                {
                    var owner = $"Design Preview Runtime value '{input.JsonKey}'";
                    var slot = ComponentVariantSlotDocumentContract.Parse(storedValue, owner);
                    var slotReference = ComponentVariantSlotDocumentContract.VariantReference(slot, owner);
                    slot["variantReference"] = _previewInputData.ValidateComponentVariantReference(
                        projectId,
                        input.ComponentType,
                        slotReference);
                    _values[key] = slot.ToJsonString();
                    continue;
                }

                var reference = storedValue;
                if (!string.IsNullOrWhiteSpace(reference))
                {
                    _values[key] = _previewInputData.ValidateComponentVariantReference(
                        projectId,
                        input.ComponentType,
                        reference);
                    continue;
                }

                if (!ComponentVariantOptionContract.SelectsComponentClass(input.ComponentType))
                {
                    _values[key] = ComponentVariantOptionContract.RequireFixedBoundary(
                        ComponentVariantOptions(input, projectId),
                        $"Design Preview Runtime Input '{input.Id}'").DefaultVariantReference;
                }
            }
        }

        private void ApplyRecordReferenceInput(
            JsonObject preview,
            ComponentInputDefinition input,
            string value,
            string themeMode,
            IReadOnlyDictionary<string, string> paletteColors)
        {
            preview[input.JsonKey] = value;
            if (string.IsNullOrWhiteSpace(input.ResolvedJsonKey))
            {
                return;
            }

            preview[input.ResolvedJsonKey] = _recordInputResolver.ResolvedPreviewValue(
                input.TableId,
                value,
                themeMode,
                paletteColors,
                input.Id,
                input.AllowEmpty,
                _allowSystemPreviewFixtures);
        }

        private IReadOnlyList<FieldOption> ComponentVariantOptions(ComponentInputDefinition input, string projectId)
        {
            return string.IsNullOrWhiteSpace(input.ComponentType)
                ? []
                : _inputOptionsData.ComponentVariantOptions(projectId, input.ComponentType, includeNone: false);
        }

        private string Value(ComponentInputDefinition input)
        {
            return _values.TryGetValue(StorageKey(input), out var value) ? value : input.DefaultValue;
        }

        private string StorageKey(ComponentInputDefinition input)
        {
            return $"{_scopeKey}:{input.JsonKey}";
        }


        private bool IsPlaying(ComponentPreviewActionDefinition action) =>
            BooleanText.ParseRequired(_values[ActionStateKey(action)], "Design action state");
        private double PlaybackTimeValue(ComponentPreviewActionDefinition action) =>
            ComponentPreviewActionRuntimeValue.RequireTime(_values[ActionTimeKey(action)], action);
        private string ActionStateKey(ComponentPreviewActionDefinition action) => ComponentPreviewTransientValues.ActionStateKey(_scopeKey, action.Id);
        private string ActionTimeKey(ComponentPreviewActionDefinition action) => ComponentPreviewTransientValues.ActionTimeKey(_scopeKey, action.Id);
        private string ActionTargetFromKey(ComponentPreviewActionDefinition action) => ComponentPreviewTransientValues.ActionTargetFromKey(_scopeKey, action.Id);
        private string ActionTargetStorageKey(ComponentPreviewActionDefinition action) =>
            action.IsCollectionItemAction ? ComponentPreviewTransientValues.ActionTargetValueKey(_scopeKey, action.Id) : $"{_scopeKey}:{action.TargetInputId}";

        private static string Signature(
            IReadOnlyList<ComponentInputDefinition> inputs,
            IReadOnlyList<RuntimeInputCollectionDefinition> collections) =>
            string.Join("|", inputs.Select(ScalarSignature)
                .Concat(collections.Select(CollectionSignature)));
        private static string ScalarSignature(ComponentInputDefinition input)
        {
            return string.Join(
                ":",
                input.Id,
                input.Label,
                input.JsonKey,
                input.Kind,
                input.ValueKind,
                input.DefaultValue,
                input.PairLabels?.First ?? "",
                input.PairLabels?.Second ?? "",
                input.Minimum.ToString(CultureInfo.InvariantCulture),
                input.Maximum.ToString(CultureInfo.InvariantCulture),
                input.Increment.ToString(CultureInfo.InvariantCulture),
                input.UseSlider,
                input.TableId,
                input.ResolvedJsonKey,
                input.ComponentType,
                input.Source,
                input.UiOrigin,
                input.UiGroupId,
                input.UiGroupLabel,
                input.UiParentGroupId,
                string.Join(",", input.Options?.Select((option) => $"{option.Value}={option.Label}") ?? []));
        }

        private static string CollectionSignature(RuntimeInputCollectionDefinition collection) =>
            string.Join(":", "collection", collection.Id, collection.JsonKey, collection.ItemLabel,
                string.Join("|", collection.Fields.Select(ScalarSignature)),
                collection.AnimationPresentation,
                collection.ComponentItems is null
                    ? ""
                    : string.Join("/", collection.ComponentItems.VariantReferenceJsonKey,
                        collection.ComponentItems.OverridesJsonKey,
                        collection.ComponentItems.InputsJsonKey));

    }
}
