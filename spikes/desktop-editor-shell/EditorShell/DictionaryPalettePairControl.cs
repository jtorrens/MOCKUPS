using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryPalettePairControl : Grid
{
    private readonly DictionaryPaletteTokenControl _firstControl;
    private readonly DictionaryPaletteTokenControl _secondControl;
    private bool _isUpdating;

    public DictionaryPalettePairControl(FieldDefinition definition, string value)
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,180,Auto,180");
        ColumnSpacing = 14;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Left;

        var pair = DictionaryFieldPairText.Split(value);
        var labels = DictionaryFieldPairText.Labels(definition);

        var firstLabel = CreateLabel(labels.First);
        SetColumn(firstLabel, 0);

        _firstControl = new DictionaryPaletteTokenControl($"{definition.Label} · {labels.First}", definition.Options, pair.First, definition.IsEditable);
        _firstControl.ValueCommitted += (_, _) => SetValueFromControls();
        SetColumn(_firstControl, 1);

        var secondLabel = CreateLabel(labels.Second);
        secondLabel.Margin = new Thickness(10, 0, 0, 0);
        SetColumn(secondLabel, 2);

        _secondControl = new DictionaryPaletteTokenControl($"{definition.Label} · {labels.Second}", definition.Options, pair.Second, definition.IsEditable);
        _secondControl.ValueCommitted += (_, _) => SetValueFromControls();
        SetColumn(_secondControl, 3);

        Children.Add(firstLabel);
        Children.Add(_firstControl);
        Children.Add(secondLabel);
        Children.Add(_secondControl);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        var pair = DictionaryFieldPairText.Split(value);
        _isUpdating = true;
        _firstControl.SetValue(pair.First);
        _secondControl.SetValue(pair.Second);
        _isUpdating = false;
    }

    private void SetValueFromControls()
    {
        if (_isUpdating) return;

        var value = DictionaryFieldPairText.Join(
            _firstControl.Value,
            _secondControl.Value);
        ValueChanged?.Invoke(this, value);
        ValueCommitted?.Invoke(this, value);
    }

    private static TextBlock CreateLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            MinWidth = 57,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.78,
        };
    }
}
