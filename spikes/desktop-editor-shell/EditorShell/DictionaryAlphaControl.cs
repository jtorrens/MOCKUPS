using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryAlphaControl : Grid, IDictionaryValueControl
{
    private readonly Slider _slider;
    private readonly TextBox _box;
    private bool _isUpdating;

    public DictionaryAlphaControl(string value, bool isEditable)
    {
        ColumnDefinitions = new ColumnDefinitions("*,Auto");
        ColumnSpacing = 8;
        Width = 188;
        VerticalAlignment = VerticalAlignment.Center;

        var alpha = PaletteAlphaPair.TryParseAlpha(value, out var parsed) ? parsed : 1;
        _slider = CreateSlider(alpha, isEditable);
        _box = CreateAlphaBox(alpha, isEditable);
        SetColumn(_box, 1);
        Children.Add(_slider);
        Children.Add(_box);

        Hook();
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public string Value => PaletteAlphaPair.FormatAlpha(_slider.Value);

    public void SetValue(string value)
    {
        var alpha = PaletteAlphaPair.TryParseAlpha(value, out var parsed) ? parsed : 1;
        _isUpdating = true;
        SetAlpha(_slider, _box, alpha);
        _isUpdating = false;
    }

    private void Hook()
    {
        _slider.PropertyChanged += (_, args) =>
        {
            if (args.Property != RangeBase.ValueProperty || _isUpdating)
            {
                return;
            }

            var snapped = PaletteAlphaPair.SnapAlpha(_slider.Value);
            _isUpdating = true;
            _slider.Value = snapped;
            _box.Text = PaletteAlphaPair.FormatAlpha(snapped);
            _isUpdating = false;
            CommitValue();
        };
        _box.LostFocus += (_, _) => CommitBoxValue(normalizeText: true);
        _box.TextChanged += (_, _) =>
        {
            if (_isUpdating || !PaletteAlphaPair.TryParseAlpha(_box.Text, out var value))
            {
                return;
            }

            _isUpdating = true;
            _slider.Value = value;
            _isUpdating = false;
            CommitValue();
        };
        _box.KeyDown += (_, args) =>
        {
            if (args.Key == Avalonia.Input.Key.Enter)
            {
                CommitBoxValue(normalizeText: true);
            }
        };
    }

    private void CommitBoxValue(bool normalizeText)
    {
        if (_isUpdating)
        {
            return;
        }

        if (!PaletteAlphaPair.TryParseAlpha(_box.Text, out var value))
        {
            value = _slider.Value;
        }

        _isUpdating = true;
        if (normalizeText)
        {
            SetAlpha(_slider, _box, value);
        }
        else
        {
            _slider.Value = value;
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

        ValueChanged?.Invoke(this, Value);
        ValueCommitted?.Invoke(this, Value);
    }

    public static Slider CreateSlider(double value, bool isEditable)
    {
        return new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = PaletteAlphaPair.ClampAlpha(value),
            TickFrequency = 0.05,
            SmallChange = 0.05,
            LargeChange = 0.1,
            Width = 126,
            IsEnabled = isEditable,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    public static TextBox CreateAlphaBox(double value, bool isEditable)
    {
        return EditorTextBoxBehavior.Configure(new TextBox
        {
            Text = PaletteAlphaPair.FormatAlpha(value),
            Width = 54,
            IsReadOnly = !isEditable,
            VerticalContentAlignment = VerticalAlignment.Center,
        });
    }

    public static void SetAlpha(Slider slider, TextBox box, double value)
    {
        var alpha = PaletteAlphaPair.ClampAlpha(value);
        slider.Value = alpha;
        box.Text = PaletteAlphaPair.FormatAlpha(alpha);
    }
}
