using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryComponentInputBindingsControl : Border, IDictionaryValueControl, IDictionaryRuntimeContractValueControl
{
    private readonly FieldDefinition _definition;
    private readonly DictionaryFieldServices _services;
    private readonly ContentControl _content = new();
    private readonly Dictionary<string, string> _pendingRuntimeTestValues = new(StringComparer.Ordinal);
    private JsonObject _value;

    public DictionaryComponentInputBindingsControl(
        FieldDefinition definition,
        string value,
        DictionaryFieldServices services)
    {
        _definition = definition;
        _services = services;
        _value = ParseValue(value);

        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        Background = Brushes.Transparent;
        VerticalAlignment = VerticalAlignment.Top;
        _content.VerticalAlignment = VerticalAlignment.Top;
        Child = _content;
        RefreshRows();
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public event EventHandler? RuntimeContractChanged;

    public void SetValue(string value)
    {
        _value = ParseValue(value);
        RefreshRows();
    }

    private void RefreshRows()
    {
        var inputs = VariantInputs().OrderBy((input) => input.UiOrder).ToList();
        var sections = new List<EditorInternalNavigationSection>();
        var ownInputs = inputs.Where((input) => string.IsNullOrWhiteSpace(input.UiGroupId)).ToList();
        if (ownInputs.Count > 0)
        {
            sections.Add(CreateNavigationSection("general", "General", ownInputs));
        }
        foreach (var group in inputs
                     .Where((input) => !string.IsNullOrWhiteSpace(input.UiGroupId))
                     .GroupBy((input) => input.UiGroupId, StringComparer.Ordinal)
                     .OrderBy((group) => group.Min((input) => input.UiOrder)))
        {
            var groupInputs = group.OrderBy((input) => input.UiOrder).ToList();
            sections.Add(CreateNavigationSection(
                group.Key,
                groupInputs.Select((input) => input.UiGroupLabel)
                    .FirstOrDefault((label) => !string.IsNullOrWhiteSpace(label)) ?? "Inputs",
                groupInputs));
        }

        if (sections.Count == 0)
        {
            _content.Content = new TextBlock { Text = "No runtime inputs.", Opacity = 0.68 };
            return;
        }

        var stateKey = $"{_definition.Id}:component-inputs";
        var selectedId = _services.StructuredCollectionUiState?.Selection(stateKey);
        var navigationWidth = _services.StructuredCollectionUiState?.NavigationWidth(
            stateKey,
            EditorInternalNavigation.DefaultNavigationWidth);
        _content.Content = new EditorSubcardLayoutHost(
            sections,
            EditorSubcardLayout.VerticalCards,
            selectedId,
            (next) => _services.StructuredCollectionUiState?.Select(stateKey, next),
            navigationWidth,
            (next) => _services.StructuredCollectionUiState?.SetNavigationWidth(stateKey, next));
    }

    private EditorInternalNavigationSection CreateNavigationSection(
        string id,
        string label,
        IReadOnlyList<ComponentInputBindingDefinition> inputs)
    {
        var panel = new StackPanel { Spacing = 8 };
        var sectionLabel = "";
        foreach (var input in inputs)
        {
            if (!string.IsNullOrWhiteSpace(input.UiSectionLabel)
                && !string.Equals(sectionLabel, input.UiSectionLabel, StringComparison.Ordinal))
            {
                panel.Children.Add(EditorGroupBlock.CreateInlineSection(input.UiSectionLabel));
                sectionLabel = input.UiSectionLabel;
            }
            panel.Children.Add(CreateInputField(input));
        }
        return new EditorInternalNavigationSection(
            id,
            label,
            "Runtime inputs",
            EditorIcons.SemanticAsset(label),
            panel,
            ShowLabel: false);
    }

    private static bool IsEmbeddedComponentInput(ComponentInputBindingDefinition input)
    {
        return input.ValueKind == ValueKind.ComponentVariant && !string.IsNullOrWhiteSpace(input.ComponentType);
    }

    private Control CreateInputGroup(IReadOnlyList<ComponentInputBindingDefinition> inputs)
    {
        var embeddedInput = inputs[0];
        var groupRows = new StackPanel
        {
            Spacing = 8,
        };
        foreach (var control in CreateInputControls(inputs))
        {
            groupRows.Children.Add(control);
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

    private IReadOnlyList<Control> CreateInputControls(
        IReadOnlyList<ComponentInputBindingDefinition> inputs)
    {
        var controls = new List<Control>();
        var currentGroupId = "";
        var currentGroup = new List<ComponentInputBindingDefinition>();

        void FlushGroup()
        {
            if (currentGroup.Count == 0)
            {
                return;
            }

            controls.Add(CreateCollapsedInputGroup(currentGroup));
            currentGroup.Clear();
            currentGroupId = "";
        }

        foreach (var input in inputs)
        {
            if (string.IsNullOrWhiteSpace(input.UiGroupId))
            {
                FlushGroup();
                controls.Add(CreateInputField(input));
                continue;
            }

            if (!currentGroupId.Equals(input.UiGroupId, StringComparison.Ordinal))
            {
                FlushGroup();
                currentGroupId = input.UiGroupId;
            }

            currentGroup.Add(input);
        }

        FlushGroup();
        return controls;
    }

    private Control CreateCollapsedInputGroup(IReadOnlyList<ComponentInputBindingDefinition> inputs)
    {
        var groupRows = new StackPanel
        {
            Spacing = 8,
        };
        foreach (var input in inputs)
        {
            groupRows.Children.Add(CreateInputField(input));
        }

        var label = inputs
            .Select((input) => input.UiGroupLabel)
            .FirstOrDefault((value) => !string.IsNullOrWhiteSpace(value)) ?? "Inputs";
        var header = new StackPanel
        {
            Spacing = 1,
        };
        header.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
        });
        header.Children.Add(new TextBlock
        {
            Text = string.Join(" · ", inputs.Select((input) => $"{input.Label}: {DisplayValue(input)}")),
            FontSize = 12,
            Opacity = 0.64,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        return new CompactInputCard(header, groupRows);
    }

    private Control CreateInputField(ComponentInputBindingDefinition input)
    {
        var forwarded = Forwarding(input);
        var content = new StackPanel { Spacing = 6 };
        if (input.ActionOnly)
        {
            content.Children.Add(new TextBlock
            {
                Text = input.Label,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.82,
            });
        }
        else
        {
            var services = ServicesFor(input);
            var field = new DictionaryFieldControl(CreateFieldValue(input), services, compact: true)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            field.ValueChanged += (_, next) => SetInputValue(input, next, commit: false);
            field.ValueCommitted += (_, next) => SetInputValue(input, next, commit: true);
            content.Children.Add(field);
        }
        if (forwarded is not null)
        {
            var runtimeLabel = new DictionaryFieldControl(
                new FieldValue(
                    new FieldDefinition(
                        $"{_definition.Id}.{input.Id}.runtimeLabel",
                        "Runtime label",
                        ValueKind.StringSingleLine,
                        DefaultValue: input.Label),
                    JsonText(forwarded["label"], input.Label)),
                _services,
                compact: true);
            runtimeLabel.ValueChanged += (_, next) => SetForwardingLabel(input, next, commit: false);
            runtimeLabel.ValueCommitted += (_, next) => SetForwardingLabel(input, next, commit: true);
            content.Children.Add(runtimeLabel);
        }

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 6,
        };
        row.Children.Add(content);
        var indicator = new Path
        {
            Width = 12,
            Height = 11,
            Data = Geometry.Parse("M 6,1 L 11,10 L 1,10 Z"),
            Fill = forwarded is null ? Brushes.Transparent : EditorOverrideVisuals.Brush,
            Stroke = forwarded is null ? null : EditorOverrideVisuals.Brush,
            StrokeThickness = 1.5,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var toggle = new Button
        {
            Content = indicator,
            Width = 30,
            Height = 30,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        if (forwarded is null)
        {
            indicator.Bind(Path.StrokeProperty, toggle.GetObservable(Button.ForegroundProperty));
        }
        ToolTip.SetTip(toggle, forwarded is null ? "Expose to parent runtime" : "Keep as Variant value");
        toggle.Click += async (_, args) =>
        {
            args.Handled = true;
            if (Forwarding(input) is null)
            {
                SetForwarding(input, enabled: true);
            }
            else
            {
                var confirmed = _services.ConfirmStopRuntimeInputForwarding is null
                    || await _services.ConfirmStopRuntimeInputForwarding(
                        JsonText(Forwarding(input)?["label"], input.Label));
                if (!confirmed) return;
                SetForwarding(input, enabled: false);
            }
            RefreshRows();
        };
        Grid.SetColumn(toggle, 1);
        row.Children.Add(toggle);
        return row;
    }

    private DictionaryFieldServices ServicesFor(ComponentInputBindingDefinition input)
    {
        if (input.ValueKind != ValueKind.ComponentVariant
            || string.IsNullOrWhiteSpace(input.ComponentType)
            || _services.OpenComponentInputBinding is null)
        {
            return _services;
        }

        return _services with
        {
            OpenEmbeddedComponent = async (_) =>
            {
                EnsureComponentVariantSlot(input, commit: true);
                await _services.OpenComponentInputBinding(_definition, input);
            },
        };
    }

    private FieldValue CreateFieldValue(ComponentInputBindingDefinition input)
    {
        var options = OptionsFor(input) ?? [];
        var value = InputValue(input, options);
        if (string.IsNullOrWhiteSpace(value) && input.ValueKind == ValueKind.ComponentVariant)
        {
            value = options.FirstOrDefault((option) => !string.IsNullOrWhiteSpace(option.Value))?.Value ?? "";
            if (!string.IsNullOrWhiteSpace(value))
            {
                _value[input.JsonKey] = ComponentVariantSlotNode(input, value);
            }
        }

        return new FieldValue(
            new FieldDefinition(
                $"{_definition.Id}.{input.Id}",
                input.Label,
                input.ValueKind,
                _definition.IsEditable && Forwarding(input) is null,
                input.DefaultValue,
                Options: options,
                Number: input.Number,
                RecordReference: input.ValueKind == ValueKind.RecordReference
                    ? new RecordReferenceDefinition(input.TableId)
                    : null),
            value);
    }

    private IReadOnlyList<FieldOption>? OptionsFor(ComponentInputBindingDefinition input)
    {
        if (input.ValueKind == ValueKind.ComponentVariant && !string.IsNullOrWhiteSpace(input.ComponentType))
        {
            return _services.GetComponentVariantOptions?.Invoke(input.ComponentType) ?? [];
        }

        if (input.ValueKind == ValueKind.PaletteColorToken)
        {
            return _services.GetPaletteColorOptions?.Invoke() ?? [];
        }

        return input.Options;
    }

    private IEnumerable<ComponentInputBindingDefinition> VariantInputs()
    {
        var declared = (_definition.ComponentInputBindings ?? [])
            .Where((input) => input.Source != ComponentInputBindingSource.Calculated)
            .ToList();
        if (string.IsNullOrWhiteSpace(_definition.RuntimeInputComponentVariantFieldId)
            || _services.GetFieldValue is null
            || _services.GetComponentVariantRuntimeInputs is null)
        {
            return declared;
        }
        var variantReference = _services.GetFieldValue(_definition.RuntimeInputComponentVariantFieldId);
        if (string.IsNullOrWhiteSpace(variantReference)) return declared;
        var known = declared.Select((input) => input.Id).ToHashSet(StringComparer.Ordinal);
        declared.AddRange(_services.GetComponentVariantRuntimeInputs(variantReference)
            .Where((input) => known.Add(input.Id)));
        return declared;
    }

    private void SetInputValue(ComponentInputBindingDefinition input, string next, bool commit)
    {
        _value[input.JsonKey] = input.ValueKind == ValueKind.ComponentVariant && !string.IsNullOrWhiteSpace(input.ComponentType)
            ? ComponentVariantSlotNode(input, next)
            : ToJsonValue(input.ValueKind, next);
        if (Forwarding(input) is { } forwarding)
        {
            forwarding["defaultValue"] = next;
        }
        var transitioned = ApplyTransition(input, next) || _pendingRuntimeTestValues.Count > 0;
        var json = _value.ToJsonString();
        ValueChanged?.Invoke(this, json);
        if (commit)
        {
            ValueCommitted?.Invoke(this, json);
            foreach (var (jsonKey, value) in _pendingRuntimeTestValues)
            {
                _services.SetRuntimeTestValue?.Invoke(jsonKey, value);
            }
            _pendingRuntimeTestValues.Clear();
            if (transitioned) RefreshRows();
        }
    }

    private bool ApplyTransition(ComponentInputBindingDefinition input, string next)
    {
        var transition = input.Transition;
        if (transition is null || !transition.TriggerValues.Contains(next, StringComparer.Ordinal))
        {
            return false;
        }
        var target = VariantInputs().FirstOrDefault((candidate) =>
            candidate.Id.Equals(transition.TargetInputId, StringComparison.Ordinal));
        if (target is null)
        {
            throw new InvalidOperationException(
                $"Component input transition target '{transition.TargetInputId}' was not declared.");
        }
        var targetForwarding = Forwarding(target);
        if (transition.ForwardedTargetOnly && targetForwarding is null)
        {
            return false;
        }
        var current = InputValue(target, OptionsFor(target) ?? []);
        if (!string.IsNullOrWhiteSpace(transition.TargetValuePattern)
            && Regex.IsMatch(current, transition.TargetValuePattern, RegexOptions.CultureInvariant))
        {
            return false;
        }
        _value[target.JsonKey] = ToJsonValue(target.ValueKind, transition.ReplacementValue);
        if (targetForwarding is not null)
        {
            targetForwarding["defaultValue"] = transition.ReplacementValue;
            var runtimeJsonKey = JsonText(targetForwarding["jsonKey"], "");
            if (!string.IsNullOrWhiteSpace(runtimeJsonKey))
            {
                _pendingRuntimeTestValues[runtimeJsonKey] = transition.ReplacementValue;
            }
        }
        return true;
    }

    private JsonObject? Forwarding(ComponentInputBindingDefinition input) =>
        (_value[RuntimeInputForwardingContract.StorageKey] as JsonObject)?[input.JsonKey] as JsonObject;

    private void SetForwarding(ComponentInputBindingDefinition input, bool enabled)
    {
        var forwards = _value[RuntimeInputForwardingContract.StorageKey] as JsonObject ?? new JsonObject();
        if (enabled)
        {
            _value[RuntimeInputForwardingContract.StorageKey] = forwards;
            forwards[input.JsonKey] = RuntimeInputForwardingContract.Definition(
                _definition,
                input,
                input.Label,
                InputValue(input, OptionsFor(input) ?? []));
        }
        else
        {
            forwards.Remove(input.JsonKey);
            if (forwards.Count == 0) _value.Remove(RuntimeInputForwardingContract.StorageKey);
        }
        var json = _value.ToJsonString();
        ValueChanged?.Invoke(this, json);
        ValueCommitted?.Invoke(this, json);
        RuntimeContractChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetForwardingLabel(ComponentInputBindingDefinition input, string label, bool commit)
    {
        if (Forwarding(input) is not { } forwarding) return;
        forwarding["label"] = string.IsNullOrWhiteSpace(label) ? input.Label : label.Trim();
        var json = _value.ToJsonString();
        ValueChanged?.Invoke(this, json);
        if (commit)
        {
            ValueCommitted?.Invoke(this, json);
            RuntimeContractChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private string DisplayValue(ComponentInputBindingDefinition input)
    {
        var value = InputValue(input, OptionsFor(input) ?? []);
        var option = OptionsFor(input)?.FirstOrDefault((candidate) => candidate.Value == value);
        return option?.Label ?? value;
    }

    private string InputValue(ComponentInputBindingDefinition input, IReadOnlyList<FieldOption> options)
    {
        if (input.ValueKind != ValueKind.ComponentVariant)
        {
            return StringValue(_value[input.JsonKey], input.DefaultValue);
        }

        var node = _value[input.JsonKey];
        if (node is JsonObject slot)
        {
            return JsonText(slot["variantReference"], input.DefaultValue);
        }

        var value = StringValue(node, input.DefaultValue);
        return string.IsNullOrWhiteSpace(value)
            ? options.FirstOrDefault((option) => !string.IsNullOrWhiteSpace(option.Value))?.Value ?? ""
            : value;
    }

    private void EnsureComponentVariantSlot(ComponentInputBindingDefinition input, bool commit)
    {
        var value = InputValue(input, OptionsFor(input) ?? []);
        if (string.IsNullOrWhiteSpace(value)) return;

        _value[input.JsonKey] = ComponentVariantSlotNode(input, value);
        if (!commit) return;

        var json = _value.ToJsonString();
        ValueChanged?.Invoke(this, json);
        ValueCommitted?.Invoke(this, json);
    }

    private JsonObject ComponentVariantSlotNode(ComponentInputBindingDefinition input, string variantReference)
    {
        var existing = _value[input.JsonKey] as JsonObject;
        var overrides = existing?["overrides"] is JsonObject existingOverrides
            ? JsonNode.Parse(existingOverrides.ToJsonString()) as JsonObject ?? new JsonObject()
            : new JsonObject();
        return new JsonObject
        {
            ["variantReference"] = variantReference,
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
            ValueKind.IconTokenList or ValueKind.IconSlots => JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value) ?? new JsonArray(),
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
            ClipToBounds = true;

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
                ClipToBounds = true,
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
