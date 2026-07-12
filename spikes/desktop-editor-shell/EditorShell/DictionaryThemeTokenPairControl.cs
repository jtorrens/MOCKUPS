using Avalonia.Controls;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryThemeTokenPairControl : Grid, IDictionaryValueControl, IDictionaryLocalHorizontalScrollControl
{
    private readonly DictionaryThemeTokenControl _firstControl;
    private readonly DictionaryThemeTokenControl _secondControl;
    private string _firstValue;
    private string _secondValue;
    private bool _isUpdating;

    public DictionaryThemeTokenPairControl(
        FieldDefinition definition,
        string value,
        Func<string, IReadOnlyList<FieldOption>?, Task<string?>>? showThemeTokenPicker)
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*");
        ColumnSpacing = 8;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        var pair = DictionaryFieldPairText.Split(value);
        var labels = DictionaryFieldPairText.Labels(definition);
        _firstValue = pair.First;
        _secondValue = pair.Second;

        var firstLabel = CreateLabel(labels.First);
        SetColumn(firstLabel, 0);
        Children.Add(firstLabel);

        _firstControl = new DictionaryThemeTokenControl(
            definition,
            pair.First,
            showThemeTokenPicker);
        _firstControl.ValueChanged += (_, nextValue) => SetFirstValue(nextValue);
        _firstControl.ValueCommitted += (_, _) => CommitValue();
        SetColumn(_firstControl, 1);
        Children.Add(_firstControl);

        var secondLabel = CreateLabel(labels.Second);
        SetColumn(secondLabel, 2);
        Children.Add(secondLabel);

        _secondControl = new DictionaryThemeTokenControl(
            definition,
            pair.Second,
            showThemeTokenPicker);
        _secondControl.ValueChanged += (_, nextValue) => SetSecondValue(nextValue);
        _secondControl.ValueCommitted += (_, _) => CommitValue();
        SetColumn(_secondControl, 3);
        Children.Add(_secondControl);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        var pair = DictionaryFieldPairText.Split(value);
        _isUpdating = true;
        _firstValue = pair.First;
        _secondValue = pair.Second;
        _firstControl.SetValue(pair.First);
        _secondControl.SetValue(pair.Second);
        _isUpdating = false;
    }

    private void SetFirstValue(string value)
    {
        if (_isUpdating) return;
        _firstValue = value;
        ValueChanged?.Invoke(this, CurrentValue());
    }

    private void SetSecondValue(string value)
    {
        if (_isUpdating) return;
        _secondValue = value;
        ValueChanged?.Invoke(this, CurrentValue());
    }

    private void CommitValue()
    {
        ValueCommitted?.Invoke(this, CurrentValue());
    }

    private string CurrentValue()
    {
        return DictionaryFieldPairText.Join(_firstValue, _secondValue);
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
