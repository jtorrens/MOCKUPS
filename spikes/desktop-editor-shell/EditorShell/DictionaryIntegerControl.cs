using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Globalization;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryIntegerControl : Grid, IDictionaryValueControl
{
    private readonly NumericUpDown _numeric;
    private bool _isUpdating;
    private string _value;
    private string _lastCommittedValue;

    public DictionaryIntegerControl(FieldDefinition definition, string value)
    {
        _value = Normalize(value);
        _lastCommittedValue = _value;
        _numeric = EditorNumericUpDownBehavior.Configure(new NumericUpDown
        {
            MinHeight = 36,
            Width = EditorUiDensity.TextAwareWidth(120),
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = definition.IsEditable,
            Increment = definition.Number?.Increment ?? 1,
            FormatString = "0",
            Value = ParseInteger(_value, 0),
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

            SetLocalValue(Format(_numeric.Value ?? 0));
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
        _numeric.Value = ParseInteger(_value, 0);
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

    private static string Normalize(string value)
    {
        return Format(ParseInteger(value, 0));
    }

    private static string Format(decimal value)
    {
        return NumericText.IntegerString(value);
    }

    private static decimal ParseInteger(string value, decimal fallback)
    {
        return NumericText.Integer(value, fallback);
    }
}
