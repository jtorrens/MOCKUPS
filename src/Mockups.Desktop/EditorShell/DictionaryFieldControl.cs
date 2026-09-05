using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryFieldControl : Grid, IDictionaryOverrideStateControl
{
    private readonly FieldDefinition _definition;
    private readonly TextBlock _label;
    private readonly Control _labelHost;
    private readonly IDictionaryValueControl? _valueControl;
    private readonly Button _restoreButton;
    private readonly bool _valueOnly;
    private readonly bool _blockLayout;
    private readonly bool _separatedComplexLayout;
    private readonly bool _compact;
    private readonly Grid? _valueHost;
    private readonly Border? _complexSeparator;
    private bool _isInherited;
    private bool _hasNestedOverrides;
    private string _value;
    private string _lastCommittedValue;
    private bool _lastPublishedOverrideState;

    public DictionaryFieldControl(
        FieldValue fieldValue,
        Func<string, ValueKind, Task<string?>>? browsePath = null)
        : this(fieldValue, new DictionaryFieldServices(BrowsePath: browsePath))
    {
    }

    public DictionaryFieldControl(
        FieldValue fieldValue,
        DictionaryFieldServices? services,
        bool compact = false,
        bool valueOnly = false)
    {
        services ??= new DictionaryFieldServices();
        _definition = fieldValue.Definition;
        _isInherited = fieldValue.IsInherited;
        _hasNestedOverrides = fieldValue.IsHighlighted;
        _value = fieldValue.IsInherited ? fieldValue.Definition.InheritedValue : fieldValue.Value;
        _lastCommittedValue = fieldValue.IsInherited ? fieldValue.Definition.InheritedStorageValue : fieldValue.Value;
        _valueOnly = valueOnly;
        _separatedComplexLayout = DictionaryFieldLayoutRules.UsesBlockLayout(_definition.ValueKind);
        _blockLayout = !valueOnly && _separatedComplexLayout;
        _compact = compact;

        MinWidth = 0;
        ClipToBounds = true;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        ColumnDefinitions = valueOnly
            ? new ColumnDefinitions("*")
            : _blockLayout
                ? new ColumnDefinitions(
                    $"*,{DictionaryFieldLayoutRules.RestoreActionWidth}")
                : DictionaryFieldLayoutRules.Columns(compact);
        if (_separatedComplexLayout)
        {
            RowDefinitions = new RowDefinitions(valueOnly ? "Auto,Auto" : "Auto,Auto,Auto");
        }
        ColumnSpacing = 12;
        MinHeight = DictionaryFieldLayoutRules.MinHeight(_definition.ValueKind);

        _label = new TextBlock
        {
            Text = _definition.DisplayLabel,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = _definition.SelectComponentClass
                ? VerticalAlignment.Top
                : DictionaryFieldLayoutRules.LabelVerticalAlignment(_definition.ValueKind),
            Margin = _definition.SelectComponentClass
                ? new Thickness(0, 7, 0, 0)
                : DictionaryFieldLayoutRules.LabelMargin(_definition.ValueKind),
            MinWidth = 0,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
        _labelHost = LabelHost(_definition, _label);
        SetColumn(_labelHost, 0);
        if (_blockLayout)
        {
            SetRow(_labelHost, 1);
        }

        if (!valueOnly && !_blockLayout)
        {
            _valueHost = new Grid
            {
                MinWidth = 0,
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            SetColumn(_valueHost, 1);
            Children.Add(_valueHost);
        }

        _valueControl = AddValueControl(DictionaryControlRegistry.Create(
            _definition,
            _value,
            services,
            fieldValue.IsHighlighted,
            fieldValue.IsInherited));

        _restoreButton = new DictionaryRestoreButton
        {
            Content = "↺",
            MinWidth = 0,
            Height = 32,
            MinHeight = 0,
            Margin = new Thickness(0),
            Padding = new Avalonia.Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = _definition.SelectComponentClass
                ? VerticalAlignment.Top
                : DictionaryFieldLayoutRules.RestoreButtonVerticalAlignment(_definition.ValueKind),
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
        SetColumn(
            _restoreButton,
            _blockLayout
                ? 1
                : DictionaryFieldLayoutRules
                    .RestoreButtonColumn());
        if (_blockLayout)
        {
            SetRow(_restoreButton, 1);
        }

        if (_separatedComplexLayout)
        {
            _complexSeparator = new Border
            {
                Height = 1,
                Margin = new Thickness(0, 8, 0, 10),
            };
            SetColumn(_complexSeparator, 0);
            SetColumnSpan(_complexSeparator, _blockLayout ? 2 : 1);
            SetRow(_complexSeparator, 0);
            Children.Add(_complexSeparator);
            ActualThemeVariantChanged += (_, _) => RefreshComplexSeparator();
            RefreshComplexSeparator();
        }

        if (!valueOnly)
        {
            Children.Add(_labelHost);
            Children.Add(_restoreButton);
        }
        UpdateState();
        SizeChanged += (_, args) => UpdateResponsiveLabelWidth(args.NewSize.Width);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public event EventHandler? RuntimeContractChanged;

    public event EventHandler? OverrideStateChanged;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!double.IsInfinity(availableSize.Width))
        {
            UpdateResponsiveLabelWidth(availableSize.Width);
        }
        var measured = base.MeasureOverride(availableSize);
        var width = double.IsInfinity(availableSize.Width)
            ? measured.Width
            : Math.Min(measured.Width, availableSize.Width);
        return new Size(width, measured.Height);
    }

    public bool IsDefault => _definition.CanInherit
        ? _isInherited
        : _value == _lastCommittedValue;

    public bool HasLocalOverride => _definition.CanInherit && !_isInherited;

    public bool HasOverrides => !IsDefault || _hasNestedOverrides;

    public bool CommitAsDefault => _definition.CommitAsDefault;

    public string FieldId => _definition.Id;

    public bool IsEditable => _definition.IsEditable;

    public string Value => _value;

    public bool RequiresLocalHorizontalViewport => _valueControl switch
    {
        DictionaryPalettePairControl pair => pair.RequiresLocalHorizontalViewport,
        IDictionaryLocalHorizontalScrollControl => true,
        _ => false,
    };

    public PairFieldLabels? UseSharedPairHeader()
    {
        if (_valueControl is not DictionaryPalettePairControl pair) return null;
        pair.UseSharedHeader();
        return pair.Labels;
    }

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

    public void SetPresentedValue(string value)
    {
        _valueControl?.SetValue(value);
    }

    private void SetLocalValue(string value)
    {
        if (!_isInherited && _value == value)
        {
            return;
        }

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
        if (valueControl is IDictionaryRuntimeContractValueControl runtimeContractControl)
        {
            runtimeContractControl.RuntimeContractChanged += (_, _) =>
                RuntimeContractChanged?.Invoke(this, EventArgs.Empty);
        }
        if (valueControl is IDictionaryOverrideStateControl overrideStateControl)
        {
            _hasNestedOverrides = overrideStateControl.HasOverrides;
            overrideStateControl.OverrideStateChanged += (_, _) =>
            {
                _hasNestedOverrides = overrideStateControl.HasOverrides;
                UpdateState();
            };
        }

        if (valueControl is Control control)
        {
            if (!_definition.IsEditable)
            {
                control.IsEnabled = false;
                control.Opacity = 0.58;
            }
            control.MinWidth = 0;
            control.HorizontalAlignment = HorizontalAlignment.Stretch;
            if (_separatedComplexLayout)
            {
                SetColumn(control, 0);
                SetRow(control, _valueOnly ? 1 : 2);
                SetColumnSpan(control, _blockLayout ? 2 : 1);
                control.Margin = new Thickness(0);
                Children.Add(control);
            }
            else if (_valueHost is not null)
            {
                _valueHost.Children.Add(control);
            }
            else
            {
                SetColumn(control, 0);
                Children.Add(control);
            }
        }

        return valueControl;
    }

    private void RefreshComplexSeparator()
    {
        if (_complexSeparator is null) return;
        _complexSeparator.Background = EditorUiVisuals.ScrollbarSeparatorBrush(
            ActualThemeVariant != ThemeVariant.Light);
    }

    private static Control LabelHost(
        FieldDefinition definition,
        TextBlock label)
    {
        if (string.IsNullOrWhiteSpace(definition.HelpText))
        {
            return label;
        }

        var help = new TextBlock
        {
            Text = definition.HelpText,
            FontSize = 10.5,
            Opacity = 0.68,
            TextWrapping = TextWrapping.Wrap,
            MinWidth = 0,
            Margin = new Thickness(0, 2, 0, 0),
        };
        var host = new StackPanel
        {
            Spacing = 0,
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                label,
                help,
            },
        };
        return EditorAccessibility.Describe(
            host,
            definition.DisplayLabel,
            definition.HelpText,
            showToolTip: false);
    }

    private void UpdateState()
    {
        var isDefault = IsDefault;
        _restoreButton.IsVisible = _definition.CanInherit && !isDefault && _definition.IsEditable;
        if (isDefault && !_hasNestedOverrides)
        {
            _label.ClearValue(TextBlock.ForegroundProperty);
        }
        else
        {
            _label.Foreground = new SolidColorBrush(Color.Parse("#D6A638"));
        }

        PseudoClasses.Set(":changed", !isDefault || _hasNestedOverrides);
        var hasOverrides = HasOverrides;
        if (_lastPublishedOverrideState != hasOverrides)
        {
            _lastPublishedOverrideState = hasOverrides;
            OverrideStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateResponsiveLabelWidth(double availableWidth)
    {
        if (_valueOnly
            || _blockLayout
            || availableWidth <= 0
            || double.IsInfinity(availableWidth))
        {
            return;
        }

        ColumnDefinitions[0].Width = new GridLength(
            DictionaryFieldLayoutRules.ResponsiveLabelWidth(
                availableWidth,
                _compact));
    }

    private sealed class DictionaryRestoreButton : Button
    {
        public DictionaryRestoreButton()
        {
            ClipToBounds = true;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = DictionaryFieldLayoutRules.RestoreActionWidth;
            base.MeasureOverride(new Size(size, size));
            return new Size(size, size);
        }
    }
}
