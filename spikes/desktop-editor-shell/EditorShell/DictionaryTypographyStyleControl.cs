using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryTypographyStyleControl : Grid, IDictionaryValueControl
{
    private static readonly FieldOption[] WeightOptions =
    [
        new("100", "100"),
        new("200", "200"),
        new("300", "300"),
        new("400", "400"),
        new("500", "500"),
        new("600", "600"),
        new("700", "700"),
        new("800", "800"),
        new("900", "900"),
    ];

    private static readonly FieldOption[] StyleOptions =
    [
        new("normal", "Normal"),
        new("italic", "Italic"),
    ];

    private static readonly FieldOption[] SizeTokenOptions =
    [
        new("theme.typography.sizes.xs", "typography.sizes.xs"),
        new("theme.typography.sizes.s", "typography.sizes.s"),
        new("theme.typography.sizes.m", "typography.sizes.m"),
        new("theme.typography.sizes.l", "typography.sizes.l"),
        new("theme.typography.sizes.xl", "typography.sizes.xl"),
    ];

    private readonly FieldDefinition _definition;
    private readonly JsonObject _inheritedValues;
    private readonly JsonObject _localValues;
    private readonly Dictionary<string, IDictionaryValueControl> _controls = [];
    private readonly Dictionary<string, Button> _restoreButtons = [];
    private readonly Button _headerButton;
    private readonly TextBlock _summaryText;
    private readonly Grid _contentGrid;
    private bool _isOpen;
    private bool _isUpdating;

    public DictionaryTypographyStyleControl(
        FieldDefinition definition,
        string value,
        bool isInherited,
        Func<string, IReadOnlyList<FieldOption>?, Task<string?>>? showThemeTokenPicker)
    {
        _definition = definition;
        _inheritedValues = TypographyStyleValue.Parse(definition.InheritedValue);
        if (_inheritedValues.Count == 0)
        {
            _inheritedValues = TypographyStyleValue.Parse(definition.DefaultValue);
        }

        _localValues = definition.CanInherit && (isInherited || value.Equals(definition.InheritedValue, StringComparison.Ordinal))
            ? []
            : TypographyStyleValue.Parse(value);

        RowDefinitions = new RowDefinitions("Auto,Auto");
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _summaryText = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var chevron = new TextBlock
        {
            Text = ">",
            Width = 16,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.72,
        };
        SetColumn(_summaryText, 0);
        SetColumn(chevron, 1);
        _headerButton = new Button
        {
            MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 6),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            Content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 8,
                Children =
                {
                    _summaryText,
                    chevron,
                },
            },
        };
        Children.Add(_headerButton);

        _contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("96,*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"),
            RowSpacing = 8,
            ColumnSpacing = 8,
            Margin = new Thickness(0, 8, 0, 0),
            IsVisible = false,
        };
        SetRow(_contentGrid, 1);
        Children.Add(_contentGrid);
        _headerButton.Click += (_, _) =>
        {
            _isOpen = !_isOpen;
            _contentGrid.IsVisible = _isOpen;
            chevron.Text = _isOpen ? "∨" : ">";
        };

        AddOptionRow(0, "Font", TypographyStyleValue.FontFamilyId, FontOptions(definition.Options), "theme");
        AddOptionRow(1, "Weight", TypographyStyleValue.Weight, WeightOptions, "400");
        AddOptionRow(2, "Style", TypographyStyleValue.Style, StyleOptions, "normal");
        AddThemeTokenRow(3, "Size", TypographyStyleValue.SizeToken, showThemeTokenPicker);
        AddDecimalRow(4, "Line", TypographyStyleValue.LineHeight, "1.2");

        RefreshRows();
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        _isUpdating = true;
        _localValues.Clear();
        var nextValues = _definition.CanInherit && value.Equals(_definition.InheritedValue, StringComparison.Ordinal)
            ? []
            : TypographyStyleValue.Parse(value);
        foreach (var pair in nextValues)
        {
            _localValues[pair.Key] = pair.Value?.DeepClone();
        }

        RefreshRows();
        _isUpdating = false;
    }

    private static IReadOnlyList<FieldOption> FontOptions(IReadOnlyList<FieldOption>? options)
    {
        return [new FieldOption("theme", "Theme"), .. options ?? []];
    }

    private void AddOptionRow(int row, string label, string key, IReadOnlyList<FieldOption> options, string fallback)
    {
        var controlDefinition = new FieldDefinition(
            $"{_definition.Id}.{key}",
            label,
            ValueKind.OptionToken,
            _definition.IsEditable,
            fallback,
            Options: options);
        var control = new DictionaryOptionTokenControl(controlDefinition, ValueFor(key, fallback));
        control.ValueChanged += (_, value) => SetSubValue(key, value);
        control.ValueCommitted += (_, value) => CommitSubValue(key, value);
        AddRow(row, label, key, control);
    }

    private void AddThemeTokenRow(
        int row,
        string label,
        string key,
        Func<string, IReadOnlyList<FieldOption>?, Task<string?>>? showThemeTokenPicker)
    {
        var controlDefinition = new FieldDefinition(
            $"{_definition.Id}.{key}",
            label,
            ValueKind.ThemeToken,
            _definition.IsEditable,
            "theme.typography.sizes.s",
            Options: SizeTokenOptions);
        var control = new DictionaryThemeTokenControl(controlDefinition, ValueFor(key, "theme.typography.sizes.s"), showThemeTokenPicker);
        control.ValueChanged += (_, value) => SetSubValue(key, value);
        control.ValueCommitted += (_, value) => CommitSubValue(key, value);
        AddRow(row, label, key, control);
    }

    private void AddDecimalRow(int row, string label, string key, string fallback)
    {
        var controlDefinition = new FieldDefinition(
            $"{_definition.Id}.{key}",
            label,
            ValueKind.Decimal,
            _definition.IsEditable,
            fallback,
            Number: new NumberDefinition(0.5m, 3, 0.05m, 2));
        var control = new DictionaryDecimalControl(controlDefinition, ValueFor(key, fallback));
        control.ValueChanged += (_, value) => SetSubValue(key, value);
        control.ValueCommitted += (_, value) => CommitSubValue(key, value);
        AddRow(row, label, key, control);
    }

    private void AddRow(int row, string label, string key, IDictionaryValueControl control)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            Opacity = 0.78,
            VerticalAlignment = VerticalAlignment.Center,
        };
        SetRow(labelBlock, row);
        SetColumn(labelBlock, 0);
        _contentGrid.Children.Add(labelBlock);

        if (control is Control visual)
        {
            visual.HorizontalAlignment = HorizontalAlignment.Left;
            SetRow(visual, row);
            SetColumn(visual, 1);
            _contentGrid.Children.Add(visual);
        }

        var restoreButton = new Button
        {
            Content = "↺",
            Width = 30,
            Height = 30,
            Padding = new Thickness(0),
            IsVisible = false,
        };
        restoreButton.Click += (_, _) => RestoreSubValue(key);
        SetRow(restoreButton, row);
        SetColumn(restoreButton, 2);
        _contentGrid.Children.Add(restoreButton);

        _controls[key] = control;
        _restoreButtons[key] = restoreButton;
    }

    private string ValueFor(string key, string fallback)
    {
        return _localValues.ContainsKey(key)
            ? ValueString(_localValues, key, fallback)
            : ValueString(_inheritedValues, key, fallback);
    }

    private static string ValueString(JsonObject values, string key, string fallback)
    {
        return key.Equals(TypographyStyleValue.LineHeight, StringComparison.Ordinal)
            ? TypographyStyleValue.NumberString(values, key, fallback)
            : TypographyStyleValue.String(values, key, fallback);
    }

    private void SetSubValue(string key, string value)
    {
        if (_isUpdating) return;

        _localValues[key] = key.Equals(TypographyStyleValue.LineHeight, StringComparison.Ordinal)
            ? JsonPath.NumberNode(value)
            : JsonValue.Create(value);
        RefreshRows();
        ValueChanged?.Invoke(this, StorageValue());
    }

    private void CommitSubValue(string key, string value)
    {
        SetSubValue(key, value);
        ValueCommitted?.Invoke(this, StorageValue());
    }

    private void RestoreSubValue(string key)
    {
        if (!_definition.CanInherit) return;

        _localValues.Remove(key);
        RefreshRows();
        ValueChanged?.Invoke(this, StorageValue());
        ValueCommitted?.Invoke(this, StorageValue());
    }

    private string StorageValue()
    {
        if (_definition.CanInherit)
        {
            return _localValues.Count == 0 ? _definition.InheritedStorageValue : _localValues.ToJsonString();
        }

        var value = new JsonObject();
        foreach (var key in new[]
                 {
                     TypographyStyleValue.FontFamilyId,
                     TypographyStyleValue.Weight,
                     TypographyStyleValue.Style,
                     TypographyStyleValue.SizeToken,
                     TypographyStyleValue.LineHeight,
                 })
        {
            value[key] = _localValues.ContainsKey(key)
                ? _localValues[key]?.DeepClone()
                : _inheritedValues[key]?.DeepClone();
        }

        return value.ToJsonString();
    }

    private void RefreshRows()
    {
        _isUpdating = true;
        foreach (var pair in _controls)
        {
            pair.Value.SetValue(ValueFor(pair.Key, ""));
        }

        foreach (var pair in _restoreButtons)
        {
            var hasLocalValue = _definition.CanInherit && _localValues.ContainsKey(pair.Key);
            pair.Value.IsVisible = hasLocalValue && _definition.IsEditable;
            pair.Value.Foreground = hasLocalValue ? new SolidColorBrush(Color.Parse("#D6A638")) : null;
        }

        _summaryText.Text = SummaryText();
        _isUpdating = false;
    }

    private string SummaryText()
    {
        var font = ValueFor(TypographyStyleValue.FontFamilyId, "theme");
        var weight = ValueFor(TypographyStyleValue.Weight, "400");
        var style = ValueFor(TypographyStyleValue.Style, "normal");
        var size = ValueFor(TypographyStyleValue.SizeToken, "theme.typography.sizes.s")
            .Replace("theme.", "", StringComparison.Ordinal);
        var lineHeight = ValueFor(TypographyStyleValue.LineHeight, "1.2");
        return $"{font} · {weight} · {style} · {size} · {lineHeight}";
    }
}
