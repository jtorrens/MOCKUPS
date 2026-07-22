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
        if (testValues[collectionJsonKey] is not JsonArray)
        {
            var definition = ReadRuntimeCollections(_runtimePreview, _config)
                .FirstOrDefault((candidate) => candidate.JsonKey == collectionJsonKey);
            testValues[collectionJsonKey] = definition is null
                ? new JsonArray()
                : new JsonArray(DesignPreviewTestValues.CollectionItems(_runtimePreview, definition)
                    .Select((item) => (JsonNode?)item.DeepClone()).ToArray());
        }
        var items = testValues[collectionJsonKey] as JsonArray ?? new JsonArray();
        testValues[collectionJsonKey] = items;
        var item = items.OfType<JsonObject>().FirstOrDefault((candidate) =>
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
            if (preview[collection.JsonKey] is not JsonArray items) continue;
            foreach (var item in items.OfType<JsonObject>())
            {
                ResolveRecordReferenceInputs(item, collection.Fields, themeMode, paletteColors);
                if (collection.ComponentItems is not { } componentItems
                    || item[componentItems.VariantReferenceJsonKey] is not JsonValue variantValue
                    || !variantValue.TryGetValue<string>(out var variantReference)
                    || string.IsNullOrWhiteSpace(variantReference)
                    || item[componentItems.InputsJsonKey] is not JsonObject componentInputs)
                {
                    continue;
                }

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
        foreach (var input in inputs.Where((field) => field.Kind == ComponentInputKind.RecordReference))
        {
            if (string.IsNullOrWhiteSpace(input.ResolvedJsonKey)) continue;
            var recordId = values[input.JsonKey]?.GetValue<string>() ?? "";
            values[input.ResolvedJsonKey] = _recordInputResolver.ResolvedPreviewValue(
                input.TableId,
                recordId,
                themeMode,
                paletteColors,
                input.Id,
                CollectionFieldAvailability.AllowsEmpty(values, input));
        }
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
        var envelope = preview.DeepClone() as JsonObject ?? new JsonObject();
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

        var stored = preview[input.JsonKey];
        _values[key] = stored switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => text,
            JsonValue jsonValue when jsonValue.TryGetValue<double>(out var number) => number.ToString(CultureInfo.InvariantCulture),
            JsonValue jsonValue when jsonValue.TryGetValue<int>(out var integer) => integer.ToString(CultureInfo.InvariantCulture),
            JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var boolean) => boolean ? "true" : "false",
            JsonArray jsonArray => jsonArray.ToJsonString(),
            JsonObject jsonObject => jsonObject.ToJsonString(),
            _ => input.DefaultValue,
        };
    }

    private void EnsureActionValues(JsonObject preview)
    {
        foreach (var action in _actions)
        {
            var stateKey = ActionStateKey(action);
            if (!_values.ContainsKey(stateKey))
            {
                _values[stateKey] = ComponentPreviewActions.Value(preview, action, action.PlayInputId) switch
                {
                    JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var boolean) => boolean ? "true" : "false",
                    JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => text,
                    _ => "false",
                };
            }

            var timeKey = ActionTimeKey(action);
            if (!_values.ContainsKey(timeKey))
            {
                _values[timeKey] = ComponentPreviewActions.Value(preview, action, action.TimeJsonKey) switch
                {
                    JsonValue jsonValue when jsonValue.TryGetValue<double>(out var number) => number.ToString(CultureInfo.InvariantCulture),
                    JsonValue jsonValue when jsonValue.TryGetValue<int>(out var integer) => integer.ToString(CultureInfo.InvariantCulture),
                    JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => text,
                    _ => "0",
                };
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
            var item = (preview[collection.JsonKey] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault((candidate) =>
                    candidate["id"] is JsonValue value
                    && value.TryGetValue<string>(out var id)
                    && id.Equals(action.CollectionItemId, StringComparison.Ordinal));
            var validValues = (item?[input.OptionsSourceCollectionJsonKey] as JsonArray)?.OfType<JsonObject>()
                .Select((candidate) => candidate[input.OptionsSourceValueJsonKey]?.GetValue<string>() ?? "")
                .Where((value) => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? [];
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
            .Where((input) => input.Kind == ComponentInputKind.ComponentVariant)
            .ToList();
        if (variantInputs.Count == 0)
        {
            return;
        }

        foreach (var input in variantInputs)
        {
            var key = StorageKey(input);
            var reference = _values.GetValueOrDefault(key, input.DefaultValue);
            if (!string.IsNullOrWhiteSpace(reference))
            {
                _values[key] = _previewInputData.ValidateComponentVariantReference(
                    projectId,
                    input.ComponentType,
                    reference);
                continue;
            }

            var firstVariant = ComponentVariantOptions(input, projectId)
                .FirstOrDefault((option) => !string.IsNullOrWhiteSpace(option.Value));
            if (firstVariant is not null)
            {
                _values[key] = firstVariant.Value;
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
        PairFieldLabels pairLabels,
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
            pairLabels,
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

    private void StopPlayback()
    {
        var wasEnabled = _playbackTimer.IsEnabled;
        if (wasEnabled)
        {
            _playbackTimer.Stop();
        }
        var hasPlayback = SupportsPlayback();
        var activeAction = ActiveAction();
        var wasPlaying = hasPlayback && activeAction is not null && IsPlaying(activeAction);
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

        var stored = ParseDouble(_values.GetValueOrDefault(ActionTimeKey(action), "0"));
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
            var value = ThemeTokenNumber(action.DurationThemeToken, 1);
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

        if (action.IsCollectionItemAction)
        {
            return Math.Max(1, JsonNodeNumber(ComponentPreviewActions.Value(_runtimePreview, action, action.DurationInputId), 1));
        }

        return Math.Max(1, ActionDurationInputValue(action, 1));
    }

    private int DurationFrames(ComponentPreviewActionDefinition action)
    {
        if (action.TimeUnit != ComponentPreviewActionTimeUnit.Frames)
        {
            return Math.Max(1, (int)Math.Ceiling(DurationSeconds(action) * Math.Max(1, _playbackFrameRate)));
        }

        if (action.IsCollectionItemAction)
        {
            return Math.Max(1, (int)Math.Ceiling(
                JsonNodeNumber(ComponentPreviewActions.Value(_runtimePreview, action, action.DurationInputId), 1)));
        }

        if (!string.IsNullOrWhiteSpace(action.DurationThemeToken))
        {
            return Math.Max(1, (int)Math.Round(ThemeTokenNumber(action.DurationThemeToken, 1), MidpointRounding.AwayFromZero));
        }

        if (!string.IsNullOrWhiteSpace(action.DurationBehaviorTimingInputId))
        {
            var fields = _runtimePreview["inputs"] is JsonArray inputs
                ? inputs.OfType<JsonObject>().ToList()
                : [];
            var definition = fields.FirstOrDefault((field) =>
                field["id"]?.GetValue<string>() == action.DurationBehaviorTimingInputId)
                ?? throw new InvalidOperationException(
                    $"Missing BehaviorTiming action input '{action.DurationBehaviorTimingInputId}'.");
            return BehaviorTimingResolver.ResolveFrames(_runtimePreview, definition, fields, _themeTokens);
        }

        if (!string.IsNullOrWhiteSpace(action.DurationCollectionJsonKey))
        {
            return CollectionDurationFrames(_runtimePreview, action);
        }

        return Math.Max(1, (int)Math.Round(ActionDurationInputValue(action, 1), MidpointRounding.AwayFromZero));
    }

    private double ThemeTokenNumber(string token, double fallback)
    {
        JsonNode? current = _themeTokens;
        foreach (var segment in token.Split('.', StringSplitOptions.RemoveEmptyEntries).SkipWhile((segment) => segment == "theme"))
        {
            current = current is JsonObject owner ? owner[segment] : null;
        }
        return JsonNodeNumber(current, fallback);
    }

    private static int CollectionDurationFrames(JsonObject preview, ComponentPreviewActionDefinition action)
    {
        if (preview[action.DurationCollectionJsonKey] is not JsonArray items)
        {
            return Math.Max(1, (int)Math.Round(action.DurationBaseFrames, MidpointRounding.AwayFromZero));
        }

        var total = action.DurationBaseFrames;
        foreach (var item in items.OfType<JsonObject>())
        {
            foreach (var key in action.DurationItemNumberKeys)
            {
                total += (double)JsonDecimal(item, key, 0);
            }
            foreach (var key in action.DurationCollectionMultiplierNumberKeys)
            {
                total += (double)JsonDecimal(preview, key, 0);
            }
        }
        return Math.Max(1, (int)Math.Ceiling(total));
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

    private double ActionDurationInputValue(ComponentPreviewActionDefinition action, double fallback)
    {
        if (string.IsNullOrWhiteSpace(action.DurationInputId)) return fallback;
        if (action.IsCollectionItemAction)
        {
            return JsonNodeNumber(
                ComponentPreviewActions.Value(_runtimePreview, action, action.DurationInputId),
                fallback);
        }

        var inputKey = $"{_scopeKey}:{action.DurationInputId}";
        if (_values.TryGetValue(inputKey, out var value))
        {
            return ParseDouble(value);
        }
        return JsonNodeNumber(
            ComponentPreviewActions.Value(_runtimePreview, action, action.DurationInputId),
            fallback);
    }

    private bool IsPlaying(ComponentPreviewActionDefinition action)
    {
        var key = ActionStateKey(action);
        return StringToBool(_values.GetValueOrDefault(key, InputDefault(key, "false")));
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
            ComponentPreviewActionTargetMode.Toggle => StringToBool(current) ? "false" : "true",
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

    private static double ParseDouble(string? value)
    {
        return double.TryParse((value ?? "").Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static double JsonNodeNumber(JsonNode? node, double fallback)
    {
        if (node is not JsonValue value)
        {
            return fallback;
        }
        if (value.TryGetValue<double>(out var number))
        {
            return number;
        }
        return value.TryGetValue<string>(out var text)
            && double.TryParse(text.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
    }

    private static JsonObject ParseJsonObject(string json)
    {
        return JsonPath.ParseRequiredObject(json, "Component input JSON");
    }

    internal static IReadOnlyList<ComponentInputDefinition> ReadRuntimeInputs(JsonObject preview, JsonObject config)
    {
        preview = RuntimeInputForwardingContract.EffectivePreview(preview, config);
        var definitions = new List<ComponentInputDefinition>();
        if (preview["inputs"] is not JsonArray inputs)
        {
            return definitions;
        }

        foreach (var item in inputs.OfType<JsonObject>())
        {
            if (!InputIsVisible(item, config))
            {
                continue;
            }

            var id = JsonString(item, "id");
            var label = JsonString(item, "label");
            var jsonKey = JsonString(item, "jsonKey");
            var kind = JsonString(item, "kind");
            if (string.IsNullOrWhiteSpace(id)
                || string.IsNullOrWhiteSpace(label)
                || string.IsNullOrWhiteSpace(jsonKey)
                || string.IsNullOrWhiteSpace(kind))
            {
                continue;
            }

            var source = ParseInputSource(JsonString(item, "source"));
            if (source != ComponentInputSource.Runtime)
            {
                continue;
            }

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
                new PairFieldLabels(
                    JsonString(item, "pairFirstLabel", "W"),
                    JsonString(item, "pairSecondLabel", "H")),
                ParseInputUiOrigin(JsonString(item, "uiOrigin")),
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
            };
            definitions.Add(definition with
            {
                Animation = ReadAnimationDefinition(item),
                BehaviorTiming = ReadBehaviorTimingDefinition(item),
                Transition = ReadInputTransitionDefinition(item),
            });
        }

        return definitions;
    }

    internal static IReadOnlyList<RuntimeInputCollectionDefinition> ReadRuntimeCollections(JsonObject preview, JsonObject config)
    {
        if (preview["collections"] is not JsonArray collections)
        {
            return [];
        }

        var definitions = new List<RuntimeInputCollectionDefinition>();
        foreach (var collection in collections.OfType<JsonObject>())
        {
            if (!InputIsVisible(collection, config))
            {
                continue;
            }

            var id = JsonString(collection, "id");
            var label = JsonString(collection, "label");
            var jsonKey = JsonString(collection, "jsonKey");
            if (string.IsNullOrWhiteSpace(id)
                || string.IsNullOrWhiteSpace(label)
                || string.IsNullOrWhiteSpace(jsonKey)
                || collection["fields"] is not JsonArray fields)
            {
                continue;
            }

            var itemFields = new List<ComponentInputDefinition>();
            foreach (var field in fields.OfType<JsonObject>())
            {
                var fieldId = JsonString(field, "id");
                var fieldLabel = JsonString(field, "label");
                var fieldJsonKey = JsonString(field, "jsonKey");
                var kind = JsonString(field, "kind");
                if (string.IsNullOrWhiteSpace(fieldId)
                    || string.IsNullOrWhiteSpace(fieldLabel)
                    || string.IsNullOrWhiteSpace(fieldJsonKey)
                    || string.IsNullOrWhiteSpace(kind))
                {
                    continue;
                }

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
                    new PairFieldLabels(
                        JsonString(field, "pairFirstLabel", "W"),
                        JsonString(field, "pairSecondLabel", "H")),
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
                    StructuredCollection = ReadRuntimeCollection(field["structuredCollection"] as JsonObject),
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

            if (itemFields.Count > 0)
            {
                definitions.Add(new RuntimeInputCollectionDefinition(
                    id,
                    label,
                    jsonKey,
                    JsonString(collection, "itemLabel", "Item"),
                    itemFields,
                    JsonString(collection, "sourceCollectionJsonKey"),
                    ReadItemPresentation(collection),
                    ReadComponentItems(collection),
                    JsonString(collection, "storageCollectionJsonKey"),
                    JsonString(collection, "itemRuntimeContractJsonKey"),
                    JsonString(collection, "uiParentCollectionJsonKey"),
                    JsonString(collection, "uiParentItemIdJsonKey"),
                    JsonString(collection, "animationPresentation", "item")));
            }
        }

        return definitions;
    }

    private static RuntimeInputCollectionDefinition? ReadRuntimeCollection(JsonObject? collection)
    {
        if (collection is null) return null;
        var wrapper = new JsonObject { ["collections"] = new JsonArray(collection.DeepClone()) };
        return ReadRuntimeCollections(wrapper, new JsonObject()).SingleOrDefault();
    }

    private static RuntimeInputCollectionItemPresentation? ReadItemPresentation(JsonObject collection)
    {
        if (collection["itemPresentation"] is not JsonObject presentation)
        {
            return null;
        }

        var subtitleFieldIds = JsonStringArray(presentation, "subtitleFieldIds");
        var iconValueMap = (presentation["iconValueMap"] as JsonObject)?
            .Where((property) => property.Value is JsonValue)
            .ToDictionary(
                (property) => property.Key,
                (property) => property.Value?.GetValue<string>() ?? "",
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
        if (collection["componentItems"] is not JsonObject componentItems)
        {
            return null;
        }

        var variantReferenceJsonKey = JsonString(componentItems, "variantReferenceJsonKey");
        var overridesJsonKey = JsonString(componentItems, "overridesJsonKey");
        var inputsJsonKey = JsonString(componentItems, "inputsJsonKey");
        return string.IsNullOrWhiteSpace(variantReferenceJsonKey)
               || string.IsNullOrWhiteSpace(overridesJsonKey)
               || string.IsNullOrWhiteSpace(inputsJsonKey)
            ? null
            : new RuntimeComponentCollectionItemDefinition(variantReferenceJsonKey, overridesJsonKey, inputsJsonKey);
    }

    private static BehaviorTimingDefinition? ReadBehaviorTimingDefinition(JsonObject field)
    {
        if (field["naturalTiming"] is not JsonObject natural) return null;
        var sourceFieldId = JsonString(natural, "sourceFieldId");
        var unit = JsonString(natural, "unit");
        var baseFramesPerUnit = (double)JsonDecimal(natural, "baseFramesPerUnit", 0);
        return string.IsNullOrWhiteSpace(sourceFieldId) || string.IsNullOrWhiteSpace(unit) || baseFramesPerUnit <= 0
            ? null
            : new BehaviorTimingDefinition(sourceFieldId, unit, baseFramesPerUnit);
    }

    private static bool InputIsVisible(JsonObject input, JsonObject config)
    {
        var path = JsonString(input, "visibleWhenPath");
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        var expected = JsonString(input, "visibleWhenValue");
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        var current = JsonPath.Get(config, path.Split('.', StringSplitOptions.RemoveEmptyEntries));
        return current is JsonValue value
            && value.TryGetValue<string>(out var text)
            && text.Equals(expected, StringComparison.Ordinal);
    }

    private static AnimationFieldDefinition? ReadAnimationDefinition(JsonObject input)
    {
        if (input["animatable"] is not JsonValue enabled
            || !enabled.TryGetValue<bool>(out var isEnabled)
            || !isEnabled)
        {
            return null;
        }
        var interpolations = JsonStringArray(input, "animationInterpolations");
        var extendsOwnerDuration = input["animationTimeline"]?["extendsOwnerDuration"]?.GetValue<bool>() != false;
        return interpolations.Count > 0
            ? new AnimationFieldDefinition(interpolations, extendsOwnerDuration)
            : new AnimationFieldDefinition(["hold"], extendsOwnerDuration);
    }

    private static ComponentInputTransitionDefinition? ReadInputTransitionDefinition(JsonObject input)
    {
        if (input["transition"] is not JsonObject transition)
        {
            return null;
        }
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
            input.PairLabels.First,
            input.PairLabels.Second,
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
        if (input["options"] is not JsonArray options)
        {
            return [];
        }

        return options
            .OfType<JsonObject>()
            .Select((option) => new FieldOption(JsonString(option, "value"), JsonString(option, "label")))
            .Where((option) => !string.IsNullOrWhiteSpace(option.Value))
            .ToList();
    }

    private static IReadOnlyList<string> JsonStringArray(JsonObject input, string key)
    {
        if (input[key] is not JsonArray values)
        {
            return [];
        }

        return values
            .OfType<JsonValue>()
            .Select((value) => value.TryGetValue<string>(out var text) ? text : "")
            .Where((text) => !string.IsNullOrWhiteSpace(text))
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
        return source.Trim().ToLowerInvariant() switch
        {
            "variant" => ComponentInputSource.Variant,
            "calculated" => ComponentInputSource.Calculated,
            _ => ComponentInputSource.Runtime,
        };
    }

    private static ComponentInputUiOrigin ParseInputUiOrigin(string origin)
    {
        return origin.Trim().ToLowerInvariant() switch
        {
            "embedded" => ComponentInputUiOrigin.Embedded,
            _ => ComponentInputUiOrigin.Self,
        };
    }

    private static string JsonString(JsonObject owner, string key)
    {
        return JsonString(owner, key, "");
    }

    private static string JsonString(JsonObject owner, string key, string fallback)
    {
        return owner[key] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : fallback;
    }

    private static decimal JsonDecimal(JsonObject owner, string key, decimal fallback)
    {
        return owner[key] is JsonValue value && value.TryGetValue<decimal>(out var number)
            ? number
            : fallback;
    }

    private static bool StringToBool(string? value)
    {
        return BooleanText.Parse(value ?? "");
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
    PairFieldLabels PairLabels = null!,
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
    bool CanEditStructure = true);

internal sealed record RuntimeComponentCollectionItemDefinition(
    string VariantReferenceJsonKey,
    string OverridesJsonKey,
    string InputsJsonKey);

internal sealed record RuntimeInputCollectionItemPresentation(
    string TitleFieldId,
    string FirstItemBadge,
    IReadOnlyList<string> SubtitleFieldIds,
    int SubtitleMaxCharacters,
    string IconFieldId,
    string FallbackIcon,
    IReadOnlyDictionary<string, string> IconValueMap);
