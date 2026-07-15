using Avalonia.Controls;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryThemeTokenPairControl : Grid, IDictionaryValueControl
{
    private const double CompactThreshold = 380;
    private readonly TextBlock _firstLabel;
    private readonly TextBlock _secondLabel;
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
        ColumnSpacing = 8;
        RowSpacing = 8;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        MinWidth = 0;

        var pair = DictionaryFieldPairText.Split(value);
        var labels = DictionaryFieldPairText.Labels(definition);
        _firstValue = pair.First;
        _secondValue = pair.Second;

        _firstLabel = CreateLabel(labels.First);
        Children.Add(_firstLabel);

        _firstControl = new DictionaryThemeTokenControl(
            definition,
            pair.First,
            showThemeTokenPicker);
        _firstControl.ValueChanged += (_, nextValue) => SetFirstValue(nextValue);
        _firstControl.ValueCommitted += (_, _) => CommitValue();
        Children.Add(_firstControl);

        _secondLabel = CreateLabel(labels.Second);
        Children.Add(_secondLabel);

        _secondControl = new DictionaryThemeTokenControl(
            definition,
            pair.Second,
            showThemeTokenPicker);
        _secondControl.ValueChanged += (_, nextValue) => SetSecondValue(nextValue);
        _secondControl.ValueCommitted += (_, _) => CommitValue();
        Children.Add(_secondControl);
        SizeChanged += (_, args) => ApplyResponsiveLayout(args.NewSize.Width);
        ApplyResponsiveLayout(double.PositiveInfinity);
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

    private void ApplyResponsiveLayout(double width)
    {
        var compact = width > 0 && width < CompactThreshold;
        ColumnDefinitions = compact
            ? new ColumnDefinitions("57,*")
            : new ColumnDefinitions("57,*,57,*");
        RowDefinitions = compact
            ? new RowDefinitions("Auto,Auto")
            : new RowDefinitions("Auto");

        SetColumn(_firstLabel, 0);
        SetRow(_firstLabel, 0);
        SetColumn(_firstControl, 1);
        SetRow(_firstControl, 0);
        SetColumn(_secondLabel, compact ? 0 : 2);
        SetRow(_secondLabel, compact ? 1 : 0);
        SetColumn(_secondControl, compact ? 1 : 3);
        SetRow(_secondControl, compact ? 1 : 0);
    }
}
