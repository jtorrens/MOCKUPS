using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Globalization;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryNumberSliderControl : Grid, IDictionaryValueControl
{
    private readonly FieldDefinition _definition;
    private readonly Slider _slider;
    private readonly TextBox _box;
    private bool _isUpdating;
    private string _value;
    private string _lastCommittedValue;

    public DictionaryNumberSliderControl(FieldDefinition definition, string value)
    {
        _definition = definition;
        var number = definition.Number
            ?? throw new InvalidOperationException($"Slider number field '{definition.Id}' requires number metadata.");
        if (number.Minimum is null || number.Maximum is null)
        {
            throw new InvalidOperationException($"Slider number field '{definition.Id}' requires minimum and maximum.");
        }

        _value = Normalize(value);
        _lastCommittedValue = _value;
        var boxWidth = EditorUiDensity.TextAwareWidth(78);
        ColumnDefinitions = new ColumnDefinitions($"*,{boxWidth.ToString(CultureInfo.InvariantCulture)}");
        ColumnSpacing = 10;
        VerticalAlignment = VerticalAlignment.Center;

        _slider = new Slider
        {
            Minimum = (double)number.Minimum.Value,
            Maximum = (double)number.Maximum.Value,
            Value = (double)ParseRequired(_value),
            TickFrequency = (double)number.Increment,
            SmallChange = (double)number.Increment,
            LargeChange = (double)Math.Max(number.Increment, number.Increment * 2),
            IsEnabled = definition.IsEditable,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _box = EditorNumericTextStyle.Apply(EditorTextBoxBehavior.Configure(new TextBox
        {
            Text = _value,
            Width = boxWidth,
            IsReadOnly = !definition.IsEditable,
            VerticalContentAlignment = VerticalAlignment.Center,
        }));
        SetColumn(_box, 1);
        Children.Add(_slider);
        Children.Add(_box);

        Hook();
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public string Value => _value;

    public void SetValue(string value)
    {
        var normalized = Normalize(value);
        if (_value == normalized) return;

        _value = normalized;
        _lastCommittedValue = normalized;
        UpdateControls();
    }

    private void Hook()
    {
        _slider.PropertyChanged += (_, args) =>
        {
            if (args.Property != RangeBase.ValueProperty || _isUpdating)
            {
                return;
            }

            SetLocalValue(Format(Snap((decimal)_slider.Value)));
            CommitValue();
        };
        _box.TextChanged += (_, _) =>
        {
            if (_isUpdating)
            {
                return;
            }

            if (!DictionaryNumericValueContract.TryParseDraft(_definition, _box.Text, out var parsed))
            {
                return;
            }
            var normalized = Format(parsed);
            if (_value == normalized)
            {
                return;
            }

            _value = normalized;
            _isUpdating = true;
            _slider.Value = (double)Clamp(parsed);
            _isUpdating = false;
            ValueChanged?.Invoke(this, _value);
            CommitValue();
        };
        _box.LostFocus += (_, _) =>
        {
            _box.Text = _value;
            CommitValue();
        };
        _box.KeyDown += (_, args) =>
        {
            if (args.Key != Avalonia.Input.Key.Enter)
            {
                return;
            }

            _box.Text = _value;
            CommitValue();
            args.Handled = true;
        };
    }

    private void SetLocalValue(string value)
    {
        var normalized = Normalize(value);
        if (_value == normalized) return;

        _value = normalized;
        UpdateControls();
        ValueChanged?.Invoke(this, _value);
    }

    private void CommitValue()
    {
        if (_lastCommittedValue == _value) return;

        _lastCommittedValue = _value;
        ValueCommitted?.Invoke(this, _value);
    }

    private void UpdateControls()
    {
        _isUpdating = true;
        _slider.Value = (double)ParseRequired(_value);
        if (_box.Text != _value)
        {
            _box.Text = _value;
        }
        _isUpdating = false;
    }

    private string Normalize(string value)
    {
        return Format(ParseRequired(value));
    }

    private decimal ParseRequired(string value) => DictionaryNumericValueContract.ParseRequired(
        _definition,
        value);

    private decimal Clamp(decimal value)
    {
        var number = _definition.Number!;
        if (number.Minimum is { } minimum)
        {
            value = Math.Max(value, minimum);
        }

        if (number.Maximum is { } maximum)
        {
            value = Math.Min(value, maximum);
        }

        return value;
    }

    private decimal Snap(decimal value)
    {
        var increment = _definition.Number!.Increment;
        if (increment <= 0)
        {
            return Clamp(value);
        }

        return Clamp(Math.Round(value / increment) * increment);
    }

    private string Format(decimal value)
    {
        if (_definition.ValueKind == ValueKind.Integer)
        {
            return NumericText.IntegerString(value);
        }

        var decimalPlaces = Math.Max(0, _definition.Number?.DecimalPlaces ?? 2);
        return value.ToString(decimalPlaces == 0 ? "0" : $"0.{new string('#', decimalPlaces)}", CultureInfo.InvariantCulture);
    }
}
