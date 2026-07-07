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
        var topLevelInputs = new List<ComponentInputBindingDefinition>();
        var groups = new List<List<ComponentInputBindingDefinition>>();
        List<ComponentInputBindingDefinition>? currentGroup = null;
        foreach (var input in VariantInputs())
        {
            if (IsEmbeddedComponentInput(input))
            {
                currentGroup = [input];
                groups.Add(currentGroup);
                continue;
            }

            if (currentGroup is null)
            {
                topLevelInputs.Add(input);
            }
            else
            {
                currentGroup.Add(input);
            }
        }

        foreach (var input in topLevelInputs)
        {
            _rows.Children.Add(CreateInputField(input));
        }

        foreach (var group in groups)
        {
            _rows.Children.Add(CreateInputGroup(group));
        }
    }

    private static bool IsEmbeddedComponentInput(ComponentInputBindingDefinition input)
    {
        return input.ValueKind == ValueKind.ComponentPreset && !string.IsNullOrWhiteSpace(input.ComponentType);
    }

    private Control CreateInputGroup(IReadOnlyList<ComponentInputBindingDefinition> inputs)
    {
        var embeddedInput = inputs[0];
        var groupRows = new StackPanel
        {
            Spacing = 8,
        };
        foreach (var input in inputs)
        {
            groupRows.Children.Add(CreateInputField(input));
        }

        var header = new StackPanel
        {
            Spacing = 1,
        };
        header.Children.Add(new TextBlock
        {
            Text = embeddedInput.Label,
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
        });
        header.Children.Add(new TextBlock
        {
            Text = $"{DisplayValue(embeddedInput)} · Embedded inputs",
            FontSize = 12,
            Opacity = 0.64,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        return new CompactInputCard(header, groupRows);
    }

    private DictionaryFieldControl CreateInputField(ComponentInputBindingDefinition input)
    {
        var services = ServicesFor(input);
        var field = new DictionaryFieldControl(CreateFieldValue(input), services, compact: true)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        field.ValueChanged += (_, next) => SetInputValue(input, next, commit: false);
        field.ValueCommitted += (_, next) => SetInputValue(input, next, commit: true);
        return field;
    }

    private DictionaryFieldServices ServicesFor(ComponentInputBindingDefinition input)
    {
        if (input.ValueKind != ValueKind.ComponentPreset
            || string.IsNullOrWhiteSpace(input.ComponentType)
            || _services.OpenComponentInputBinding is null)
        {
            return _services;
        }

        return _services with
        {
            OpenEmbeddedComponent = async (_) =>
            {
                EnsureComponentPresetSlot(input, commit: true);
                await _services.OpenComponentInputBinding(_definition, input);
            },
        };
    }

    private FieldValue CreateFieldValue(ComponentInputBindingDefinition input)
    {
        var options = OptionsFor(input) ?? [];
        var value = InputValue(input, options);
        if (string.IsNullOrWhiteSpace(value) && input.ValueKind == ValueKind.ComponentPreset)
        {
            value = options.FirstOrDefault((option) => !string.IsNullOrWhiteSpace(option.Value))?.Value ?? "";
            if (!string.IsNullOrWhiteSpace(value))
            {
                _value[input.JsonKey] = ComponentPresetSlotNode(input, value);
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

        if (input.ValueKind == ValueKind.PaletteColorToken)
        {
            return _services.GetPaletteColorOptions?.Invoke() ?? [];
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
        _value[input.JsonKey] = input.ValueKind == ValueKind.ComponentPreset && !string.IsNullOrWhiteSpace(input.ComponentType)
            ? ComponentPresetSlotNode(input, next)
            : ToJsonValue(input.ValueKind, next);
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
        var value = InputValue(input, OptionsFor(input) ?? []);
        var option = OptionsFor(input)?.FirstOrDefault((candidate) => candidate.Value == value);
        return option?.Label ?? value;
    }

    private string InputValue(ComponentInputBindingDefinition input, IReadOnlyList<FieldOption> options)
    {
        if (input.ValueKind != ValueKind.ComponentPreset)
        {
            return StringValue(_value[input.JsonKey], input.DefaultValue);
        }

        var node = _value[input.JsonKey];
        if (node is JsonObject slot)
        {
            return JsonText(slot["presetId"], input.DefaultValue);
        }

        var value = StringValue(node, input.DefaultValue);
        return string.IsNullOrWhiteSpace(value)
            ? options.FirstOrDefault((option) => !string.IsNullOrWhiteSpace(option.Value))?.Value ?? ""
            : value;
    }

    private void EnsureComponentPresetSlot(ComponentInputBindingDefinition input, bool commit)
    {
        var value = InputValue(input, OptionsFor(input) ?? []);
        if (string.IsNullOrWhiteSpace(value)) return;

        _value[input.JsonKey] = ComponentPresetSlotNode(input, value);
        if (!commit) return;

        var json = _value.ToJsonString();
        ValueChanged?.Invoke(this, json);
        ValueCommitted?.Invoke(this, json);
    }

    private JsonObject ComponentPresetSlotNode(ComponentInputBindingDefinition input, string presetId)
    {
        var existing = _value[input.JsonKey] as JsonObject;
        var overrides = existing?["overrides"] is JsonObject existingOverrides
            ? JsonNode.Parse(existingOverrides.ToJsonString()) as JsonObject ?? new JsonObject()
            : new JsonObject();
        return new JsonObject
        {
            ["presetId"] = presetId,
            ["overrides"] = overrides,
        };
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

    private static string JsonText(JsonNode? node, string fallback)
    {
        return node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : fallback;
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

    private sealed class CompactInputCard : Border
    {
        private readonly TextBlock _indicator = new();
        private readonly Border _contentHost;
        private bool _isExpanded;

        public CompactInputCard(Control header, Control content)
        {
            CornerRadius = new CornerRadius(10);
            BorderThickness = new Thickness(1);
            Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255));
            BorderBrush = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255));
            HorizontalAlignment = HorizontalAlignment.Stretch;

            var root = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto"),
            };

            var headerRow = new Border
            {
                Padding = new Thickness(10, 8),
                Cursor = new Cursor(StandardCursorType.Hand),
                Background = Brushes.Transparent,
            };
            headerRow.PointerPressed += (_, args) =>
            {
                if (args.Source is Button)
                {
                    return;
                }

                IsExpanded = !IsExpanded;
                args.Handled = true;
            };

            var headerGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 8,
            };
            Grid.SetColumn(header, 0);
            headerGrid.Children.Add(header);

            _indicator.Width = 24;
            _indicator.FontSize = 18;
            _indicator.FontWeight = FontWeight.Bold;
            _indicator.Opacity = 0.72;
            _indicator.TextAlignment = TextAlignment.Center;
            _indicator.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_indicator, 1);
            headerGrid.Children.Add(_indicator);

            headerRow.Child = headerGrid;
            Grid.SetRow(headerRow, 0);
            root.Children.Add(headerRow);

            _contentHost = new Border
            {
                Padding = new Thickness(10, 0, 10, 10),
                Child = content,
            };
            Grid.SetRow(_contentHost, 1);
            root.Children.Add(_contentHost);

            Child = root;
            ApplyExpandedState();
        }

        private bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;

                _isExpanded = value;
                ApplyExpandedState();
                if (_isExpanded)
                {
                    DeferredBringIntoView.Request(_contentHost);
                }
            }
        }

        private void ApplyExpandedState()
        {
            _contentHost.IsVisible = _isExpanded;
            _indicator.Text = _isExpanded ? "v" : ">";
        }
    }

    private void ApplyThemeBrushes()
    {
        var isLight = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light;
        Background = new SolidColorBrush(Color.Parse(isLight ? "#10000000" : "#10FFFFFF"));
        BorderBrush = new SolidColorBrush(Color.Parse(isLight ? "#1E000000" : "#24FFFFFF"));
    }
}
