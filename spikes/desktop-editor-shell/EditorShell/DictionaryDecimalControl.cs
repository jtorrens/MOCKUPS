using Avalonia.Controls;
using Avalonia.Layout;
using System;
using System.Globalization;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryDecimalControl : Grid, IDictionaryValueControl
{
    private readonly FieldDefinition _definition;
    private readonly NumericUpDown _numeric;
    private bool _isUpdating;
    private string _value;
    private string _lastCommittedValue;

    public DictionaryDecimalControl(FieldDefinition definition, string value)
    {
        _definition = definition;
        _value = Normalize(value);
        _lastCommittedValue = _value;
        _numeric = EditorNumericUpDownBehavior.Configure(new NumericUpDown
        {
            MinHeight = 36,
            Width = EditorUiDensity.TextAwareWidth(140),
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = definition.IsEditable,
            Increment = definition.Number?.Increment ?? 0.1m,
            Value = ParseRequired(_value),
        });

        if (definition.Number?.Minimum is { } minimum)
        {
            _numeric.Minimum = minimum;
        }

        if (definition.Number?.Maximum is { } maximum)
        {
            _numeric.Maximum = maximum;
        }

        _numeric.PropertyChanged += (_, change) =>
        {
            if (change.Property != NumericUpDown.ValueProperty || _isUpdating) return;

            if (_numeric.Value is not { } numericValue) return;

            SetLocalValue(Format(numericValue));
            CommitValue();
        };
        Children.Add(_numeric);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        var normalized = Normalize(value);
        if (_value == normalized) return;

        _value = normalized;
        _lastCommittedValue = normalized;
        _isUpdating = true;
        _numeric.Value = ParseRequired(_value);
        _isUpdating = false;
    }

    private void SetLocalValue(string value)
    {
        var normalized = Normalize(value);
        if (_value == normalized) return;

        _value = normalized;
        ValueChanged?.Invoke(this, _value);
    }

    private void CommitValue()
    {
        if (_lastCommittedValue == _value) return;

        _lastCommittedValue = _value;
        ValueCommitted?.Invoke(this, _value);
    }

    private string Normalize(string value)
    {
        return Format(ParseRequired(value));
    }

    private string Format(decimal value)
    {
        var decimalPlaces = Math.Max(0, _definition.Number?.DecimalPlaces ?? 2);
        return value.ToString(decimalPlaces == 0 ? "0" : $"0.{new string('#', decimalPlaces)}", CultureInfo.InvariantCulture);
    }

    private decimal ParseRequired(string value) => DictionaryNumericValueContract.ParseRequired(
        _definition,
        value);
}
