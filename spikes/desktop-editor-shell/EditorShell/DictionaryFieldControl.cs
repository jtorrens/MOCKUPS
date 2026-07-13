using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryFieldControl : Grid
{
    private readonly FieldDefinition _definition;
    private readonly TextBlock _label;
    private readonly IDictionaryValueControl? _valueControl;
    private readonly Button _restoreButton;
    private bool _isInherited;
    private string _value;
    private string _lastCommittedValue;

    public DictionaryFieldControl(
        FieldValue fieldValue,
        Func<string, ValueKind, Task<string?>>? browsePath = null)
        : this(fieldValue, new DictionaryFieldServices(BrowsePath: browsePath))
    {
    }

    public DictionaryFieldControl(
        FieldValue fieldValue,
        DictionaryFieldServices? services,
        bool compact = false)
    {
        services ??= new DictionaryFieldServices();
        _definition = fieldValue.Definition;
        _isInherited = fieldValue.IsInherited;
        _value = fieldValue.IsInherited ? fieldValue.Definition.InheritedValue : fieldValue.Value;
        _lastCommittedValue = fieldValue.IsInherited ? fieldValue.Definition.InheritedStorageValue : fieldValue.Value;

        ColumnDefinitions = DictionaryFieldLayoutRules.Columns(_definition.ValueKind, compact);
        ColumnSpacing = 12;
        MinHeight = DictionaryFieldLayoutRules.MinHeight(_definition.ValueKind);

        _label = new TextBlock
        {
            Text = _definition.DisplayLabel,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = DictionaryFieldLayoutRules.LabelVerticalAlignment(_definition.ValueKind),
            Margin = DictionaryFieldLayoutRules.LabelMargin(_definition.ValueKind),
        };
        SetColumn(_label, 0);

        _valueControl = AddValueControl(DictionaryControlRegistry.Create(
            _definition,
            _value,
            services,
            fieldValue.IsHighlighted,
            fieldValue.IsInherited));

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
        EditorAccessibility.Describe(
            _restoreButton,
            $"Restore {_definition.DisplayLabel} to its inherited value");
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

    public string FieldId => _definition.Id;

    public string Value => _value;

    public bool RequiresLocalHorizontalViewport => _valueControl is IDictionaryLocalHorizontalScrollControl;

    public void RefreshPreview()
    {
        if (_valueControl is IDictionaryPreviewValueControl previewControl)
        {
            previewControl.RefreshPreview();
        }
    }

    public void AcceptCurrentValueAsDefault()
    {
        if (_definition.CanInherit && _isInherited)
        {
            _lastCommittedValue = _definition.InheritedStorageValue;
            UpdateState();
            return;
        }

        _lastCommittedValue = _value;
        _isInherited = false;
        UpdateState();
    }

    public void MarkCurrentValueCommitted()
    {
        _lastCommittedValue = _isInherited ? _definition.InheritedStorageValue : _value;
        UpdateState();
    }

    public void AcceptInheritedValueAsDefault()
    {
        if (!_definition.CanInherit) return;

        _isInherited = true;
        SetDisplayedValue(_definition.InheritedValue);
        _lastCommittedValue = _definition.InheritedStorageValue;
        UpdateState();
    }

    public void SetValue(string value, bool commit = false)
    {
        if (_definition.CanInherit && value == _definition.InheritedStorageValue)
        {
            SetInheritedValue(commit);
            return;
        }

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

        UpdateState();
        ValueChanged?.Invoke(this, _value);
        if (commit)
        {
            CommitValue();
        }
    }

    private void SetLocalValue(string value)
    {
        if (_definition.CanInherit && value == _definition.InheritedStorageValue)
        {
            if (_isInherited)
            {
                return;
            }

            _isInherited = true;
            SetDisplayedValue(_definition.InheritedValue);
            UpdateState();
            ValueChanged?.Invoke(this, _definition.InheritedStorageValue);
            return;
        }

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
