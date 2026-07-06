using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DesignPreviewInputsPanel : ContentControl
{
    private readonly SpikeDatabase _database;
    private readonly Action _refreshPreview;
    private readonly DispatcherTimer _playbackTimer;
    private readonly StackPanel _rowsPanel;
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private Button? _playbackButton;
    private string _scopeKey = "";
    private string _componentType = "";
    private string _projectId = "";
    private string _inputSignature = "";
    private DateTime _playbackStartedAtUtc;
    private double _playbackStartSeconds;
    private bool _isUpdating;

    public DesignPreviewInputsPanel(SpikeDatabase database, Action refreshPreview)
    {
        _database = database;
        _refreshPreview = refreshPreview;
        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120),
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

            var inputs = DesignPreviewInputCatalog.ForComponent(payload.ComponentType).ToList();
            if (inputs.Count == 0)
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

            IsVisible = true;
            var scopeKey = $"{payload.Kind}:{payload.ComponentType}:{payload.Name}";
            var inputSignature = string.Join("|", inputs.Select((input) => input.Id));
            var shouldRebuild = scopeKey != _scopeKey
                || projectId != _projectId
                || inputSignature != _inputSignature
                || Content is null;
            _scopeKey = scopeKey;
            _componentType = payload.ComponentType;
            _projectId = projectId;
            _inputSignature = inputSignature;
            var preview = ParseJsonObject(payload.DesignPreviewJson);
            foreach (var input in inputs)
            {
                EnsureValue(input, preview);
            }
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

    private void RebuildCard(IReadOnlyList<DesignPreviewInput> inputs, string projectId)
    {
        _rowsPanel.Children.Clear();
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
        header.Children.Add(EditorCardHeader.Create("Inputs", "Preview sample values", icon));
        if (_componentType == "audio")
        {
            _playbackButton = new Button
            {
                MinWidth = 72,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _playbackButton.PointerPressed += (_, args) => args.Handled = true;
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

    public DesignPreviewPayload ApplyInputs(DesignPreviewPayload payload)
    {
        if (!IsVisible || string.IsNullOrWhiteSpace(_scopeKey))
        {
            return payload;
        }

        var preview = ParseJsonObject(payload.DesignPreviewJson);
        foreach (var input in DesignPreviewInputCatalog.ForComponent(payload.ComponentType))
        {
            var value = Value(input);
            switch (input.Kind)
            {
                case DesignPreviewInputKind.Number:
                    if (double.TryParse(value.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    {
                        preview[input.JsonKey] = number;
                    }
                    break;
                default:
                    preview[input.JsonKey] = value;
                    break;
            }
        }
        if (payload.ComponentType == "audio")
        {
            preview["currentTimeSeconds"] = CurrentPlaybackSeconds();
        }

        return payload with { DesignPreviewJson = preview.ToJsonString() };
    }

    private Control CreateInputRow(DesignPreviewInput input, string projectId)
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
            DesignPreviewInputKind.Option => CreateOptionInput(input),
            DesignPreviewInputKind.Actor => CreateActorInput(input, projectId),
            DesignPreviewInputKind.Number => CreateNumberInput(input),
            _ => CreateTextInput(input),
        };
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        return row;
    }

    private Control CreateOptionInput(DesignPreviewInput input)
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

    private Control CreateActorInput(DesignPreviewInput input, string projectId)
    {
        var combo = new EditorInstantComboBox
        {
            MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var options = _database.GetActorOptions(projectId);
        combo.ItemsSource = options;
        combo.SelectedItem = options.FirstOrDefault((option) => option.Value == Value(input))
            ?? options.FirstOrDefault((option) => !string.IsNullOrWhiteSpace(option.Value))
            ?? options.FirstOrDefault();
        combo.SelectionChanged += (_, _) =>
        {
            if (_isUpdating || combo.SelectedItem is null) return;
            SetValue(input, combo.SelectedItem.Value);
        };
        return combo;
    }

    private Control CreateNumberInput(DesignPreviewInput input)
    {
        var numeric = EditorNumericUpDownBehavior.Configure(new NumericUpDown
        {
            MinHeight = 36,
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

    private Control CreateTextInput(DesignPreviewInput input)
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

    private void EnsureValue(DesignPreviewInput input, JsonObject preview)
    {
        var key = StorageKey(input);
        if (_values.ContainsKey(key)) return;

        var stored = preview[input.JsonKey];
        _values[key] = stored switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => text,
            JsonValue jsonValue when jsonValue.TryGetValue<double>(out var number) => number.ToString(CultureInfo.InvariantCulture),
            JsonValue jsonValue when jsonValue.TryGetValue<int>(out var integer) => integer.ToString(CultureInfo.InvariantCulture),
            _ => input.DefaultValue,
        };
    }

    private string Value(DesignPreviewInput input)
    {
        return _values.TryGetValue(StorageKey(input), out var value) ? value : input.DefaultValue;
    }

    private void SetValue(DesignPreviewInput input, string value)
    {
        _values[StorageKey(input)] = value;
        if (_componentType == "audio" && input.Id == "durationSeconds")
        {
            ClampCurrentPlaybackToDuration();
        }
        SyncPlaybackTimer();
        _refreshPreview();
    }

    private string StorageKey(DesignPreviewInput input)
    {
        return $"{_scopeKey}:{input.Id}";
    }

    private void SyncPlaybackTimer()
    {
        if (_componentType != "audio")
        {
            StopPlayback();
            UpdatePlaybackButton();
            return;
        }

        var state = _values.GetValueOrDefault($"{_scopeKey}:playbackState", "paused");
        if (state == "playing")
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
        if (_componentType != "audio") return;

        var stateKey = $"{_scopeKey}:playbackState";
        var state = _values.GetValueOrDefault(stateKey, "paused");
        if (state == "playing")
        {
            _values[$"{_scopeKey}:currentTimeSeconds"] = CurrentPlaybackSeconds().ToString(CultureInfo.InvariantCulture);
            _values[stateKey] = "paused";
        }
        else
        {
            _playbackStartSeconds = CurrentPlaybackSeconds();
            _playbackStartedAtUtc = DateTime.UtcNow;
            _values[stateKey] = "playing";
        }

        SyncPlaybackTimer();
        _refreshPreview();
    }

    private void UpdatePlaybackButton()
    {
        if (_playbackButton is null) return;

        var state = _values.GetValueOrDefault($"{_scopeKey}:playbackState", "paused");
        _playbackButton.Content = state == "playing" ? "Pause" : "Play";
    }

    private void StopPlayback()
    {
        if (!_playbackTimer.IsEnabled) return;

        _playbackTimer.Stop();
    }

    private void AdvancePlaybackFrame()
    {
        if (_componentType != "audio"
            || _values.GetValueOrDefault($"{_scopeKey}:playbackState", "paused") != "playing")
        {
            StopPlayback();
            return;
        }

        _values[$"{_scopeKey}:currentTimeSeconds"] = CurrentPlaybackSeconds().ToString(CultureInfo.InvariantCulture);
        _refreshPreview();
    }

    private double CurrentPlaybackSeconds()
    {
        var duration = DurationSeconds();
        var stored = ParseDouble(_values.GetValueOrDefault($"{_scopeKey}:currentTimeSeconds", "0"));
        if (!_playbackTimer.IsEnabled)
        {
            return Math.Clamp(stored, 0, duration);
        }

        var elapsed = (DateTime.UtcNow - _playbackStartedAtUtc).TotalSeconds;
        return PositiveModulo(_playbackStartSeconds + elapsed, duration);
    }

    private void ClampCurrentPlaybackToDuration()
    {
        if (_componentType != "audio") return;

        _values[$"{_scopeKey}:currentTimeSeconds"] = Math.Clamp(
            CurrentPlaybackSeconds(),
            0,
            DurationSeconds()).ToString(CultureInfo.InvariantCulture);
    }

    private double DurationSeconds()
    {
        return Math.Max(1, ParseDouble(_values.GetValueOrDefault($"{_scopeKey}:durationSeconds", "65")));
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
}

internal enum DesignPreviewInputKind
{
    Text,
    Number,
    Option,
    Actor,
}

internal sealed record DesignPreviewInput(
    string Id,
    string Label,
    string JsonKey,
    DesignPreviewInputKind Kind,
    string DefaultValue,
    IReadOnlyList<FieldOption>? Options = null,
    decimal Minimum = 0,
    decimal Maximum = 9999,
    decimal Increment = 1);

internal static class DesignPreviewInputCatalog
{
    public static IEnumerable<DesignPreviewInput> ForComponent(string componentType)
    {
        return componentType switch
        {
            "label" =>
            [
                new("sampleText", "Text", "sampleText", DesignPreviewInputKind.Text, "Sample"),
                new("sampleSubtext", "Subtext", "sampleSubtext", DesignPreviewInputKind.Text, "Subtitle"),
            ],
            "avatar" =>
            [
                new("actorId", "Actor", "actorId", DesignPreviewInputKind.Actor, ""),
            ],
            "buttonIcon" =>
            [
                new("sampleText", "Text", "sampleText", DesignPreviewInputKind.Text, "Action"),
                new("sampleSubtext", "Subtext", "sampleSubtext", DesignPreviewInputKind.Text, "Subtitle"),
            ],
            "audio" =>
            [
                new("durationSeconds", "Duration", "durationSeconds", DesignPreviewInputKind.Number, "65", Minimum: 1, Maximum: 86400, Increment: 1),
                new("actorId", "Actor", "actorId", DesignPreviewInputKind.Actor, ""),
            ],
            _ => [],
        };
    }
}
