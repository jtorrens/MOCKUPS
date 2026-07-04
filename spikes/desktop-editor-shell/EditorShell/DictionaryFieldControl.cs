using Avalonia;
using Avalonia.Controls;
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
    private readonly IDictionaryValueControl? _valueControl;
    private readonly DictionaryPathBrowseButton? _pathBrowseButton;
    private readonly Button _restoreButton;
    private bool _isInherited;
    private string _value;
    private string _lastCommittedValue;

    public DictionaryFieldControl(
        FieldValue fieldValue,
        Func<string, ValueKind, Task<string?>>? browsePath = null,
        Func<string, bool, Task<string?>>? showIconTokenPicker = null,
        Func<string, IReadOnlyList<FieldOption>?, Task<string?>>? showThemeTokenPicker = null,
        Func<string, Control>? createIconPreview = null,
        Func<string, string?>? resolveImagePath = null,
        Func<string, string>? getFieldValue = null)
    {
        _definition = fieldValue.Definition;
        _isInherited = fieldValue.IsInherited;
        _value = fieldValue.IsInherited ? fieldValue.Definition.InheritedValue : fieldValue.Value;
        _lastCommittedValue = fieldValue.IsInherited ? fieldValue.Definition.InheritedStorageValue : fieldValue.Value;

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

        _valueControl = AddValueControl(DictionaryValueControlFactory.Create(
            _definition,
            _value,
            showIconTokenPicker,
            showThemeTokenPicker,
            createIconPreview,
            resolveImagePath,
            getFieldValue));

        if (_definition.ValueKind is ValueKind.DirectoryPath or ValueKind.ImageFilePath)
        {
            _pathBrowseButton = new DictionaryPathBrowseButton(_definition.ValueKind, _value, _definition.IsEditable, browsePath);
            _pathBrowseButton.ValueCommitted += (_, value) =>
            {
                SetValue(value, commit: true);
            };
            SetColumn(_pathBrowseButton, 2);
            Children.Add(_pathBrowseButton);
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

    public void RefreshPreview()
    {
        if (_valueControl is IDictionaryPreviewValueControl previewControl)
        {
            previewControl.RefreshPreview();
        }
    }

    public void AcceptCurrentValueAsDefault()
    {
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
        _valueControl?.SetValue(value);
        _pathBrowseButton?.SetValue(value);

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
        _valueControl?.SetValue(value);
        _pathBrowseButton?.SetValue(value);
    }

    private IDictionaryValueControl AddValueControl(IDictionaryValueControl valueControl)
    {
        valueControl.ValueChanged += (_, value) => SetLocalValue(value);
        valueControl.ValueCommitted += (_, value) =>
        {
            SetLocalValue(value);
            CommitValue();
        };

        if (valueControl is Control control)
        {
            SetColumn(control, 1);
            Children.Add(control);
        }

        return valueControl;
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
