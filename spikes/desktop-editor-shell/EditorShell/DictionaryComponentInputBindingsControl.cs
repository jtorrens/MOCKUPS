using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryComponentInputBindingsControl : Border, IDictionaryValueControl
{
    private readonly FieldDefinition _definition;
    private readonly DictionaryFieldServices _services;
    private readonly TextBlock _summary = new();
    private readonly TextBlock _indicator = new();
    private readonly StackPanel _rows = new() { Spacing = 8 };
    private JsonObject _value;
    private bool _isExpanded;

    public DictionaryComponentInputBindingsControl(
        FieldDefinition definition,
        string value,
        DictionaryFieldServices services)
    {
        _definition = definition;
        _services = services;
        _value = ParseValue(value);

        CornerRadius = new CornerRadius(8);
        BorderThickness = new Thickness(1);
        Padding = new Thickness(0);
        Background = new SolidColorBrush(Color.Parse("#10FFFFFF"));
        BorderBrush = new SolidColorBrush(Color.Parse("#24FFFFFF"));

        Child = Build();
        ActualThemeVariantChanged += (_, _) => ApplyThemeBrushes();
        ApplyThemeBrushes();
        RefreshRows();
        ApplyExpandedState();
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        _value = ParseValue(value);
        RefreshRows();
        RefreshSummary();
    }

    private Control Build()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
        };

        var header = new Border
        {
            Padding = new Thickness(10, 7),
            Cursor = new Cursor(StandardCursorType.Hand),
            Background = Brushes.Transparent,
        };
        header.PointerPressed += (_, args) =>
        {
            IsExpanded = !IsExpanded;
            args.Handled = true;
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
        };
        _summary.VerticalAlignment = VerticalAlignment.Center;
        _summary.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(_summary, 0);
        headerGrid.Children.Add(_summary);

        _indicator.Width = 20;
        _indicator.FontSize = 18;
        _indicator.FontWeight = FontWeight.Bold;
        _indicator.TextAlignment = TextAlignment.Center;
        _indicator.VerticalAlignment = VerticalAlignment.Center;
        _indicator.Opacity = 0.72;
        Grid.SetColumn(_indicator, 1);
        headerGrid.Children.Add(_indicator);

        header.Child = headerGrid;
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var content = new Border
        {
            Padding = new Thickness(10, 0, 10, 10),
            Child = _rows,
        };
        Grid.SetRow(content, 1);
        root.Children.Add(content);

        return root;
    }

    private bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;

            _isExpanded = value;
            ApplyExpandedState();
        }
    }

    private void ApplyExpandedState()
    {
        _rows.IsVisible = _isExpanded;
        _indicator.Text = _isExpanded ? "v" : ">";
        RefreshSummary();
    }

    private void RefreshRows()
    {
        _rows.Children.Clear();
        foreach (var input in VariantInputs())
        {
            var field = new DictionaryFieldControl(CreateFieldValue(input), _services);
            field.ValueChanged += (_, next) => SetInputValue(input, next, commit: false);
            field.ValueCommitted += (_, next) => SetInputValue(input, next, commit: true);
            _rows.Children.Add(field);
        }
    }

    private FieldValue CreateFieldValue(ComponentInputBindingDefinition input)
    {
        var options = OptionsFor(input) ?? [];
        var value = StringValue(_value[input.JsonKey], input.DefaultValue);
        if (string.IsNullOrWhiteSpace(value) && input.ValueKind == ValueKind.ComponentPreset)
        {
            value = options.FirstOrDefault((option) => !string.IsNullOrWhiteSpace(option.Value))?.Value ?? "";
            if (!string.IsNullOrWhiteSpace(value))
            {
                _value[input.JsonKey] = value;
            }
        }

        return new FieldValue(
            new FieldDefinition(
                $"{_definition.Id}.{input.Id}",
                input.Label,
                input.ValueKind,
                _definition.IsEditable,
                input.DefaultValue,
                Options: options,
                Number: input.Number),
            value);
    }

    private IReadOnlyList<FieldOption>? OptionsFor(ComponentInputBindingDefinition input)
    {
        if (input.ValueKind == ValueKind.ComponentPreset && !string.IsNullOrWhiteSpace(input.ComponentType))
        {
            return _services.GetComponentPresetOptions?.Invoke(input.ComponentType) ?? [];
        }

        return input.Options;
    }

    private IEnumerable<ComponentInputBindingDefinition> VariantInputs()
    {
        return _definition.ComponentInputBindings?
            .Where((input) => input.Source == ComponentInputBindingSource.Variant)
            ?? [];
    }

    private void SetInputValue(ComponentInputBindingDefinition input, string next, bool commit)
    {
        _value[input.JsonKey] = ToJsonValue(input.ValueKind, next);
        var json = _value.ToJsonString();
        ValueChanged?.Invoke(this, json);
        RefreshSummary();
        if (commit)
        {
            ValueCommitted?.Invoke(this, json);
        }
    }

    private void RefreshSummary()
    {
        var values = VariantInputs()
            .Select((input) => $"{input.Label}: {DisplayValue(input)}")
            .ToList();
        _summary.Text = values.Count == 0
            ? "No variant inputs"
            : string.Join("  ·  ", values);
    }

    private string DisplayValue(ComponentInputBindingDefinition input)
    {
        var value = StringValue(_value[input.JsonKey], input.DefaultValue);
        var option = OptionsFor(input)?.FirstOrDefault((candidate) => candidate.Value == value);
        return option?.Label ?? value;
    }

    private static JsonObject ParseValue(string value)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "{}" : value) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    private static string StringValue(JsonNode? node, string fallback)
    {
        return node switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            JsonValue value when value.TryGetValue<double>(out var number) => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonValue value when value.TryGetValue<int>(out var integer) => integer.ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonArray array => array.ToJsonString(),
            JsonObject obj => obj.ToJsonString(),
            _ => fallback,
        };
    }

    private static JsonNode ToJsonValue(ValueKind kind, string value)
    {
        return kind switch
        {
            ValueKind.Integer or ValueKind.Decimal or ValueKind.Alpha => NumberNode(value),
            ValueKind.Boolean => JsonValue.Create(BooleanText.Parse(value))!,
            ValueKind.IconTokenList => JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value) ?? new JsonArray(),
            _ => JsonValue.Create(value)!,
        };
    }

    private static JsonNode NumberNode(string value)
    {
        var normalized = value.Replace(",", ".");
        return decimal.TryParse(
            normalized,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var number)
            ? JsonValue.Create(number)!
            : JsonValue.Create(0)!;
    }

    private void ApplyThemeBrushes()
    {
        var isLight = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light;
        Background = new SolidColorBrush(Color.Parse(isLight ? "#10000000" : "#10FFFFFF"));
        BorderBrush = new SolidColorBrush(Color.Parse(isLight ? "#1E000000" : "#24FFFFFF"));
    }
}
