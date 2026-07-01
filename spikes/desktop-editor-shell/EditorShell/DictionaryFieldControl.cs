using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryFieldControl : Grid
{
    private readonly FieldDefinition _definition;
    private readonly TextBox _textBox;
    private readonly Button _restoreButton;
    private string _value;

    public DictionaryFieldControl(FieldValue fieldValue)
    {
        _definition = fieldValue.Definition;
        _value = fieldValue.Value;

        ColumnDefinitions = new ColumnDefinitions("180,*,Auto");
        ColumnSpacing = 12;
        MinHeight = _definition.ValueKind == ValueKind.StringMultiline ? 96 : 40;

        var label = new TextBlock
        {
            Text = _definition.Label,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = _definition.ValueKind == ValueKind.StringMultiline
                ? VerticalAlignment.Top
                : VerticalAlignment.Center,
            Margin = _definition.ValueKind == ValueKind.StringMultiline
                ? new Avalonia.Thickness(0, 7, 0, 0)
                : new Avalonia.Thickness(0),
        };
        SetColumn(label, 0);

        _textBox = CreateTextBox();
        _textBox.Text = _value;
        _textBox.TextChanged += (_, _) =>
        {
            _value = _textBox.Text ?? "";
            UpdateState();
            ValueChanged?.Invoke(this, _value);
        };
        SetColumn(_textBox, 1);

        _restoreButton = new Button
        {
            Content = "↺",
            Width = 32,
            Height = 32,
            Padding = new Avalonia.Thickness(0),
            VerticalAlignment = _definition.ValueKind == ValueKind.StringMultiline
                ? VerticalAlignment.Top
                : VerticalAlignment.Center,
            IsVisible = !fieldValue.IsDefault && _definition.IsEditable,
        };
        _restoreButton.Click += (_, _) =>
        {
            SetValue(_definition.DefaultValue);
        };
        SetColumn(_restoreButton, 2);

        Children.Add(label);
        Children.Add(_textBox);
        Children.Add(_restoreButton);
        UpdateState();
    }

    public event EventHandler<string>? ValueChanged;

    public bool IsDefault => _value == _definition.DefaultValue;

    public void SetValue(string value)
    {
        if (_textBox.Text == value) return;
        _textBox.Text = value;
        _value = value;
        UpdateState();
        ValueChanged?.Invoke(this, _value);
    }

    private TextBox CreateTextBox()
    {
        var textBox = new TextBox
        {
            IsReadOnly = !_definition.IsEditable || _definition.ValueKind == ValueKind.StringReadOnly,
            AcceptsReturn = _definition.ValueKind == ValueKind.StringMultiline,
            TextWrapping = _definition.ValueKind == ValueKind.StringMultiline
                ? TextWrapping.Wrap
                : TextWrapping.NoWrap,
            MinHeight = _definition.ValueKind == ValueKind.StringMultiline ? 88 : 36,
            VerticalContentAlignment = _definition.ValueKind == ValueKind.StringMultiline
                ? VerticalAlignment.Top
                : VerticalAlignment.Center,
        };
        return textBox;
    }

    private void UpdateState()
    {
        var isDefault = IsDefault;
        _restoreButton.IsVisible = !isDefault && _definition.IsEditable;

        PseudoClasses.Set(":changed", !isDefault);
    }
}
