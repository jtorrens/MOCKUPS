using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryPalettePairControl : Grid, IDictionaryValueControl, IDictionaryLocalHorizontalScrollControl
{
    private readonly DictionaryPaletteTokenControl _firstControl;
    private readonly DictionaryPaletteTokenControl _secondControl;
    private bool _isUpdating;
    private readonly PairFieldLabels _labels;
    private bool _usesSharedHeader;

    public DictionaryPalettePairControl(FieldDefinition definition, string value)
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,Auto");
        ColumnSpacing = 10;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Left;

        var pair = DictionaryFieldPairText.Split(value);
        var labels = DictionaryFieldPairText.Labels(definition);
        _labels = new PairFieldLabels(labels.First, labels.Second);

        _firstControl = new DictionaryPaletteTokenControl($"{definition.Label} · {labels.First}", definition.Options, pair.First, definition.IsEditable);
        _firstControl.ValueCommitted += (_, _) => SetValueFromControls();

        _secondControl = new DictionaryPaletteTokenControl($"{definition.Label} · {labels.Second}", definition.Options, pair.Second, definition.IsEditable);
        _secondControl.ValueCommitted += (_, _) => SetValueFromControls();

        var firstGroup = CreateGroup(labels.First, _firstControl);
        var secondGroup = CreateGroup(labels.Second, _secondControl);
        SetColumn(firstGroup, 0);
        SetColumn(secondGroup, 1);
        Children.Add(firstGroup);
        Children.Add(secondGroup);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public PairFieldLabels Labels => _labels;

    public bool RequiresLocalHorizontalViewport => !_usesSharedHeader;

    public void UseSharedHeader()
    {
        if (_usesSharedHeader) return;
        _usesSharedHeader = true;
        ColumnDefinitions = new ColumnDefinitions("*,*");
        HorizontalAlignment = HorizontalAlignment.Stretch;
        _firstControl.UseCompactWidth();
        _secondControl.UseCompactWidth();
        if (_firstControl.Parent is Panel firstParent)
        {
            firstParent.Children.Remove(_firstControl);
        }
        if (_secondControl.Parent is Panel secondParent)
        {
            secondParent.Children.Remove(_secondControl);
        }
        Children.Clear();
        SetColumn(_firstControl, 0);
        SetColumn(_secondControl, 1);
        Children.Add(_firstControl);
        Children.Add(_secondControl);
    }

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

    private static Border CreateGroup(string label, Control control)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.78,
        };

        Grid.SetColumn(control, 1);

        return new Border
        {
            Padding = new Thickness(10, 8),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#4C5664")),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("62,Auto"),
                ColumnSpacing = 10,
                Children =
                {
                    labelBlock,
                    control,
                },
            },
        };
    }
}
