using Avalonia.Threading;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ComponentPreviewInputSession
{
    public event Action<PlaybackRunInfo>? PlaybackStarted;
    public event Action<PlaybackRunInfo>? PlaybackStopped;
    public event Action<bool>? PlaybackBusyChanged;
    private readonly ComponentPreviewInputDataSource _previewInputData;
    private readonly RuntimeInputOptionsDataSource _inputOptionsData;
    private readonly ComponentPreviewRecordInputResolver _recordInputResolver;
    private readonly NestedRuntimeRecordReferenceResolver _nestedRecordInputResolver;
    private readonly Action _refreshPreview;
    private readonly Func<ComponentPreviewActionDefinition, Task<bool>>? _preparePlaybackFrames;
    private readonly DispatcherTimer _playbackTimer;
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _inputDefaults = new(StringComparer.Ordinal);
    private string _scopeKey = "";
    private string _projectId = "";
    private string _inputSignature = "";
    private string _testValuesSignature = "";
    private IReadOnlyList<ComponentPreviewActionDefinition> _actions = [];
    private string _activeActionId = "";
    private JsonObject _config = [];
    private JsonObject _themeTokens = [];
    private JsonObject _runtimePreview = [];
    private string _preparingActionId = "";
    private int _playbackFrameRate = 25;
    private long _playbackStartedTimestamp;
    private double _playbackStartedAtSeconds;
    private int _lastPlaybackRefreshFrame = -1;
    private readonly Dictionary<string, double> _playbackSecondsByActionId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, ActionValueSnapshot>> _actionSnapshots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonObject> _transientCollectionTestValuesByScope = new(StringComparer.Ordinal);
    private bool _presentEveryPlaybackFrame;
    private bool _awaitingPlaybackPresentation;
    private bool _stopAfterPlaybackPresentation;
    private string _heldFinalActionId = "";

    public bool PresentEveryPlaybackFrame
    {
        get => _presentEveryPlaybackFrame;
        set
        {
            if (_presentEveryPlaybackFrame == value) return;
            _presentEveryPlaybackFrame = value;
            UpdatePlaybackTimerInterval();
        }
    }

    public void NotifyPlaybackFramePresented()
    {
        if (!_presentEveryPlaybackFrame || !_awaitingPlaybackPresentation) return;
        _awaitingPlaybackPresentation = false;
        if (_stopAfterPlaybackPresentation)
        {
            _stopAfterPlaybackPresentation = false;
            var activeAction = ActiveAction();
            if (activeAction is not null)
            {
                CompletePlayback(activeAction);
            }
            _refreshPreview();
            return;
        }
        SyncPlaybackTimer();
    }

    public ComponentPreviewInputSession(
        IDictionaryFieldContextRepository database,
        IProjectPathResolver projectPaths,
        Action refreshPreview,
        Func<ComponentPreviewActionDefinition, Task<bool>>? preparePlaybackFrames = null)
    {
        _previewInputData = new ComponentPreviewInputDataSource(database);
        _inputOptionsData = new RuntimeInputOptionsDataSource(database);
        var actorDataSource = new ActorPreviewDataSource(database);
        _recordInputResolver = new ComponentPreviewRecordInputResolver(
            actorDataSource,
            projectPaths);
        _nestedRecordInputResolver = new NestedRuntimeRecordReferenceResolver(
            actorDataSource,
            projectPaths);
        _refreshPreview = refreshPreview;
        _preparePlaybackFrames = preparePlaybackFrames;
        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 50),
        };
        _playbackTimer.Tick += (_, _) => AdvancePlaybackFrame();
    }

    public void UpdateForPayload(DesignPreviewPayload? payload, string? projectId)
    {
        if (payload is null || !SupportsInputs(payload) || string.IsNullOrWhiteSpace(projectId))
        {
            _scopeKey = "";
            _projectId = "";
            _inputSignature = "";
            _testValuesSignature = "";
            _actions = [];
            _activeActionId = "";
            _heldFinalActionId = "";
            _config = [];
            _themeTokens = [];
            _runtimePreview = [];
            StopPlayback();
            return;
        }

        ApplyProjectFrameRate(projectId);
        var config = ParseJsonObject(payload.ConfigJson);
        _config = config;
        _themeTokens = ParseJsonObject(payload.ThemeTokensJson);
        var preview = ApplyTransientTestValues(
            ParseJsonObject(payload.DesignPreviewJson),
            ScopeKey(payload),
            config);
        _runtimePreview = preview;
        var inputs = RuntimeInputDefinitionReader.ReadInputs(preview, config);
        var collections = RuntimeInputDefinitionReader.ReadCollections(preview, config);
        _actions = ComponentPreviewActions.ReadWithEmbedded(
            preview,
            _previewInputData.ComponentVariantRuntimeContract);
        if (inputs.Count == 0 && collections.Count == 0)
        {
            _scopeKey = "";
            _projectId = "";
            _inputSignature = "";
            _actions = [];
            _activeActionId = "";
            _heldFinalActionId = "";
            _config = [];
            _runtimePreview = [];
            StopPlayback();
            return;
        }

        var scopeKey = ScopeKey(payload);
        var inputSignature = string.Join("|", inputs.Select(InputSignature)
            .Concat(collections.Select(CollectionSignature))
            .Concat(_actions.Select(ActionSignature)));
        var testValuesSignature = preview["testValues"]?.ToJsonString() ?? "";
        if (_scopeKey.Equals(scopeKey, StringComparison.Ordinal)
            && _inputSignature.Length > 0
            && !_inputSignature.Equals(inputSignature, StringComparison.Ordinal))
        {
            ClearTransientContractValues(scopeKey);
            StopPlayback();
        }
        _scopeKey = scopeKey;
        _projectId = projectId;
        _inputSignature = inputSignature;
        _testValuesSignature = testValuesSignature;
        foreach (var input in inputs)
        {
            EnsureValue(input, preview);
        }
        EnsureActionValues(preview);
        EnsureRecordReferenceValues(inputs, projectId);
        SyncPlaybackTimer();
    }

    private void ClearTransientContractValues(string scopeKey)
    {
        var prefix = $"{scopeKey}:";
        foreach (var key in _values.Keys.Where((key) => key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _values.Remove(key);
            _inputDefaults.Remove(key);
        }
        foreach (var key in _actionSnapshots.Keys.Where((key) => key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _actionSnapshots.Remove(key);
        }
    }

    public bool IsPlaybackActive => SupportsPlayback()
        && ActiveAction() is { } activeAction
        && IsPlaying(activeAction)
        && _heldFinalActionId != activeAction.Id;

    public bool IsPreparingPlayback => !string.IsNullOrWhiteSpace(_preparingActionId);

    public int PlaybackFrameRate => _playbackFrameRate;

    public int CurrentPreviewFrame => ActiveAction() is { } action && SupportsPlayback()
        ? CurrentPlaybackFrame(action)
        : 0;

    public bool TriggerAction(string actionId, string? targetValue = null)
    {
        var action = _actions.FirstOrDefault((candidate) => candidate.Id == actionId);
        if (action is not null)
        {
            ResetCompletedActionForReplay(action);
            CaptureActionSnapshot(action);
            ApplyActionTarget(action, targetValue);
            TogglePlayback(action);
            return true;
        }

        PreviewDebugLog.Write(
            "preview.playback.action-missing",
            ("scope", _scopeKey),
            ("action", actionId),
            ("availableActions", string.Join(",", _actions.Select((candidate) => candidate.Id))));
        return false;
    }

    public bool CanRestoreAction(string actionId)
    {
        return _actions.Any((candidate) => candidate.Id == actionId)
            && _actionSnapshots.ContainsKey(ActionSnapshotKey(actionId));
    }

    public bool RestoreAction(string actionId)
    {
        var action = _actions.FirstOrDefault((candidate) => candidate.Id == actionId);
        if (action is null
            || !_actionSnapshots.Remove(ActionSnapshotKey(actionId), out var snapshot))
        {
            return false;
        }

        StopPlayback();
        ApplyActionSnapshot(snapshot);
        _playbackSecondsByActionId.Remove(action.Id);
        if (_heldFinalActionId == action.Id) _heldFinalActionId = "";
        if (_activeActionId == action.Id) _activeActionId = "";
        _refreshPreview();
        return true;
    }

    public void SetExternalInputValue(string jsonKey, string value)
    {
        if (string.IsNullOrWhiteSpace(_scopeKey) || string.IsNullOrWhiteSpace(jsonKey))
        {
            return;
        }

        _values[$"{_scopeKey}:{jsonKey}"] = value;
        _refreshPreview();
    }

    public void SetExternalCollectionItemValues(
        string collectionJsonKey,
        string itemId,
        IReadOnlyDictionary<string, JsonNode?> values)
    {
        if (string.IsNullOrWhiteSpace(_scopeKey)
            || string.IsNullOrWhiteSpace(collectionJsonKey)
            || string.IsNullOrWhiteSpace(itemId)
            || values.Count == 0)
        {
            return;
        }

        var item = ExternalCollectionItem(collectionJsonKey, itemId);
        foreach (var (itemJsonKey, value) in values)
        {
            if (string.IsNullOrWhiteSpace(itemJsonKey))
            {
                throw new InvalidOperationException(
                    "Transient collection item value key cannot be empty.");
            }
            item[itemJsonKey] = value?.DeepClone();
        }
        _refreshPreview();
    }

    private JsonObject ExternalCollectionItem(
        string collectionJsonKey,
        string itemId)
    {
        var testValues = _transientCollectionTestValuesByScope.GetValueOrDefault(_scopeKey);
        if (testValues is null)
        {
            testValues = new JsonObject();
            _transientCollectionTestValuesByScope[_scopeKey] = testValues;
        }
        if (!testValues.TryGetPropertyValue(collectionJsonKey, out var collectionNode))
        {
            var definition = RuntimeInputDefinitionReader.ReadCollections(_runtimePreview, _config)
                .FirstOrDefault((candidate) => candidate.JsonKey == collectionJsonKey);
            collectionNode = definition is null
                ? new JsonArray()
                : new JsonArray(DesignPreviewTestValues.CollectionItems(_runtimePreview, definition)
                    .Select((item) => (JsonNode?)item.DeepClone()).ToArray());
            testValues[collectionJsonKey] = collectionNode;
        }
        var items = collectionNode as JsonArray
            ?? throw new InvalidOperationException(
                $"Transient collection Test Values '{collectionJsonKey}' must be an array.");
        RuntimeCollectionDocumentContract.Validate(
            items,
            $"Transient collection Test Values '{collectionJsonKey}'");
        var item = items.Select((node) => node!.AsObject()).FirstOrDefault((candidate) =>
            candidate["id"] is JsonValue idValue
            && idValue.TryGetValue<string>(out var candidateId)
            && candidateId == itemId);
        if (item is null)
        {
            item = new JsonObject { ["id"] = itemId };
            items.Add(item);
        }
        return item;
    }

    public void SetExternalCollectionItems(
        DesignPreviewPayload payload,
        string collectionJsonKey,
        IReadOnlyList<JsonObject> items)
    {
        if (!SupportsInputs(payload) || string.IsNullOrWhiteSpace(collectionJsonKey)) return;
        var scopeKey = ScopeKey(payload);
        var testValues = _transientCollectionTestValuesByScope.GetValueOrDefault(scopeKey) ?? new JsonObject();
        _transientCollectionTestValuesByScope[scopeKey] = testValues;
        testValues[collectionJsonKey] = new JsonArray(items.Select((item) => (JsonNode?)item.DeepClone()).ToArray());
        _refreshPreview();
    }

    public JsonObject ApplyTransientTestValues(JsonObject preview)
    {
        return ApplyTransientTestValues(preview, _scopeKey, _config);
    }

    public JsonObject ApplyTransientTestValues(JsonObject preview, DesignPreviewPayload payload)
    {
        return ApplyTransientTestValues(
            preview,
            ScopeKey(payload),
            ParseJsonObject(payload.ConfigJson));
    }

    public bool ResetCurrentTestValues()
    {
        return ResetTestValues(_scopeKey);
    }

    public bool ResetTestValues(DesignPreviewPayload payload)
    {
        return ResetTestValues(ScopeKey(payload));
    }

    private bool ResetTestValues(string scopeKey)
    {
        if (string.IsNullOrWhiteSpace(scopeKey)) return false;

        StopPlayback();
        var prefix = $"{scopeKey}:";
        var removed = false;
        foreach (var key in _values.Keys.Where((key) => key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            removed |= _values.Remove(key);
        }
        removed |= _transientCollectionTestValuesByScope.Remove(scopeKey);
        _activeActionId = "";
        _heldFinalActionId = "";
        _playbackSecondsByActionId.Clear();
        foreach (var key in _actionSnapshots.Keys.Where((key) => key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _actionSnapshots.Remove(key);
            removed = true;
        }
        if (removed) _refreshPreview();
        return removed;
    }

    public DesignPreviewPayload ApplyInputs(DesignPreviewPayload payload, string themeMode, string? projectId)
    {
        if (!SupportsInputs(payload))
        {
            return payload;
        }

        var config = ParseJsonObject(payload.ConfigJson);
        var preview = ApplyTransientTestValues(
            ParseJsonObject(payload.DesignPreviewJson),
            ScopeKey(payload),
            config);
        _nestedRecordInputResolver.Resolve(config, themeMode, payload.PaletteColors);
        _runtimePreview = preview;
        var inputs = RuntimeInputDefinitionReader.ReadInputs(preview, config);
        var collections = RuntimeInputDefinitionReader.ReadCollections(preview, config);
        _actions = ComponentPreviewActions.ReadWithEmbedded(
            preview,
            _previewInputData.ComponentVariantRuntimeContract);
        if (inputs.Count == 0 && collections.Count == 0)
        {
            return payload;
        }

        if (string.IsNullOrWhiteSpace(_scopeKey))
        {
            _scopeKey = ScopeKey(payload);
        }

        foreach (var input in inputs)
        {
            EnsureValue(input, preview);
        }
        EnsureActionValues(preview);

        var effectiveProjectId = string.IsNullOrWhiteSpace(projectId) ? _projectId : projectId;
        if (!string.IsNullOrWhiteSpace(effectiveProjectId))
        {
            EnsureRecordReferenceValues(inputs, effectiveProjectId);
            EnsureComponentVariantReferenceValues(inputs, effectiveProjectId);
            ResolveCollectionRecordReferences(preview, config, themeMode, payload.PaletteColors);
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

        _nestedRecordInputResolver.Resolve(preview, themeMode, payload.PaletteColors);
        ReconcileRuntimeStructure(preview, config);

        return payload with
        {
            ConfigJson = config.ToJsonString(),
            DesignPreviewJson = preview.ToJsonString(),
            RuntimeContractJson = preview.ToJsonString(),
        };
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
                ResolveRecordReferenceInputs(item, collection.Fields, themeMode, paletteColors);
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
                    paletteColors);
            }
        }
    }

    private void ResolveRecordReferenceInputs(
        JsonObject values,
        IReadOnlyList<ComponentInputDefinition> inputs,
        string themeMode,
        IReadOnlyDictionary<string, string> paletteColors)
    {
        _nestedRecordInputResolver.ResolveDeclaredValues(
            values,
            inputs,
            themeMode,
            paletteColors);
    }

    private static string ScopeKey(DesignPreviewPayload payload)
    {
        var instanceId = ParseJsonObject(payload.InstanceJson)["context"]?["moduleInstanceId"]?.GetValue<string>() ?? "";
        var ownerIdentity = string.IsNullOrWhiteSpace(payload.OwnerId)
            ? $"{payload.ComponentType}:{payload.Name}"
            : payload.OwnerId;
        return $"{payload.Kind}:{ownerIdentity}:{instanceId}";
    }

    private JsonObject ApplyTransientTestValues(
        JsonObject preview,
        string scopeKey,
        JsonObject config)
    {
        var envelope = preview.DeepClone().AsObject();
        if (!string.IsNullOrWhiteSpace(scopeKey)
            && _transientCollectionTestValuesByScope.TryGetValue(scopeKey, out var collectionTestValues))
        {
            envelope["testValues"] = collectionTestValues.DeepClone();
        }

        var effective = ParseJsonObject(DesignPreviewTestValues.RuntimeJson(envelope.ToJsonString()));
        ReconcileRuntimeStructure(effective, config);
        if (string.IsNullOrWhiteSpace(scopeKey))
        {
            return effective;
        }

        foreach (var input in RuntimeInputDefinitionReader.ReadInputs(effective, config))
        {
            var key = $"{scopeKey}:{input.JsonKey}";
            if (_values.TryGetValue(key, out var value))
            {
                DesignPreviewTestValues.SetValue(effective, input, value);
            }
        }
        effective = ParseJsonObject(DesignPreviewTestValues.RuntimeJson(effective.ToJsonString()));
        ReconcileRuntimeStructure(effective, config);
        return effective;
    }

    private void ReconcileRuntimeStructure(
        JsonObject preview,
        JsonObject config)
    {
        StructuredRuntimeCollectionProjection.Apply(preview, config);
        foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(
                     preview,
                     config,
                     includeHidden: true))
        {
            foreach (var item in DesignPreviewTestValues.CurrentCollectionItems(
                         preview,
                         collection))
            {
                var runtimeKey = !string.IsNullOrWhiteSpace(
                    collection.ItemRuntimeContractJsonKey)
                    ? collection.ItemRuntimeContractJsonKey
                    : collection.ComponentItems?.InputsJsonKey ?? "";
                if (string.IsNullOrWhiteSpace(runtimeKey)
                    || item[runtimeKey] is not JsonObject childRuntime)
                {
                    continue;
                }

                var variantReference = RuntimeCollectionItemContractOwner
                    .ResolveItemVariantReference(
                    item,
                    collection,
                    config,
                    _previewInputData.ComponentVariantConfig);
                if (string.IsNullOrWhiteSpace(variantReference)) continue;
                var childConfig = _previewInputData.ComponentVariantConfig(
                    variantReference);
                StructuredRuntimeCollectionProjection.Apply(
                    childRuntime,
                    childConfig);
            }
        }
    }

    private static bool SupportsInputs(DesignPreviewPayload payload)
    {
        return payload.Kind is "componentClass" or "module" or "moduleInstance";
    }

    private void EnsureValue(ComponentInputDefinition input, JsonObject preview)
    {
        var key = StorageKey(input);
        _inputDefaults[key] = input.DefaultValue;
        if (_values.ContainsKey(key)) return;

        if (!preview.TryGetPropertyValue(input.JsonKey, out var stored))
        {
            _values[key] = input.DefaultValue;
            return;
        }
        if (stored is null)
        {
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
            var validValues = (RuntimeInputDynamicOptions.Resolve(_inputOptionsData, input, item)
                    ?? throw new InvalidOperationException(
                        $"Runtime action '{action.Id}' has no declared option source."))
                .Select((option) => option.Value)
                .ToList();
            var targetKey = ActionTargetStorageKey(action);
            var current = _values.GetValueOrDefault(targetKey, "");
            if (validValues.Contains(current)) continue;

            StopPlayback();
            var replacement = validValues.FirstOrDefault() ?? "";
            _values[targetKey] = replacement;
            _values[ActionTargetFromKey(action)] = replacement;
            _values[ActionStateKey(action)] = "false";
            _values[ActionTimeKey(action)] = "0";
            _actionSnapshots.Remove(ActionSnapshotKey(action.Id));
            _playbackSecondsByActionId.Remove(action.Id);
            if (_activeActionId == action.Id) _activeActionId = "";
            if (_heldFinalActionId == action.Id) _heldFinalActionId = "";
        }
    }

    private void EnsureRecordReferenceValues(IReadOnlyList<ComponentInputDefinition> inputs, string projectId)
    {
        var recordInputs = inputs
            .Where((input) => input.Kind == ComponentInputKind.RecordReference)
            .ToList();
        if (recordInputs.Count == 0)
        {
            return;
        }

        foreach (var input in recordInputs)
        {
            if (input.AllowEmpty) continue;
            var key = StorageKey(input);
            if (!string.IsNullOrWhiteSpace(_values.GetValueOrDefault(key)))
            {
                continue;
            }

            var firstRecord = RecordReferenceOptions(input, projectId)
                .FirstOrDefault((option) => !string.IsNullOrWhiteSpace(option.Value));
            if (firstRecord is not null)
            {
                _values[key] = firstRecord.Value;
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
            input.AllowEmpty);
    }

    private IReadOnlyList<FieldOption> RecordReferenceOptions(ComponentInputDefinition input, string projectId)
    {
        return _recordInputResolver.Options(projectId, input.TableId, input.Id);
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

    private void SyncPlaybackTimer()
    {
        if (!SupportsPlayback())
        {
            StopPlayback();
            return;
        }

        var activeAction = ActiveAction();
        if (activeAction is not null && IsPlaying(activeAction))
        {
            if (_heldFinalActionId == activeAction.Id)
            {
                if (_playbackTimer.IsEnabled) _playbackTimer.Stop();
                return;
            }
            if (_awaitingPlaybackPresentation)
            {
                if (_playbackTimer.IsEnabled) _playbackTimer.Stop();
                return;
            }
            if (!_playbackTimer.IsEnabled)
            {
                _playbackTimer.Start();
            }
            return;
        }

        StopPlayback();
    }

    private void ApplyProjectFrameRate(string projectId)
    {
        var projectFps = _previewInputData.ProjectDefaultFrameRate(projectId);
        var previousFps = _playbackFrameRate;
        var previousInterval = _playbackTimer.Interval;
        var previewFps = PreviewPlaybackTiming.PreviewFrameRate(projectFps);
        _playbackFrameRate = previewFps;
        var interval = PlaybackTimerInterval(previewFps);
        if (previousFps != previewFps || previousInterval != interval)
        {
            PreviewDebugLog.Write(
                "preview.playback.fps",
                ("projectId", projectId),
                ("projectFps", projectFps),
                ("previewFps", previewFps),
                ("multiplier", PreviewPlaybackTiming.FrameRateMultiplier),
                ("frameIntervalMs", 1000.0 / previewFps),
                ("schedulerIntervalMs", interval.TotalMilliseconds));
        }
        if (_playbackTimer.Interval != interval)
        {
            _playbackTimer.Interval = interval;
        }
    }

    private TimeSpan PlaybackTimerInterval(int previewFps)
    {
        return TimeSpan.FromMilliseconds(1000.0 / (previewFps * 2.0));
    }

    private void UpdatePlaybackTimerInterval()
    {
        var interval = PlaybackTimerInterval(Math.Max(1, _playbackFrameRate));
        if (_playbackTimer.Interval != interval) _playbackTimer.Interval = interval;
    }

    private void TogglePlayback(ComponentPreviewActionDefinition action)
    {
        var startsPlayback = !IsPlaying(action) || _heldFinalActionId == action.Id;
        PreviewDebugLog.Write(
            "preview.playback.toggle",
            ("scope", _scopeKey),
            ("action", action.Id),
            ("label", action.Label),
            ("startsPlayback", startsPlayback),
            ("fps", _playbackFrameRate),
            ("durationSec", DurationSeconds(action)),
            ("durationFrames", DurationFrames(action)),
            ("timeUnit", action.TimeUnit));
        if (startsPlayback)
        {
            _ = StartPlaybackAsync(action);
            return;
        }

        SetPlaybackState(action, false);
        SyncPlaybackTimer();
        _refreshPreview();
    }

    private async Task StartPlaybackAsync(ComponentPreviewActionDefinition action)
    {
        StopPlayback();
        PlaybackBusyChanged?.Invoke(true);
        _activeActionId = action.Id;
        var prepared = true;
        if (_preparePlaybackFrames is not null)
        {
            _preparingActionId = action.Id;
            var stopwatch = Stopwatch.StartNew();
            PreviewDebugLog.Write(
                "preview.playback.prepare.start",
                ("scope", _scopeKey),
                ("action", action.Id),
                ("fps", _playbackFrameRate),
                ("durationSec", DurationSeconds(action)),
                ("durationFrames", DurationFrames(action)),
                ("timeUnit", action.TimeUnit),
                ("timeKey", action.TimeJsonKey));
            try
            {
                if (_preparePlaybackFrames is not null && !await _preparePlaybackFrames(action))
                {
                    prepared = false;
                }
            }
            finally
            {
                _preparingActionId = "";
                PreviewDebugLog.Write(
                    "preview.playback.prepare.end",
                    ("scope", _scopeKey),
                    ("action", action.Id),
                    ("ms", stopwatch.Elapsed.TotalMilliseconds));
            }

            if (!prepared)
            {
                SetPlaybackState(action, false);
                PlaybackBusyChanged?.Invoke(false);
                return;
            }
        }
        else
        {
            _preparingActionId = "";
            PreviewDebugLog.Write(
                "preview.playback.prepare.skip",
                ("scope", _scopeKey),
                ("action", action.Id),
                ("reason", "prepare-handler-unavailable"));
        }

        if (!SupportsPlayback())
        {
            PlaybackBusyChanged?.Invoke(false);
            return;
        }

        SetPlaybackState(action, true);
        _heldFinalActionId = "";
        SyncDeactivatedPlaybackInputs(action);
        _values[ActionTimeKey(action)] = "0";
        _playbackStartedAtSeconds = 0;
        _playbackStartedTimestamp = Stopwatch.GetTimestamp();
        _lastPlaybackRefreshFrame = 0;
        _awaitingPlaybackPresentation = _presentEveryPlaybackFrame;
        PreviewDebugLog.Write(
            "preview.playback.start",
            ("scope", _scopeKey),
            ("action", action.Id),
            ("fps", _playbackFrameRate),
            ("durationSec", DurationSeconds(action)),
            ("durationFrames", DurationFrames(action)),
            ("timeUnit", action.TimeUnit));
        PlaybackStarted?.Invoke(new PlaybackRunInfo(DurationFrames(action) + 1, _playbackFrameRate));
        SyncPlaybackTimer();
        _refreshPreview();
    }

    public bool StopActivePlayback()
    {
        var wasPreparing = IsPreparingPlayback;
        var wasPlaying = IsPlaybackActive;
        if (!wasPreparing && !wasPlaying)
        {
            return false;
        }

        StopPlayback(clearPlayingState: true);
        if (wasPreparing && !wasPlaying)
        {
            PlaybackBusyChanged?.Invoke(false);
        }
        _refreshPreview();
        return true;
    }

    private void StopPlayback(bool clearPlayingState = false)
    {
        var wasEnabled = _playbackTimer.IsEnabled;
        if (wasEnabled)
        {
            _playbackTimer.Stop();
        }
        var hasPlayback = SupportsPlayback();
        var activeAction = ActiveAction();
        var wasPlaying = hasPlayback && activeAction is not null && IsPlaying(activeAction);
        if (clearPlayingState && wasPlaying && activeAction is not null)
        {
            SetPlaybackState(activeAction, false);
        }
        if (wasEnabled || wasPlaying)
        {
            PreviewDebugLog.Write(
                "preview.playback.stop",
                ("scope", _scopeKey),
                ("action", activeAction?.Id ?? ""),
                ("timeSec", hasPlayback && activeAction is not null ? CurrentPlaybackSeconds(activeAction) : 0),
                ("durationSec", hasPlayback && activeAction is not null ? DurationSeconds(activeAction) : 0),
                ("frame", hasPlayback && activeAction is not null ? CurrentPlaybackFrame(activeAction) : 0));
            if (activeAction is not null)
            {
                PlaybackStopped?.Invoke(new PlaybackRunInfo(DurationFrames(activeAction) + 1, _playbackFrameRate));
            }
            PlaybackBusyChanged?.Invoke(false);
        }
        _playbackStartedTimestamp = 0;
        _playbackStartedAtSeconds = 0;
        _lastPlaybackRefreshFrame = -1;
        _awaitingPlaybackPresentation = false;
        _stopAfterPlaybackPresentation = false;
        if (activeAction is not null && !IsPlaying(activeAction))
        {
            _playbackSecondsByActionId.Remove(activeAction.Id);
        }
    }

    public sealed record PlaybackRunInfo(int TargetFrames, int TargetFps);

    private void AdvancePlaybackFrame()
    {
        var activeAction = ActiveAction();
        if (!SupportsPlayback()
            || activeAction is null
            || !IsPlaying(activeAction))
        {
            StopPlayback();
            return;
        }

        if (_playbackStartedTimestamp == 0)
        {
            _playbackStartedAtSeconds = CurrentPlaybackSeconds(activeAction);
            _playbackStartedTimestamp = Stopwatch.GetTimestamp();
        }
        var elapsed = Stopwatch.GetElapsedTime(_playbackStartedTimestamp).TotalSeconds;
        var current = _presentEveryPlaybackFrame
            ? NextPlaybackFrameSeconds(activeAction)
            : NormalizedPlaybackSeconds(activeAction, _playbackStartedAtSeconds + elapsed);
        _playbackSecondsByActionId[activeAction.Id] = current;
        _values[ActionTimeKey(activeAction)] = PlaybackTimeStorageValue(activeAction, current);
        var currentFrame = CurrentPlaybackFrame(activeAction);
        var completesPlayback = current >= DurationSeconds(activeAction);
        if (_presentEveryPlaybackFrame && completesPlayback)
        {
            _stopAfterPlaybackPresentation = true;
        }
        PreviewDebugLog.Write(
            "preview.playback.tick",
            ("scope", _scopeKey),
            ("action", activeAction.Id),
            ("timeSec", current),
            ("frame", currentFrame),
            ("durationSec", DurationSeconds(activeAction)),
            ("durationFrames", DurationFrames(activeAction)),
            ("fps", _playbackFrameRate));
        if (currentFrame != _lastPlaybackRefreshFrame)
        {
            _lastPlaybackRefreshFrame = currentFrame;
            if (_presentEveryPlaybackFrame)
            {
                _awaitingPlaybackPresentation = true;
                _playbackTimer.Stop();
            }
            _refreshPreview();
        }

        if (completesPlayback)
        {
            if (_presentEveryPlaybackFrame)
            {
                return;
            }
            CompletePlayback(activeAction);
            _refreshPreview();
        }
    }

    private void CompletePlayback(ComponentPreviewActionDefinition action)
    {
        if (action.CompletionBehavior == ComponentPreviewActionCompletionBehavior.HoldFinal)
        {
            _heldFinalActionId = action.Id;
            StopPlayback();
            PlaybackBusyChanged?.Invoke(false);
            return;
        }

        _heldFinalActionId = "";
        _values[ActionStateKey(action)] = "false";
        StopPlayback();
        PlaybackBusyChanged?.Invoke(false);
    }

    private double CurrentPlaybackSeconds(ComponentPreviewActionDefinition action)
    {
        if (IsPlaying(action) && _playbackSecondsByActionId.TryGetValue(action.Id, out var seconds))
        {
            return NormalizedPlaybackSeconds(action, seconds);
        }

        var stored = ComponentPreviewActionRuntimeValue.RequireTime(
            _values.GetValueOrDefault(ActionTimeKey(action), "0"),
            action);
        return NormalizedPlaybackSeconds(
            action,
            action.TimeUnit == ComponentPreviewActionTimeUnit.Frames
                ? stored / Math.Max(1, _playbackFrameRate)
                : action.TimeUnit == ComponentPreviewActionTimeUnit.Milliseconds
                    ? stored / 1000.0
                : stored);
    }

    private double NextPlaybackFrameSeconds(ComponentPreviewActionDefinition action)
    {
        return NormalizedPlaybackSeconds(action, CurrentPlaybackSeconds(action) + 1.0 / Math.Max(1, _playbackFrameRate));
    }

    private double DurationSeconds(ComponentPreviewActionDefinition action)
    {
        if (!string.IsNullOrWhiteSpace(action.DurationStateCollectionJsonKey))
        {
            return ComponentPreviewActions.MotionStateTransitionDurationMilliseconds(
                _runtimePreview,
                action,
                _themeTokens.ToJsonString()) / 1000.0;
        }
        if (!string.IsNullOrWhiteSpace(action.DurationThemeToken))
        {
            var value = ThemeNumericTokenValue.RequirePositive(
                _themeTokens,
                action.DurationThemeToken,
                $"Design Preview action '{action.Id}' duration");
            return action.TimeUnit switch
            {
                ComponentPreviewActionTimeUnit.Milliseconds => value / 1000.0,
                ComponentPreviewActionTimeUnit.Frames => value / Math.Max(1, _playbackFrameRate),
                _ => value,
            };
        }
        if (action.TimeUnit == ComponentPreviewActionTimeUnit.Frames)
        {
            return DurationFrames(action) / (double)Math.Max(1, _playbackFrameRate);
        }

        if (action.DurationSeconds > 0)
        {
            return action.DurationSeconds;
        }

        return ActionDurationInputValue(action);
    }

    private int DurationFrames(ComponentPreviewActionDefinition action)
    {
        if (action.TimeUnit != ComponentPreviewActionTimeUnit.Frames)
        {
            return Math.Max(1, (int)Math.Ceiling(DurationSeconds(action) * Math.Max(1, _playbackFrameRate)));
        }

        if (!string.IsNullOrWhiteSpace(action.DurationThemeToken))
        {
            return Math.Max(1, (int)Math.Round(
                ThemeNumericTokenValue.RequirePositive(
                    _themeTokens,
                    action.DurationThemeToken,
                    $"Design Preview action '{action.Id}' duration"),
                MidpointRounding.AwayFromZero));
        }

        if (!string.IsNullOrWhiteSpace(action.DurationBehaviorTimingInputId))
        {
            var owner = ComponentPreviewActions.RequiredOwner(_runtimePreview, action);
            var fields = ComponentPreviewActionRuntimeValue.RequireInputDefinitions(
                _runtimePreview,
                action);
            var definition = fields.FirstOrDefault((field) =>
                field["id"]?.GetValue<string>() == action.DurationBehaviorTimingInputId)
                ?? throw new InvalidOperationException(
                    $"Missing BehaviorTiming action input '{action.DurationBehaviorTimingInputId}'.");
            return BehaviorTimingResolver.ResolveFrames(owner, definition, fields, _themeTokens);
        }

        if (!string.IsNullOrWhiteSpace(action.DurationCollectionJsonKey))
        {
            return ComponentPreviewActionRuntimeValue.CollectionDurationFrames(_runtimePreview, action);
        }

        if (action.DurationOwnerTimeline)
        {
            return RuntimeTimeline.DurationFrames(
                _runtimePreview.ToJsonString(),
                _runtimePreview.ToJsonString(),
                "{}",
                1,
                _themeTokens.ToJsonString());
        }

        return Math.Max(1, (int)Math.Round(ActionDurationInputValue(action), MidpointRounding.AwayFromZero));
    }

    private double PlaybackTimeValue(ComponentPreviewActionDefinition action)
    {
        return action.TimeUnit == ComponentPreviewActionTimeUnit.Frames
            ? CurrentPlaybackFrame(action)
            : action.TimeUnit == ComponentPreviewActionTimeUnit.Milliseconds
                ? NormalizedPlaybackSeconds(action, CurrentPlaybackSeconds(action)) * 1000
            : NormalizedPlaybackSeconds(action, CurrentPlaybackSeconds(action));
    }

    private int CurrentPlaybackFrame(ComponentPreviewActionDefinition action)
    {
        if (CurrentPlaybackSeconds(action) >= DurationSeconds(action))
        {
            return DurationFrames(action);
        }
        var frame = (int)Math.Floor(CurrentPlaybackSeconds(action) * Math.Max(1, _playbackFrameRate) + 0.0001);
        return Math.Max(0, Math.Min(DurationFrames(action), frame));
    }

    private double ActionDurationInputValue(ComponentPreviewActionDefinition action)
    {
        if (action.IsCollectionItemAction)
        {
            return ComponentPreviewActionRuntimeValue.RequireDurationInput(_runtimePreview, action);
        }

        var durationJsonKey = ComponentPreviewActions.DurationJsonKey(_runtimePreview, action);
        var inputKey = $"{_scopeKey}:{durationJsonKey}";
        if (_values.TryGetValue(inputKey, out var value))
        {
            return ComponentPreviewActionRuntimeValue.RequireDurationInput(value, action);
        }
        return ComponentPreviewActionRuntimeValue.RequireDurationInput(_runtimePreview, action);
    }

    private bool IsPlaying(ComponentPreviewActionDefinition action)
    {
        var key = ActionStateKey(action);
        return BooleanText.ParseRequired(
            _values.GetValueOrDefault(key, InputDefault(key, "false")),
            $"Design Preview action '{action.Id}' playback state");
    }

    private void SetPlaybackState(ComponentPreviewActionDefinition action, bool isPlaying)
    {
        var stateKey = ActionStateKey(action);
        if (isPlaying)
        {
            _heldFinalActionId = "";
            StopPlayback();
            foreach (var otherAction in _actions)
            {
                if (otherAction.Id == action.Id)
                {
                    continue;
                }

                _values[ActionStateKey(otherAction)] = "false";
            }

            _activeActionId = action.Id;
            _playbackSecondsByActionId[action.Id] = 0;
            _values[ActionTimeKey(action)] = "0";
            _values[stateKey] = "true";
            foreach (var key in ActivatedPlaybackInputKeys(action))
            {
                _values[key] = "true";
            }
            return;
        }

        var seconds = NormalizedPlaybackSeconds(action, CurrentPlaybackSeconds(action));
        if (_heldFinalActionId == action.Id) _heldFinalActionId = "";
        _playbackSecondsByActionId.Remove(action.Id);
        _values[ActionTimeKey(action)] = PlaybackTimeStorageValue(action, seconds);
        _values[stateKey] = "false";
    }

    private string PlaybackTimeStorageValue(ComponentPreviewActionDefinition action, double seconds)
    {
        if (action.TimeUnit == ComponentPreviewActionTimeUnit.Frames)
        {
            var frame = (int)Math.Floor(
                NormalizedPlaybackSeconds(action, seconds) * Math.Max(1, _playbackFrameRate) + 0.0001);
            return Math.Max(0, Math.Min(DurationFrames(action), frame)).ToString(CultureInfo.InvariantCulture);
        }

        if (action.TimeUnit == ComponentPreviewActionTimeUnit.Milliseconds)
        {
            return (NormalizedPlaybackSeconds(action, seconds) * 1000)
                .ToString(CultureInfo.InvariantCulture);
        }

        return NormalizedPlaybackSeconds(action, seconds).ToString(CultureInfo.InvariantCulture);
    }

    private double NormalizedPlaybackSeconds(ComponentPreviewActionDefinition action, double seconds)
    {
        var durationSeconds = DurationSeconds(action);
        var clamped = Math.Max(0, Math.Min(durationSeconds, seconds));
        var frameRate = Math.Max(1, _playbackFrameRate);
        var snapped = Math.Round(clamped * frameRate, MidpointRounding.AwayFromZero) / frameRate;
        return Math.Max(0, Math.Min(durationSeconds, snapped));
    }

    private bool SupportsPlayback()
    {
        return _actions.Count > 0;
    }

    private string ActionStateKey(ComponentPreviewActionDefinition action)
    {
        return $"{_scopeKey}:action:{action.Id}:state";
    }

    private string ActionTimeKey(ComponentPreviewActionDefinition action)
    {
        return $"{_scopeKey}:action:{action.Id}:time";
    }

    private IEnumerable<string> ActivatedPlaybackInputKeys(ComponentPreviewActionDefinition action)
    {
        return action.ActivateInputIds
            .Where((id) => !string.IsNullOrWhiteSpace(id))
            .Select((id) => $"{_scopeKey}:{id}");
    }

    private IEnumerable<string> DeactivatedPlaybackInputKeys(ComponentPreviewActionDefinition action)
    {
        return action.DeactivateInputIds
            .Where((id) => !string.IsNullOrWhiteSpace(id))
            .Select((id) => $"{_scopeKey}:{id}");
    }

    private void CaptureActionSnapshot(ComponentPreviewActionDefinition action)
    {
        var snapshotKey = ActionSnapshotKey(action.Id);
        if (_actionSnapshots.ContainsKey(snapshotKey)) return;

        var keys = new[] { ActionStateKey(action), ActionTimeKey(action), ActionTargetFromKey(action) }
            .Concat(ActivatedPlaybackInputKeys(action))
            .Concat(DeactivatedPlaybackInputKeys(action))
            .Concat(ActionTargetInputKeys(action))
            .Distinct(StringComparer.Ordinal);
        _actionSnapshots[snapshotKey] = keys.ToDictionary(
            (key) => key,
            (key) => _values.TryGetValue(key, out var value)
                ? new ActionValueSnapshot(true, value)
                : new ActionValueSnapshot(false, ""),
            StringComparer.Ordinal);
    }

    private void ResetCompletedActionForReplay(ComponentPreviewActionDefinition action)
    {
        if (!_actionSnapshots.TryGetValue(ActionSnapshotKey(action.Id), out var snapshot))
        {
            return;
        }
        var holdsFinal = _heldFinalActionId == action.Id;
        var completedReset = !IsPlaying(action)
            && CurrentPlaybackSeconds(action) >= DurationSeconds(action);
        if (!holdsFinal && !completedReset)
        {
            return;
        }

        ApplyActionSnapshot(snapshot);
        _playbackSecondsByActionId.Remove(action.Id);
        if (holdsFinal) _heldFinalActionId = "";
        _activeActionId = action.Id;
    }

    private void ApplyActionSnapshot(
        IReadOnlyDictionary<string, ActionValueSnapshot> snapshot)
    {
        foreach (var (key, value) in snapshot)
        {
            if (value.Exists)
            {
                _values[key] = value.Value;
            }
            else
            {
                _values.Remove(key);
            }
        }
    }

    private string ActionSnapshotKey(string actionId) => $"{_scopeKey}:action-snapshot:{actionId}";

    private IEnumerable<string> ActionTargetInputKeys(ComponentPreviewActionDefinition action)
    {
        return string.IsNullOrWhiteSpace(action.TargetInputId)
            ? []
            : [ActionTargetStorageKey(action)];
    }

    private void ApplyActionTarget(ComponentPreviewActionDefinition action, string? explicitValue)
    {
        if (string.IsNullOrWhiteSpace(action.TargetInputId)) return;
        var key = ActionTargetStorageKey(action);
        var current = _values.GetValueOrDefault(key, InputDefault(key, "false"));
        if (!string.IsNullOrWhiteSpace(action.TargetFromJsonKey))
        {
            _values[ActionTargetFromKey(action)] = current;
        }
        var target = action.TargetMode switch
        {
            ComponentPreviewActionTargetMode.Toggle => BooleanText.ParseRequired(
                current,
                $"Design Preview action '{action.Id}' target '{action.TargetInputId}'")
                    ? "false"
                    : "true",
            ComponentPreviewActionTargetMode.Option or ComponentPreviewActionTargetMode.Value
                when !string.IsNullOrWhiteSpace(explicitValue) => explicitValue,
            _ => "",
        };
        if (!string.IsNullOrWhiteSpace(target)) _values[key] = target;
    }

    private string ActionTargetFromKey(ComponentPreviewActionDefinition action) =>
        $"{_scopeKey}:action:{action.Id}:target-from";

    private string ActionTargetStorageKey(ComponentPreviewActionDefinition action) =>
        action.IsCollectionItemAction
            ? $"{_scopeKey}:action:{action.Id}:target-value"
            : $"{_scopeKey}:{action.TargetInputId}";

    private void SyncDeactivatedPlaybackInputs(ComponentPreviewActionDefinition action)
    {
        foreach (var key in DeactivatedPlaybackInputKeys(action))
        {
            _values[key] = "false";
        }
    }

    private ComponentPreviewActionDefinition? ActiveAction()
    {
        return _actions.FirstOrDefault((action) => action.Id == _activeActionId)
            ?? _actions.FirstOrDefault((action) => IsPlaying(action))
            ?? _actions.FirstOrDefault();
    }

    private string InputDefault(string key, string defaultValue)
    {
        return _inputDefaults.GetValueOrDefault(key, defaultValue);
    }

    private readonly record struct ActionValueSnapshot(bool Exists, string Value);

    private static JsonObject ParseJsonObject(string json)
    {
        return JsonPath.ParseRequiredObject(json, "Component input JSON");
    }

    private static string InputSignature(ComponentInputDefinition input)
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
            string.Join("|", collection.Fields.Select(InputSignature)),
            collection.AnimationPresentation,
            collection.ComponentItems is null
                ? ""
                : string.Join("/", collection.ComponentItems.VariantReferenceJsonKey,
                    collection.ComponentItems.OverridesJsonKey,
                    collection.ComponentItems.InputsJsonKey));

    private static string ActionSignature(ComponentPreviewActionDefinition action)
    {
        return string.Join(
            ":",
            "action",
            action.Id,
            action.Label,
            action.PlayInputId,
            action.DurationInputId,
            action.DurationJsonKey,
            action.DurationBehaviorTimingInputId,
            action.DurationSeconds.ToString(CultureInfo.InvariantCulture),
            action.DurationCollectionJsonKey,
            action.DurationThemeToken,
            string.Join(",", action.DurationItemNumberKeys),
            string.Join(",", action.DurationCollectionMultiplierNumberKeys),
            action.DurationBaseFrames.ToString(CultureInfo.InvariantCulture),
            action.TimeJsonKey,
            action.TimeUnit,
            action.CompletionBehavior,
            action.PrewarmFrames.ToString(CultureInfo.InvariantCulture),
            action.PrewarmWhenJsonKey,
            action.PrewarmWhenConfigPath,
            action.PrewarmWhenValue,
            string.Join(",", action.ActivateInputIds),
            string.Join(",", action.DeactivateInputIds),
            action.CollectionJsonKey,
            action.CollectionItemId);
    }

}
