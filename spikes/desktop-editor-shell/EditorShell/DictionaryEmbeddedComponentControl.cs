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
    private readonly bool _isHighlighted;
    private string _value;

    public DictionaryEmbeddedComponentControl(
        FieldDefinition definition,
        string value,
        bool isHighlighted,
        Func<string, Task>? openEmbeddedComponent)
    {
        _definition = definition;
        _isHighlighted = isHighlighted;
        _value = value;
        ColumnDefinitions = new ColumnDefinitions("*,Auto");
        ColumnSpacing = 8;

        _label = new TextBlock
        {
            Text = DisplayText(definition, value),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.82,
        };
        ApplyLabelBrush();
        SetColumn(_label, 0);
        Children.Add(_label);

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
            Background = new SolidColorBrush(Color.Parse("#24D6A638")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D6A638")),
            Foreground = new SolidColorBrush(Color.Parse("#D6A638")),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEnabled = definition.IsEditable,
        };
        button.Click += async (_, _) =>
        {
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
        _label.Text = DisplayText(_definition, value);
        ApplyLabelBrush();
    }

    private static string DisplayText(FieldDefinition definition, string value)
    {
        if (definition.Options is not null)
        {
            foreach (var option in definition.Options)
            {
                if (option.Value.Equals(value, StringComparison.Ordinal))
                {
                    return option.Label;
                }
            }
        }

        return string.IsNullOrWhiteSpace(value) ? "Embedded component" : value;
    }

    private static IBrush LabelBrush()
    {
        return new SolidColorBrush(Color.Parse("#D6A638"));
    }

    private void ApplyLabelBrush()
    {
        if (_isHighlighted)
        {
            _label.Foreground = LabelBrush();
            return;
        }

        _label.ClearValue(TextBlock.ForegroundProperty);
    }
}
