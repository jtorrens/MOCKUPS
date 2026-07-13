using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryComponentPresetControl : Grid, IDictionaryValueControl
{
    private readonly FieldDefinition _definition;
    private readonly EditorInstantComboBox _comboBox;
    private bool _isUpdating;

    public DictionaryComponentPresetControl(
        FieldDefinition definition,
        string value,
        bool isHighlighted,
        Func<string, Task>? openComponentPresetReference,
        Func<string, Task>? openEmbeddedComponent)
    {
        _definition = definition;
        ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto");
        ColumnSpacing = 8;

        _comboBox = DictionaryOptionSelector.CreateComboBox(definition, value);
        SetValue(value);
        _comboBox.SelectionChanged += (_, _) =>
        {
            if (_isUpdating) return;

            var nextValue = DictionaryOptionSelector.Value(_comboBox);
            ValueChanged?.Invoke(this, nextValue);
            ValueCommitted?.Invoke(this, nextValue);
        };
        SetColumn(_comboBox, 0);
        Children.Add(_comboBox);

        if (openComponentPresetReference is not null)
        {
            var openButton = new Button
            {
                Content = EditorIcons.Create(EditorIcons.Edit, 15),
                Width = 32,
                Height = 32,
                Padding = new Avalonia.Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Foreground = Brushes.White,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = !string.IsNullOrWhiteSpace(DictionaryOptionSelector.Value(_comboBox)),
            };
            EditorAccessibility.Describe(openButton, $"Open selected {_definition.DisplayLabel} component variant");
            openButton.Click += async (_, _) =>
            {
                var selectedReference = DictionaryOptionSelector.Value(_comboBox);
                if (!string.IsNullOrWhiteSpace(selectedReference))
                {
                    await openComponentPresetReference(selectedReference);
                }
            };
            _comboBox.SelectionChanged += (_, _) =>
            {
                openButton.IsEnabled = !string.IsNullOrWhiteSpace(DictionaryOptionSelector.Value(_comboBox));
            };
            SetColumn(openButton, 1);
            Children.Add(openButton);
        }

        if (openEmbeddedComponent is null)
        {
            return;
        }

        var button = new Button
        {
            Content = "···",
            Width = 40,
            Height = 32,
            Padding = new Avalonia.Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEnabled = definition.IsEditable,
        };
        EditorAccessibility.Describe(button, $"Edit overrides for {_definition.DisplayLabel}");
        EditorOverrideVisuals.ApplyActionButton(button, isHighlighted);
        button.Click += async (_, _) =>
        {
            await openEmbeddedComponent(_definition.Id);
        };
        SetColumn(button, 2);
        Children.Add(button);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        _isUpdating = true;
        _comboBox.SelectedItem = DictionaryOptionSelector.SelectedOption(_definition, value);
        _isUpdating = false;
    }
}
