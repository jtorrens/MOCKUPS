using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryEmbeddedComponentControl : Grid, IDictionaryValueControl,
    IDictionaryOverrideStateControl
{
    private readonly FieldDefinition _definition;
    private readonly TextBlock _label;
    private readonly Button? _restoreButton;
    private bool _isHighlighted;
    private string _value;

    public DictionaryEmbeddedComponentControl(
        FieldDefinition definition,
        string value,
        bool isHighlighted,
        Func<string, Task>? openEmbeddedComponent,
        Func<string, Task>? restoreEmbeddedComponentOverrides)
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
        if (restoreEmbeddedComponentOverrides is null)
        {
            throw new InvalidOperationException(
                $"Dictionary field '{definition.Id}' exposes Overrides without Restore.");
        }

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
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
        EditorOverrideVisuals.ApplyBoundaryActionButton(
            button,
            isHighlighted);
        button.Click += async (_, _) =>
        {
            await openEmbeddedComponent(_definition.Id);
        };
        actions.Children.Add(button);
        _restoreButton = new Button
        {
            Content = "↺",
            Width = 32,
            Height = 32,
            Padding = new Avalonia.Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEnabled = definition.IsEditable && isHighlighted,
            IsVisible = isHighlighted,
        };
        EditorAccessibility.Describe(
            _restoreButton,
            $"Restore all overrides for {definition.DisplayLabel}");
        ToolTip.SetTip(
            _restoreButton,
            $"Restore all overrides for {definition.DisplayLabel}");
        EditorOverrideVisuals.ApplyBoundaryActionButton(
            _restoreButton,
            isHighlighted);
        _restoreButton.Click += async (_, _) =>
        {
            await restoreEmbeddedComponentOverrides(_definition.Id);
            _isHighlighted = false;
            ApplyLabelBrush();
            EditorOverrideVisuals.ApplyBoundaryActionButton(
                button,
                false);
            EditorOverrideVisuals.ApplyBoundaryActionButton(
                _restoreButton,
                false);
            _restoreButton.IsEnabled = false;
            _restoreButton.IsVisible = false;
            OverrideStateChanged?.Invoke(this, EventArgs.Empty);
        };
        actions.Children.Add(_restoreButton);
        SetColumn(actions, 1);
        Children.Add(actions);
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

    public bool HasOverrides => _isHighlighted;

    public event EventHandler? OverrideStateChanged;

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
        return EditorOverrideVisuals.Brush;
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
