using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryRecordReferenceControl : DockPanel,
    IDictionaryValueControl
{
    private readonly FieldDefinition _definition;
    private readonly DictionaryOptionTokenControl _selector;
    private readonly Button _overridesButton;
    private readonly Func<FieldDefinition, string, Task>?
        _openOverrides;
    private string _value;

    public DictionaryRecordReferenceControl(
        FieldDefinition definition,
        string value,
        bool isHighlighted,
        Func<FieldDefinition, string, Task>? openOverrides)
    {
        _definition = definition;
        _value = value;
        _openOverrides = openOverrides;
        LastChildFill = true;
        MinWidth = 0;
        ClipToBounds = true;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _overridesButton = new Button
        {
            Content = EditorIcons.CreateSemantic(
                "Edit overrides",
                EditorIcons.Edit,
                15),
            Width = 40,
            Height = 32,
            MinWidth = 0,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEnabled = definition.IsEditable
                && openOverrides is not null
                && !string.IsNullOrWhiteSpace(value),
        };
        EditorAccessibility.Describe(
            _overridesButton,
            $"Edit overrides for {definition.DisplayLabel}");
        EditorOverrideVisuals.ApplyActionButton(
            _overridesButton,
            isHighlighted);
        _overridesButton.Click += async (_, _) =>
        {
            if (_openOverrides is not null
                && !string.IsNullOrWhiteSpace(_value))
            {
                await _openOverrides(
                    _definition,
                    _value);
            }
        };
        SetDock(_overridesButton, Dock.Right);
        Children.Add(_overridesButton);

        _selector = new DictionaryOptionTokenControl(
            definition,
            value);
        _selector.ValueChanged += (_, next) =>
        {
            _value = next;
            UpdateButton();
            ValueChanged?.Invoke(this, next);
        };
        _selector.ValueCommitted += (_, next) =>
        {
            _value = next;
            UpdateButton();
            ValueCommitted?.Invoke(this, next);
        };
        Children.Add(_selector);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        _value = value;
        _selector.SetValue(value);
        UpdateButton();
    }

    private void UpdateButton()
    {
        _overridesButton.IsEnabled = _definition.IsEditable
            && _openOverrides is not null
            && !string.IsNullOrWhiteSpace(_value);
    }
}
