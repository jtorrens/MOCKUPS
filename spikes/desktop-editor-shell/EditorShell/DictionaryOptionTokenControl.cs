using Avalonia.Controls;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryOptionTokenControl : Grid, IDictionaryValueControl
{
    private readonly FieldDefinition _definition;
    private readonly EditorInstantComboBox _comboBox;
    private bool _isUpdating;

    public DictionaryOptionTokenControl(FieldDefinition definition, string value)
    {
        _definition = definition;
        _comboBox = DictionaryOptionSelector.CreateComboBox(definition, value);
        SetValue(value);
        _comboBox.SelectionChanged += (_, _) =>
        {
            if (_isUpdating) return;

            var nextValue = DictionaryOptionSelector.Value(_comboBox);
            ValueChanged?.Invoke(this, nextValue);
            ValueCommitted?.Invoke(this, nextValue);
        };
        Children.Add(_comboBox);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        _isUpdating = true;
        _comboBox.SelectedItem = DictionaryOptionSelector.SelectedOption(_definition, value);
        _isUpdating = false;
    }
}
