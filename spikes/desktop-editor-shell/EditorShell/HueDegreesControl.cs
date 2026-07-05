using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Globalization;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class HueDegreesControl : Grid, IDictionaryValueControl
{
    private readonly Slider _slider;
    private readonly TextBox _textBox;
    private bool _isUpdating;
    private string _value;
    private string _lastCommittedValue;

    public HueDegreesControl(string value, bool isEditable)
    {
        _value = NormalizeHue(value);
        _lastCommittedValue = _value;
        ColumnDefinitions = new ColumnDefinitions("*,78");
        ColumnSpacing = 10;
        VerticalAlignment = VerticalAlignment.Center;

        var sliderHost = new Grid
        {
            Height = 36,
            VerticalAlignment = VerticalAlignment.Center,
        };
        sliderHost.Children.Add(new Border
        {
            Height = 12,
            CornerRadius = new CornerRadius(6),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Colors.Red, 0.00),
                    new GradientStop(Colors.Yellow, 0.17),
                    new GradientStop(Colors.Lime, 0.33),
                    new GradientStop(Colors.Cyan, 0.50),
                    new GradientStop(Colors.Blue, 0.67),
                    new GradientStop(Colors.Magenta, 0.83),
                    new GradientStop(Colors.Red, 1.00),
                },
            },
        });

        _slider = new Slider
        {
            Minimum = 0,
            Maximum = 360,
            TickFrequency = 30,
            IsSnapToTickEnabled = false,
            Value = HueNumber(_value),
            IsEnabled = isEditable,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
        };
        _slider.PropertyChanged += (_, change) =>
        {
            if (change.Property != Slider.ValueProperty || _isUpdating) return;

            SetLocalValue(Math.Round(_slider.Value).ToString(CultureInfo.InvariantCulture));
        };
        _slider.LostFocus += (_, _) => CommitValue();
        _slider.PointerReleased += (_, _) => CommitValue();
        sliderHost.Children.Add(_slider);
        Grid.SetColumn(sliderHost, 0);

        _textBox = new TextBox
        {
            Text = _value,
            IsReadOnly = !isEditable,
            MinHeight = 36,
            Width = 78,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        EditorTextBoxBehavior.Configure(_textBox);
        _textBox.TextChanged += (_, _) =>
        {
            if (_isUpdating) return;

            SetLocalValue(NormalizeHue(_textBox.Text ?? ""));
        };
        _textBox.LostFocus += (_, _) => CommitValue();
        _textBox.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter) return;

            CommitValue();
            args.Handled = true;
        };
        Grid.SetColumn(_textBox, 1);

        Children.Add(sliderHost);
        Children.Add(_textBox);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public string Value => _value;

    public void SetValue(string value)
    {
        var normalized = NormalizeHue(value);
        if (_value == normalized) return;

        _value = normalized;
        UpdateControls();
    }

    private void SetLocalValue(string value)
    {
        var normalized = NormalizeHue(value);
        if (_value == normalized) return;

        _value = normalized;
        UpdateControls();
        ValueChanged?.Invoke(this, _value);
    }

    private void CommitValue()
    {
        if (_lastCommittedValue == _value) return;

        _lastCommittedValue = _value;
        ValueCommitted?.Invoke(this, _value);
    }

    private void UpdateControls()
    {
        _isUpdating = true;
        _slider.Value = HueNumber(_value);
        if (_textBox.Text != _value)
        {
            _textBox.Text = _value;
        }
        _isUpdating = false;
    }

    private static double HueNumber(string value)
    {
        return NumericText.ClampedDouble(value, 0, 0, 360);
    }

    private static string NormalizeHue(string value)
    {
        return Math.Round(HueNumber(value)).ToString(CultureInfo.InvariantCulture);
    }
}
