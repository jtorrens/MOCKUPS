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
    private readonly SpikeDatabase _database;
    private readonly ComponentPreviewRecordInputResolver _recordInputResolver;
    private readonly Action _refreshPreview;
    private readonly Func<Task<bool>>? _preparePlaybackFrames;
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
    private JsonObject _runtimePreview = [];
    private string _preparingActionId = "";
    private int _playbackFrameRate = 25;
    private long _playbackStartedTimestamp;
    private double _playbackStartedAtSeconds;
    private readonly Dictionary<string, double> _playbackSecondsByActionId = new(StringComparer.Ordinal);

    public ComponentPreviewInputSession(
        SpikeDatabase database,
        Action refreshPreview,
        Func<Task<bool>>? preparePlaybackFrames = null)
    {
        _database = database;
        _recordInputResolver = new ComponentPreviewRecordInputResolver(database);
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
            _config = [];
            _runtimePreview = [];
            StopPlayback();
            return;
        }

        ApplyProjectFrameRate(projectId);
        var preview = ParseJsonObject(payload.DesignPreviewJson);
        _runtimePreview = preview;
        var config = ParseJsonObject(payload.ConfigJson);
        _config = config;
        var inputs = ReadRuntimeInputs(preview, config);
        _actions = ComponentPreviewActions.Read(preview);
        if (inputs.Count == 0)
        {
            _scopeKey = "";
            _projectId = "";
            _inputSignature = "";
            _actions = [];
            _activeActionId = "";
            _config = [];
            _runtimePreview = [];
            StopPlayback();
            return;
        }

        var scopeKey = ScopeKey(payload);
        var inputSignature = string.Join("|", inputs.Select(InputSignature).Concat(_actions.Select(ActionSignature)));
        var testValuesSignature = preview["testValues"]?.ToJsonString() ?? "";
        if (scopeKey == _scopeKey && testValuesSignature != _testValuesSignature) _values.Clear();
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

    public bool IsPlaybackActive => SupportsPlayback()
        && ActiveAction() is { } activeAction
        && IsPlaying(activeAction);

    public int PlaybackFrameRate => _playbackFrameRate;

    public int CurrentPreviewFrame => ActiveAction() is { } action && SupportsPlayback()
        ? CurrentPlaybackFrame(action)
        : 0;

    public bool TriggerAction(string actionId)
    {
        var action = _actions.FirstOrDefault((candidate) => candidate.Id == actionId);
        if (action is not null)
        {
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

    public void SetExternalInputValue(string jsonKey, string value)
    {
        if (string.IsNullOrWhiteSpace(_scopeKey) || string.IsNullOrWhiteSpace(jsonKey))
        {
            return;
        }

        _values[$"{_scopeKey}:{jsonKey}"] = value;
        _refreshPreview();
    }

    public DesignPreviewPayload ApplyInputs(DesignPreviewPayload payload, string themeMode, string? projectId)
    {
        if (!SupportsInputs(payload))
        {
            return payload;
        }

        var preview = ParseJsonObject(payload.DesignPreviewJson);
        _runtimePreview = preview;
        var config = ParseJsonObject(payload.ConfigJson);
        var inputs = ReadRuntimeInputs(preview, config);
        _actions = ComponentPreviewActions.Read(preview);
        if (inputs.Count == 0)
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
            EnsureComponentPresetReferenceValues(inputs, effectiveProjectId);
        }

        foreach (var input in inputs)
        {
            var value = Value(input);
            switch (input.Kind)
            {
                case ComponentInputKind.Number:
                    if (double.TryParse(value.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    {
                        preview[input.JsonKey] = number;
                    }
                    break;
                case ComponentInputKind.Boolean:
                    preview[input.JsonKey] = StringToBool(value);
                    break;
                case ComponentInputKind.IconList:
                    preview[input.JsonKey] = JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value) ?? new JsonArray();
                    break;
                case ComponentInputKind.RecordReference:
                    ApplyRecordReferenceInput(preview, input, value, themeMode, payload.PaletteColors);
                    break;
                default:
                    preview[input.JsonKey] = value;
                    break;
            }
        }
        foreach (var action in _actions)
        {
            preview[action.PlayInputId] = IsPlaying(action);
            preview[action.TimeJsonKey] = PlaybackTimeValue(action);
        }

        return payload with { DesignPreviewJson = preview.ToJsonString() };
    }

    private static string ScopeKey(DesignPreviewPayload payload)
    {
        return $"{payload.Kind}:{payload.ComponentType}:{payload.Name}";
    }

    private static bool SupportsInputs(DesignPreviewPayload payload)
    {
        return payload.Kind is "componentClass" or "module";
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
                _values[stateKey] = preview[action.PlayInputId] switch
                {
                    JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var boolean) => boolean ? "true" : "false",
                    JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => text,
                    _ => "false",
                };
            }

            var timeKey = ActionTimeKey(action);
            if (!_values.ContainsKey(timeKey))
            {
                _values[timeKey] = preview[action.TimeJsonKey] switch
                {
                    JsonValue jsonValue when jsonValue.TryGetValue<double>(out var number) => number.ToString(CultureInfo.InvariantCulture),
                    JsonValue jsonValue when jsonValue.TryGetValue<int>(out var integer) => integer.ToString(CultureInfo.InvariantCulture),
                    JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => text,
                    _ => "0",
                };
            }
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

    private void EnsureComponentPresetReferenceValues(IReadOnlyList<ComponentInputDefinition> inputs, string projectId)
    {
        var presetInputs = inputs
            .Where((input) => input.Kind == ComponentInputKind.ComponentPreset)
            .ToList();
        if (presetInputs.Count == 0)
        {
            return;
        }

        foreach (var input in presetInputs)
        {
            var key = StorageKey(input);
            var reference = _values.GetValueOrDefault(key, input.DefaultValue);
            if (!string.IsNullOrWhiteSpace(reference))
            {
                _values[key] = _database.ValidateComponentPresetReferenceValue(
                    projectId,
                    input.ComponentType,
                    reference);
                continue;
            }

            var firstPreset = ComponentPresetOptions(input, projectId)
                .FirstOrDefault((option) => !string.IsNullOrWhiteSpace(option.Value));
            if (firstPreset is not null)
            {
                _values[key] = firstPreset.Value;
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
            input.Id);
    }

    private IReadOnlyList<FieldOption> RecordReferenceOptions(ComponentInputDefinition input, string projectId)
    {
        return _recordInputResolver.Options(projectId, input.TableId, input.Id);
    }

    private IReadOnlyList<FieldOption> ComponentPresetOptions(ComponentInputDefinition input, string projectId)
    {
        return string.IsNullOrWhiteSpace(input.ComponentType)
            ? []
            : _database.GetComponentPresetReferenceOptionsByType(projectId, input.ComponentType);
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
        string uiParentGroupId)
    {
        var normalizedKind = ParseKind(kind);
        var normalizedValueKind = ParseValueKind(valueKind, normalizedKind);
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
            uiParentGroupId);
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
            UpdateActionButtons();
            return;
        }

        var activeAction = ActiveAction();
        if (activeAction is not null && IsPlaying(activeAction))
        {
            if (!_playbackTimer.IsEnabled)
            {
                _playbackTimer.Start();
            }
            UpdateActionButtons();
            return;
        }

        StopPlayback();
        UpdateActionButtons();
    }

    private void ApplyProjectFrameRate(string projectId)
    {
        var projectFps = _database.GetProjectSettings(projectId).DefaultFps;
        var previousFps = _playbackFrameRate;
        var previousInterval = _playbackTimer.Interval;
        var previewFps = PreviewPlaybackTiming.PreviewFrameRate(projectFps);
        _playbackFrameRate = previewFps;
        var interval = TimeSpan.FromMilliseconds(1000.0 / previewFps);
        if (previousFps != previewFps || previousInterval != interval)
        {
            PreviewDebugLog.Write(
                "preview.playback.fps",
                ("projectId", projectId),
                ("projectFps", projectFps),
                ("previewFps", previewFps),
                ("multiplier", PreviewPlaybackTiming.FrameRateMultiplier),
                ("intervalMs", interval.TotalMilliseconds));
        }
        if (_playbackTimer.Interval != interval)
        {
            _playbackTimer.Interval = interval;
        }
    }

    private void TogglePlayback(ComponentPreviewActionDefinition action)
    {
        var stateKey = ActionStateKey(action);
        var startsPlayback = !IsPlaying(action);
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
        SyncBooleanInput(stateKey);
        SyncPlaybackTimer();
        _refreshPreview();
    }

    private static void UpdateActionButtons() { }

    private async Task StartPlaybackAsync(ComponentPreviewActionDefinition action)
    {
        StopPlayback();
        PlaybackBusyChanged?.Invoke(true);
        _activeActionId = action.Id;
        var prepared = true;
        if (ShouldPrewarmFrames(action))
        {
            _preparingActionId = action.Id;
            UpdateActionButtons();
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
                if (_preparePlaybackFrames is not null && !await _preparePlaybackFrames())
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
                SyncBooleanInput(ActionStateKey(action));
                UpdateActionButtons();
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
                ("reason", action.PrewarmFrames ? "prewarm-condition" : "prewarm-disabled"));
        }

        if (!SupportsPlayback())
        {
            UpdateActionButtons();
            PlaybackBusyChanged?.Invoke(false);
            return;
        }

        SetPlaybackState(action, true);
        SyncBooleanInput(ActionStateKey(action));
        SyncActivatedPlaybackInputs(action);
        SyncDeactivatedPlaybackInputs(action);
        _values[ActionTimeKey(action)] = "0";
        _playbackStartedAtSeconds = 0;
        _playbackStartedTimestamp = Stopwatch.GetTimestamp();
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
        UpdateActionButtons();
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
        if (wasEnabled)
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
        var current = NormalizedPlaybackSeconds(activeAction, _playbackStartedAtSeconds + elapsed);
        _playbackSecondsByActionId[activeAction.Id] = current;
        _values[ActionTimeKey(activeAction)] = PlaybackTimeStorageValue(activeAction, current);
        PreviewDebugLog.Write(
            "preview.playback.tick",
            ("scope", _scopeKey),
            ("action", activeAction.Id),
            ("timeSec", current),
            ("frame", CurrentPlaybackFrame(activeAction)),
            ("durationSec", DurationSeconds(activeAction)),
            ("durationFrames", DurationFrames(activeAction)),
            ("fps", _playbackFrameRate));
        if (current >= DurationSeconds(activeAction))
        {
            _values[ActionStateKey(activeAction)] = "false";
            SyncBooleanInput(ActionStateKey(activeAction));
            StopPlayback();
            UpdateActionButtons();
        }

        _refreshPreview();
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
                : stored);
    }

    private double NextPlaybackFrameSeconds(ComponentPreviewActionDefinition action)
    {
        return NormalizedPlaybackSeconds(action, CurrentPlaybackSeconds(action) + 1.0 / Math.Max(1, _playbackFrameRate));
    }

    private void ClampCurrentPlaybackToDuration(ComponentPreviewActionDefinition action)
    {
        if (!SupportsPlayback()) return;

        var seconds = NormalizedPlaybackSeconds(action, CurrentPlaybackSeconds(action));
        _values[ActionTimeKey(action)] = PlaybackTimeStorageValue(action, seconds);
    }

    private double DurationSeconds(ComponentPreviewActionDefinition action)
    {
        if (action.TimeUnit == ComponentPreviewActionTimeUnit.Frames)
        {
            return DurationFrames(action) / (double)Math.Max(1, _playbackFrameRate);
        }

        if (action.DurationSeconds > 0)
        {
            return action.DurationSeconds;
        }

        var key = ActionDurationKey(action);
        return Math.Max(1, ParseDouble(_values.GetValueOrDefault(key, InputDefault(key, "1"))));
    }

    private int DurationFrames(ComponentPreviewActionDefinition action)
    {
        if (action.TimeUnit != ComponentPreviewActionTimeUnit.Frames)
        {
            return Math.Max(1, (int)Math.Ceiling(DurationSeconds(action) * Math.Max(1, _playbackFrameRate)));
        }

        if (!string.IsNullOrWhiteSpace(action.DurationCollectionJsonKey))
        {
            return CollectionDurationFrames(_runtimePreview, action);
        }

        var key = ActionDurationKey(action);
        return Math.Max(1, (int)Math.Round(ParseDouble(_values.GetValueOrDefault(key, InputDefault(key, "1"))), MidpointRounding.AwayFromZero));
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
            : NormalizedPlaybackSeconds(action, CurrentPlaybackSeconds(action));
    }

    private int CurrentPlaybackFrame(ComponentPreviewActionDefinition action)
    {
        var frame = (int)Math.Floor(CurrentPlaybackSeconds(action) * Math.Max(1, _playbackFrameRate) + 0.0001);
        return Math.Max(0, Math.Min(DurationFrames(action), frame));
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
            StopPlayback();
            foreach (var otherAction in _actions)
            {
                if (otherAction.Id == action.Id)
                {
                    continue;
                }

                _values[ActionStateKey(otherAction)] = "false";
                SyncBooleanInput(ActionStateKey(otherAction));
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
        return $"{_scopeKey}:{action.PlayInputId}";
    }

    private string ActionDurationKey(ComponentPreviewActionDefinition action)
    {
        var durationInputId = action.DurationInputId;
        return string.IsNullOrWhiteSpace(durationInputId)
            ? ""
            : $"{_scopeKey}:{durationInputId}";
    }

    private string ActionTimeKey(ComponentPreviewActionDefinition action)
    {
        return $"{_scopeKey}:{action.TimeJsonKey}";
    }

    private IEnumerable<string> ActivatedPlaybackInputKeys(ComponentPreviewActionDefinition action)
    {
        return action.ActivateInputIds
            .Where((id) => !string.IsNullOrWhiteSpace(id))
            .Select((id) => $"{_scopeKey}:{id}");
    }

    private void SyncActivatedPlaybackInputs(ComponentPreviewActionDefinition action)
    {
        foreach (var key in ActivatedPlaybackInputKeys(action))
        {
            SyncBooleanInput(key);
        }
    }

    private void SyncDeactivatedPlaybackInputs(ComponentPreviewActionDefinition action)
    {
        foreach (var inputId in action.DeactivateInputIds.Where((id) => !string.IsNullOrWhiteSpace(id)))
        {
            var key = $"{_scopeKey}:{inputId}";
            _values[key] = "false";
            SyncBooleanInput(key);
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

    private static void SyncBooleanInput(string key) { }

    private static double ParseDouble(string? value)
    {
        return double.TryParse((value ?? "").Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static JsonObject ParseJsonObject(string json)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    internal static IReadOnlyList<ComponentInputDefinition> ReadRuntimeInputs(JsonObject preview, JsonObject config)
    {
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

            definitions.Add(CreateInputDefinition(
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
                JsonString(item, "uiParentGroupId")) with
            {
                UiOrder = (int)JsonDecimal(item, "uiOrder", 0),
                UiSectionLabel = JsonString(item, "uiSectionLabel"),
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

                itemFields.Add(CreateInputDefinition(
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
                    JsonString(field, "uiParentGroupId")) with
                {
                    EnabledWhenItemJsonKey = JsonString(field, "enabledWhenItemJsonKey"),
                    EnabledWhenItemValues = JsonStringArray(field, "enabledWhenItemValues"),
                    UiOrder = (int)JsonDecimal(field, "uiOrder", 0),
                    UiSectionLabel = JsonString(field, "uiSectionLabel"),
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
                    JsonString(collection, "sourceCollectionJsonKey")));
            }
        }

        return definitions;
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

    private static string ActionSignature(ComponentPreviewActionDefinition action)
    {
        return string.Join(
            ":",
            "action",
            action.Id,
            action.Label,
            action.PlayInputId,
            action.DurationInputId,
            action.DurationSeconds.ToString(CultureInfo.InvariantCulture),
            action.DurationCollectionJsonKey,
            string.Join(",", action.DurationItemNumberKeys),
            string.Join(",", action.DurationCollectionMultiplierNumberKeys),
            action.DurationBaseFrames.ToString(CultureInfo.InvariantCulture),
            action.TimeJsonKey,
            action.TimeUnit,
            action.PrewarmFrames.ToString(CultureInfo.InvariantCulture),
            action.PrewarmWhenJsonKey,
            action.PrewarmWhenConfigPath,
            action.PrewarmWhenValue,
            string.Join(",", action.ActivateInputIds),
            string.Join(",", action.DeactivateInputIds));
    }

    private bool ShouldPrewarmFrames(ComponentPreviewActionDefinition action)
    {
        if (!action.PrewarmFrames)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(action.PrewarmWhenJsonKey)
            && string.IsNullOrWhiteSpace(action.PrewarmWhenConfigPath))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(action.PrewarmWhenJsonKey))
        {
            return _values.TryGetValue(action.PrewarmWhenJsonKey, out var current)
                && current.Equals(action.PrewarmWhenValue, StringComparison.Ordinal);
        }

        var configValue = JsonPath.Get(
            _config,
            action.PrewarmWhenConfigPath.Split('.', StringSplitOptions.RemoveEmptyEntries));
        return configValue is JsonValue value
            && value.TryGetValue<string>(out var text)
            && text.Equals(action.PrewarmWhenValue, StringComparison.Ordinal);
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
            "number" => ComponentInputKind.Number,
            "integerpair" or "integer_pair" or "size" => ComponentInputKind.IntegerPair,
            "boolean" => ComponentInputKind.Boolean,
            "option" => ComponentInputKind.Option,
            "recordreference" or "record_reference" => ComponentInputKind.RecordReference,
            "componentpreset" or "component_preset" => ComponentInputKind.ComponentPreset,
            "themetoken" or "theme_token" => ComponentInputKind.ThemeToken,
            "icon" => ComponentInputKind.Icon,
            "iconlist" or "icon_list" or "icons" => ComponentInputKind.IconList,
            "multilinetext" or "multiline_text" or "textmultiline" or "text_multiline" => ComponentInputKind.MultilineText,
            _ => ComponentInputKind.Text,
        };
    }

    private static ValueKind ParseValueKind(string valueKind, ComponentInputKind fallbackKind)
    {
        if (Enum.TryParse<ValueKind>(valueKind, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return fallbackKind switch
        {
            ComponentInputKind.Number => ValueKind.Decimal,
            ComponentInputKind.IntegerPair => ValueKind.IntegerPair,
            ComponentInputKind.Boolean => ValueKind.Boolean,
            ComponentInputKind.Option => ValueKind.OptionToken,
            ComponentInputKind.RecordReference => ValueKind.RecordReference,
            ComponentInputKind.ComponentPreset => ValueKind.ComponentPreset,
            ComponentInputKind.ThemeToken => ValueKind.ThemeToken,
            ComponentInputKind.Icon => ValueKind.IconToken,
            ComponentInputKind.IconList => ValueKind.IconTokenList,
            ComponentInputKind.MultilineText => ValueKind.StringMultiline,
            _ => ValueKind.StringSingleLine,
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
    ComponentPreset,
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
    int UiOrder = 0,
    string UiSectionLabel = "");

internal sealed record RuntimeInputCollectionDefinition(
    string Id,
    string Label,
    string JsonKey,
    string ItemLabel,
    IReadOnlyList<ComponentInputDefinition> Fields,
    string SourceCollectionJsonKey = "");
