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
        new("theme.typography.weight", "typography.weight"),
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
        new("theme.typography.style", "typography.style"),
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

    private static readonly FieldOption[] LineHeightTokenOptions =
    [
        new("theme.typography.lineHeights.tight", "lineHeights.tight"),
        new("theme.typography.lineHeights.compact", "lineHeights.compact"),
        new("theme.typography.lineHeights.normal", "lineHeights.normal"),
        new("theme.typography.lineHeights.relaxed", "lineHeights.relaxed"),
        new("theme.typography.lineHeights.loose", "lineHeights.loose"),
    ];

    private readonly FieldDefinition _definition;
    private readonly JsonObject _inheritedValues;
    private readonly JsonObject _localValues;
    private readonly Dictionary<string, IDictionaryValueControl> _controls = [];
    private readonly Dictionary<string, TextBlock> _rowLabels = [];
    private readonly Dictionary<string, Button> _restoreButtons = [];
    private readonly Border _container;
    private readonly Button _headerButton;
    private readonly TextBlock _summaryText;
    private readonly Grid _contentGrid;
    private readonly string _fixedFontFamilyId;
    private bool _isOpen;
    private bool _isUpdating;

    public DictionaryTypographyStyleControl(
        FieldDefinition definition,
        string value,
        bool isInherited,
        Func<string, IReadOnlyList<FieldOption>?, Task<string?>>? showThemeTokenPicker,
        string fixedFontFamilyId = "")
    {
        _definition = definition;
        _fixedFontFamilyId = fixedFontFamilyId;
        _inheritedValues = TypographyStyleValue.Parse(definition.InheritedValue);
        if (_inheritedValues.Count == 0)
        {
            _inheritedValues = TypographyStyleValue.Parse(definition.DefaultValue);
        }

        _localValues = definition.CanInherit && (isInherited || value.Equals(definition.InheritedValue, StringComparison.Ordinal))
            ? []
            : TypographyStyleValue.Parse(value);

        RowDefinitions = new RowDefinitions("Auto");
        HorizontalAlignment = HorizontalAlignment.Stretch;

        var innerGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
        };
        _container = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(0),
            Child = innerGrid,
        };
        Children.Add(_container);

        _summaryText = new TextBlock
        {
            Margin = new Thickness(10, 0, 4, 0),
            IsHitTestVisible = false,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var chevron = new TextBlock
        {
            Text = ">",
            Width = 16,
            Margin = new Thickness(0, 0, 10, 0),
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.72,
            IsHitTestVisible = false,
        };
        var headerGrid = new Grid
        {
            MinHeight = 36,
            ColumnDefinitions = new ColumnDefinitions("*,22"),
        };
        SetColumn(_summaryText, 0);
        SetColumn(chevron, 1);
        _headerButton = new Button
        {
            MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
        };
        SetColumnSpan(_headerButton, 2);
        headerGrid.Children.Add(_headerButton);
        headerGrid.Children.Add(_summaryText);
        headerGrid.Children.Add(chevron);
        innerGrid.Children.Add(headerGrid);

        _contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("96,*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"),
            RowSpacing = 8,
            ColumnSpacing = 8,
            Margin = new Thickness(10, 2, 10, 10),
            IsVisible = false,
        };
        SetRow(_contentGrid, 1);
        innerGrid.Children.Add(_contentGrid);
        _headerButton.Click += (_, _) =>
        {
            _isOpen = !_isOpen;
            _contentGrid.IsVisible = _isOpen;
            chevron.Text = _isOpen ? "v" : ">";
            ApplyThemeBrushes();
        };

        var row = 0;
        if (string.IsNullOrWhiteSpace(_fixedFontFamilyId))
        {
            AddOptionRow(row++, "Font", TypographyStyleValue.FontFamilyId, FontOptions(definition.Options), "theme");
        }
        AddOptionRow(row++, "Weight", TypographyStyleValue.Weight, WeightOptions, "theme.typography.weight");
        AddOptionRow(row++, "Style", TypographyStyleValue.Style, StyleOptions, "theme.typography.style");
        AddThemeTokenRow(row++, "Size", TypographyStyleValue.SizeToken, showThemeTokenPicker);
        AddThemeTokenRow(
            row,
            "Line",
            TypographyStyleValue.LineHeight,
            showThemeTokenPicker,
            "theme.typography.lineHeights.normal",
            LineHeightTokenOptions);

        ActualThemeVariantChanged += (_, _) => ApplyThemeBrushes();
        ApplyThemeBrushes();
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
        ApplyFixedFontFamily();

        RefreshRows();
        _isUpdating = false;
    }

    private static IReadOnlyList<FieldOption> FontOptions(IReadOnlyList<FieldOption>? options)
    {
        return
        [
            new FieldOption("theme", "Theme"),
            .. (options ?? []).Select((option) =>
                string.IsNullOrWhiteSpace(option.Value)
                    ? new FieldOption("system", option.Label, option.ColorHex, option.IsNeutral)
                    : option),
        ];
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
        Func<string, IReadOnlyList<FieldOption>?, Task<string?>>? showThemeTokenPicker,
        string fallback = "theme.typography.sizes.s",
        IReadOnlyList<FieldOption>? options = null)
    {
        var controlDefinition = new FieldDefinition(
            $"{_definition.Id}.{key}",
            label,
            ValueKind.ThemeToken,
            _definition.IsEditable,
            fallback,
            Options: options ?? SizeTokenOptions);
        var control = new DictionaryThemeTokenControl(controlDefinition, ValueFor(key, fallback), showThemeTokenPicker);
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
        _rowLabels[key] = labelBlock;
        _restoreButtons[key] = restoreButton;
    }

    private string ValueFor(string key, string fallback)
    {
        if (key == TypographyStyleValue.FontFamilyId && !string.IsNullOrWhiteSpace(_fixedFontFamilyId))
        {
            return _fixedFontFamilyId;
        }
        var value = _localValues.ContainsKey(key)
            ? ValueString(_localValues, key, fallback)
            : ValueString(_inheritedValues, key, fallback);
        return key == TypographyStyleValue.FontFamilyId && string.IsNullOrWhiteSpace(value)
            ? "system"
            : value;
    }

    private static string ValueString(JsonObject values, string key, string fallback)
    {
        return TypographyStyleValue.String(values, key, fallback);
    }

    private void SetSubValue(string key, string value)
    {
        if (_isUpdating) return;

        _localValues[key] = JsonValue.Create(value);
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

        ApplyFixedFontFamily();
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

    private void ApplyFixedFontFamily()
    {
        if (!string.IsNullOrWhiteSpace(_fixedFontFamilyId))
        {
            _localValues[TypographyStyleValue.FontFamilyId] = JsonValue.Create(_fixedFontFamilyId);
        }
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
            var hasLocalValue = HasLocalSubValue(pair.Key);
            pair.Value.IsVisible = hasLocalValue && _definition.IsEditable;
            pair.Value.Foreground = hasLocalValue ? new SolidColorBrush(Color.Parse("#D6A638")) : null;
        }

        foreach (var pair in _rowLabels)
        {
            var hasLocalValue = HasLocalSubValue(pair.Key);
            if (hasLocalValue)
            {
                pair.Value.Foreground = new SolidColorBrush(Color.Parse("#D6A638"));
            }
            else
            {
                pair.Value.ClearValue(TextBlock.ForegroundProperty);
            }
        }

        _summaryText.Text = SummaryText();
        if (_controls.Keys.Any(HasLocalSubValue))
        {
            _summaryText.Foreground = new SolidColorBrush(Color.Parse("#D6A638"));
        }
        else
        {
            _summaryText.ClearValue(TextBlock.ForegroundProperty);
        }
        _isUpdating = false;
    }

    private bool HasLocalSubValue(string key)
    {
        if (_definition.CanInherit)
        {
            return _localValues.ContainsKey(key);
        }

        return ValueString(_localValues, key, "") != ValueString(_inheritedValues, key, "");
    }

    private string SummaryText()
    {
        var font = ValueFor(TypographyStyleValue.FontFamilyId, "theme");
        var weight = ShortToken(ValueFor(TypographyStyleValue.Weight, "theme.typography.weight"));
        var style = ShortToken(ValueFor(TypographyStyleValue.Style, "theme.typography.style"));
        var size = ValueFor(TypographyStyleValue.SizeToken, "theme.typography.sizes.s")
            .Replace("theme.", "", StringComparison.Ordinal);
        var lineHeight = ShortToken(ValueFor(TypographyStyleValue.LineHeight, "theme.typography.lineHeights.normal"));
        return $"{font} · {weight} · {style} · {size} · {lineHeight}";
    }

    private static string ShortToken(string value)
    {
        return value.StartsWith("theme.typography.", StringComparison.Ordinal)
            ? value.Replace("theme.typography.", "", StringComparison.Ordinal)
            : value;
    }

    private void ApplyThemeBrushes()
    {
        var isLight = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light;
        var closedBackground = isLight ? "#14000000" : "#10FFFFFF";
        _container.Background = new SolidColorBrush(Color.Parse(closedBackground));
        _container.BorderBrush = Brushes.Transparent;
        _container.BorderThickness = new Thickness(0);
    }
}
