using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryFieldControl : Grid
{
    private readonly FieldDefinition _definition;
    private readonly TextBlock _label;
    private readonly TextBox? _textBox;
    private readonly DictionaryIntegerPairControl? _integerPairControl;
    private readonly HueDegreesControl? _hueControl;
    private readonly ComboBox? _comboBox;
    private readonly DictionaryPaletteTokenControl? _paletteTokenControl;
    private readonly DictionaryPalettePairControl? _palettePairControl;
    private readonly DictionaryHexColorControl? _hexColorControl;
    private readonly ToggleSwitch? _toggleSwitch;
    private readonly IconSlotsControl? _iconSlotsControl;
    private readonly Button? _themeTokenButton;
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
        else if (_definition.ValueKind == ValueKind.OptionToken)
        {
            _comboBox = DictionaryOptionSelector.CreateComboBox(_definition, _value);
            _comboBox.SelectionChanged += (_, _) =>
            {
                if (_isUpdatingColorControl) return;

                SetLocalValue(DictionaryOptionSelector.Value(_comboBox));
                CommitValue();
            };
            SetColumn(_comboBox, 1);
            Children.Add(_comboBox);
        }
        else if (_definition.ValueKind == ValueKind.PaletteColorToken)
        {
            _paletteTokenControl = new DictionaryPaletteTokenControl(_definition.Label, _definition.Options, _value, _definition.IsEditable);
            _paletteTokenControl.ValueCommitted += (_, value) =>
            {
                SetLocalValue(value);
                CommitValue();
            };
            SetColumn(_paletteTokenControl, 1);
            Children.Add(_paletteTokenControl);
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
            _palettePairControl = new DictionaryPalettePairControl(_definition, _value);
            _palettePairControl.ValueChanged += (_, value) => SetLocalValue(value);
            _palettePairControl.ValueCommitted += (_, value) =>
            {
                SetLocalValue(value);
                CommitValue();
            };
            SetColumn(_palettePairControl, 1);
            Children.Add(_palettePairControl);
        }
        else if (_definition.ValueKind == ValueKind.HexColor)
        {
            _hexColorControl = new DictionaryHexColorControl(_definition, _value);
            _hexColorControl.ValueChanged += (_, value) => SetLocalValue(value);
            _hexColorControl.ValueCommitted += (_, value) =>
            {
                SetLocalValue(value);
                CommitValue();
            };
            SetColumn(_hexColorControl, 1);
            Children.Add(_hexColorControl);
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
            _integerPairControl = new DictionaryIntegerPairControl(_definition, _value);
            _integerPairControl.ValueChanged += (_, value) => SetLocalValue(value);
            _integerPairControl.ValueCommitted += (_, value) =>
            {
                SetLocalValue(value);
                CommitValue();
            };
            SetColumn(_integerPairControl, 1);
            Children.Add(_integerPairControl);
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
            _textBox = DictionaryTextBoxFactory.Create(_definition);
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
        else if (_definition.ValueKind == ValueKind.HexColor && _hexColorControl is not null)
        {
            _hexColorControl.SetValue(value);
        }
        else if (_definition.ValueKind == ValueKind.HueDegrees && _hueControl is not null)
        {
            _hueControl.SetValue(value);
        }
        else if (_definition.ValueKind == ValueKind.IntegerPair && _integerPairControl is not null)
        {
            _integerPairControl.SetValue(value);
        }
        else if (_definition.ValueKind == ValueKind.IconSlots && _iconSlotsControl is not null)
        {
            _iconSlotsControl.SetValue(value);
        }
        else if (_definition.ValueKind == ValueKind.ThemeToken && _themeTokenButton is not null)
        {
            _themeTokenButton.Content = ThemeTokenButtonContent(value);
        }
        else if (_definition.ValueKind == ValueKind.OptionToken)
        {
            UpdateOptionComboFromValue();
        }
        else if (_definition.ValueKind == ValueKind.PaletteColorToken && _paletteTokenControl is not null)
        {
            _paletteTokenControl.SetValue(value);
        }
        else if (_definition.ValueKind == ValueKind.PaletteColorPair && _palettePairControl is not null)
        {
            _palettePairControl.SetValue(value);
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
        else if (_definition.ValueKind == ValueKind.HexColor && _hexColorControl is not null)
        {
            _hexColorControl.SetValue(value);
        }
        else if (_definition.ValueKind == ValueKind.HueDegrees && _hueControl is not null)
        {
            _hueControl.SetValue(value);
        }
        else if (_definition.ValueKind == ValueKind.IntegerPair && _integerPairControl is not null)
        {
            _integerPairControl.SetValue(value);
        }
        else if (_definition.ValueKind == ValueKind.IconSlots && _iconSlotsControl is not null)
        {
            _iconSlotsControl.SetValue(value);
        }
        else if (_definition.ValueKind == ValueKind.ThemeToken && _themeTokenButton is not null)
        {
            _themeTokenButton.Content = ThemeTokenButtonContent(value);
        }
        else if (_definition.ValueKind == ValueKind.OptionToken)
        {
            UpdateOptionComboFromValue();
        }
        else if (_definition.ValueKind == ValueKind.PaletteColorToken && _paletteTokenControl is not null)
        {
            _paletteTokenControl.SetValue(value);
        }
        else if (_definition.ValueKind == ValueKind.PaletteColorPair && _palettePairControl is not null)
        {
            _palettePairControl.SetValue(value);
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

    private static Control ThemeTokenButtonContent(string value)
    {
        return new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(value) ? "Select theme token…" : value,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private void UpdateOptionComboFromValue()
    {
        if (_comboBox is null) return;

        _isUpdatingColorControl = true;
        _comboBox.SelectedItem = DictionaryOptionSelector.SelectedOption(_definition, _value);
        _isUpdatingColorControl = false;
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
