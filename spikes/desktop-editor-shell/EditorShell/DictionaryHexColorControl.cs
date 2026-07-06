using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryHexColorControl : Grid, IDictionaryValueControl
{
    private readonly FieldDefinition _definition;
    private readonly Border _swatch;
    private readonly TextBox _textBox;
    private bool _isUpdating;
    private string _value;

    public DictionaryHexColorControl(FieldDefinition definition, string value)
    {
        _definition = definition;
        _value = value;
        ColumnDefinitions = new ColumnDefinitions("28,*,Auto");
        ColumnSpacing = 12;

        _swatch = CreateColorSwatch(value);
        SetColumn(_swatch, 0);
        Children.Add(_swatch);

        _textBox = DictionaryTextBoxFactory.Create(definition);
        _textBox.Text = value;
        _textBox.TextChanged += (_, _) =>
        {
            if (_isUpdating) return;

            SetLocalValue(DictionaryFieldColorValue.NormalizeHex(_textBox.Text ?? ""));
            UpdateColorControlsFromValue();
        };
        EditorTextBoxBehavior.AttachDeferredCommit(_textBox, CommitValue);
        SetColumn(_textBox, 1);
        Children.Add(_textBox);

        var pickerButton = new Button
        {
            Content = "Pick",
            MinWidth = 58,
            Height = 34,
            IsEnabled = definition.IsEditable,
            VerticalAlignment = VerticalAlignment.Center,
        };
        pickerButton.Click += async (_, _) =>
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is null) return;

            var color = await HexColorPickerDialog.Show(owner, definition.Label, _value);
            if (color is null) return;
            SetValue(color);
            CommitValue();
        };
        SetColumn(pickerButton, 2);
        Children.Add(pickerButton);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        if (_value == value) return;

        _value = value;
        UpdateColorControlsFromValue();
    }

    private void SetLocalValue(string value)
    {
        if (_value == value) return;

        _value = value;
        ValueChanged?.Invoke(this, _value);
    }

    private void CommitValue()
    {
        ValueCommitted?.Invoke(this, _value);
    }

    private static Border CreateColorSwatch(string value)
    {
        return new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(DictionaryFieldColorValue.Parse(value)),
            BorderBrush = new SolidColorBrush(Color.Parse("#667085")),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
    }

    private void UpdateColorControlsFromValue()
    {
        _isUpdating = true;
        _swatch.Background = new SolidColorBrush(DictionaryFieldColorValue.Parse(_value));
        if (_textBox.Text != _value)
        {
            _textBox.Text = _value;
        }

        _isUpdating = false;
    }
}
