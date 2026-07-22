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

internal sealed class DictionaryComponentVariantControl : Grid, IDictionaryValueControl
{
    private readonly FieldDefinition _definition;
    private readonly IReadOnlyList<FieldOption> _references;
    private readonly EditorInstantComboBox? _componentCombo;
    private readonly EditorInstantComboBox _variantCombo;
    private readonly Button? _openButton;
    private readonly Button? _overrideButton;
    private bool _isUpdating;

    public DictionaryComponentVariantControl(
        FieldDefinition definition,
        string value,
        bool isHighlighted,
        Func<string, Task>? openComponentVariantReference,
        Func<string, Task>? openEmbeddedComponent)
    {
        _definition = definition;
        _references = definition.Options ?? [];
        var selectsComponentClass = definition.SelectComponentClass;
        MinWidth = 0;
        ClipToBounds = true;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        ColumnDefinitions = new ColumnDefinitions(selectsComponentClass ? "72,*" : "*");
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

        if (openComponentVariantReference is not null)
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
                    await openComponentVariantReference(selectedReference);
                }
            };
        }

        if (openEmbeddedComponent is not null)
        {
            _overrideButton = new Button
            {
                Content = EditorIcons.CreateSemantic("Edit overrides", EditorIcons.Edit, 15),
                Width = 40,
                Height = 32,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = definition.IsEditable,
            };
            EditorAccessibility.Describe(_overrideButton, $"Edit overrides for {_definition.DisplayLabel}");
            EditorOverrideVisuals.ApplyActionButton(_overrideButton, isHighlighted);
            _overrideButton.Click += async (_, _) => await openEmbeddedComponent(_definition.Id);
        }

        var variantRow = new DockPanel
        {
            LastChildFill = true,
            MinWidth = 0,
            ClipToBounds = true,
        };
        if (_overrideButton is not null)
        {
            _overrideButton.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(_overrideButton, Dock.Right);
            variantRow.Children.Add(_overrideButton);
        }
        if (_openButton is not null)
        {
            _openButton.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(_openButton, Dock.Right);
            variantRow.Children.Add(_openButton);
        }
        variantRow.Children.Add(_variantCombo);
        SetColumn(variantRow, selectsComponentClass ? 1 : 0);
        SetRow(variantRow, selectsComponentClass ? 1 : 0);
        Children.Add(variantRow);

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
        MinWidth = 0,
        MinHeight = 36,
        HorizontalAlignment = HorizontalAlignment.Stretch,
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
                && option.Value.Equals(VariantReferenceId.Format(componentId, VariantEnvelopeContract.DefaultId), StringComparison.Ordinal))?.Value
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
        return VariantReferenceId.TryParse(reference, out var componentId, out _)
            ? componentId
            : "";
    }
}
