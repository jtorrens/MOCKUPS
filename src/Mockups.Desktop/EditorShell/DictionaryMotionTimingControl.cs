using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryMotionTimingControl : Grid, IDictionaryValueControl
{
    private readonly NumericUpDown _duration;
    private readonly NumericUpDown _delay;
    private readonly EditorInstantComboBox _easing;
    private readonly NumericUpDown _intensity;
    private readonly FieldOption[] _easingOptions;
    private MotionTimingValue _value;
    private bool _isUpdating;
    private string _lastCommittedValue;

    public DictionaryMotionTimingControl(FieldDefinition definition, string value)
    {
        _value = MotionTimingValue.Parse(value);
        _lastCommittedValue = _value.ToJsonString();
        _easingOptions = definition.Options?.ToArray() ?? [];

        ColumnSpacing = EditorUiDensity.Card(8);
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _duration = CreateNumber(definition.IsEditable);
        _delay = CreateNumber(definition.IsEditable);
        _easing = new EditorInstantComboBox
        {
            ItemsSource = _easingOptions,
            IsEnabled = definition.IsEditable,
            MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _intensity = CreateIntensity(definition.IsEditable);

        var durationDefinition = new FieldDefinition(
            $"{definition.Id}.durationMs",
            "Duration",
            ValueKind.Integer,
            IsEditable: definition.IsEditable,
            Unit: "ms");
        var delayDefinition = new FieldDefinition(
            $"{definition.Id}.delayMs",
            "Delay",
            ValueKind.Integer,
            IsEditable: definition.IsEditable,
            Unit: "ms");
        var easingDefinition = new FieldDefinition(
            $"{definition.Id}.easing",
            "Easing",
            ValueKind.OptionToken,
            IsEditable: definition.IsEditable);
        var intensityDefinition = new FieldDefinition(
            $"{definition.Id}.intensity",
            "Intensity",
            ValueKind.Decimal,
            IsEditable: definition.IsEditable);

        var presentation = definition.MotionTiming ?? new MotionTimingDefinition();
        var fields = new List<(Control Shell, GridLength Width)>();
        if (presentation.ShowDuration)
        {
            fields.Add((FieldShell(durationDefinition, _duration, EditorUiDensity.TextAwareWidth(104)), GridLength.Auto));
        }
        if (presentation.ShowDelay)
        {
            fields.Add((FieldShell(delayDefinition, _delay, EditorUiDensity.TextAwareWidth(104)), GridLength.Auto));
        }
        if (presentation.ShowEasing)
        {
            fields.Add((FieldShell(easingDefinition, _easing, 0), new GridLength(1, GridUnitType.Star)));
        }
        if (presentation.ShowIntensity)
        {
            fields.Add((FieldShell(intensityDefinition, _intensity, EditorUiDensity.TextAwareWidth(104)), GridLength.Auto));
        }

        ColumnDefinitions = new ColumnDefinitions();
        for (var index = 0; index < fields.Count; index++)
        {
            ColumnDefinitions.Add(new ColumnDefinition(fields[index].Width));
            Grid.SetColumn(fields[index].Shell, index);
            Children.Add(fields[index].Shell);
        }

        _duration.PropertyChanged += (_, change) =>
        {
            if (_isUpdating || change.Property != NumericUpDown.ValueProperty) return;
            SetLocal(_value with { DurationMs = ValueAsInt(_duration.Value) }, commit: true);
        };
        _delay.PropertyChanged += (_, change) =>
        {
            if (_isUpdating || change.Property != NumericUpDown.ValueProperty) return;
            SetLocal(_value with { DelayMs = ValueAsInt(_delay.Value) }, commit: true);
        };
        _easing.SelectionChanged += (_, _) =>
        {
            if (_isUpdating) return;
            SetLocal(_value with { Easing = SelectedEasingValue() }, commit: true);
        };
        _intensity.PropertyChanged += (_, change) =>
        {
            if (_isUpdating || change.Property != NumericUpDown.ValueProperty) return;
            SetLocal(_value with { Intensity = _intensity.Value }, commit: true);
        };

        UpdateControls();
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        _value = MotionTimingValue.Parse(value);
        _lastCommittedValue = _value.ToJsonString();
        UpdateControls();
    }

    private void SetLocal(MotionTimingValue value, bool commit)
    {
        _value = value;
        UpdateControls();
        var json = _value.ToJsonString();
        ValueChanged?.Invoke(this, json);
        if (commit && _lastCommittedValue != json)
        {
            _lastCommittedValue = json;
            ValueCommitted?.Invoke(this, json);
        }
    }

    private void UpdateControls()
    {
        _isUpdating = true;
        _duration.Value = _value.DurationMs;
        _delay.Value = _value.DelayMs;
        _easing.SelectedItem = _easingOptions.FirstOrDefault((option) =>
            option.Value.Equals(_value.Easing ?? "", StringComparison.Ordinal));
        _intensity.Value = _value.Intensity;
        _isUpdating = false;
    }

    private string? SelectedEasingValue()
    {
        return _easing.SelectedItem is FieldOption option ? option.Value : null;
    }

    private static NumericUpDown CreateNumber(bool isEditable)
    {
        return EditorNumericUpDownBehavior.Configure(new NumericUpDown
        {
            MinHeight = 36,
            Minimum = 0,
            Increment = 10,
            FormatString = "0",
            IsEnabled = isEditable,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
    }

    private static NumericUpDown CreateIntensity(bool isEditable)
    {
        return EditorNumericUpDownBehavior.Configure(new NumericUpDown
        {
            MinHeight = 36,
            Minimum = 0,
            Maximum = 5,
            Increment = 0.05m,
            FormatString = "0.##",
            IsEnabled = isEditable,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
    }

    private static StackPanel FieldShell(FieldDefinition definition, Control control, double width)
    {
        if (width > 0)
        {
            control.Width = width;
        }

        return new StackPanel
        {
            Spacing = 3,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                new TextBlock
                {
                    Text = definition.DisplayLabel,
                    FontSize = 11,
                    Opacity = 0.72,
                },
                control,
            },
        };
    }

    private static int? ValueAsInt(decimal? value)
    {
        return value is { } number
            ? (int)Math.Round(number, MidpointRounding.AwayFromZero)
            : null;
    }
}
