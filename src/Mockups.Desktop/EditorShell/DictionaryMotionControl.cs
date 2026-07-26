using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Mockups.DesktopEditorShell.Common;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryMotionControl : Grid, IDictionaryValueControl
{
    private static readonly FieldOption[] TransitionOptions =
    [
        new(MotionVariantValue.None, "None"),
        new(MotionVariantValue.Slide, "Slide"),
        new(MotionVariantValue.Swipe, "Swipe"),
        new(MotionVariantValue.ScaleTransition, "Scale"),
    ];

    private static readonly FieldOption[] DirectionOptions =
    [
        new(MotionVariantValue.Top, "Top"),
        new(MotionVariantValue.Bottom, "Bottom"),
        new(MotionVariantValue.Left, "Left"),
        new(MotionVariantValue.Right, "Right"),
    ];

    private static readonly FieldOption[] BoundsOptions =
    [
        new(MotionVariantValue.Parent, "Parent"),
        new(MotionVariantValue.Screen, "Screen"),
    ];

    private readonly Border _card;
    private readonly Button _headerButton;
    private readonly TextBlock _summary;
    private readonly TextBlock _chevron;
    private readonly StackPanel _content;
    private readonly EditorInstantComboBox _transitionCombo;
    private readonly EditorInstantComboBox _directionCombo;
    private readonly EditorInstantComboBox _boundsCombo;
    private readonly ToggleSwitch _fadeSwitch;
    private readonly ToggleSwitch _translateSwitch;
    private readonly ToggleSwitch _scaleSwitch;
    private MotionVariantValue _value;
    private bool _isExpanded;
    private bool _isUpdating;

    public DictionaryMotionControl(FieldDefinition definition, string value)
    {
        _value = MotionVariantValue.Parse(value);
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _summary = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = FontWeight.SemiBold,
        };
        _chevron = new TextBlock
        {
            Text = ">",
            Width = 18,
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
        };
        header.Children.Add(_summary);
        Grid.SetColumn(_chevron, 1);
        header.Children.Add(_chevron);

        _headerButton = new Button
        {
            Content = header,
            MinHeight = 36,
            Padding = new Avalonia.Thickness(10, 5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            BorderThickness = new Avalonia.Thickness(0),
        };
        _headerButton.Click += (_, _) => ToggleExpanded();

        _content = new StackPanel
        {
            Spacing = 8,
            Margin = new Avalonia.Thickness(10, 0, 10, 10),
            IsVisible = false,
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

        _transitionCombo = CreateCombo(TransitionOptions, definition.IsEditable);
        _directionCombo = CreateCombo(DirectionOptions, definition.IsEditable);
        _boundsCombo = CreateCombo(BoundsOptions, definition.IsEditable);
        _fadeSwitch = CreateSwitch(definition.IsEditable);
        _translateSwitch = CreateSwitch(definition.IsEditable);
        _scaleSwitch = CreateSwitch(definition.IsEditable);

        _content.Children.Add(CreateRow("Transition", _transitionCombo));
        _content.Children.Add(CreateRow("Direction", _directionCombo));
        _content.Children.Add(CreateRow("Bounds", _boundsCombo));
        _content.Children.Add(CreateRow("Fade", _fadeSwitch));
        _content.Children.Add(CreateRow("Translate", _translateSwitch));
        _content.Children.Add(CreateRow("Scale", _scaleSwitch));

        Hook();
        ActualThemeVariantChanged += (_, _) => ApplyThemeBrushes();
        ApplyThemeBrushes();
        UpdateControls();
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        _value = MotionVariantValue.Parse(value);
        UpdateControls();
    }

    private void ToggleExpanded()
    {
        _isExpanded = !_isExpanded;
        _content.IsVisible = _isExpanded;
        _chevron.Text = _isExpanded ? "v" : ">";
    }

    private void Hook()
    {
        _transitionCombo.SelectionChanged += (_, _) =>
        {
            if (_isUpdating || _transitionCombo.SelectedItem is not FieldOption option) return;
            SetLocal(_value with { Transition = option.Value }, commit: true);
        };
        _directionCombo.SelectionChanged += (_, _) =>
        {
            if (_isUpdating || _directionCombo.SelectedItem is not FieldOption option) return;
            SetLocal(_value with { Direction = option.Value }, commit: true);
        };
        _boundsCombo.SelectionChanged += (_, _) =>
        {
            if (_isUpdating || _boundsCombo.SelectedItem is not FieldOption option) return;
            SetLocal(_value with { Bounds = option.Value }, commit: true);
        };
        _fadeSwitch.PropertyChanged += (_, args) =>
        {
            if (_isUpdating || args.Property != ToggleSwitch.IsCheckedProperty) return;
            SetLocal(_value with { Fade = _fadeSwitch.IsChecked == true }, commit: true);
        };
        _translateSwitch.PropertyChanged += (_, args) =>
        {
            if (_isUpdating || args.Property != ToggleSwitch.IsCheckedProperty) return;
            SetLocal(_value with { Translate = _translateSwitch.IsChecked == true }, commit: true);
        };
        _scaleSwitch.PropertyChanged += (_, args) =>
        {
            if (_isUpdating || args.Property != ToggleSwitch.IsCheckedProperty) return;
            SetLocal(_value with { Scale = _scaleSwitch.IsChecked == true }, commit: true);
        };
    }

    private void UpdateControls()
    {
        _isUpdating = true;
        SetCombo(_transitionCombo, TransitionOptions, _value.Transition);
        SetCombo(_directionCombo, DirectionOptions, _value.Direction);
        SetCombo(_boundsCombo, BoundsOptions, _value.Bounds);
        _fadeSwitch.IsChecked = _value.Fade;
        _translateSwitch.IsChecked = _value.Translate;
        _scaleSwitch.IsChecked = _value.Scale;
        _summary.Text = _value.Summary();
        _isUpdating = false;
    }

    private void SetLocal(MotionVariantValue value, bool commit)
    {
        _value = value;
        UpdateControls();
        var json = _value.ToJsonString();
        ValueChanged?.Invoke(this, json);
        if (commit)
        {
            ValueCommitted?.Invoke(this, json);
        }
    }

    private static EditorInstantComboBox CreateCombo(FieldOption[] options, bool isEditable)
    {
        return new EditorInstantComboBox
        {
            ItemsSource = options,
            IsEnabled = isEditable,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
    }

    private static ToggleSwitch CreateSwitch(bool isEditable)
    {
        return new ToggleSwitch
        {
            IsEnabled = isEditable,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static Grid CreateRow(string label, Control control)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("120,*"),
            ColumnSpacing = 10,
            MinHeight = 36,
        };
        row.Children.Add(new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold,
        });
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        return row;
    }

    private static void SetCombo(EditorInstantComboBox combo, FieldOption[] options, string value)
    {
        combo.SelectedItem = Array.Find(options, (option) => option.Value.Equals(value, StringComparison.Ordinal))
            ?? options[0];
    }

    private void ApplyThemeBrushes()
    {
        var isLight = ActualThemeVariant == ThemeVariant.Light;
        _card.Background = new SolidColorBrush(Color.Parse(isLight ? "#12000000" : "#12FFFFFF"));
        _card.BorderBrush = new SolidColorBrush(Color.Parse(isLight ? "#22000000" : "#22FFFFFF"));
        _headerButton.Background = Brushes.Transparent;
    }
}
