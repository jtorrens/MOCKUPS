using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Globalization;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryAlignmentPlacementControl : Grid, IDictionaryValueControl
{
    private readonly EditorInstantComboBox _modeCombo;
    private readonly Slider _alignXSlider;
    private readonly Slider _alignYSlider;
    private readonly TextBox _alignXBox;
    private readonly TextBox _alignYBox;
    private readonly TextBox _offsetXBox;
    private readonly TextBox _offsetYBox;
    private AlignmentPlacementValue _value;
    private bool _isUpdating;

    public DictionaryAlignmentPlacementControl(FieldDefinition definition, string value)
    {
        _value = AlignmentPlacementValue.Parse(value);
        RowDefinitions = new RowDefinitions("Auto,Auto,Auto");
        RowSpacing = 8;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _modeCombo = new EditorInstantComboBox
        {
            ItemsSource =
            [
                new FieldOption(AlignmentPlacementValue.EdgeMode, "Edge anchor"),
                new FieldOption(AlignmentPlacementValue.CenterMode, "Center anchor"),
            ],
            SelectedItem = new FieldOption(_value.Mode, _value.Mode == AlignmentPlacementValue.CenterMode ? "Center anchor" : "Edge anchor"),
            IsEnabled = definition.IsEditable,
        };
        _modeCombo.SelectionChanged += (_, _) =>
        {
            if (_isUpdating || _modeCombo.SelectedItem is null) return;

            SetLocal(_value with { Mode = _modeCombo.SelectedItem.Value }, commit: true);
        };

        var topRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("160,*"),
            ColumnSpacing = 10,
        };
        topRow.Children.Add(Label("Mode"));
        Grid.SetColumn(_modeCombo, 1);
        topRow.Children.Add(_modeCombo);
        Grid.SetRow(topRow, 0);
        Children.Add(topRow);

        var alignGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("160,*,78"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 10,
            RowSpacing = 6,
        };
        _alignXSlider = CreateAlignSlider(_value.AlignX, definition.IsEditable);
        _alignYSlider = CreateAlignSlider(_value.AlignY, definition.IsEditable);
        _alignXBox = CreateDecimalBox(_value.AlignX, definition.IsEditable);
        _alignYBox = CreateDecimalBox(_value.AlignY, definition.IsEditable);
        AddSliderRow(alignGrid, 0, "Align X", _alignXSlider, _alignXBox);
        AddSliderRow(alignGrid, 1, "Align Y", _alignYSlider, _alignYBox);
        Grid.SetRow(alignGrid, 1);
        Children.Add(alignGrid);

        var bottomRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("160,Auto,90,Auto,90,*"),
            ColumnSpacing = 8,
        };
        bottomRow.Children.Add(Label("Offset"));
        var offsetXLabel = SmallLabel("X");
        Grid.SetColumn(offsetXLabel, 1);
        bottomRow.Children.Add(offsetXLabel);
        _offsetXBox = DictionaryTextBoxFactory.CreateCompactPair(_value.OffsetX.ToString(CultureInfo.InvariantCulture));
        _offsetXBox.IsReadOnly = !definition.IsEditable;
        Grid.SetColumn(_offsetXBox, 2);
        bottomRow.Children.Add(_offsetXBox);
        var offsetYLabel = SmallLabel("Y");
        Grid.SetColumn(offsetYLabel, 3);
        bottomRow.Children.Add(offsetYLabel);
        _offsetYBox = DictionaryTextBoxFactory.CreateCompactPair(_value.OffsetY.ToString(CultureInfo.InvariantCulture));
        _offsetYBox.IsReadOnly = !definition.IsEditable;
        Grid.SetColumn(_offsetYBox, 4);
        bottomRow.Children.Add(_offsetYBox);
        var preset = CreatePresetGrid(definition.IsEditable);
        Grid.SetColumn(preset, 5);
        bottomRow.Children.Add(preset);
        Grid.SetRow(bottomRow, 2);
        Children.Add(bottomRow);

        Hook();
        UpdateControls();
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        _value = AlignmentPlacementValue.Parse(value);
        UpdateControls();
    }

    private void Hook()
    {
        _alignXSlider.PropertyChanged += (_, args) =>
        {
            if (args.Property == RangeBase.ValueProperty && !_isUpdating)
            {
                SetLocal(_value with { AlignX = Snap(_alignXSlider.Value) }, commit: true);
            }
        };
        _alignYSlider.PropertyChanged += (_, args) =>
        {
            if (args.Property == RangeBase.ValueProperty && !_isUpdating)
            {
                SetLocal(_value with { AlignY = Snap(_alignYSlider.Value) }, commit: true);
            }
        };
        HookDecimalBox(_alignXBox, (number) => SetLocal(_value with { AlignX = Clamp01(number) }, commit: true));
        HookDecimalBox(_alignYBox, (number) => SetLocal(_value with { AlignY = Clamp01(number) }, commit: true));
        HookIntegerBox(_offsetXBox, (number) => SetLocal(_value with { OffsetX = number }, commit: true));
        HookIntegerBox(_offsetYBox, (number) => SetLocal(_value with { OffsetY = number }, commit: true));
    }

    private Grid CreatePresetGrid(bool isEditable)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("22,22,22"),
            RowDefinitions = new RowDefinitions("22,22,22"),
            ColumnSpacing = 3,
            RowSpacing = 3,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                var alignX = x * 0.5;
                var alignY = y * 0.5;
                var button = new Button
                {
                    Content = "•",
                    Width = 22,
                    Height = 22,
                    Padding = new Avalonia.Thickness(0),
                    IsEnabled = isEditable,
                };
                button.Click += (_, _) => SetLocal(_value with { AlignX = alignX, AlignY = alignY }, commit: true);
                Grid.SetColumn(button, x);
                Grid.SetRow(button, y);
                grid.Children.Add(button);
            }
        }

        return grid;
    }

    private void SetLocal(AlignmentPlacementValue value, bool commit)
    {
        var next = value with
        {
            AlignX = Clamp01(value.AlignX),
            AlignY = Clamp01(value.AlignY),
        };
        if (_value == next) return;

        _value = next;
        UpdateControls();
        var formatted = _value.ToJsonString();
        ValueChanged?.Invoke(this, formatted);
        if (commit)
        {
            ValueCommitted?.Invoke(this, formatted);
        }
    }

    private void UpdateControls()
    {
        _isUpdating = true;
        var selected = _value.Mode == AlignmentPlacementValue.CenterMode
            ? new FieldOption(AlignmentPlacementValue.CenterMode, "Center anchor")
            : new FieldOption(AlignmentPlacementValue.EdgeMode, "Edge anchor");
        _modeCombo.SelectedItem = selected;
        _alignXSlider.Value = _value.AlignX;
        _alignYSlider.Value = _value.AlignY;
        _alignXBox.Text = FormatAlign(_value.AlignX);
        _alignYBox.Text = FormatAlign(_value.AlignY);
        _offsetXBox.Text = _value.OffsetX.ToString(CultureInfo.InvariantCulture);
        _offsetYBox.Text = _value.OffsetY.ToString(CultureInfo.InvariantCulture);
        _isUpdating = false;
    }

    private static Slider CreateAlignSlider(double value, bool isEditable)
    {
        return new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = Clamp01(value),
            TickFrequency = 0.05,
            SmallChange = 0.05,
            LargeChange = 0.1,
            IsEnabled = isEditable,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static TextBox CreateDecimalBox(double value, bool isEditable)
    {
        return EditorTextBoxBehavior.Configure(new TextBox
        {
            Text = FormatAlign(value),
            Width = 78,
            IsReadOnly = !isEditable,
            VerticalContentAlignment = VerticalAlignment.Center,
        });
    }

    private static void AddSliderRow(Grid grid, int row, string label, Slider slider, TextBox box)
    {
        var text = Label(label);
        Grid.SetRow(text, row);
        grid.Children.Add(text);
        Grid.SetColumn(slider, 1);
        Grid.SetRow(slider, row);
        grid.Children.Add(slider);
        Grid.SetColumn(box, 2);
        Grid.SetRow(box, row);
        grid.Children.Add(box);
    }

    private static void HookDecimalBox(TextBox box, Action<double> commit)
    {
        void Commit()
        {
            if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant))
            {
                commit(invariant);
                return;
            }

            if (double.TryParse(box.Text, out var local))
            {
                commit(local);
            }
        }

        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter) return;
            Commit();
            args.Handled = true;
        };
    }

    private static void HookIntegerBox(TextBox box, Action<int> commit)
    {
        void Commit()
        {
            if (int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var invariant))
            {
                commit(invariant);
                return;
            }

            if (int.TryParse(box.Text, out var local))
            {
                commit(local);
            }
        }

        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter) return;
            Commit();
            args.Handled = true;
        };
    }

    private static TextBlock Label(string text)
    {
        return new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.78,
        };
    }

    private static TextBlock SmallLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            MinWidth = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.78,
        };
    }

    private static double Snap(double value)
    {
        return Clamp01(Math.Round(value / 0.05) * 0.05);
    }

    private static double Clamp01(double value)
    {
        return Math.Clamp(value, 0, 1);
    }

    private static string FormatAlign(double value)
    {
        return Clamp01(value).ToString("0.###", CultureInfo.InvariantCulture);
    }
}
