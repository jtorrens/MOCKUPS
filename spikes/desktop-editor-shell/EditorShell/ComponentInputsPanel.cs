using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ComponentInputsPanel : ContentControl
{
    private readonly SpikeDatabase _database;
    private readonly Action _refreshPreview;
    private readonly Window _owner;
    private readonly DispatcherTimer _playbackTimer;
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _inputDefaults = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DictionaryFieldControl> _booleanInputs = new(StringComparer.Ordinal);
    private Button? _playbackButton;
    private string _scopeKey = "";
    private string _projectId = "";
    private string _inputSignature = "";
    private ComponentInputAnimation? _animation;
    private DateTime _playbackStartedAtUtc;
    private double _playbackStartSeconds;
    private bool _isUpdating;

    public ComponentInputsPanel(SpikeDatabase database, Action refreshPreview, Window owner)
    {
        _database = database;
        _refreshPreview = refreshPreview;
        _owner = owner;
        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(80),
        };
        _playbackTimer.Tick += (_, _) => AdvancePlaybackFrame();
        IsVisible = false;
    }

    public void UpdateForPayload(DesignPreviewPayload? payload, string? projectId)
    {
        _isUpdating = true;
        try
        {
            if (payload is null || payload.Kind != "componentClass" || string.IsNullOrWhiteSpace(projectId))
            {
                IsVisible = false;
                _scopeKey = "";
                _projectId = "";
                _inputSignature = "";
                Content = null;
                StopPlayback();
                return;
            }

            var preview = ParseJsonObject(payload.DesignPreviewJson);
            var config = ParseJsonObject(payload.ConfigJson);
            var inputs = ReadInputs(preview, config).ToList();
            _animation = ReadAnimation(preview);
            if (inputs.Count == 0)
            {
                IsVisible = false;
                _scopeKey = "";
                _projectId = "";
                _inputSignature = "";
                _animation = null;
                Content = null;
                StopPlayback();
                return;
            }

            IsVisible = true;
            var scopeKey = ScopeKey(payload);
            var inputSignature = string.Join("|", inputs.Select(InputSignature));
            var shouldRebuild = scopeKey != _scopeKey
                || projectId != _projectId
                || inputSignature != _inputSignature
                || Content is null;
            _scopeKey = scopeKey;
            _projectId = projectId;
            _inputSignature = inputSignature;
            foreach (var input in inputs)
            {
                EnsureValue(input, preview);
            }
            EnsureRecordReferenceValues(inputs, projectId);
            if (shouldRebuild)
            {
                RebuildCard(inputs, projectId);
            }
            SyncPlaybackTimer();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void RebuildCard(IReadOnlyList<ComponentInputDefinition> inputs, string projectId)
    {
        var contentPanel = new StackPanel
        {
            Spacing = 10,
        };
        _booleanInputs.Clear();

        var ownInputs = inputs
            .Where((input) => input.UiOrigin != ComponentInputUiOrigin.Embedded)
            .ToList();
        var embeddedGroups = inputs
            .Where((input) => input.UiOrigin == ComponentInputUiOrigin.Embedded)
            .GroupBy((input) => string.IsNullOrWhiteSpace(input.UiGroupId) ? input.Id : input.UiGroupId)
            .ToList();

        if (ownInputs.Count > 0)
        {
            contentPanel.Children.Add(CreateInputRowsPanel(ownInputs, projectId, maxVisibleRows: 5));
        }

        foreach (var group in embeddedGroups)
        {
            var groupInputs = group.ToList();
            var groupLabel = groupInputs
                .Select((input) => input.UiGroupLabel)
                .FirstOrDefault((label) => !string.IsNullOrWhiteSpace(label)) ?? "Embedded inputs";
            contentPanel.Children.Add(new InstantEditorCard(
                EditorCardHeader.Create(groupLabel, "Embedded control inputs", EditorIcons.Create(EditorIcons.Component, 16)),
                new Border
                {
                    Padding = new Thickness(10),
                    Child = CreateInputRowsPanel(groupInputs, projectId, maxVisibleRows: 5),
                },
                isExpanded: false)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            });
        }

        var icon = EditorIcons.Create(EditorIcons.Design, 18);
        Content = new InstantEditorCard(
            CreateHeader(icon),
            new Border
            {
                Padding = new Thickness(10),
                Child = contentPanel,
            },
            isExpanded: true)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        UpdatePlaybackButton();
    }

    private Control CreateInputRowsPanel(
        IReadOnlyList<ComponentInputDefinition> inputs,
        string projectId,
        int maxVisibleRows)
    {
        var rowsPanel = new StackPanel
        {
            Spacing = 8,
        };
        foreach (var input in inputs)
        {
            rowsPanel.Children.Add(CreateInputRow(input, projectId));
        }

        return new ScrollViewer
        {
            MaxHeight = maxVisibleRows * 46,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = rowsPanel,
        };
    }

    private Control CreateHeader(Control icon)
    {
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Children.Add(EditorCardHeader.Create("Inputs", "Component input values", icon));
        if (SupportsPlayback())
        {
            _playbackButton = new Button
            {
                MinWidth = 72,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _playbackButton.Click += (_, args) =>
            {
                args.Handled = true;
                TogglePlayback();
            };
            Grid.SetColumn(_playbackButton, 1);
            header.Children.Add(_playbackButton);
        }
        else
        {
            _playbackButton = null;
        }

        return header;
    }

    public DesignPreviewPayload ApplyInputs(DesignPreviewPayload payload, string themeMode, string? projectId)
    {
        if (payload.Kind != "componentClass")
        {
            return payload;
        }

        var preview = ParseJsonObject(payload.DesignPreviewJson);
        var config = ParseJsonObject(payload.ConfigJson);
        var inputs = ReadInputs(preview, config).ToList();
        _animation = ReadAnimation(preview);
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
        if (SupportsPlayback())
        {
            preview[_animation!.TimeJsonKey] = NormalizedPlaybackSeconds(CurrentPlaybackSeconds());
        }

        return payload with { DesignPreviewJson = preview.ToJsonString() };
    }

    private static string ScopeKey(DesignPreviewPayload payload)
    {
        return $"{payload.Kind}:{payload.ComponentType}:{payload.Name}";
    }

    private Control CreateInputRow(ComponentInputDefinition input, string projectId)
    {
        return CreateDictionaryInput(input, projectId);
    }

    private Control CreateDictionaryInput(ComponentInputDefinition input, string projectId)
    {
        var field = new DictionaryFieldControl(
            new FieldValue(
                CreateFieldDefinition(input, projectId),
                Value(input)),
            CreateDictionaryServices(projectId));
        if (input.Kind == ComponentInputKind.Boolean)
        {
            _booleanInputs[StorageKey(input)] = field;
        }

        field.ValueChanged += (_, value) =>
        {
            if (_isUpdating) return;
            SetValue(input, value);
        };
        field.ValueCommitted += (_, value) =>
        {
            if (_isUpdating) return;
            SetValue(input, value);
        };
        return field;
    }

    private FieldDefinition CreateFieldDefinition(ComponentInputDefinition input, string projectId)
    {
        return new FieldDefinition(
            input.Id,
            input.Label,
            input.ValueKind,
            DefaultValue: input.DefaultValue,
            Options: input.ValueKind switch
            {
                ValueKind.RecordReference => RecordReferenceOptions(input, projectId),
                ValueKind.ComponentPreset => ComponentPresetOptions(input, projectId),
                ValueKind.PaletteColorToken => _database.GetPaletteColorOptions(projectId),
                _ => input.Options,
            },
            PairLabels: input.ValueKind == ValueKind.IntegerPair ? input.PairLabels : null,
            Number: input.ValueKind is ValueKind.Decimal or ValueKind.Integer or ValueKind.Alpha
                ? new NumberDefinition(input.Minimum, input.Maximum, input.Increment, input.ValueKind == ValueKind.Integer ? 0 : 2)
                : null,
            RecordReference: input.ValueKind == ValueKind.RecordReference
                ? new RecordReferenceDefinition(input.TableId)
                : null);
    }

    private DictionaryFieldServices CreateDictionaryServices(string projectId)
    {
        return new DictionaryFieldServices(
            ShowIconTokenPicker: (currentValue, allowMultiple) =>
                new IconTokenPickerDialog(_owner, _database).Show(projectId, currentValue, allowMultiple),
            ShowThemeTokenPicker: (currentValue, allowedOptions) =>
                new ThemeTokenPickerDialog(_owner, _database).Show(projectId, currentValue, allowedOptions),
            CreateIconPreview: (token) =>
                SvgIconPreview.CreateProjectIconTokenPreview(_database, projectId, token, 18),
            GetPaletteColorOptions: () =>
                _database.GetPaletteColorOptions(projectId),
            GetComponentPresetOptions: (componentType) =>
                _database.GetComponentPresetReferenceOptionsByType(projectId, componentType));
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
            if (!string.IsNullOrWhiteSpace(_values.GetValueOrDefault(key)))
            {
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

        preview[input.ResolvedJsonKey] = input.TableId switch
        {
            "actors" => !string.IsNullOrWhiteSpace(value)
                ? ActorPreviewInputFactory.Create(_database, value, themeMode, paletteColors)
                : ActorPreviewInputFactory.CreateSample(),
            _ => throw new InvalidOperationException(
                $"Unsupported record reference input table '{input.TableId}' for '{input.Id}'."),
        };
    }

    private IReadOnlyList<FieldOption> RecordReferenceOptions(ComponentInputDefinition input, string projectId)
    {
        return input.TableId switch
        {
            "actors" => _database.GetActorOptions(projectId),
            _ => throw new InvalidOperationException(
                $"Unsupported record reference input table '{input.TableId}' for '{input.Id}'."),
        };
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
        string uiGroupLabel)
    {
        var normalizedKind = ParseKind(kind);
        var normalizedValueKind = ParseValueKind(valueKind, normalizedKind);
        var normalizedTableId = tableId;
        var normalizedResolvedJsonKey = resolvedJsonKey;
        if (kind.Trim().Equals("actor", StringComparison.OrdinalIgnoreCase))
        {
            normalizedKind = ComponentInputKind.RecordReference;
            normalizedValueKind = ValueKind.RecordReference;
            normalizedTableId = string.IsNullOrWhiteSpace(tableId) ? "actors" : tableId;
            normalizedResolvedJsonKey = string.IsNullOrWhiteSpace(resolvedJsonKey) ? "actor" : resolvedJsonKey;
        }

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
            normalizedTableId,
            normalizedResolvedJsonKey,
            componentType,
            source,
            pairLabels,
            uiOrigin,
            uiGroupId,
            uiGroupLabel);
    }

    private string Value(ComponentInputDefinition input)
    {
        return _values.TryGetValue(StorageKey(input), out var value) ? value : input.DefaultValue;
    }

    private void SetValue(ComponentInputDefinition input, string value)
    {
        if (SupportsPlayback() && input.Id == _animation!.PlayInputId)
        {
            SetPlaybackState(StringToBool(value));
        }
        else
        {
            _values[StorageKey(input)] = value;
        }

        if (SupportsPlayback() && input.Id == _animation!.DurationInputId)
        {
            ClampCurrentPlaybackToDuration();
        }
        SyncPlaybackTimer();
        _refreshPreview();
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
            UpdatePlaybackButton();
            return;
        }

        if (IsPlaying())
        {
            if (!_playbackTimer.IsEnabled)
            {
                _playbackStartSeconds = CurrentPlaybackSeconds();
                _playbackStartedAtUtc = DateTime.UtcNow;
                _playbackTimer.Start();
            }
            UpdatePlaybackButton();
            return;
        }

        StopPlayback();
        UpdatePlaybackButton();
    }

    private void TogglePlayback()
    {
        if (!SupportsPlayback()) return;

        var stateKey = PlaybackStateKey();
        SetPlaybackState(!IsPlaying());

        SyncBooleanInput(stateKey);
        SyncPlaybackTimer();
        _refreshPreview();
    }

    private void UpdatePlaybackButton()
    {
        if (_playbackButton is null) return;

        _playbackButton.Content = IsPlaying() ? "Pause" : "Play";
    }

    private void StopPlayback()
    {
        if (!_playbackTimer.IsEnabled) return;

        _playbackTimer.Stop();
    }

    private void AdvancePlaybackFrame()
    {
        if (!SupportsPlayback()
            || !IsPlaying())
        {
            StopPlayback();
            return;
        }

        _values[PlaybackTimeKey()] = NormalizedPlaybackSeconds(CurrentPlaybackSeconds()).ToString(CultureInfo.InvariantCulture);
        _refreshPreview();
    }

    private double CurrentPlaybackSeconds()
    {
        var duration = DurationSeconds();
        var stored = ParseDouble(_values.GetValueOrDefault(PlaybackTimeKey(), "0"));
        if (!_playbackTimer.IsEnabled)
        {
            return NormalizedPlaybackSeconds(stored);
        }

        var elapsed = (DateTime.UtcNow - _playbackStartedAtUtc).TotalSeconds;
        return NormalizedPlaybackSeconds(_playbackStartSeconds + elapsed);
    }

    private void ClampCurrentPlaybackToDuration()
    {
        if (!SupportsPlayback()) return;

        _values[PlaybackTimeKey()] = NormalizedPlaybackSeconds(CurrentPlaybackSeconds()).ToString(CultureInfo.InvariantCulture);
    }

    private double DurationSeconds()
    {
        var key = PlaybackDurationKey();
        return Math.Max(1, ParseDouble(_values.GetValueOrDefault(key, InputDefault(key, "1"))));
    }

    private bool IsPlaying()
    {
        var key = PlaybackStateKey();
        return StringToBool(_values.GetValueOrDefault(key, InputDefault(key, "false")));
    }

    private void SetPlaybackState(bool isPlaying)
    {
        var stateKey = PlaybackStateKey();
        if (isPlaying)
        {
            _playbackStartSeconds = NormalizedPlaybackSeconds(CurrentPlaybackSeconds());
            _playbackStartedAtUtc = DateTime.UtcNow;
            _values[stateKey] = "true";
            return;
        }

        _values[PlaybackTimeKey()] = NormalizedPlaybackSeconds(CurrentPlaybackSeconds()).ToString(CultureInfo.InvariantCulture);
        _values[stateKey] = "false";
    }

    private double NormalizedPlaybackSeconds(double seconds)
    {
        return PositiveModulo(seconds, DurationSeconds());
    }

    private bool SupportsPlayback()
    {
        return _animation is not null;
    }

    private string PlaybackStateKey()
    {
        return $"{_scopeKey}:{PlaybackAnimation().PlayInputId}";
    }

    private string PlaybackDurationKey()
    {
        return $"{_scopeKey}:{PlaybackAnimation().DurationInputId}";
    }

    private string PlaybackTimeKey()
    {
        return $"{_scopeKey}:{PlaybackAnimation().TimeJsonKey}";
    }

    private ComponentInputAnimation PlaybackAnimation()
    {
        return _animation
            ?? throw new InvalidOperationException("Playback animation is not available for the active component inputs.");
    }

    private string InputDefault(string key, string defaultValue)
    {
        return _inputDefaults.GetValueOrDefault(key, defaultValue);
    }

    private void SyncBooleanInput(string key)
    {
        if (!_booleanInputs.TryGetValue(key, out var input)) return;

        _isUpdating = true;
        try
        {
            input.SetValue(_values.GetValueOrDefault(key, InputDefault(key, "false")));
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private static double ParseDouble(string? value)
    {
        return double.TryParse((value ?? "").Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static double PositiveModulo(double value, double divisor)
    {
        if (divisor <= 0) return 0;
        var result = value % divisor;
        return result < 0 ? result + divisor : result;
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

    private static IEnumerable<ComponentInputDefinition> ReadInputs(JsonObject preview, JsonObject config)
    {
        if (preview["inputs"] is not JsonArray inputs)
        {
            yield break;
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

            yield return CreateInputDefinition(
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
                JsonString(item, "uiGroupLabel"));
        }
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
            string.Join(",", input.Options?.Select((option) => $"{option.Value}={option.Label}") ?? []));
    }

    private static ComponentInputAnimation? ReadAnimation(JsonObject preview)
    {
        if (preview["animation"] is not JsonObject animation)
        {
            return null;
        }

        var playInputId = JsonString(animation, "playInputId");
        var durationInputId = JsonString(animation, "durationInputId");
        var timeJsonKey = JsonString(animation, "timeJsonKey");
        if (string.IsNullOrWhiteSpace(playInputId)
            || string.IsNullOrWhiteSpace(durationInputId)
            || string.IsNullOrWhiteSpace(timeJsonKey))
        {
            return null;
        }

        return new ComponentInputAnimation(playInputId, durationInputId, timeJsonKey);
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
    string UiGroupLabel = "");

internal sealed record ComponentInputAnimation(
    string PlayInputId,
    string DurationInputId,
    string TimeJsonKey);
