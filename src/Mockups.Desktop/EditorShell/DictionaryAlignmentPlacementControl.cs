using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryAlignmentPlacementControl : Grid, IDictionaryValueControl
{
    private readonly Border _card;
    private readonly Button _headerButton;
    private readonly TextBlock _chevron;
    private readonly TextBlock _summary;
    private readonly StackPanel _content;
    private readonly EditorInstantComboBox _modeCombo;
    private readonly Slider _alignXSlider;
    private readonly Slider _alignYSlider;
    private readonly Slider _offsetXSlider;
    private readonly Slider _offsetYSlider;
    private readonly TextBox _alignXBox;
    private readonly TextBox _alignYBox;
    private readonly TextBox _offsetXBox;
    private readonly TextBox _offsetYBox;
    private readonly List<AnchorButton> _anchorButtons = [];
    private AlignmentPlacementValue _value;
    private bool _isUpdating;
    private bool _isExpanded;

    private sealed record AnchorButton(Button Button, Border Dot, double AlignX, double AlignY);

    public DictionaryAlignmentPlacementControl(FieldDefinition definition, string value)
    {
        _value = AlignmentPlacementValue.Parse(value);
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _chevron = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 18,
            TextAlignment = TextAlignment.Center,
        };
        _summary = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = FontWeight.SemiBold,
        };
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 6,
        };
        header.Children.Add(_chevron);
        Grid.SetColumn(_summary, 1);
        header.Children.Add(_summary);
        _headerButton = new Button
        {
            Content = header,
            MinHeight = 36,
            Padding = new Avalonia.Thickness(8, 5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            BorderThickness = new Avalonia.Thickness(0),
        };
        _headerButton.Click += (_, _) => ToggleExpanded();

        _content = new StackPanel
        {
            Spacing = 8,
            IsVisible = false,
            Margin = new Avalonia.Thickness(8, 0, 8, 8),
        };

        _card = new Border
        {
            CornerRadius = new Avalonia.CornerRadius(8),
            BorderThickness = new Avalonia.Thickness(1),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    _headerButton,
                    _content,
                },
            },
        };
        Children.Add(_card);

        _modeCombo = new EditorInstantComboBox
        {
            ItemsSource =
            [
                new FieldOption(AlignmentPlacementValue.CenterMode, "Center anchor"),
                new FieldOption(AlignmentPlacementValue.InsideEdgeMode, "Inside edge"),
                new FieldOption(AlignmentPlacementValue.OutsideEdgeMode, "Outside edge"),
            ],
            SelectedItem = ModeOption(_value.Mode),
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
        _content.Children.Add(topRow);

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
        _content.Children.Add(alignGrid);

        var offsetGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("160,*,78"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 10,
            RowSpacing = 6,
        };
        _offsetXSlider = CreateOffsetSlider(_value.OffsetX, definition.IsEditable);
        _offsetYSlider = CreateOffsetSlider(_value.OffsetY, definition.IsEditable);
        _offsetXBox = DictionaryTextBoxFactory.CreateCompactPair(_value.OffsetX.ToString(CultureInfo.InvariantCulture));
        _offsetXBox.IsReadOnly = !definition.IsEditable;
        _offsetYBox = DictionaryTextBoxFactory.CreateCompactPair(_value.OffsetY.ToString(CultureInfo.InvariantCulture));
        _offsetYBox.IsReadOnly = !definition.IsEditable;
        AddSliderRow(offsetGrid, 0, "Offset X", _offsetXSlider, _offsetXBox);
        AddSliderRow(offsetGrid, 1, "Offset Y", _offsetYSlider, _offsetYBox);
        _content.Children.Add(offsetGrid);

        var anchorRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("160,*"),
            ColumnSpacing = 10,
        };
        anchorRow.Children.Add(Label("Anchors"));
        var anchors = CreateAnchorGrid(definition.IsEditable);
        Grid.SetColumn(anchors, 1);
        anchorRow.Children.Add(anchors);
        _content.Children.Add(anchorRow);

        Hook();
        ActualThemeVariantChanged += (_, _) => ApplyThemeBrushes();
        ApplyThemeBrushes();
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
        _offsetXSlider.PropertyChanged += (_, args) =>
        {
            if (args.Property == RangeBase.ValueProperty && !_isUpdating)
            {
                SetLocal(_value with { OffsetX = SnapOffset(_offsetXSlider.Value) }, commit: true);
            }
        };
        _offsetYSlider.PropertyChanged += (_, args) =>
        {
            if (args.Property == RangeBase.ValueProperty && !_isUpdating)
            {
                SetLocal(_value with { OffsetY = SnapOffset(_offsetYSlider.Value) }, commit: true);
            }
        };
        HookIntegerBox(_offsetXBox, (number) => SetLocal(_value with { OffsetX = number }, commit: true));
        HookIntegerBox(_offsetYBox, (number) => SetLocal(_value with { OffsetY = number }, commit: true));
    }

    private Grid CreateAnchorGrid(bool isEditable)
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
                var dot = new Border
                {
                    Width = 6,
                    Height = 6,
                    CornerRadius = new Avalonia.CornerRadius(3),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var button = new Button
                {
                    Content = dot,
                    Width = 22,
                    Height = 22,
                    Padding = new Avalonia.Thickness(0),
                    IsEnabled = isEditable,
                };
                button.Click += (_, _) => SetLocal(_value with { AlignX = alignX, AlignY = alignY }, commit: true);
                Grid.SetColumn(button, x);
                Grid.SetRow(button, y);
                grid.Children.Add(button);
                _anchorButtons.Add(new AnchorButton(button, dot, alignX, alignY));
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
        var selected = ModeOption(_value.Mode);
        _modeCombo.SelectedItem = selected;
        _alignXSlider.Value = _value.AlignX;
        _alignYSlider.Value = _value.AlignY;
        _alignXBox.Text = FormatAlign(_value.AlignX);
        _alignYBox.Text = FormatAlign(_value.AlignY);
        _offsetXSlider.Value = ClampOffset(_value.OffsetX);
        _offsetYSlider.Value = ClampOffset(_value.OffsetY);
        _offsetXBox.Text = _value.OffsetX.ToString(CultureInfo.InvariantCulture);
        _offsetYBox.Text = _value.OffsetY.ToString(CultureInfo.InvariantCulture);
        _summary.Text = Summary(_value);
        _chevron.Text = _isExpanded ? "v" : ">";
        UpdateAnchorBrushes();
        _isUpdating = false;
    }

    private void ToggleExpanded()
    {
        _isExpanded = !_isExpanded;
        _content.IsVisible = _isExpanded;
        _chevron.Text = _isExpanded ? "v" : ">";
    }

    private void ApplyThemeBrushes()
    {
        var isLight = ActualThemeVariant == ThemeVariant.Light;
        _card.Background = new SolidColorBrush(Color.Parse(isLight ? "#12000000" : "#12FFFFFF"));
        _card.BorderBrush = new SolidColorBrush(Color.Parse(isLight ? "#22000000" : "#22FFFFFF"));
        _headerButton.Background = Brushes.Transparent;
        UpdateAnchorBrushes();
    }

    private void UpdateAnchorBrushes()
    {
        var isLight = ActualThemeVariant == ThemeVariant.Light;
        var selectedDot = EditorSukiWindowTheme.AccentBrush();
        var neutralDot = new SolidColorBrush(Color.Parse(isLight ? "#73000000" : "#8AFFFFFF"));
        var selectedBackground = EditorSukiWindowTheme.AccentBrush(0x24);
        var selectedBorder = EditorSukiWindowTheme.AccentBrush(0x80);
        foreach (var anchor in _anchorButtons)
        {
            var isSelected = AreSameAnchor(_value.AlignX, anchor.AlignX) && AreSameAnchor(_value.AlignY, anchor.AlignY);
            anchor.Button.Background = isSelected ? selectedBackground : Brushes.Transparent;
            anchor.Button.BorderBrush = isSelected ? selectedBorder : Brushes.Transparent;
            anchor.Dot.Background = isSelected ? selectedDot : neutralDot;
        }
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

    private static Slider CreateOffsetSlider(int value, bool isEditable)
    {
        return new Slider
        {
            Minimum = -200,
            Maximum = 200,
            Value = ClampOffset(value),
            TickFrequency = 1,
            SmallChange = 1,
            LargeChange = 8,
            IsEnabled = isEditable,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static TextBox CreateDecimalBox(double value, bool isEditable)
    {
        return EditorNumericTextStyle.Apply(new TextBox
        {
            Text = FormatAlign(value),
            Width = EditorUiDensity.TextAwareWidth(78),
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

    private static double Snap(double value)
    {
        return Clamp01(Math.Round(value / 0.05) * 0.05);
    }

    private static double Clamp01(double value)
    {
        return Math.Clamp(value, 0, 1);
    }

    private static int SnapOffset(double value)
    {
        return (int)Math.Round(ClampOffset(value));
    }

    private static double ClampOffset(double value)
    {
        return Math.Clamp(value, -200, 200);
    }

    private static bool AreSameAnchor(double left, double right)
    {
        return Math.Abs(left - right) < 0.001;
    }

    private static string FormatAlign(double value)
    {
        return Clamp01(value).ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Summary(AlignmentPlacementValue value)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{value.Mode} ({FormatAlign(value.AlignX)}, {FormatAlign(value.AlignY)}) ({value.OffsetX}, {value.OffsetY})");
    }

    private static FieldOption ModeOption(string mode)
    {
        return mode switch
        {
            AlignmentPlacementValue.CenterMode => new FieldOption(AlignmentPlacementValue.CenterMode, "Center anchor"),
            AlignmentPlacementValue.InsideEdgeMode => new FieldOption(AlignmentPlacementValue.InsideEdgeMode, "Inside edge"),
            AlignmentPlacementValue.OutsideEdgeMode => new FieldOption(AlignmentPlacementValue.OutsideEdgeMode, "Outside edge"),
            _ => throw new InvalidOperationException($"Alignment placement mode '{mode}' is not supported."),
        };
    }
}
