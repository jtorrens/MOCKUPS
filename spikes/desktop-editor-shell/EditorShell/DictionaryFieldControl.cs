using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryFieldControl : Grid
{
    private readonly FieldDefinition _definition;
    private readonly TextBlock _label;
    private readonly TextBox? _textBox;
    private readonly TextBox? _pairFirstTextBox;
    private readonly TextBox? _pairSecondTextBox;
    private readonly ComboBox? _comboBox;
    private readonly ComboBox? _pairFirstComboBox;
    private readonly ComboBox? _pairSecondComboBox;
    private readonly CheckBox? _checkBox;
    private readonly Border? _colorSwatch;
    private readonly ColorPicker? _colorPicker;
    private readonly Button _restoreButton;
    private readonly Func<string, ValueKind, Task<string?>>? _browsePath;
    private bool _isUpdatingColorControl;
    private string _defaultValue;
    private string _value;

    public DictionaryFieldControl(
        FieldValue fieldValue,
        Func<string, ValueKind, Task<string?>>? browsePath = null)
    {
        _definition = fieldValue.Definition;
        _defaultValue = fieldValue.Definition.DefaultValue;
        _value = fieldValue.Value;
        _browsePath = browsePath;

        ColumnDefinitions = _definition.ValueKind switch
        {
            ValueKind.DirectoryPath => new ColumnDefinitions("180,*,Auto,Auto"),
            ValueKind.ImageFilePath => new ColumnDefinitions("180,*,Auto,Auto"),
            ValueKind.HexColor => new ColumnDefinitions("180,28,*,Auto,Auto"),
            ValueKind.IntegerPair => new ColumnDefinitions("180,*,Auto"),
            ValueKind.OptionToken => new ColumnDefinitions("180,*,Auto"),
            ValueKind.PaletteColorToken => new ColumnDefinitions("180,*,Auto"),
            ValueKind.PaletteColorPair => new ColumnDefinitions("180,*,Auto"),
            _ => new ColumnDefinitions("180,*,Auto"),
        };
        ColumnSpacing = 12;
        MinHeight = _definition.ValueKind == ValueKind.StringMultiline ? 96 : 40;

        _label = new TextBlock
        {
            Text = _definition.Label,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = _definition.ValueKind == ValueKind.StringMultiline
                ? VerticalAlignment.Top
                : VerticalAlignment.Center,
            Margin = _definition.ValueKind == ValueKind.StringMultiline
                ? new Avalonia.Thickness(0, 7, 0, 0)
                : new Avalonia.Thickness(0),
        };
        SetColumn(_label, 0);

        if (_definition.ValueKind == ValueKind.Boolean)
        {
            _checkBox = new CheckBox
            {
                IsChecked = StringToBool(_value),
                IsEnabled = _definition.IsEditable,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _checkBox.PropertyChanged += (_, change) =>
            {
                if (change.Property != CheckBox.IsCheckedProperty) return;

                _value = BoolToString(_checkBox.IsChecked == true);
                UpdateState();
                ValueChanged?.Invoke(this, _value);
            };
            SetColumn(_checkBox, 1);
            Children.Add(_checkBox);
        }
        else if (_definition.ValueKind is ValueKind.OptionToken or ValueKind.PaletteColorToken)
        {
            _comboBox = CreateOptionComboBox(_value);
            _comboBox.SelectionChanged += (_, _) =>
            {
                if (_isUpdatingColorControl) return;

                _value = ComboValue(_comboBox);
                UpdateState();
                ValueChanged?.Invoke(this, _value);
            };
            SetColumn(_comboBox, 1);
            Children.Add(_comboBox);
        }
        else if (_definition.ValueKind == ValueKind.PaletteColorPair)
        {
            var pair = SplitPair(_value);
            var pairControl = CreatePalettePairControl(pair.First, pair.Second);
            _pairFirstComboBox = pairControl.FirstComboBox;
            _pairSecondComboBox = pairControl.SecondComboBox;
            SetColumn(pairControl.Control, 1);
            Children.Add(pairControl.Control);
        }
        else if (_definition.ValueKind == ValueKind.HexColor)
        {
            _colorSwatch = CreateColorSwatch(_value);
            SetColumn(_colorSwatch, 1);
            Children.Add(_colorSwatch);

            _textBox = CreateTextBox();
            _textBox.Text = _value;
            _textBox.TextChanged += (_, _) =>
            {
                if (_isUpdatingColorControl) return;

                _value = NormalizeHex(_textBox.Text ?? "");
                UpdateColorControlsFromValue();
                UpdateState();
                ValueChanged?.Invoke(this, _value);
            };
            SetColumn(_textBox, 2);
            Children.Add(_textBox);

            _colorPicker = new ColorPicker
            {
                Color = ParseColor(_value),
                Width = 38,
                Height = 34,
                Padding = new Avalonia.Thickness(0),
                IsEnabled = _definition.IsEditable,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _colorPicker.ColorChanged += (_, args) =>
            {
                if (_isUpdatingColorControl) return;

                SetValue(ColorToHex(args.NewColor));
            };
            SetColumn(_colorPicker, 3);
            Children.Add(_colorPicker);
        }
        else if (_definition.ValueKind == ValueKind.IntegerPair)
        {
            var pair = SplitPair(_value);
            var pairControl = CreatePairControl(pair.First, pair.Second);
            _pairFirstTextBox = pairControl.FirstTextBox;
            _pairSecondTextBox = pairControl.SecondTextBox;
            SetColumn(pairControl.Control, 1);
            Children.Add(pairControl.Control);
        }
        else
        {
            _textBox = CreateTextBox();
            _textBox.Text = _value;
            _textBox.TextChanged += (_, _) =>
            {
                _value = _textBox.Text ?? "";
                UpdateState();
                ValueChanged?.Invoke(this, _value);
            };
            SetColumn(_textBox, 1);
            Children.Add(_textBox);
        }

        if (_definition.ValueKind is ValueKind.DirectoryPath or ValueKind.ImageFilePath)
        {
            var browseButton = new Button
            {
                Content = "Browse",
                MinWidth = 86,
                MinHeight = 36,
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = _definition.IsEditable && _browsePath is not null,
            };
            browseButton.Click += async (_, _) =>
            {
                if (_browsePath is null) return;

                var selectedPath = await _browsePath(_value, _definition.ValueKind);
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    SetValue(selectedPath);
                }
            };
            SetColumn(browseButton, 2);
            Children.Add(browseButton);
        }

        _restoreButton = new Button
        {
            Content = "↺",
            Width = 32,
            Height = 32,
            Padding = new Avalonia.Thickness(0),
            VerticalAlignment = _definition.ValueKind == ValueKind.StringMultiline
                ? VerticalAlignment.Top
                : VerticalAlignment.Center,
            IsVisible = !IsDefault && _definition.IsEditable,
        };
        _restoreButton.Click += (_, _) =>
        {
            SetValue(_defaultValue);
        };
        SetColumn(_restoreButton, _definition.ValueKind switch
        {
            ValueKind.DirectoryPath => 3,
            ValueKind.ImageFilePath => 3,
            ValueKind.HexColor => 4,
            _ => 2,
        });

        Children.Add(_label);
        Children.Add(_restoreButton);
        UpdateState();
    }

    public event EventHandler<string>? ValueChanged;

    public bool IsDefault => _value == _defaultValue;

    public void AcceptCurrentValueAsDefault()
    {
        _defaultValue = _value;
        UpdateState();
    }

    public void SetValue(string value)
    {
        if (_value == value) return;
        _value = value;
        if (_checkBox is not null)
        {
            _checkBox.IsChecked = StringToBool(value);
        }
        else if (_definition.ValueKind == ValueKind.HexColor)
        {
            UpdateColorControlsFromValue();
        }
        else if (_definition.ValueKind == ValueKind.IntegerPair)
        {
            UpdatePairControlsFromValue();
        }
        else if (_definition.ValueKind is ValueKind.OptionToken or ValueKind.PaletteColorToken)
        {
            UpdateOptionComboFromValue();
        }
        else if (_definition.ValueKind == ValueKind.PaletteColorPair)
        {
            UpdatePalettePairControlsFromValue();
        }
        else if (_textBox is not null)
        {
            _textBox.Text = value;
        }

        UpdateState();
        ValueChanged?.Invoke(this, _value);
    }

    private TextBox CreateTextBox()
    {
        var textBox = new TextBox
        {
            IsReadOnly = !_definition.IsEditable || _definition.ValueKind == ValueKind.StringReadOnly,
            AcceptsReturn = _definition.ValueKind == ValueKind.StringMultiline,
            TextWrapping = _definition.ValueKind == ValueKind.StringMultiline
                ? TextWrapping.Wrap
                : TextWrapping.NoWrap,
            MinHeight = _definition.ValueKind == ValueKind.StringMultiline ? 88 : 36,
            PlaceholderText = _definition.ValueKind == ValueKind.DirectoryPath ? "Select folder…" : null,
            VerticalContentAlignment = _definition.ValueKind == ValueKind.StringMultiline
                ? VerticalAlignment.Top
                : VerticalAlignment.Center,
        };
        if (_definition.ValueKind == ValueKind.ImageFilePath)
        {
            textBox.MaxWidth = 420;
            textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        return textBox;
    }

    private (Control Control, TextBox FirstTextBox, TextBox SecondTextBox) CreatePairControl(string first, string second)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,90,Auto,90"),
            ColumnSpacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var labels = PairLabels(_definition.Id);
        var firstLabel = new TextBlock
        {
            Text = labels.First,
            MinWidth = 57,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.78,
        };
        Grid.SetColumn(firstLabel, 0);

        var firstTextBox = CreateCompactPairTextBox(first);
        firstTextBox.TextChanged += (_, _) => SetPairValueFromTextBoxes(firstTextBox, null);
        Grid.SetColumn(firstTextBox, 1);

        var secondLabel = new TextBlock
        {
            Text = labels.Second,
            MinWidth = 57,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.78,
        };
        Grid.SetColumn(secondLabel, 2);

        var secondTextBox = CreateCompactPairTextBox(second);
        secondTextBox.TextChanged += (_, _) => SetPairValueFromTextBoxes(null, secondTextBox);
        Grid.SetColumn(secondTextBox, 3);

        grid.Children.Add(firstLabel);
        grid.Children.Add(firstTextBox);
        grid.Children.Add(secondLabel);
        grid.Children.Add(secondTextBox);
        return (grid, firstTextBox, secondTextBox);
    }

    private ComboBox CreateOptionComboBox(string value)
    {
        var comboBox = new ComboBox
        {
            MinHeight = 36,
            MinWidth = 220,
            IsEnabled = _definition.IsEditable,
            ItemsSource = _definition.Options ?? [],
            ItemTemplate = new FuncDataTemplate<FieldOption>((option, _) => option is null
                ? new TextBlock()
                : CreateOptionView(option)),
            SelectionBoxItemTemplate = new FuncDataTemplate<FieldOption>((option, _) => option is null
                ? new TextBlock()
                : CreateOptionView(option)),
        };

        comboBox.SelectedItem = SelectedOption(value);
        return comboBox;
    }

    private (Control Control, ComboBox FirstComboBox, ComboBox SecondComboBox) CreatePalettePairControl(string first, string second)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,180,Auto,180"),
            ColumnSpacing = 14,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var labels = PairLabels(_definition.Id);
        var firstLabel = new TextBlock
        {
            Text = labels.First,
            MinWidth = 57,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.78,
        };
        Grid.SetColumn(firstLabel, 0);

        var firstCombo = CreateOptionComboBox(first);
        firstCombo.SelectionChanged += (_, _) => SetPalettePairValueFromComboBoxes(firstCombo, null);
        Grid.SetColumn(firstCombo, 1);

        var secondLabel = new TextBlock
        {
            Text = labels.Second,
            MinWidth = 57,
            Margin = new Avalonia.Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.78,
        };
        Grid.SetColumn(secondLabel, 2);

        var secondCombo = CreateOptionComboBox(second);
        secondCombo.SelectionChanged += (_, _) => SetPalettePairValueFromComboBoxes(null, secondCombo);
        Grid.SetColumn(secondCombo, 3);

        grid.Children.Add(firstLabel);
        grid.Children.Add(firstCombo);
        grid.Children.Add(secondLabel);
        grid.Children.Add(secondCombo);
        return (grid, firstCombo, secondCombo);
    }

    private static Control CreateOptionView(FieldOption option)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("18,*"),
            ColumnSpacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (!string.IsNullOrWhiteSpace(option.ColorHex))
        {
            var swatch = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(ParseColor(option.ColorHex)),
                BorderBrush = new SolidColorBrush(Color.Parse("#667085")),
                BorderThickness = new Avalonia.Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(swatch, 0);
            grid.Children.Add(swatch);
        }

        var label = new TextBlock
        {
            Text = option.Label,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 1);

        grid.Children.Add(label);
        return grid;
    }

    private FieldOption? SelectedOption(string value)
    {
        return (_definition.Options ?? []).FirstOrDefault((option) => option.Value == value)
            ?? (_definition.Options ?? []).FirstOrDefault();
    }

    private static string ComboValue(ComboBox comboBox)
    {
        return comboBox.SelectedItem is FieldOption option ? option.Value : "";
    }

    private void SetPalettePairValueFromComboBoxes(ComboBox? firstComboBox, ComboBox? secondComboBox)
    {
        if (_isUpdatingColorControl) return;

        _value = JoinPair(
            firstComboBox is not null ? ComboValue(firstComboBox) : (_pairFirstComboBox is not null ? ComboValue(_pairFirstComboBox) : ""),
            secondComboBox is not null ? ComboValue(secondComboBox) : (_pairSecondComboBox is not null ? ComboValue(_pairSecondComboBox) : ""));
        UpdateState();
        ValueChanged?.Invoke(this, _value);
    }

    private void UpdateOptionComboFromValue()
    {
        if (_comboBox is null) return;

        _isUpdatingColorControl = true;
        _comboBox.SelectedItem = SelectedOption(_value);
        _isUpdatingColorControl = false;
    }

    private void UpdatePalettePairControlsFromValue()
    {
        var pair = SplitPair(_value);
        _isUpdatingColorControl = true;
        if (_pairFirstComboBox is not null)
        {
            _pairFirstComboBox.SelectedItem = SelectedOption(pair.First);
        }

        if (_pairSecondComboBox is not null)
        {
            _pairSecondComboBox.SelectedItem = SelectedOption(pair.Second);
        }

        _isUpdatingColorControl = false;
    }

    private static TextBox CreateCompactPairTextBox(string value)
    {
        return new TextBox
        {
            Text = value,
            Width = 90,
            MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
    }

    private void SetPairValueFromTextBoxes(TextBox? firstTextBox, TextBox? secondTextBox)
    {
        if (_isUpdatingColorControl) return;

        _value = JoinPair(
            firstTextBox?.Text ?? _pairFirstTextBox?.Text ?? "",
            secondTextBox?.Text ?? _pairSecondTextBox?.Text ?? "");
        UpdateState();
        ValueChanged?.Invoke(this, _value);
    }

    private void UpdatePairControlsFromValue()
    {
        var pair = SplitPair(_value);
        _isUpdatingColorControl = true;
        if (_pairFirstTextBox is not null && _pairFirstTextBox.Text != pair.First)
        {
            _pairFirstTextBox.Text = pair.First;
        }

        if (_pairSecondTextBox is not null && _pairSecondTextBox.Text != pair.Second)
        {
            _pairSecondTextBox.Text = pair.Second;
        }

        _isUpdatingColorControl = false;
    }

    private static (string First, string Second) PairLabels(string fieldId)
    {
        return fieldId switch
        {
            var id when id.EndsWith(".size", StringComparison.Ordinal) || id.EndsWith(".renderSize", StringComparison.Ordinal) => ("W", "H"),
            var id when id.EndsWith(".position", StringComparison.Ordinal) => ("X", "Y"),
            var id when id.EndsWith(".vertical", StringComparison.Ordinal) => ("Top", "Bottom"),
            var id when id.EndsWith(".horizontal", StringComparison.Ordinal) => ("Left", "Right"),
            var id when id.EndsWith(".modes", StringComparison.Ordinal) => ("Light", "Dark"),
            _ => ("A", "B"),
        };
    }

    private static (string First, string Second) SplitPair(string value)
    {
        var parts = value.Split('|', 2);
        return (parts.ElementAtOrDefault(0) ?? "", parts.ElementAtOrDefault(1) ?? "");
    }

    private static string JoinPair(string first, string second)
    {
        return $"{first}|{second}";
    }

    private static Border CreateColorSwatch(string value)
    {
        return new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(ParseColor(value)),
            BorderBrush = new SolidColorBrush(Color.Parse("#667085")),
            BorderThickness = new Avalonia.Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
    }

    private void UpdateColorControlsFromValue()
    {
        _isUpdatingColorControl = true;
        var color = ParseColor(_value);
        if (_colorSwatch is not null)
        {
            _colorSwatch.Background = new SolidColorBrush(color);
        }

        if (_colorPicker is not null)
        {
            _colorPicker.Color = color;
        }

        if (_textBox is not null && _textBox.Text != _value)
        {
            _textBox.Text = _value;
        }

        _isUpdatingColorControl = false;
    }

    private static string NormalizeHex(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 6 && !trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            trimmed = $"#{trimmed}";
        }

        return trimmed;
    }

    private static Color ParseColor(string value)
    {
        try
        {
            return Color.Parse(string.IsNullOrWhiteSpace(value) ? "#808080" : NormalizeHex(value));
        }
        catch (FormatException)
        {
            return Color.Parse("#808080");
        }
    }

    private static string ColorToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static bool StringToBool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value == "1";
    }

    private static string BoolToString(bool value)
    {
        return value ? "true" : "false";
    }

    private void UpdateState()
    {
        var isDefault = IsDefault;
        _restoreButton.IsVisible = !isDefault && _definition.IsEditable;
        if (isDefault)
        {
            _label.ClearValue(TextBlock.ForegroundProperty);
        }
        else
        {
            _label.Foreground = new SolidColorBrush(Color.Parse("#D6A638"));
        }

        PseudoClasses.Set(":changed", !isDefault);
    }
}
