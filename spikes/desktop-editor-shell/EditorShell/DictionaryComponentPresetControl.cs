using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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
        Func<string, Task>? openEmbeddedComponent)
    {
        _definition = definition;
        ColumnDefinitions = new ColumnDefinitions("*,Auto");
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

        var button = new Button
        {
            Content = "···",
            Width = 40,
            Height = 32,
            Padding = new Avalonia.Thickness(0),
            Background = new SolidColorBrush(Color.Parse(isHighlighted ? "#38D6A638" : "#24D6A638")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D6A638")),
            Foreground = new SolidColorBrush(Color.Parse("#D6A638")),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEnabled = definition.IsEditable && openEmbeddedComponent is not null,
        };
        button.Click += async (_, _) =>
        {
            if (openEmbeddedComponent is null) return;

            await openEmbeddedComponent(_definition.Id);
        };
        SetColumn(button, 1);
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
