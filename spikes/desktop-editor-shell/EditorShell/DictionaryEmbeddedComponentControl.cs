using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryEmbeddedComponentControl : Grid, IDictionaryValueControl
{
    private readonly FieldDefinition _definition;
    private readonly TextBlock _label;
    private string _value;

    public DictionaryEmbeddedComponentControl(
        FieldDefinition definition,
        string value,
        Func<string, Task>? openEmbeddedComponent)
    {
        _definition = definition;
        _value = value;
        ColumnDefinitions = new ColumnDefinitions("*,Auto");
        ColumnSpacing = 8;

        _label = new TextBlock
        {
            Text = DisplayText(value),
            Foreground = LabelBrush(definition, value),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.82,
        };
        SetColumn(_label, 0);
        Children.Add(_label);

        var button = new Button
        {
            Content = "···",
            Width = 40,
            Height = 32,
            Padding = new Avalonia.Thickness(0),
            Background = new SolidColorBrush(Color.Parse("#24D6A638")),
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

    public event EventHandler<string>? ValueChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<string>? ValueCommitted
    {
        add { }
        remove { }
    }

    public void SetValue(string value)
    {
        if (_value == value) return;

        _value = value;
        _label.Text = DisplayText(value);
        _label.Foreground = LabelBrush(_definition, value);
    }

    private static string DisplayText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Embedded component" : value;
    }

    private static IBrush? LabelBrush(FieldDefinition definition, string value)
    {
        return !string.Equals(value, definition.DefaultValue, StringComparison.Ordinal)
            ? new SolidColorBrush(Color.Parse("#D6A638"))
            : null;
    }
}
