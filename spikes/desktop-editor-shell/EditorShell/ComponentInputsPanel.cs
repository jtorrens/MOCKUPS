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
        SpikeDatabase database,
        Action refreshPreview,
        Func<ComponentPreviewActionDefinition, Task<bool>>? preparePlaybackFrames = null)
    {
        _previewInputData = new ComponentPreviewInputDataSource(database);
        _inputOptionsData = new RuntimeInputOptionsDataSource(database);
        var actorDataSource = new ActorPreviewDataSource(database);
        _recordInputResolver = new ComponentPreviewRecordInputResolver(actorDataSource);
        _nestedRecordInputResolver = new NestedRuntimeRecordReferenceResolver(actorDataSource);
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
        var inputs = ReadRuntimeInputs(preview, config);
        var collections = ReadRuntimeCollections(preview, config);
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
            ClearTransientValues(scopeKey);
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

    private void ClearTransientValues(string scopeKey)
    {
        var prefix = $"{scopeKey}:";
        foreach (var key in _values.Keys.Where((key) => key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _values.Remove(key);
            _inputDefaults.Remove(key);
        }
        _transientCollectionTestValuesByScope.Remove(scopeKey);
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

    public void SetExternalCollectionInputValue(
        string collectionJsonKey,
        string itemId,
        ComponentInputDefinition input,
        string value)
    {
        if (string.IsNullOrWhiteSpace(_scopeKey)
            || string.IsNullOrWhiteSpace(collectionJsonKey)
            || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        var testValues = _transientCollectionTestValuesByScope.GetValueOrDefault(_scopeKey);
        if (testValues is null)
        {
            testValues = new JsonObject();
            _transientCollectionTestValuesByScope[_scopeKey] = testValues;
        }
        if (!testValues.TryGetPropertyValue(collectionJsonKey, out var collectionNode))
        {
            var definition = ReadRuntimeCollections(_runtimePreview, _config)
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
        item[input.JsonKey] = DesignPreviewTestValues.ValueNode(input, value);
        _refreshPreview();
    }

    public void SetExternalCollectionItems(string collectionJsonKey, IReadOnlyList<JsonObject> items)
    {
        if (string.IsNullOrWhiteSpace(_scopeKey) || string.IsNullOrWhiteSpace(collectionJsonKey)) return;
        var testValues = _transientCollectionTestValuesByScope.GetValueOrDefault(_scopeKey) ?? new JsonObject();
        _transientCollectionTestValuesByScope[_scopeKey] = testValues;
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
        var inputs = ReadRuntimeInputs(preview, config);
        var collections = ReadRuntimeCollections(preview, config);
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
        foreach (var collection in ReadRuntimeCollections(preview, config))
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
                    ReadRuntimeInputs(componentInputs, componentConfig),
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
        return $"{payload.Kind}:{payload.ComponentType}:{payload.Name}:{instanceId}";
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
        if (string.IsNullOrWhiteSpace(scopeKey))
        {
            return effective;
        }

        foreach (var input in ReadRuntimeInputs(effective, config))
        {
            var key = $"{scopeKey}:{input.JsonKey}";
            if (_values.TryGetValue(key, out var value))
            {
                DesignPreviewTestValues.SetValue(effective, input, value);
            }
        }
        return ParseJsonObject(DesignPreviewTestValues.RuntimeJson(effective.ToJsonString()));
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
        var collections = ReadRuntimeCollections(preview, _config)
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

    private static ComponentInputDefinition CreateInputDefinition(
        string id,
        string label,
        string jsonKey,
        string kind,
        string valueKind,
        string defaultValue,
        IReadOnlyList<FieldOption> options,
        decimal minimum,
        decimal maximum,
        decimal increment,
        string tableId,
        string resolvedJsonKey,
        string componentType,
        ComponentInputSource source,
        PairFieldLabels? pairLabels,
        ComponentInputUiOrigin uiOrigin,
        string uiGroupId,
        string uiGroupLabel,
        string uiParentGroupId,
        string unit)
    {
        var normalizedValueKind = RuntimeInputValueKindContract.RequireCompatible(
            kind,
            valueKind,
            $"Runtime Input '{id}'");
        var normalizedKind = ParseKind(kind);
        var normalizedPairLabels = PairFieldLabelsContract.ForField(
            normalizedValueKind,
            pairLabels,
            $"Runtime Input '{id}'");
        return new ComponentInputDefinition(
            id,
            label,
            jsonKey,
            normalizedKind,
            normalizedValueKind,
            defaultValue,
            options,
            minimum,
            maximum,
            increment,
            tableId,
            resolvedJsonKey,
            componentType,
            source,
            normalizedPairLabels,
            uiOrigin,
            uiGroupId,
            uiGroupLabel,
            uiParentGroupId,
            Unit: unit);
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

    internal static IReadOnlyList<ComponentInputDefinition> ReadRuntimeInputs(JsonObject preview, JsonObject config)
    {
        preview = RuntimeInputForwardingContract.EffectivePreview(preview, config);
        var definitions = new List<ComponentInputDefinition>();
        if (preview["inputs"] is null)
        {
            return definitions;
        }
        var inputs = preview["inputs"] as JsonArray
            ?? throw new InvalidOperationException(
                "Runtime Input definitions must be an array when present.");
        var inputDefinitions = inputs.Select((node, index) => node as JsonObject
                ?? throw new InvalidOperationException(
                    $"Runtime Input definition at index {index} must be an object."))
            .ToList();
        RuntimeInputValueKindContract.ValidateBehaviorTimingDefinitions(
            inputDefinitions,
            "Runtime Input definitions");

        for (var index = 0; index < inputDefinitions.Count; index++)
        {
            var item = inputDefinitions[index];
            var owner = $"Runtime Input definition at index {index}";
            var id = JsonPath.RequiredString(item, "id", owner);
            var label = JsonPath.RequiredString(item, "label", owner);
            var jsonKey = JsonPath.RequiredString(item, "jsonKey", owner);
            var kind = JsonPath.RequiredString(item, "kind", owner);
            _ = RuntimeInputValueKindContract.CreateDefaultValue(item, $"Runtime Input '{id}'");
            var source = ParseInputSource(JsonString(item, "source"));
            var uiOrigin = ParseInputUiOrigin(JsonString(item, "uiOrigin"));

            var definition = CreateInputDefinition(
                id,
                label,
                jsonKey,
                kind,
                JsonString(item, "valueKind"),
                JsonString(item, "defaultValue"),
                ReadOptions(item),
                JsonDecimal(item, "minimum", 0),
                JsonDecimal(item, "maximum", 9999),
                JsonDecimal(item, "increment", 1),
                JsonString(item, "tableId"),
                JsonString(item, "resolvedJsonKey"),
                JsonString(item, "componentType"),
                source,
                ReadPairLabels(item),
                uiOrigin,
                JsonString(item, "uiGroupId"),
                JsonString(item, "uiGroupLabel"),
                JsonString(item, "uiParentGroupId"),
                JsonString(item, "unit")) with
            {
                UiOrder = (int)JsonDecimal(item, "uiOrder", 0),
                UiSectionLabel = JsonString(item, "uiSectionLabel"),
                EnabledWhenPath = JsonString(item, "enabledWhenPath"),
                EnabledWhenValue = JsonString(item, "enabledWhenValue"),
                RefreshOnCommit = item["refreshOnCommit"]?.GetValue<bool>() == true,
                ActionOnly = item["actionOnly"]?.GetValue<bool>() == true,
                AllowEmpty = item["allowEmpty"]?.GetValue<bool>() == true,
                AllowEmptyWhenItemJsonKey = JsonString(item, "allowEmptyWhenItemJsonKey"),
                AllowEmptyWhenItemValues = JsonStringArray(item, "allowEmptyWhenItemValues"),
                OptionsSourceCollectionJsonKey = JsonString(item, "optionsSourceCollectionJsonKey"),
                OptionsSourceValueJsonKey = JsonString(item, "optionsSourceValueJsonKey", "id"),
                OptionsSourceLabelJsonKey = JsonString(item, "optionsSourceLabelJsonKey"),
                OptionsSourceFirstItemBadge = JsonString(item, "optionsSourceFirstItemBadge"),
            };
            var completeDefinition = definition with
            {
                Animation = ReadAnimationDefinition(item),
                BehaviorTiming = ReadBehaviorTimingDefinition(item),
                Transition = ReadInputTransitionDefinition(item),
            };
            if (source == ComponentInputSource.Runtime && InputIsVisible(item, config))
            {
                definitions.Add(completeDefinition);
            }
        }

        return definitions;
    }

    internal static IReadOnlyList<RuntimeInputCollectionDefinition> ReadRuntimeCollections(
        JsonObject preview,
        JsonObject config,
        bool includeHidden = false)
    {
        if (preview["collections"] is null)
        {
            return [];
        }
        var collections = preview["collections"] as JsonArray
            ?? throw new InvalidOperationException(
                "Runtime Input collections must be an array when present.");

        var definitions = new List<RuntimeInputCollectionDefinition>();
        for (var collectionIndex = 0; collectionIndex < collections.Count; collectionIndex++)
        {
            var collection = collections[collectionIndex] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Runtime Input collection at index {collectionIndex} must be an object.");
            var owner = $"Runtime Input collection at index {collectionIndex}";
            var id = JsonPath.RequiredString(collection, "id", owner);
            var label = JsonPath.RequiredString(collection, "label", owner);
            var jsonKey = JsonPath.RequiredString(collection, "jsonKey", owner);
            var itemLabel = JsonPath.RequiredString(collection, "itemLabel", owner);
            var fields = collection["fields"] as JsonArray
                ?? throw new InvalidOperationException(
                    $"Runtime Input collection '{id}' fields must be an array.");
            var fieldDefinitions = fields.Select((node, index) => node as JsonObject
                    ?? throw new InvalidOperationException(
                        $"Runtime Input collection '{id}' field at index {index} must be an object."))
                .ToList();
            RuntimeInputValueKindContract.ValidateBehaviorTimingDefinitions(
                fieldDefinitions,
                $"Runtime Input collection '{id}' fields");
            var isVisible = InputIsVisible(collection, config);

            var itemFields = new List<ComponentInputDefinition>();
            for (var fieldIndex = 0; fieldIndex < fieldDefinitions.Count; fieldIndex++)
            {
                var field = fieldDefinitions[fieldIndex];
                var fieldOwner = $"Runtime Input collection '{id}' field at index {fieldIndex}";
                var fieldId = JsonPath.RequiredString(field, "id", fieldOwner);
                var fieldLabel = JsonPath.RequiredString(field, "label", fieldOwner);
                var fieldJsonKey = JsonPath.RequiredString(field, "jsonKey", fieldOwner);
                var kind = JsonPath.RequiredString(field, "kind", fieldOwner);
                _ = RuntimeInputValueKindContract.CreateDefaultValue(
                    field,
                    $"Runtime Input collection '{id}' field '{fieldId}'");

                var definition = CreateInputDefinition(
                    fieldId,
                    fieldLabel,
                    fieldJsonKey,
                    kind,
                    JsonString(field, "valueKind"),
                    JsonString(field, "defaultValue"),
                    ReadOptions(field),
                    JsonDecimal(field, "minimum", 0),
                    JsonDecimal(field, "maximum", 9999),
                    JsonDecimal(field, "increment", 1),
                    JsonString(field, "tableId"),
                    JsonString(field, "resolvedJsonKey"),
                    JsonString(field, "componentType"),
                    ComponentInputSource.Runtime,
                    ReadPairLabels(field),
                    string.IsNullOrWhiteSpace(JsonString(field, "uiGroupId"))
                        ? ComponentInputUiOrigin.Self
                        : ComponentInputUiOrigin.Embedded,
                    JsonString(field, "uiGroupId"),
                    JsonString(field, "uiGroupLabel"),
                    JsonString(field, "uiParentGroupId"),
                    JsonString(field, "unit")) with
                {
                    EnabledWhenItemJsonKey = JsonString(field, "enabledWhenItemJsonKey"),
                    EnabledWhenItemValues = JsonStringArray(field, "enabledWhenItemValues"),
                    MinimumItemIndex = (int)JsonDecimal(field, "minimumItemIndex", 0),
                    UiOrder = (int)JsonDecimal(field, "uiOrder", 0),
                    UiSectionLabel = JsonString(field, "uiSectionLabel"),
                };
                itemFields.Add(definition with
                {
                    Animation = ReadAnimationDefinition(field),
                    BehaviorTiming = ReadBehaviorTimingDefinition(field),
                    StructuredCollection = ReadRuntimeCollection(OptionalObject(
                        field,
                        "structuredCollection",
                        $"Runtime Input collection '{id}' field '{fieldId}'")),
                    AllowEmpty = field["allowEmpty"]?.GetValue<bool>() == true,
                    AllowEmptyWhenItemJsonKey = JsonString(field, "allowEmptyWhenItemJsonKey"),
                    AllowEmptyWhenItemValues = JsonStringArray(field, "allowEmptyWhenItemValues"),
                    ActionOnly = field["actionOnly"]?.GetValue<bool>() == true,
                    Transition = ReadInputTransitionDefinition(field),
                    OptionsSourceCollectionJsonKey = JsonString(field, "optionsSourceCollectionJsonKey"),
                    OptionsSourceValueJsonKey = JsonString(field, "optionsSourceValueJsonKey", "id"),
                    OptionsSourceLabelJsonKey = JsonString(field, "optionsSourceLabelJsonKey"),
                    OptionsSourceFirstItemBadge = JsonString(field, "optionsSourceFirstItemBadge"),
                });
            }

            var itemPresentation = ReadItemPresentation(collection);
            var componentItems = ReadComponentItems(collection);
            if (componentItems is not null)
            {
                var matchingFields = itemFields.Where((field) =>
                        field.JsonKey.Equals(
                            componentItems.VariantReferenceJsonKey,
                            StringComparison.Ordinal))
                    .ToList();
                if (matchingFields.Count != 1
                    || matchingFields[0].ValueKind != ValueKind.ComponentVariant)
                {
                    throw new InvalidOperationException(
                        $"Runtime Input collection '{id}' componentItems must reference one ComponentVariant field.");
                }
                if (itemFields.Any((field) =>
                        field.JsonKey.Equals(componentItems.OverridesJsonKey, StringComparison.Ordinal)
                        || field.JsonKey.Equals(componentItems.InputsJsonKey, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        $"Runtime Input collection '{id}' componentItems object keys must not overlap field keys.");
                }
            }
            if (!isVisible && !includeHidden) continue;
            var uiPresentation = JsonString(collection, "uiPresentation", "collection");
            if (uiPresentation is not "collection" and not "itemSections")
            {
                throw new InvalidOperationException(
                    $"Runtime Input collection '{id}' has unsupported uiPresentation '{uiPresentation}'.");
            }
            definitions.Add(new RuntimeInputCollectionDefinition(
                id,
                label,
                jsonKey,
                itemLabel,
                itemFields,
                JsonString(collection, "sourceCollectionJsonKey"),
                itemPresentation,
                componentItems,
                JsonString(collection, "storageCollectionJsonKey"),
                JsonString(collection, "itemRuntimeContractJsonKey"),
                JsonString(collection, "uiParentCollectionJsonKey"),
                JsonString(collection, "uiParentItemIdJsonKey"),
                JsonString(collection, "animationPresentation", "item"),
                collection["canEditStructure"]?.GetValue<bool>() ?? true,
                FixedCollectionItemCount(collection, config),
                uiPresentation));
        }

        return definitions;
    }

    private static RuntimeInputCollectionDefinition? ReadRuntimeCollection(JsonObject? collection)
    {
        if (collection is null) return null;
        var wrapper = new JsonObject { ["collections"] = new JsonArray(collection.DeepClone()) };
        return ReadRuntimeCollections(wrapper, new JsonObject()).SingleOrDefault();
    }

    private static int FixedCollectionItemCount(JsonObject collection, JsonObject config)
    {
        var path = JsonString(collection, "fixedCountPath");
        if (string.IsNullOrWhiteSpace(path)) return 0;
        var node = JsonPath.Get(
            config,
            path.Split('.', StringSplitOptions.RemoveEmptyEntries));
        if (node is null && config.Count == 0)
        {
            return 0;
        }
        if (node is not JsonValue value
            || !value.TryGetValue<double>(out var number)
            || number < 1
            || number != Math.Truncate(number))
        {
            throw new InvalidOperationException(
                $"Runtime Input collection '{JsonString(collection, "id")}' fixedCountPath '{path}' must resolve to a positive integer.");
        }
        return checked((int)number);
    }

    private static RuntimeInputCollectionItemPresentation? ReadItemPresentation(JsonObject collection)
    {
        var presentation = OptionalObject(
            collection,
            "itemPresentation",
            $"Runtime Input collection '{JsonString(collection, "id")}'");
        if (presentation is null) return null;

        var subtitleFieldIds = JsonStringArray(presentation, "subtitleFieldIds");
        var iconValueMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (presentation["iconValueMap"] is not null)
        {
            var mapping = presentation["iconValueMap"] as JsonObject
                ?? throw new InvalidOperationException(
                    "Runtime Input itemPresentation iconValueMap must be an object when present.");
            foreach (var (key, node) in mapping)
            {
                if (string.IsNullOrWhiteSpace(key)
                    || node is not JsonValue value
                    || !value.TryGetValue<string>(out var icon)
                    || string.IsNullOrWhiteSpace(icon))
                {
                    throw new InvalidOperationException(
                        "Runtime Input itemPresentation iconValueMap requires non-empty string keys and values.");
                }
                iconValueMap.Add(key, icon);
            }
        }
        return new RuntimeInputCollectionItemPresentation(
            JsonString(presentation, "titleFieldId"),
            JsonString(presentation, "firstItemBadge"),
            subtitleFieldIds,
            Math.Max(16, (int)JsonDecimal(presentation, "subtitleMaxCharacters", 72)),
            JsonString(presentation, "iconFieldId"),
            JsonString(presentation, "fallbackIcon", EditorIcons.Component),
            iconValueMap);
    }

    private static RuntimeComponentCollectionItemDefinition? ReadComponentItems(JsonObject collection)
    {
        var keys = RuntimeComponentCollectionItemDocumentContract.ReadDefinition(
            collection,
            $"Runtime Input collection '{JsonString(collection, "id")}'");
        return keys is null
            ? null
            : new RuntimeComponentCollectionItemDefinition(
                keys.VariantReferenceJsonKey,
                keys.OverridesJsonKey,
                keys.InputsJsonKey);
    }

    private static BehaviorTimingDefinition? ReadBehaviorTimingDefinition(JsonObject field)
    {
        return RuntimeInputValueKindContract.ReadBehaviorTimingDefinition(
            field,
            ownerDefinitions: null,
            "Runtime Input");
    }

    private static bool InputIsVisible(JsonObject input, JsonObject config)
    {
        var path = JsonString(input, "visibleWhenPath");
        var expected = JsonString(input, "visibleWhenValue");
        if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(expected))
        {
            throw new InvalidOperationException(
                "Runtime Input visibility requires visibleWhenPath and visibleWhenValue together.");
        }

        var current = JsonPath.Get(config, path.Split('.', StringSplitOptions.RemoveEmptyEntries));
        return current is JsonValue value
            && value.TryGetValue<string>(out var text)
            && text.Equals(expected, StringComparison.Ordinal);
    }

    private static AnimationFieldDefinition? ReadAnimationDefinition(JsonObject input)
    {
        if (input["animatable"] is null)
        {
            return null;
        }
        if (input["animatable"] is not JsonValue enabled
            || !enabled.TryGetValue<bool>(out var isEnabled))
        {
            throw new InvalidOperationException(
                "Runtime Input animatable must be a JSON boolean when present.");
        }
        if (!isEnabled) return null;
        var interpolations = JsonStringArray(input, "animationInterpolations");
        var animationTimeline = OptionalObject(input, "animationTimeline", "Runtime Input animation");
        var extendsOwnerDuration = animationTimeline?["extendsOwnerDuration"] is null
            || JsonPath.RequiredBoolean(
                animationTimeline,
                "extendsOwnerDuration",
                "Runtime Input animationTimeline");
        return interpolations.Count > 0
            ? new AnimationFieldDefinition(interpolations, extendsOwnerDuration)
            : new AnimationFieldDefinition(["hold"], extendsOwnerDuration);
    }

    private static ComponentInputTransitionDefinition? ReadInputTransitionDefinition(JsonObject input)
    {
        var transition = OptionalObject(input, "transition", "Runtime Input");
        if (transition is null) return null;
        var targetInputId = JsonString(transition, "targetInputId");
        var replacementValue = JsonString(transition, "replacementValue");
        var triggerValues = JsonStringArray(transition, "triggerValues");
        if (string.IsNullOrWhiteSpace(targetInputId)
            || triggerValues.Count == 0)
        {
            throw new InvalidOperationException("Component input transitions require targetInputId and triggerValues.");
        }
        return new ComponentInputTransitionDefinition(
            targetInputId,
            triggerValues,
            replacementValue,
            JsonString(transition, "targetValuePattern"),
            transition["forwardedTargetOnly"]?.GetValue<bool>() == true);
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

    private static IReadOnlyList<FieldOption> ReadOptions(JsonObject input)
    {
        if (input["options"] is null)
        {
            return [];
        }
        var options = input["options"] as JsonArray
            ?? throw new InvalidOperationException(
                "Runtime Input options must be an array when present.");
        var values = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<FieldOption>(options.Count);
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Runtime Input option at index {index} must be an object.");
            var value = JsonPath.RequiredString(option, "value", $"Runtime Input option at index {index}");
            var label = JsonPath.RequiredString(option, "label", $"Runtime Input option at index {index}");
            if (!values.Add(value))
            {
                throw new InvalidOperationException(
                    $"Runtime Input options contain duplicate value '{value}'.");
            }
            result.Add(new FieldOption(value, label));
        }

        return result;
    }

    private static IReadOnlyList<string> JsonStringArray(JsonObject input, string key)
    {
        if (input[key] is null)
        {
            return [];
        }
        var values = input[key] as JsonArray
            ?? throw new InvalidOperationException(
                $"Runtime Input {key} must be an array when present.");

        return values.Select((node, index) => node is JsonValue value
                && value.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text)
                    ? text
                    : throw new InvalidOperationException(
                        $"Runtime Input {key}[{index}] must be a non-empty string."))
            .ToList();
    }

    private static ComponentInputKind ParseKind(string kind)
    {
        return kind.Trim().ToLowerInvariant() switch
        {
            "text" => ComponentInputKind.Text,
            "number" => ComponentInputKind.Number,
            "integerpair" => ComponentInputKind.IntegerPair,
            "boolean" => ComponentInputKind.Boolean,
            "option" => ComponentInputKind.Option,
            "recordreference" => ComponentInputKind.RecordReference,
            "componentvariant" => ComponentInputKind.ComponentVariant,
            "componentvariantslot" => ComponentInputKind.ComponentVariantSlot,
            "themetoken" => ComponentInputKind.ThemeToken,
            "icon" => ComponentInputKind.Icon,
            "iconlist" => ComponentInputKind.IconList,
            "multilinetext" => ComponentInputKind.MultilineText,
            "mediafilepath" or "behaviortiming" or "collection" => ComponentInputKind.Text,
            _ => throw new InvalidOperationException($"Unsupported runtime input kind '{kind}'."),
        };
    }

    private static ComponentInputSource ParseInputSource(string source)
    {
        return source switch
        {
            "" or "runtime" => ComponentInputSource.Runtime,
            "variant" => ComponentInputSource.Variant,
            "calculated" => ComponentInputSource.Calculated,
            _ => throw new InvalidOperationException(
                $"Unknown Runtime Input source '{source}'."),
        };
    }

    private static ComponentInputUiOrigin ParseInputUiOrigin(string origin)
    {
        return origin switch
        {
            "" or "self" => ComponentInputUiOrigin.Self,
            "embedded" => ComponentInputUiOrigin.Embedded,
            _ => throw new InvalidOperationException(
                $"Unknown Runtime Input uiOrigin '{origin}'."),
        };
    }

    private static string JsonString(JsonObject owner, string key)
    {
        return JsonString(owner, key, "");
    }

    private static PairFieldLabels? ReadPairLabels(JsonObject owner)
    {
        var first = JsonString(owner, "pairFirstLabel");
        var second = JsonString(owner, "pairSecondLabel");
        if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(second))
        {
            return null;
        }

        return new PairFieldLabels(first, second);
    }

    private static string JsonString(JsonObject owner, string key, string fallback)
    {
        if (owner[key] is null) return fallback;
        return owner[key] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : throw new InvalidOperationException(
                $"Runtime Input {key} must be a string when present.");
    }

    private static decimal JsonDecimal(JsonObject owner, string key, decimal fallback)
    {
        if (owner[key] is null) return fallback;
        try
        {
            return (decimal)JsonPath.RequiredNumber(owner[key], $"Runtime Input {key}");
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException(
                $"Runtime Input {key} must fit a decimal value.",
                exception);
        }
    }

    private static JsonObject? OptionalObject(JsonObject owner, string key, string context)
    {
        if (owner[key] is null) return null;
        return owner[key] as JsonObject
            ?? throw new InvalidOperationException(
                $"{context} {key} must be an object when present.");
    }

}

internal enum ComponentInputKind
{
    Text,
    Number,
    IntegerPair,
    Boolean,
    Option,
    RecordReference,
    ComponentVariant,
    ComponentVariantSlot,
    ThemeToken,
    Icon,
    IconList,
    MultilineText,
}

internal enum ComponentInputSource
{
    Runtime,
    Variant,
    Calculated,
}

internal enum ComponentInputUiOrigin
{
    Self,
    Embedded,
}

internal sealed record ComponentInputDefinition(
    string Id,
    string Label,
    string JsonKey,
    ComponentInputKind Kind,
    ValueKind ValueKind,
    string DefaultValue,
    IReadOnlyList<FieldOption>? Options = null,
    decimal Minimum = 0,
    decimal Maximum = 9999,
    decimal Increment = 1,
    string TableId = "",
    string ResolvedJsonKey = "",
    string ComponentType = "",
    ComponentInputSource Source = ComponentInputSource.Runtime,
    PairFieldLabels? PairLabels = null,
    ComponentInputUiOrigin UiOrigin = ComponentInputUiOrigin.Self,
    string UiGroupId = "",
    string UiGroupLabel = "",
    string UiParentGroupId = "",
    string EnabledWhenItemJsonKey = "",
    IReadOnlyList<string>? EnabledWhenItemValues = null,
    int MinimumItemIndex = 0,
    int UiOrder = 0,
    string UiSectionLabel = "",
    string Unit = "",
    AnimationFieldDefinition? Animation = null,
    BehaviorTimingDefinition? BehaviorTiming = null,
    ComponentInputTransitionDefinition? Transition = null,
    string EnabledWhenPath = "",
    string EnabledWhenValue = "",
    bool RefreshOnCommit = false,
    RuntimeInputCollectionDefinition? StructuredCollection = null,
    bool AllowEmpty = false,
    string AllowEmptyWhenItemJsonKey = "",
    IReadOnlyList<string>? AllowEmptyWhenItemValues = null,
    bool ActionOnly = false,
    string OptionsSourceCollectionJsonKey = "",
    string OptionsSourceValueJsonKey = "id",
    string OptionsSourceLabelJsonKey = "",
    string OptionsSourceFirstItemBadge = "",
    bool ShowInEditor = true);

internal sealed record RuntimeInputCollectionDefinition(
    string Id,
    string Label,
    string JsonKey,
    string ItemLabel,
    IReadOnlyList<ComponentInputDefinition> Fields,
    string SourceCollectionJsonKey = "",
    RuntimeInputCollectionItemPresentation? ItemPresentation = null,
    RuntimeComponentCollectionItemDefinition? ComponentItems = null,
    string StorageCollectionJsonKey = "",
    string ItemRuntimeContractJsonKey = "",
    string UiParentCollectionJsonKey = "",
    string UiParentItemIdJsonKey = "",
    string AnimationPresentation = "item",
    bool CanEditStructure = true,
    int FixedItemCount = 0,
    string UiPresentation = "collection");

internal sealed record RuntimeComponentCollectionItemDefinition(
    string VariantReferenceJsonKey,
    string OverridesJsonKey,
    string InputsJsonKey)
{
    public RuntimeComponentCollectionDocumentKeys DocumentKeys => new(
        VariantReferenceJsonKey,
        OverridesJsonKey,
        InputsJsonKey);
}

internal sealed record RuntimeInputCollectionItemPresentation(
    string TitleFieldId,
    string FirstItemBadge,
    IReadOnlyList<string> SubtitleFieldIds,
    int SubtitleMaxCharacters,
    string IconFieldId,
    string FallbackIcon,
    IReadOnlyDictionary<string, string> IconValueMap);
