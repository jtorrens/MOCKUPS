using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryRecordReferenceControl : DockPanel,
    IDictionaryValueControl, IDictionaryOverrideStateControl
{
    private readonly FieldDefinition _definition;
    private readonly DictionaryOptionTokenControl _selector;
    private readonly Button _overridesButton;
    private readonly Button _restoreButton;
    private readonly Func<FieldDefinition, string, Task>?
        _openOverrides;
    private readonly Func<FieldDefinition, string, Task>?
        _restoreOverrides;
    private bool _hasOverrides;
    private string _value;

    public DictionaryRecordReferenceControl(
        FieldDefinition definition,
        string value,
        bool isHighlighted,
        Func<FieldDefinition, string, Task>? openOverrides,
        Func<FieldDefinition, string, Task>? restoreOverrides)
    {
        _definition = definition;
        _value = value;
        _openOverrides = openOverrides;
        _restoreOverrides = restoreOverrides;
        _hasOverrides = isHighlighted;
        if (openOverrides is not null && restoreOverrides is null)
        {
            throw new InvalidOperationException(
                $"Record reference '{definition.Id}' exposes Overrides without Restore.");
        }
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
            IsEnabled = openOverrides is not null
                && !string.IsNullOrWhiteSpace(value),
        };
        EditorAccessibility.Describe(
            _overridesButton,
            $"Edit overrides for {definition.DisplayLabel}");
        EditorOverrideVisuals.ApplyBoundaryActionButton(
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

        _restoreButton = new Button
        {
            Content = "↺",
            Width = 32,
            Height = 32,
            MinWidth = 0,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
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
            if (_restoreOverrides is not null
                && !string.IsNullOrWhiteSpace(_value))
            {
                await _restoreOverrides(_definition, _value);
                SetOverrideState(false);
            }
        };
        SetDock(_restoreButton, Dock.Right);
        Children.Insert(0, _restoreButton);
        SetOverrideState(isHighlighted);

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

    public bool HasOverrides => _hasOverrides;

    public event EventHandler? OverrideStateChanged;

    public void SetValue(string value)
    {
        _value = value;
        _selector.SetValue(value);
        UpdateButton();
    }

    private void UpdateButton()
    {
        _overridesButton.IsEnabled = _openOverrides is not null
            && !string.IsNullOrWhiteSpace(_value);
        _restoreButton.IsEnabled = _restoreOverrides is not null
            && _hasOverrides
            && !string.IsNullOrWhiteSpace(_value);
    }

    private void SetOverrideState(bool active)
    {
        var changed = _hasOverrides != active;
        _hasOverrides = active;
        EditorOverrideVisuals.ApplyBoundaryActionButton(
            _overridesButton,
            active);
        EditorOverrideVisuals.ApplyBoundaryActionButton(
            _restoreButton,
            active);
        _restoreButton.IsVisible = active;
        _restoreButton.IsEnabled = _restoreOverrides is not null
            && active
            && !string.IsNullOrWhiteSpace(_value);
        if (changed)
        {
            OverrideStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
