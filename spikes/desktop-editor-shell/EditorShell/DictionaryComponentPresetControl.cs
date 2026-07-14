using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryComponentPresetControl : Grid, IDictionaryValueControl
{
    private const string PresetSeparator = "::preset::";
    private readonly FieldDefinition _definition;
    private readonly IReadOnlyList<FieldOption> _references;
    private readonly EditorInstantComboBox? _componentCombo;
    private readonly EditorInstantComboBox _variantCombo;
    private readonly Button? _openButton;
    private bool _isUpdating;

    public DictionaryComponentPresetControl(
        FieldDefinition definition,
        string value,
        bool isHighlighted,
        Func<string, Task>? openComponentPresetReference,
        Func<string, Task>? openEmbeddedComponent)
    {
        _definition = definition;
        _references = definition.Options ?? [];
        var selectsComponentClass = definition.SelectComponentClass;
        ColumnDefinitions = new ColumnDefinitions(selectsComponentClass ? "72,*,Auto,Auto" : "*,Auto,Auto");
        RowDefinitions = new RowDefinitions(selectsComponentClass ? "Auto,Auto" : "Auto");
        ColumnSpacing = 8;
        RowSpacing = 6;

        if (selectsComponentClass)
        {
            AddLabel("Component", 0);
            _componentCombo = CreateComboBox();
            _componentCombo.ItemsSource = ComponentOptions();
            _componentCombo.SelectionChanged += (_, _) => ComponentChanged();
            SetColumn(_componentCombo, 1);
            SetRow(_componentCombo, 0);
            Children.Add(_componentCombo);
            AddLabel("Variant", 1);
        }

        _variantCombo = selectsComponentClass
            ? CreateComboBox()
            : DictionaryOptionSelector.CreateComboBox(definition, value);
        _variantCombo.SelectionChanged += (_, _) => VariantChanged();
        SetColumn(_variantCombo, selectsComponentClass ? 1 : 0);
        SetRow(_variantCombo, selectsComponentClass ? 1 : 0);
        Children.Add(_variantCombo);

        var actionRow = selectsComponentClass ? 1 : 0;
        var openColumn = selectsComponentClass ? 2 : 1;
        var overrideColumn = selectsComponentClass ? 3 : 2;

        if (openComponentPresetReference is not null)
        {
            _openButton = new Button
            {
                Content = EditorIcons.CreateSemantic("Open selected component variant", EditorIcons.Edit, 15),
                Width = 32,
                Height = 32,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Foreground = Brushes.White,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            EditorAccessibility.Describe(_openButton, $"Open selected {_definition.DisplayLabel} component variant");
            _openButton.Click += async (_, _) =>
            {
                var selectedReference = SelectedReference();
                if (!string.IsNullOrWhiteSpace(selectedReference))
                {
                    await openComponentPresetReference(selectedReference);
                }
            };
            SetColumn(_openButton, openColumn);
            SetRow(_openButton, actionRow);
            Children.Add(_openButton);
        }

        if (openEmbeddedComponent is not null)
        {
            var editButton = new Button
            {
                Content = EditorIcons.CreateSemantic("Edit overrides", EditorIcons.Edit, 15),
                Width = 40,
                Height = 32,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = definition.IsEditable,
            };
            EditorAccessibility.Describe(editButton, $"Edit overrides for {_definition.DisplayLabel}");
            EditorOverrideVisuals.ApplyActionButton(editButton, isHighlighted);
            editButton.Click += async (_, _) => await openEmbeddedComponent(_definition.Id);
            SetColumn(editButton, overrideColumn);
            SetRow(editButton, actionRow);
            Children.Add(editButton);
        }

        SetValue(value);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        _isUpdating = true;
        if (!_definition.SelectComponentClass)
        {
            DictionaryOptionSelector.SetValue(_variantCombo, _definition, value);
            _isUpdating = false;
            UpdateOpenButton();
            return;
        }
        var reference = _references.FirstOrDefault((option) => option.Value.Equals(value, StringComparison.Ordinal));
        var componentValue = reference?.GroupValue ?? ComponentId(value);
        _componentCombo!.SelectedItem = ComponentOptions()
            .FirstOrDefault((option) => option.Value.Equals(componentValue, StringComparison.Ordinal));
        SetVariantOptions(componentValue, value);
        _isUpdating = false;
        UpdateOpenButton();
    }

    private EditorInstantComboBox CreateComboBox() => new()
    {
        MinHeight = 36,
        IsEnabled = _definition.IsEditable,
    };

    private void AddLabel(string text, int row)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 11,
            Opacity = 0.72,
            VerticalAlignment = VerticalAlignment.Center,
        };
        SetColumn(label, 0);
        SetRow(label, row);
        Children.Add(label);
    }

    private IReadOnlyList<FieldOption> ComponentOptions()
    {
        var options = _references
            .Where((option) => !string.IsNullOrWhiteSpace(option.GroupValue))
            .GroupBy((option) => option.GroupValue, StringComparer.Ordinal)
            .Select((group) => new FieldOption(group.Key, group.First().GroupLabel))
            .ToList();
        if (_references.Any((option) => string.IsNullOrWhiteSpace(option.Value)))
        {
            options.Insert(0, new FieldOption("", "None"));
        }
        return options;
    }

    private void ComponentChanged()
    {
        if (_isUpdating) return;
        _isUpdating = true;
        var componentId = DictionaryOptionSelector.Value(_componentCombo!);
        var defaultReference = string.IsNullOrWhiteSpace(componentId)
            ? ""
            : _references.SingleOrDefault((option) =>
                option.GroupValue.Equals(componentId, StringComparison.Ordinal)
                && option.Value.Equals($"{componentId}{PresetSeparator}default", StringComparison.Ordinal))?.Value
              ?? throw new InvalidOperationException($"Component '{componentId}' has no explicit default Variant.");
        SetVariantOptions(componentId, defaultReference);
        _isUpdating = false;
        Publish(defaultReference);
    }

    private void VariantChanged()
    {
        if (_isUpdating) return;
        Publish(SelectedReference());
    }

    private void SetVariantOptions(string componentId, string selectedReference)
    {
        var variants = string.IsNullOrWhiteSpace(componentId)
            ? new List<FieldOption> { new("", "None") }
            : _references
                .Where((option) => option.GroupValue.Equals(componentId, StringComparison.Ordinal))
                .Select((option) => new FieldOption(
                    option.Value,
                    string.IsNullOrWhiteSpace(option.LocalLabel) ? option.Label : option.LocalLabel))
                .ToList();
        _variantCombo.ItemsSource = variants;
        _variantCombo.SelectedItem = variants.FirstOrDefault((option) =>
            option.Value.Equals(selectedReference, StringComparison.Ordinal));
    }

    private string SelectedReference() => DictionaryOptionSelector.Value(_variantCombo);

    private void Publish(string reference)
    {
        UpdateOpenButton();
        ValueChanged?.Invoke(this, reference);
        ValueCommitted?.Invoke(this, reference);
    }

    private void UpdateOpenButton()
    {
        if (_openButton is not null)
        {
            _openButton.IsEnabled = !string.IsNullOrWhiteSpace(SelectedReference());
        }
    }

    private static string ComponentId(string reference)
    {
        var separatorIndex = reference.IndexOf(PresetSeparator, StringComparison.Ordinal);
        return separatorIndex > 0 ? reference[..separatorIndex] : "";
    }
}
