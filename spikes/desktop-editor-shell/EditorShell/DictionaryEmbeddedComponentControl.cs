using Avalonia.Controls;
using Avalonia.Layout;
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
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.82,
        };
        SetColumn(_label, 0);
        Children.Add(_label);

        var button = new Button
        {
            Content = "...",
            Width = 40,
            Height = 32,
            Padding = new Avalonia.Thickness(0),
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
    }

    private static string DisplayText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Embedded component" : value;
    }
}
