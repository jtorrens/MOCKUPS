using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryPaletteAlphaPairControl : Grid, IDictionaryValueControl, IDictionaryLocalHorizontalScrollControl
{
    private readonly DictionaryPaletteTokenControl _firstColorControl;
    private readonly DictionaryPaletteTokenControl _secondColorControl;
    private readonly Slider _firstAlphaSlider;
    private readonly Slider _secondAlphaSlider;
    private readonly TextBox _firstAlphaBox;
    private readonly TextBox _secondAlphaBox;
    private bool _isUpdating;

    public DictionaryPaletteAlphaPairControl(FieldDefinition definition, string value)
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,Auto");
        ColumnSpacing = 10;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Left;

        var pair = PaletteAlphaPair.Split(value);
        var labels = DictionaryFieldPairText.Labels(definition);

        _firstColorControl = new DictionaryPaletteTokenControl($"{definition.Label} · {labels.First}", definition.Options, pair.First.ColorToken, definition.IsEditable);
        _secondColorControl = new DictionaryPaletteTokenControl($"{definition.Label} · {labels.Second}", definition.Options, pair.Second.ColorToken, definition.IsEditable);
        _firstAlphaSlider = DictionaryAlphaControl.CreateSlider(pair.First.Alpha, definition.IsEditable);
        _secondAlphaSlider = DictionaryAlphaControl.CreateSlider(pair.Second.Alpha, definition.IsEditable);
        _firstAlphaBox = DictionaryAlphaControl.CreateAlphaBox(pair.First.Alpha, definition.IsEditable);
        _secondAlphaBox = DictionaryAlphaControl.CreateAlphaBox(pair.Second.Alpha, definition.IsEditable);

        Hook(_firstColorControl);
        Hook(_secondColorControl);
        Hook(_firstAlphaSlider, _firstAlphaBox);
        Hook(_secondAlphaSlider, _secondAlphaBox);

        var firstGroup = CreateGroup(labels.First, _firstColorControl, _firstAlphaSlider, _firstAlphaBox);
        var secondGroup = CreateGroup(labels.Second, _secondColorControl, _secondAlphaSlider, _secondAlphaBox);
        SetColumn(firstGroup, 0);
        SetColumn(secondGroup, 1);
        Children.Add(firstGroup);
        Children.Add(secondGroup);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        var pair = PaletteAlphaPair.Split(value);
        _isUpdating = true;
        _firstColorControl.SetValue(pair.First.ColorToken);
        _secondColorControl.SetValue(pair.Second.ColorToken);
        SetAlpha(_firstAlphaSlider, _firstAlphaBox, pair.First.Alpha);
        SetAlpha(_secondAlphaSlider, _secondAlphaBox, pair.Second.Alpha);
        _isUpdating = false;
    }

    private void Hook(DictionaryPaletteTokenControl control)
    {
        control.ValueCommitted += (_, _) => CommitValue();
    }

    private void Hook(Slider slider, TextBox box)
    {
        slider.PropertyChanged += (_, args) =>
        {
            if (args.Property != RangeBase.ValueProperty || _isUpdating)
            {
                return;
            }

            var snapped = PaletteAlphaPair.SnapAlpha(slider.Value);
            _isUpdating = true;
            slider.Value = snapped;
            box.Text = PaletteAlphaPair.FormatAlpha(snapped);
            _isUpdating = false;
            CommitValue();
        };
        box.LostFocus += (_, _) =>
        {
            if (_isUpdating)
            {
                return;
            }

            CommitBoxValue(slider, box, normalizeText: true);
        };
        box.TextChanged += (_, _) =>
        {
            if (_isUpdating || !PaletteAlphaPair.TryParseAlpha(box.Text, out var value))
            {
                return;
            }

            _isUpdating = true;
            slider.Value = value;
            _isUpdating = false;
            CommitValue();
        };
        box.KeyDown += (_, args) =>
        {
            if (args.Key != Avalonia.Input.Key.Enter)
            {
                return;
            }

            CommitBoxValue(slider, box, normalizeText: true);
        };
    }

    private void CommitBoxValue(Slider slider, TextBox box, bool normalizeText)
    {
        if (!PaletteAlphaPair.TryParseAlpha(box.Text, out var value))
        {
            value = slider.Value;
        }

        _isUpdating = true;
        if (normalizeText)
        {
            SetAlpha(slider, box, value);
        }
        else
        {
            slider.Value = value;
        }
        _isUpdating = false;
        CommitValue();
    }

    private void CommitValue()
    {
        if (_isUpdating)
        {
            return;
        }

        var value = PaletteAlphaPair.Join(
            new PaletteAlphaValue(_firstColorControl.Value, _firstAlphaSlider.Value),
            new PaletteAlphaValue(_secondColorControl.Value, _secondAlphaSlider.Value));
        ValueChanged?.Invoke(this, value);
        ValueCommitted?.Invoke(this, value);
    }

    private static Border CreateGroup(
        string label,
        Control colorControl,
        Slider alphaSlider,
        TextBox alphaBox)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.78,
        };
        Grid.SetColumn(colorControl, 1);

        var alphaGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children =
            {
                alphaSlider,
                alphaBox,
            },
        };
        Grid.SetColumn(alphaBox, 1);

        var stack = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("62,Auto"),
                    ColumnSpacing = 10,
                    Children =
                    {
                        labelBlock,
                        colorControl,
                    },
                },
                alphaGrid,
            },
        };

        return new Border
        {
            Padding = new Thickness(10, 8),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#4C5664")),
            Child = stack,
        };
    }

    private static void SetAlpha(Slider slider, TextBox box, double value)
    {
        DictionaryAlphaControl.SetAlpha(slider, box, value);
    }
}
