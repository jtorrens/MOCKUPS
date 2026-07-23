using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryComponentVariantSlotControl : StackPanel, IDictionaryValueControl
{
    private readonly FieldDefinition _definition;
    private readonly DictionaryComponentVariantControl _variantControl;
    private readonly Func<string, JsonObject, Action<JsonObject>, Task>? _openRuntimeComponentOverrides;
    private JsonObject _slot;

    public DictionaryComponentVariantSlotControl(
        FieldDefinition definition,
        string value,
        Func<string, Task>? openComponentVariantReference,
        Func<string, JsonObject, Action<JsonObject>, Task>? openRuntimeComponentOverrides)
    {
        _definition = definition;
        _openRuntimeComponentOverrides = openRuntimeComponentOverrides;
        Spacing = 6;
        MinWidth = 0;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _slot = ComponentVariantSlotDocumentContract.Parse(
            value,
            $"Dictionary field '{definition.Id}'");
        var reference = ComponentVariantSlotDocumentContract.VariantReference(
            _slot,
            $"Dictionary field '{definition.Id}'");
        Func<string, Task>? openOverrides = _openRuntimeComponentOverrides is null
            ? null
            : async (_) => await OpenOverrides();
        _variantControl = new DictionaryComponentVariantControl(
            definition with
            {
                ValueKind = ValueKind.ComponentVariant,
                DefaultValue = reference,
            },
            reference,
            isHighlighted: ComponentVariantSlotDocumentContract.Overrides(
                _slot,
                $"Dictionary field '{definition.Id}'").Count > 0,
            openComponentVariantReference,
            openEmbeddedComponent: openOverrides);
        _variantControl.ValueChanged += (_, next) =>
        {
            SetReference(next);
            ValueChanged?.Invoke(this, Serialize());
        };
        _variantControl.ValueCommitted += (_, next) =>
        {
            SetReference(next);
            ValueCommitted?.Invoke(this, Serialize());
        };
        Children.Add(_variantControl);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        _slot = ComponentVariantSlotDocumentContract.Parse(
            value,
            $"Dictionary field '{_definition.Id}'");
        _variantControl.SetValue(ComponentVariantSlotDocumentContract.VariantReference(
            _slot,
            $"Dictionary field '{_definition.Id}'"));
        RefreshOverrideButton();
    }

    private void SetReference(string reference)
    {
        var owner = $"Dictionary field '{_definition.Id}'";
        var current = ComponentVariantSlotDocumentContract.VariantReference(_slot, owner);
        if (current.Equals(reference, StringComparison.Ordinal)) return;
        _slot = ComponentVariantSlotDocumentContract.Create(reference, new JsonObject(), owner);
        RefreshOverrideButton();
    }

    private void RefreshOverrideButton()
    {
        var overrides = ComponentVariantSlotDocumentContract.Overrides(
            _slot,
            $"Dictionary field '{_definition.Id}'");
        _variantControl.SetOverrideHighlighted(overrides.Count > 0);
    }

    private async Task OpenOverrides()
    {
        if (_openRuntimeComponentOverrides is null) return;
        var owner = $"Dictionary field '{_definition.Id}'";
        var currentReference = ComponentVariantSlotDocumentContract.VariantReference(_slot, owner);
        var currentOverrides = ComponentVariantSlotDocumentContract.Overrides(_slot, owner);
        await _openRuntimeComponentOverrides(
            currentReference,
            currentOverrides.DeepClone().AsObject(),
            (next) =>
            {
                _slot["overrides"] = next.DeepClone();
                ComponentVariantSlotDocumentContract.Validate(_slot, owner);
                RefreshOverrideButton();
                var serialized = Serialize();
                ValueChanged?.Invoke(this, serialized);
                ValueCommitted?.Invoke(this, serialized);
            });
    }

    private string Serialize()
    {
        ComponentVariantSlotDocumentContract.Validate(
            _slot,
            $"Dictionary field '{_definition.Id}'");
        return _slot.ToJsonString();
    }
}
