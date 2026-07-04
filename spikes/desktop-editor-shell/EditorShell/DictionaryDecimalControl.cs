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

    public DictionaryDecimalControl(FieldDefinition definition, string value)
    {
        _definition = definition;
        _value = Normalize(value);
        _numeric = new NumericUpDown
        {
            MinHeight = 36,
            Width = 140,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = definition.IsEditable,
            Increment = definition.Number?.Increment ?? 0.1m,
            Value = ParseDecimal(_value, 0),
        };

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

            SetLocalValue(Format(_numeric.Value ?? 0));
            ValueCommitted?.Invoke(this, _value);
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
        _isUpdating = true;
        _numeric.Value = ParseDecimal(_value, 0);
        _isUpdating = false;
    }

    private void SetLocalValue(string value)
    {
        var normalized = Normalize(value);
        if (_value == normalized) return;

        _value = normalized;
        ValueChanged?.Invoke(this, _value);
    }

    private string Normalize(string value)
    {
        return Format(ParseDecimal(value, 0));
    }

    private string Format(decimal value)
    {
        var decimalPlaces = Math.Max(0, _definition.Number?.DecimalPlaces ?? 2);
        return value.ToString(decimalPlaces == 0 ? "0" : $"0.{new string('#', decimalPlaces)}", CultureInfo.InvariantCulture);
    }

    private static decimal ParseDecimal(string value, decimal fallback)
    {
        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant)
            ? invariant
            : decimal.TryParse(value, out var local) ? local : fallback;
    }
}
