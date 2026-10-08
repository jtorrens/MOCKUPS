using Avalonia.Threading;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Collections.Frozen;
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
    private readonly Action _refreshPreview;
    private readonly Action _refreshPlaybackFrame;
    private readonly Func<ComponentPreviewActionDefinition, Task<bool>>? _preparePlaybackFrames;
    private readonly DispatcherTimer _playbackTimer;
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _inputDefaults = new(StringComparer.Ordinal);
    private string _scopeKey = "";
    private string _projectId = "";
    private string _inputSignature = "";
    private IReadOnlyList<ComponentPreviewActionDefinition> _actions = [];
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _actionSignaturesByScope = new(StringComparer.Ordinal);
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
            RefreshCompletedPlayback(activeAction);
            return;
        }
        SyncPlaybackTimer();
    }

    public ComponentPreviewInputSession(
        Action refreshPreview,
        Action refreshPlaybackFrame,
        Func<ComponentPreviewActionDefinition, Task<bool>>? preparePlaybackFrames = null)
    {
        _refreshPreview = refreshPreview;
        _refreshPlaybackFrame = refreshPlaybackFrame;
        _preparePlaybackFrames = preparePlaybackFrames;
        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 50),
        };
        _playbackTimer.Tick += (_, _) => AdvancePlaybackFrame();
    }

    public DesignPreviewInputCapture CapturePreparation(ProjectTreeNode node)
    {
        var transient = CaptureTransientState(node, node.Kind == ProjectTreeNodeKind.ModuleInstance);
        return CapturePreparation(transient);
    }

    public bool IsPreparedFor(ProjectTreeNode node) =>
        _scopeKey.Length > 0 && _scopeKey == ComponentPreviewTransientValues.ScopeKey(
            node, node.Kind == ProjectTreeNodeKind.ModuleInstance);

    public DesignPreviewInputCapture CapturePreparation(DesignPreviewPayload payload)
    {
        var transient = CaptureTransientState(payload);
        return CapturePreparation(transient);
    }

    private DesignPreviewInputCapture CapturePreparation(ComponentPreviewTransientState transient) =>
        new(transient, transient.ScopeKey == _scopeKey ? _inputSignature : "",
            _actionSignaturesByScope.GetValueOrDefault(transient.ScopeKey, FrozenDictionary<string, string>.Empty));

    public void ClearPreparedContext()
    {
        StopPlayback();
        _scopeKey = "";
        _projectId = "";
        _inputSignature = "";
        _actions = [];
        _activeActionId = "";
        _heldFinalActionId = "";
        _config = [];
        _themeTokens = [];
        _runtimePreview = [];
    }

    public void ApplyPrepared(PreparedDesignPreviewInputs prepared)
    {
        if (prepared.ResetSession)
        {
            StopPlayback();
            ClearTransientContractValues(prepared.ScopeKey);
        }
        if (_scopeKey == prepared.ScopeKey && prepared.ResetActionIds.Contains(_activeActionId))
        {
            StopPlayback();
            _activeActionId = "";
        }
        var owner = DesignPreviewPayloadLayers.PrimaryOwner(prepared.Payload);
        _scopeKey = prepared.ScopeKey;
        _projectId = owner.ProjectId;
        _inputSignature = prepared.InputSignature;
        _config = ParseJsonObject(owner.ConfigJson);
        _themeTokens = ParseJsonObject(owner.ThemeTokensJson);
        _runtimePreview = ParseJsonObject(prepared.RuntimeJson);
        _actions = prepared.Actions;
        _actionSignaturesByScope[_scopeKey] = prepared.ActionSignatures;
        ApplyProjectFrameRate(owner.FrameRate);
        foreach (var actionId in prepared.ResetActionIds)
        {
            foreach (var key in ComponentPreviewTransientValues.ActionKeys(_scopeKey, actionId)) _values.Remove(key);
            _actionSnapshots.Remove(ActionSnapshotKey(actionId));
            _playbackSecondsByActionId.Remove(actionId);
            if (_heldFinalActionId == actionId) _heldFinalActionId = "";
        }
        foreach (var (key, value) in prepared.Values) _values[key] = value;
        foreach (var input in RuntimeInputDefinitionReader.ReadInputs(_runtimePreview, _config))
            _inputDefaults[StorageKey(input)] = input.DefaultValue;
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
        return _actions.Any((candidate) => candidate.Id == actionId);
    }

    public bool IsActionPlaying(string actionId)
    {
        return _actions.FirstOrDefault((candidate) => candidate.Id == actionId) is { } action
            && IsPlaying(action)
            && _heldFinalActionId != action.Id;
    }

    public bool CanStepActionFrame(string actionId, int delta)
    {
        return delta is -1 or 1
            && _actions.Any((candidate) => candidate.Id == actionId);
    }

    public int CurrentActionFrame(string actionId)
    {
        return _actions.FirstOrDefault((candidate) => candidate.Id == actionId) is { } action
            ? CurrentPlaybackFrame(action)
            : 0;
    }

    public int MaximumActionFrame(string actionId)
    {
        return _actions.FirstOrDefault((candidate) => candidate.Id == actionId) is { } action
            ? DurationFrames(action)
            : 0;
    }

    public bool StepActionFrame(
        string actionId,
        int delta,
        string? targetValue = null)
    {
        if (delta is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(delta),
                delta,
                "Design Preview action frame steps must be -1 or 1.");
        }
        var action = _actions.FirstOrDefault((candidate) => candidate.Id == actionId);
        return action is not null
            && SetActionFrame(
                actionId,
                CurrentPlaybackFrame(action) + delta,
                targetValue);
    }

    public bool SetActionFrame(
        string actionId,
        int requestedFrame,
        string? targetValue = null)
    {
        var action = _actions.FirstOrDefault((candidate) => candidate.Id == actionId);
        if (action is null || IsPreparingPlayback)
        {
            return false;
        }

        var targetFrame = Math.Clamp(requestedFrame, 0, DurationFrames(action));
        var hasSnapshot = _actionSnapshots.ContainsKey(ActionSnapshotKey(action.Id));
        CaptureActionSnapshot(action);
        StopPlayback(clearPlayingState: true);
        if (!hasSnapshot)
        {
            ApplyActionTarget(action, targetValue);
        }
        _activeActionId = action.Id;
        _playbackSecondsByActionId.Remove(action.Id);
        _values[ActionTimeKey(action)] = PlaybackTimeStorageValue(
            action,
            targetFrame / (double)Math.Max(1, _playbackFrameRate));
        _values[ActionStateKey(action)] = "true";
        foreach (var key in ActivatedPlaybackInputKeys(action))
        {
            _values[key] = "true";
        }
        SyncDeactivatedPlaybackInputs(action);
        _heldFinalActionId = action.Id;
        _refreshPreview();
        return true;
    }

    public bool RestoreAction(string actionId)
    {
        var action = _actions.FirstOrDefault((candidate) => candidate.Id == actionId);
        if (action is null || IsPreparingPlayback)
        {
            return false;
        }

        CaptureActionSnapshot(action);
        var snapshot = _actionSnapshots[ActionSnapshotKey(actionId)];
        StopPlayback(clearPlayingState: true);
        ApplyActionSnapshot(snapshot);
        _playbackSecondsByActionId.Remove(action.Id);
        _values[ActionTimeKey(action)] = PlaybackTimeStorageValue(action, 0);
        _values[ActionStateKey(action)] = "true";
        foreach (var key in ActivatedPlaybackInputKeys(action))
        {
            _values[key] = "true";
        }
        SyncDeactivatedPlaybackInputs(action);
        _activeActionId = action.Id;
        _heldFinalActionId = action.Id;
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

    public void SetOwnerOverrideValue(ProjectTreeNode node, string jsonKey, string value, bool isCollection)
    {
        var scope = ComponentPreviewTransientValues.ScopeKey(node, isInstance: node.Kind == ProjectTreeNodeKind.ModuleInstance);
        if (scope.Length == 0) throw new InvalidOperationException("Test Values require an exact Runtime owner.");
        if (isCollection)
        {
            var current = _transientCollectionTestValuesByScope.GetValueOrDefault(scope) ?? new JsonObject();
            current[jsonKey] = JsonPath.ParseRequiredArray(value, "Override collection Test Values");
            _transientCollectionTestValuesByScope[scope] = current;
        }
        else _values[$"{scope}:{jsonKey}"] = value;
        _refreshPreview();
    }

    public void ApplyRuntimeValueEdit(ProjectTreeNode node, PreparedRuntimeValueEdit edit)
    {
        var scope = ComponentPreviewTransientValues.ScopeKey(node, node.Kind == ProjectTreeNodeKind.ModuleInstance);
        if (scope.Length == 0) throw new InvalidOperationException("Runtime edits require an exact owner.");
        var collections = edit.Collections.ToDictionary(pair => pair.Key,
            pair => JsonPath.ParseRequiredArray(pair.Value, "Prepared Runtime collection"), StringComparer.Ordinal);
        if (collections.Count > 0)
        {
            var current = _transientCollectionTestValuesByScope.GetValueOrDefault(scope) ?? new JsonObject();
            foreach (var (key, items) in collections) current[key] = items;
            _transientCollectionTestValuesByScope[scope] = current;
        }
        _values[$"{scope}:{edit.JsonKey}"] = edit.Value;
        _refreshPreview();
    }

    public void DiscardExternalInputValue(ProjectTreeNode node, string jsonKey)
    {
        var scope = ComponentPreviewTransientValues.ScopeKey(node, node.Kind == ProjectTreeNodeKind.ModuleInstance);
        if (scope.Length == 0) throw new InvalidOperationException("Runtime edits require an exact owner.");
        var key = $"{scope}:{jsonKey}";
        _values.Remove(key);
        _inputDefaults.Remove(key);
    }

    public void DiscardExternalCollectionValues(
        ProjectTreeNode node,
        string rootStorageJsonKey)
    {
        var scope = ComponentPreviewTransientValues.ScopeKey(node, node.Kind == ProjectTreeNodeKind.ModuleInstance);
        if (scope.Length == 0) throw new InvalidOperationException("Runtime edits require an exact owner.");
        if (!_transientCollectionTestValuesByScope.TryGetValue(
                scope,
                out var testValues))
        {
            return;
        }

        testValues.Remove(rootStorageJsonKey);
        if (testValues.Count == 0)
        {
            _transientCollectionTestValuesByScope.Remove(
                scope);
        }
    }

    public void SetExternalCollectionItems(
        DesignPreviewPayload payload,
        string collectionJsonKey,
        IReadOnlyList<JsonObject> items)
    {
        if (!SupportsInputs(payload) || string.IsNullOrWhiteSpace(collectionJsonKey)) return;
        SetCollectionItems(ScopeKey(payload), collectionJsonKey, items);
    }

    private void SetCollectionItems(string scopeKey, string collectionJsonKey, IReadOnlyList<JsonObject> items)
    {
        var testValues = _transientCollectionTestValuesByScope.GetValueOrDefault(scopeKey) ?? new JsonObject();
        _transientCollectionTestValuesByScope[scopeKey] = testValues;
        testValues[collectionJsonKey] = new JsonArray(items.Select((item) => (JsonNode?)item.DeepClone()).ToArray());
        _refreshPreview();
    }

    public ComponentPreviewTransientState CaptureTransientState(
        DesignPreviewPayload payload) =>
        CaptureTransientState(
            ScopeKey(payload));

    public ComponentPreviewTransientState CaptureTransientState(
        ProjectTreeNode node,
        bool isInstance) =>
        CaptureTransientState(
            ComponentPreviewTransientValues.ScopeKey(
                node,
                isInstance));

    public bool ResetCurrentTestValues()
    {
        return ResetTestValues(_scopeKey);
    }

    public bool ResetTestValues(DesignPreviewPayload payload) => ResetTestValues(ScopeKey(payload));

    public bool ResetTestValues(ProjectTreeNode node) =>
        ResetTestValues(ComponentPreviewTransientValues.ScopeKey(node, node.Kind == ProjectTreeNodeKind.ModuleInstance));

    public void AcknowledgeSavedTestValues(ComponentPreviewTransientState saved)
    {
        if (string.IsNullOrWhiteSpace(saved.ScopeKey))
            throw new InvalidOperationException("Saved Test Values require an exact owner.");
        var prefix = $"{saved.ScopeKey}:";
        if (saved.Values.Keys.Any(key => !key.StartsWith(prefix, StringComparison.Ordinal)))
            throw new InvalidOperationException("Saved Test Values contain a different owner.");
        var savedCollections = JsonPath.ParseRequiredObject(saved.CollectionTestValuesJson, "Saved Test Values collections");
        foreach (var (key, value) in saved.Values)
        {
            if (_values.TryGetValue(key, out var current) && current == value)
                _values.Remove(key);
        }
        if (_transientCollectionTestValuesByScope.TryGetValue(saved.ScopeKey, out var currentCollections))
        {
            foreach (var (key, value) in savedCollections)
            {
                if (currentCollections.TryGetPropertyValue(key, out var current) && JsonNode.DeepEquals(current, value))
                    currentCollections.Remove(key);
            }
            if (currentCollections.Count == 0) _transientCollectionTestValuesByScope.Remove(saved.ScopeKey);
        }
        // Defaults can also feed another Variant or an embedded dependency.
        // Invalidate prepared metadata, never those owners' temporary authoring.
        _inputSignature = "";
        _inputDefaults.Clear();
        _refreshPreview();
    }

    public void SetExternalCollectionItems(ProjectTreeNode node, string collectionJsonKey, IReadOnlyList<JsonObject> items)
    {
        var scope = ComponentPreviewTransientValues.ScopeKey(node, node.Kind == ProjectTreeNodeKind.ModuleInstance);
        if (scope.Length == 0) throw new InvalidOperationException("Test Values require an exact Runtime owner.");
        SetCollectionItems(scope, collectionJsonKey, items);
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

    private ComponentPreviewTransientState CaptureTransientState(
        string scopeKey) =>
        ComponentPreviewTransientState.Capture(
            scopeKey,
            _values,
            _transientCollectionTestValuesByScope);

    private static bool SupportsInputs(DesignPreviewPayload payload)
    {
        return DesignPreviewPayloadLayers.PrimaryOwner(payload).Kind
            is "componentClass" or "module" or "moduleInstance";
    }

    private static string ScopeKey(DesignPreviewPayload payload) =>
        ComponentPreviewTransientValues.ScopeKey(
            DesignPreviewPayloadLayers.PrimaryOwner(payload));

    private string StorageKey(ComponentInputDefinition input) => $"{_scopeKey}:{input.JsonKey}";

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

    private void ApplyProjectFrameRate(int projectFps)
    {
        var previousFps = _playbackFrameRate;
        var previousInterval = _playbackTimer.Interval;
        var previewFps = PreviewPlaybackTiming.PreviewFrameRate(projectFps);
        _playbackFrameRate = previewFps;
        var interval = PlaybackTimerInterval(previewFps);
        if (previousFps != previewFps || previousInterval != interval)
        {
            PreviewDebugLog.Write(
                "preview.playback.fps",
                ("projectId", _projectId),
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
        _refreshPlaybackFrame();
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
            _refreshPlaybackFrame();
        }

        if (completesPlayback)
        {
            if (_presentEveryPlaybackFrame)
            {
                return;
            }
            CompletePlayback(activeAction);
            RefreshCompletedPlayback(activeAction);
        }
    }

    private void RefreshCompletedPlayback(ComponentPreviewActionDefinition? action)
    {
        if (action?.CompletionBehavior == ComponentPreviewActionCompletionBehavior.HoldFinal)
            _refreshPlaybackFrame();
        else
            _refreshPreview();
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

    private double DurationSeconds(ComponentPreviewActionDefinition action) =>
        ComponentPreviewActionRuntimeValue.DurationSeconds(action, _runtimePreview, _playbackFrameRate, _themeTokens.ToJsonString());

    private int DurationFrames(ComponentPreviewActionDefinition action) =>
        ComponentPreviewActionRuntimeValue.DurationFrames(action, _runtimePreview, _playbackFrameRate, _themeTokens.ToJsonString());

    private int CurrentPlaybackFrame(ComponentPreviewActionDefinition action)
    {
        if (CurrentPlaybackSeconds(action) >= DurationSeconds(action))
        {
            return DurationFrames(action);
        }
        var frame = (int)Math.Floor(CurrentPlaybackSeconds(action) * Math.Max(1, _playbackFrameRate) + 0.0001);
        return Math.Max(0, Math.Min(DurationFrames(action), frame));
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
        return ComponentPreviewTransientValues.ActionStateKey(_scopeKey, action.Id);
    }

    private string ActionTimeKey(ComponentPreviewActionDefinition action)
    {
        return ComponentPreviewTransientValues.ActionTimeKey(_scopeKey, action.Id);
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
        ComponentPreviewTransientValues.ActionTargetFromKey(_scopeKey, action.Id);

    private string ActionTargetStorageKey(ComponentPreviewActionDefinition action) =>
        action.IsCollectionItemAction
            ? ComponentPreviewTransientValues.ActionTargetValueKey(_scopeKey, action.Id)
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


}
