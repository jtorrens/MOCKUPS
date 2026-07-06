using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
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
    private readonly DispatcherTimer _playbackTimer;
    private readonly StackPanel _rowsPanel;
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ToggleSwitch> _booleanInputs = new(StringComparer.Ordinal);
    private Button? _playbackButton;
    private string _scopeKey = "";
    private string _componentType = "";
    private string _projectId = "";
    private string _inputSignature = "";
    private ComponentInputAnimation? _animation;
    private DateTime _playbackStartedAtUtc;
    private double _playbackStartSeconds;
    private bool _isUpdating;

    public ComponentInputsPanel(SpikeDatabase database, Action refreshPreview)
    {
        _database = database;
        _refreshPreview = refreshPreview;
        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(80),
        };
        _playbackTimer.Tick += (_, _) => AdvancePlaybackFrame();
        IsVisible = false;

        _rowsPanel = new StackPanel
        {
            Spacing = 8,
        };
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
                _componentType = "";
                _projectId = "";
                _inputSignature = "";
                Content = null;
                StopPlayback();
                return;
            }

            var preview = ParseJsonObject(payload.DesignPreviewJson);
            var inputs = ReadInputs(preview).ToList();
            _animation = ReadAnimation(preview);
            if (inputs.Count == 0)
            {
                IsVisible = false;
                _scopeKey = "";
                _componentType = "";
                _projectId = "";
                _inputSignature = "";
                _animation = null;
                Content = null;
                StopPlayback();
                return;
            }

            IsVisible = true;
            var scopeKey = ScopeKey(payload);
            var inputSignature = string.Join("|", inputs.Select((input) => input.Id));
            var shouldRebuild = scopeKey != _scopeKey
                || projectId != _projectId
                || inputSignature != _inputSignature
                || Content is null;
            _scopeKey = scopeKey;
            _componentType = payload.ComponentType;
            _projectId = projectId;
            _inputSignature = inputSignature;
            foreach (var input in inputs)
            {
                EnsureValue(input, preview);
            }
            EnsureActorValues(inputs, projectId);
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
        _rowsPanel.Children.Clear();
        _booleanInputs.Clear();
        foreach (var input in inputs)
        {
            _rowsPanel.Children.Add(CreateInputRow(input, projectId));
        }

        var icon = EditorIcons.Create(EditorIcons.Design, 18);
        Content = new InstantEditorCard(
            CreateHeader(icon),
            new Border
            {
                Padding = new Thickness(10),
                Child = new ScrollViewer
                {
                    MaxHeight = 5 * 46,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = _rowsPanel,
                },
            },
            isExpanded: true)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        UpdatePlaybackButton();
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
        var inputs = ReadInputs(preview).ToList();
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
            EnsureActorValues(inputs, effectiveProjectId);
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
                case ComponentInputKind.Actor:
                    preview[input.JsonKey] = value;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        preview["actor"] = ActorPreviewInputFactory.Create(_database, value, themeMode, payload.PaletteColors);
                    }
                    else
                    {
                        preview["actor"] = ActorPreviewInputFactory.CreateSample();
                    }
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
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("120,*"),
            ColumnSpacing = 10,
            MinHeight = 38,
        };
        row.Children.Add(new TextBlock
        {
            Text = input.Label,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold,
            FontSize = 12,
            Opacity = 0.86,
        });

        var control = input.Kind switch
        {
            ComponentInputKind.Option => CreateOptionInput(input),
            ComponentInputKind.Actor => CreateActorInput(input, projectId),
            ComponentInputKind.Number => CreateNumberInput(input),
            ComponentInputKind.Boolean => CreateBooleanInput(input),
            _ => CreateTextInput(input),
        };
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        return row;
    }

    private Control CreateOptionInput(ComponentInputDefinition input)
    {
        var options = input.Options ?? [];
        var combo = new EditorInstantComboBox
        {
            MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        combo.ItemsSource = options;
        combo.SelectedItem = options.FirstOrDefault((option) => option.Value == Value(input)) ?? options.FirstOrDefault();
        combo.SelectionChanged += (_, _) =>
        {
            if (_isUpdating || combo.SelectedItem is null) return;
            SetValue(input, combo.SelectedItem.Value);
        };
        return combo;
    }

    private Control CreateActorInput(ComponentInputDefinition input, string projectId)
    {
        var combo = new EditorInstantComboBox
        {
            MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var options = _database.GetActorOptions(projectId).ToList();
        combo.ItemsSource = options;
        var selected = options.FirstOrDefault((option) => option.Value == Value(input))
            ?? options.FirstOrDefault((option) => !string.IsNullOrWhiteSpace(option.Value))
            ?? options.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(Value(input)) && selected is not null)
        {
            _values[StorageKey(input)] = selected.Value;
        }
        combo.SelectedItem = selected;
        combo.SelectionChanged += (_, _) =>
        {
            if (_isUpdating || combo.SelectedItem is null) return;
            SetValue(input, combo.SelectedItem.Value);
        };
        return combo;
    }

    private Control CreateNumberInput(ComponentInputDefinition input)
    {
        var numeric = EditorNumericUpDownBehavior.Configure(new NumericUpDown
        {
            MinHeight = 36,
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Left,
            Minimum = input.Minimum,
            Maximum = input.Maximum,
            Increment = input.Increment,
            Value = decimal.TryParse(Value(input).Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : decimal.Parse(input.DefaultValue, CultureInfo.InvariantCulture),
        });
        numeric.ValueChanged += (_, args) =>
        {
            if (_isUpdating || args.NewValue is null) return;
            SetValue(input, args.NewValue.Value.ToString(CultureInfo.InvariantCulture));
        };
        return numeric;
    }

    private Control CreateBooleanInput(ComponentInputDefinition input)
    {
        var toggle = new ToggleSwitch
        {
            MinHeight = 36,
            IsChecked = StringToBool(Value(input)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _booleanInputs[StorageKey(input)] = toggle;
        toggle.IsCheckedChanged += (_, _) =>
        {
            if (_isUpdating) return;
            SetValue(input, toggle.IsChecked == true ? "true" : "false");
        };
        return toggle;
    }

    private Control CreateTextInput(ComponentInputDefinition input)
    {
        var textBox = DictionaryTextBoxFactory.Create(new FieldDefinition(
            input.Id,
            input.Label,
            ValueKind.StringSingleLine,
            DefaultValue: input.DefaultValue));
        textBox.Text = Value(input);
        textBox.TextChanged += (_, _) =>
        {
            if (_isUpdating) return;
            SetValue(input, textBox.Text ?? "");
        };
        return textBox;
    }

    private void EnsureValue(ComponentInputDefinition input, JsonObject preview)
    {
        var key = StorageKey(input);
        if (_values.ContainsKey(key)) return;

        var stored = preview[input.JsonKey];
        _values[key] = stored switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => text,
            JsonValue jsonValue when jsonValue.TryGetValue<double>(out var number) => number.ToString(CultureInfo.InvariantCulture),
            JsonValue jsonValue when jsonValue.TryGetValue<int>(out var integer) => integer.ToString(CultureInfo.InvariantCulture),
            JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var boolean) => boolean ? "true" : "false",
            _ => input.DefaultValue,
        };
    }

    private void EnsureActorValues(IReadOnlyList<ComponentInputDefinition> inputs, string projectId)
    {
        if (!inputs.Any((input) => input.Kind == ComponentInputKind.Actor))
        {
            return;
        }

        var firstActor = _database.GetActorOptions(projectId)
            .FirstOrDefault((option) => !string.IsNullOrWhiteSpace(option.Value));
        if (firstActor is null)
        {
            return;
        }

        foreach (var input in inputs.Where((candidate) => candidate.Kind == ComponentInputKind.Actor))
        {
            var key = StorageKey(input);
            if (string.IsNullOrWhiteSpace(_values.GetValueOrDefault(key)))
            {
                _values[key] = firstActor.Value;
            }
        }
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
        return $"{_scopeKey}:{input.Id}";
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
        return Math.Max(1, ParseDouble(_values.GetValueOrDefault(PlaybackDurationKey(), "65")));
    }

    private bool IsPlaying()
    {
        return StringToBool(_values.GetValueOrDefault(PlaybackStateKey(), "false"));
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
        return $"{_scopeKey}:{_animation?.PlayInputId ?? "isPlaying"}";
    }

    private string PlaybackDurationKey()
    {
        return $"{_scopeKey}:{_animation?.DurationInputId ?? "durationSeconds"}";
    }

    private string PlaybackTimeKey()
    {
        return $"{_scopeKey}:{_animation?.TimeJsonKey ?? "currentTimeSeconds"}";
    }

    private void SyncBooleanInput(string key)
    {
        if (!_booleanInputs.TryGetValue(key, out var input)) return;

        _isUpdating = true;
        try
        {
            input.IsChecked = StringToBool(_values.GetValueOrDefault(key, "false"));
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

    private static IEnumerable<ComponentInputDefinition> ReadInputs(JsonObject preview)
    {
        if (preview["inputs"] is not JsonArray inputs)
        {
            yield break;
        }

        foreach (var item in inputs.OfType<JsonObject>())
        {
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

            yield return new ComponentInputDefinition(
                id,
                label,
                jsonKey,
                ParseKind(kind),
                JsonString(item, "defaultValue"),
                ReadOptions(item),
                JsonDecimal(item, "minimum", 0),
                JsonDecimal(item, "maximum", 9999),
                JsonDecimal(item, "increment", 1));
        }
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
            "boolean" => ComponentInputKind.Boolean,
            "option" => ComponentInputKind.Option,
            "actor" => ComponentInputKind.Actor,
            _ => ComponentInputKind.Text,
        };
    }

    private static string JsonString(JsonObject owner, string key)
    {
        return owner[key] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : "";
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
    Boolean,
    Option,
    Actor,
}

internal sealed record ComponentInputDefinition(
    string Id,
    string Label,
    string JsonKey,
    ComponentInputKind Kind,
    string DefaultValue,
    IReadOnlyList<FieldOption>? Options = null,
    decimal Minimum = 0,
    decimal Maximum = 9999,
    decimal Increment = 1);

internal sealed record ComponentInputAnimation(
    string PlayInputId,
    string DurationInputId,
    string TimeJsonKey);
