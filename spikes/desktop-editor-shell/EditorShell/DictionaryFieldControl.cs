using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
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
    private readonly HueDegreesControl? _hueControl;
    private readonly ComboBox? _comboBox;
    private readonly ComboBox? _pairFirstComboBox;
    private readonly ComboBox? _pairSecondComboBox;
    private readonly ToggleSwitch? _toggleSwitch;
    private readonly IconSlotsControl? _iconSlotsControl;
    private readonly Button? _themeTokenButton;
    private readonly Border? _colorSwatch;
    private readonly Button? _colorPickerButton;
    private readonly Button _restoreButton;
    private readonly Func<string, ValueKind, Task<string?>>? _browsePath;
    private readonly Func<string, bool, Task<string?>>? _showIconTokenPicker;
    private readonly Func<string, IReadOnlyList<FieldOption>?, Task<string?>>? _showThemeTokenPicker;
    private readonly Func<string, Control>? _createIconPreview;
    private bool _isUpdatingColorControl;
    private bool _isInherited;
    private string _defaultValue;
    private string _value;
    private string _lastCommittedValue;

    public DictionaryFieldControl(
        FieldValue fieldValue,
        Func<string, ValueKind, Task<string?>>? browsePath = null,
        Func<string, bool, Task<string?>>? showIconTokenPicker = null,
        Func<string, IReadOnlyList<FieldOption>?, Task<string?>>? showThemeTokenPicker = null,
        Func<string, Control>? createIconPreview = null)
    {
        _definition = fieldValue.Definition;
        _isInherited = fieldValue.IsInherited;
        _defaultValue = fieldValue.Definition.DefaultValue;
        _value = fieldValue.IsInherited ? fieldValue.Definition.InheritedValue : fieldValue.Value;
        _lastCommittedValue = fieldValue.IsInherited ? fieldValue.Definition.InheritedStorageValue : fieldValue.Value;
        _browsePath = browsePath;
        _showIconTokenPicker = showIconTokenPicker;
        _showThemeTokenPicker = showThemeTokenPicker;
        _createIconPreview = createIconPreview;

        ColumnDefinitions = DictionaryFieldLayoutRules.Columns(_definition.ValueKind);
        ColumnSpacing = 12;
        MinHeight = DictionaryFieldLayoutRules.MinHeight(_definition.ValueKind);

        _label = new TextBlock
        {
            Text = _definition.Label,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = DictionaryFieldLayoutRules.LabelVerticalAlignment(_definition.ValueKind),
            Margin = DictionaryFieldLayoutRules.LabelMargin(_definition.ValueKind),
        };
        SetColumn(_label, 0);

        if (_definition.ValueKind == ValueKind.Boolean)
        {
            _toggleSwitch = new ToggleSwitch
            {
                IsChecked = StringToBool(_value),
                IsEnabled = _definition.IsEditable,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _toggleSwitch.PropertyChanged += (_, change) =>
            {
                if (change.Property != ToggleSwitch.IsCheckedProperty) return;
                if (_isUpdatingColorControl) return;

                SetLocalValue(BoolToString(_toggleSwitch.IsChecked == true));
                CommitValue();
            };
            SetColumn(_toggleSwitch, 1);
            Children.Add(_toggleSwitch);
        }
        else if (_definition.ValueKind is ValueKind.OptionToken or ValueKind.PaletteColorToken)
        {
            _comboBox = CreateOptionComboBox(_value);
            _comboBox.SelectionChanged += (_, _) =>
            {
                if (_isUpdatingColorControl) return;

                SetLocalValue(ComboValue(_comboBox));
                CommitValue();
            };
            SetColumn(_comboBox, 1);
            Children.Add(_comboBox);
        }
        else if (_definition.ValueKind == ValueKind.ThemeToken)
        {
            _themeTokenButton = new Button
            {
                Content = ThemeTokenButtonContent(_value),
                MinHeight = 36,
                MinWidth = 260,
                HorizontalAlignment = HorizontalAlignment.Left,
                IsEnabled = _definition.IsEditable && _showThemeTokenPicker is not null,
            };
            _themeTokenButton.Click += async (_, _) =>
            {
                if (_showThemeTokenPicker is null) return;

                var selected = await _showThemeTokenPicker(_value, _definition.Options);
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    SetValue(selected, commit: true);
                }
            };
            SetColumn(_themeTokenButton, 1);
            Children.Add(_themeTokenButton);
        }
        else if (_definition.ValueKind == ValueKind.PaletteColorPair)
        {
            var pair = DictionaryFieldPairText.Split(_value);
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

                SetLocalValue(DictionaryFieldColorValue.NormalizeHex(_textBox.Text ?? ""));
                UpdateColorControlsFromValue();
            };
            AttachDeferredCommit(_textBox);
            SetColumn(_textBox, 2);
            Children.Add(_textBox);

            _colorPickerButton = new Button
            {
                Content = "Pick",
                MinWidth = 58,
                Height = 34,
                IsEnabled = _definition.IsEditable,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _colorPickerButton.Click += async (_, _) =>
            {
                var color = await ShowHexColorDialogAsync();
                if (color is null) return;
                SetValue(color, commit: true);
            };
            SetColumn(_colorPickerButton, 3);
            Children.Add(_colorPickerButton);
        }
        else if (_definition.ValueKind == ValueKind.HueDegrees)
        {
            _hueControl = new HueDegreesControl(_value, _definition.IsEditable);
            _hueControl.ValueChanged += (_, value) => SetLocalValue(value);
            _hueControl.ValueCommitted += (_, value) =>
            {
                SetLocalValue(value);
                CommitValue();
            };
            SetColumn(_hueControl, 1);
            Children.Add(_hueControl);
        }
        else if (_definition.ValueKind == ValueKind.IntegerPair)
        {
            var pair = DictionaryFieldPairText.Split(_value);
            var pairControl = CreatePairControl(pair.First, pair.Second);
            _pairFirstTextBox = pairControl.FirstTextBox;
            _pairSecondTextBox = pairControl.SecondTextBox;
            SetColumn(pairControl.Control, 1);
            Children.Add(pairControl.Control);
        }
        else if (_definition.ValueKind == ValueKind.IconSlots)
        {
            _iconSlotsControl = new IconSlotsControl(_value, _definition.IsEditable, _showIconTokenPicker, _createIconPreview);
            _iconSlotsControl.ValueChanged += (_, value) => SetLocalValue(value);
            _iconSlotsControl.ValueCommitted += (_, value) =>
            {
                SetLocalValue(value);
                CommitValue();
            };
            SetColumn(_iconSlotsControl, 1);
            Children.Add(_iconSlotsControl);
        }
        else
        {
            _textBox = CreateTextBox();
            _textBox.Text = _value;
            _textBox.TextChanged += (_, _) =>
            {
                if (_isUpdatingColorControl) return;

                SetLocalValue(_textBox.Text ?? "");
            };
            AttachDeferredCommit(_textBox);
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
                    SetValue(selectedPath, commit: true);
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
            VerticalAlignment = DictionaryFieldLayoutRules.RestoreButtonVerticalAlignment(_definition.ValueKind),
            IsVisible = !IsDefault && _definition.IsEditable,
        };
        _restoreButton.Click += (_, _) =>
        {
            if (_definition.CanInherit)
            {
                SetInheritedValue(commit: true);
            }
        };
        SetColumn(_restoreButton, DictionaryFieldLayoutRules.RestoreButtonColumn(_definition.ValueKind));

        Children.Add(_label);
        Children.Add(_restoreButton);
        UpdateState();
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public bool IsDefault => _definition.CanInherit
        ? _isInherited
        : _value == _lastCommittedValue;

    public bool HasLocalOverride => _definition.CanInherit && !_isInherited;

    public bool CommitAsDefault => _definition.CommitAsDefault;

    public string Value => _value;

    public void AcceptCurrentValueAsDefault()
    {
        _defaultValue = _value;
        _lastCommittedValue = _value;
        _isInherited = false;
        UpdateState();
    }

    public void MarkCurrentValueCommitted()
    {
        _lastCommittedValue = _isInherited ? _definition.InheritedStorageValue : _value;
        UpdateState();
    }

    public void SetValue(string value, bool commit = false)
    {
        if (_value == value)
        {
            if (commit)
            {
                CommitValue();
            }

            return;
        }

        _value = value;
        _isInherited = false;
        _isUpdatingColorControl = true;
        if (_toggleSwitch is not null)
        {
            _toggleSwitch.IsChecked = StringToBool(value);
        }
        else if (_definition.ValueKind == ValueKind.HexColor)
        {
            UpdateColorControlsFromValue();
        }
        else if (_definition.ValueKind == ValueKind.HueDegrees && _hueControl is not null)
        {
            _hueControl.SetValue(value);
        }
        else if (_definition.ValueKind == ValueKind.IntegerPair)
        {
            UpdatePairControlsFromValue();
        }
        else if (_definition.ValueKind == ValueKind.IconSlots && _iconSlotsControl is not null)
        {
            _iconSlotsControl.SetValue(value);
        }
        else if (_definition.ValueKind == ValueKind.ThemeToken && _themeTokenButton is not null)
        {
            _themeTokenButton.Content = ThemeTokenButtonContent(value);
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
        _isUpdatingColorControl = false;

        UpdateState();
        ValueChanged?.Invoke(this, _value);
        if (commit)
        {
            CommitValue();
        }
    }

    private void SetLocalValue(string value)
    {
        if (!_isInherited && _value == value) return;

        _value = value;
        _isInherited = false;
        UpdateState();
        ValueChanged?.Invoke(this, _value);
    }

    private void CommitValue()
    {
        var storageValue = _isInherited ? _definition.InheritedStorageValue : _value;
        if (_lastCommittedValue == storageValue) return;

        _lastCommittedValue = storageValue;
        ValueCommitted?.Invoke(this, storageValue);
    }

    private void SetInheritedValue(bool commit)
    {
        if (!_definition.CanInherit) return;

        _isInherited = true;
        SetDisplayedValue(_definition.InheritedValue);
        UpdateState();
        ValueChanged?.Invoke(this, _definition.InheritedStorageValue);
        if (commit)
        {
            CommitValue();
        }
    }

    private void SetDisplayedValue(string value)
    {
        _value = value;
        _isUpdatingColorControl = true;
        if (_toggleSwitch is not null)
        {
            _toggleSwitch.IsChecked = StringToBool(value);
        }
        else if (_definition.ValueKind == ValueKind.HexColor)
        {
            UpdateColorControlsFromValue();
        }
        else if (_definition.ValueKind == ValueKind.HueDegrees && _hueControl is not null)
        {
            _hueControl.SetValue(value);
        }
        else if (_definition.ValueKind == ValueKind.IntegerPair)
        {
            UpdatePairControlsFromValue();
        }
        else if (_definition.ValueKind == ValueKind.IconSlots && _iconSlotsControl is not null)
        {
            _iconSlotsControl.SetValue(value);
        }
        else if (_definition.ValueKind == ValueKind.ThemeToken && _themeTokenButton is not null)
        {
            _themeTokenButton.Content = ThemeTokenButtonContent(value);
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
        _isUpdatingColorControl = false;
    }

    private void AttachDeferredCommit(TextBox textBox)
    {
        textBox.LostFocus += (_, _) => CommitValue();
        textBox.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter || _definition.ValueKind == ValueKind.StringMultiline) return;

            CommitValue();
            args.Handled = true;
        };
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
        EditorTextBoxBehavior.Configure(textBox);
        if (_definition.ValueKind == ValueKind.ImageFilePath)
        {
            textBox.MaxWidth = 420;
            textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        return textBox;
    }

    private static Control ThemeTokenButtonContent(string value)
    {
        return new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(value) ? "Select theme token…" : value,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
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

        var labels = DictionaryFieldPairText.Labels(_definition);
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
        AttachDeferredCommit(firstTextBox);
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
        AttachDeferredCommit(secondTextBox);
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
            ItemTemplate = _definition.ValueKind is ValueKind.PaletteColorToken or ValueKind.PaletteColorPair
                ? DictionaryPaletteOptionTemplate.Create()
                : null,
        };
        EditorComboBoxBehavior.Configure(comboBox);

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

        var labels = DictionaryFieldPairText.Labels(_definition);
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

        SetLocalValue(DictionaryFieldPairText.Join(
            firstComboBox is not null ? ComboValue(firstComboBox) : (_pairFirstComboBox is not null ? ComboValue(_pairFirstComboBox) : ""),
            secondComboBox is not null ? ComboValue(secondComboBox) : (_pairSecondComboBox is not null ? ComboValue(_pairSecondComboBox) : "")));
        CommitValue();
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
        var pair = DictionaryFieldPairText.Split(_value);
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
        return EditorTextBoxBehavior.Configure(new TextBox
        {
            Text = value,
            Width = 90,
            MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
        });
    }

    private void SetPairValueFromTextBoxes(TextBox? firstTextBox, TextBox? secondTextBox)
    {
        if (_isUpdatingColorControl) return;

        SetLocalValue(DictionaryFieldPairText.Join(
            firstTextBox?.Text ?? _pairFirstTextBox?.Text ?? "",
            secondTextBox?.Text ?? _pairSecondTextBox?.Text ?? ""));
    }

    private void UpdatePairControlsFromValue()
    {
        var pair = DictionaryFieldPairText.Split(_value);
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

    private static Border CreateColorSwatch(string value)
    {
        return new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(DictionaryFieldColorValue.Parse(value)),
            BorderBrush = new SolidColorBrush(Color.Parse("#667085")),
            BorderThickness = new Avalonia.Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
    }

    private void UpdateColorControlsFromValue()
    {
        _isUpdatingColorControl = true;
        var color = DictionaryFieldColorValue.Parse(_value);
        if (_colorSwatch is not null)
        {
            _colorSwatch.Background = new SolidColorBrush(color);
        }

        if (_textBox is not null && _textBox.Text != _value)
        {
            _textBox.Text = _value;
        }

        _isUpdatingColorControl = false;
    }

    private async Task<string?> ShowHexColorDialogAsync()
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return null;
        }

        return await HexColorPickerDialog.Show(owner, _definition.Label, _value);
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
        _restoreButton.IsVisible = _definition.CanInherit && !isDefault && _definition.IsEditable;
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
