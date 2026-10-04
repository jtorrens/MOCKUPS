using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryComponentVariantOverridesControl : StackPanel,
    IDictionaryValueControl,
    IDictionaryOverrideStateControl
{
    private readonly FieldDefinition _definition;
    private readonly string _variantReference;
    private readonly DictionaryComponentVariantControl _variantControl;
    private readonly Func<FieldDefinition, string, JsonObject, Func<JsonObject, Task>, Task>?
        _openRuntimeComponentOverrides;
    private JsonObject _overrides;

    public DictionaryComponentVariantOverridesControl(
        FieldDefinition definition,
        string value,
        Func<ThemeComponentVariantSource, string>? getThemeComponentVariantReference,
        Func<string, Task>? openComponentVariantReference,
        Func<FieldDefinition, string, JsonObject, Func<JsonObject, Task>, Task>?
            openRuntimeComponentOverrides)
    {
        _definition = definition;
        _openRuntimeComponentOverrides = openRuntimeComponentOverrides;
        if (definition.ThemeComponentVariantSource
            == ThemeComponentVariantSource.None)
        {
            throw new InvalidOperationException(
                $"Dictionary field '{definition.Id}' is missing its Theme Component Variant source.");
        }
        _variantReference = getThemeComponentVariantReference?.Invoke(
                definition.ThemeComponentVariantSource)
            ?? throw new InvalidOperationException(
                $"Dictionary field '{definition.Id}' has no prepared Theme Component Variant source.");
        if (definition.Options is null
            || !definition.Options.Any((option) =>
                option.Value.Equals(
                    _variantReference,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Dictionary field '{definition.Id}' Theme Variant '{_variantReference}' is not a declared Component option.");
        }
        _overrides = Parse(value);

        Spacing = 6;
        MinWidth = 0;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        Func<string, Task>? openOverrides = _openRuntimeComponentOverrides is null
            ? null
            : async (_) => await OpenOverridesAsync();
        Func<string, Task>? restoreOverrides = _openRuntimeComponentOverrides is null
            ? null
            : (_) => RestoreOverrides();
        _variantControl = new DictionaryComponentVariantControl(
            definition with
            {
                ValueKind = ValueKind.ComponentVariant,
                DefaultValue = _variantReference,
            },
            _variantReference,
            isHighlighted: OverrideDocumentContract.HasAuthoredValues(_overrides),
            openComponentVariantReference,
            openEmbeddedComponent: openOverrides,
            restoreEmbeddedComponentOverrides: restoreOverrides,
            variantSelectionEditable: false);
        _variantControl.OverrideStateChanged += (_, _) =>
            OverrideStateChanged?.Invoke(this, EventArgs.Empty);
        Children.Add(_variantControl);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public bool HasOverrides => _variantControl.HasOverrides;

    public event EventHandler? OverrideStateChanged;

    public void SetValue(string value)
    {
        _overrides = Parse(value);
        RefreshOverrideButton();
    }

    internal async Task<bool> OpenOverridesAsync()
    {
        if (_openRuntimeComponentOverrides is null) return false;
        await _openRuntimeComponentOverrides(
            _definition,
            _variantReference,
            _overrides.DeepClone().AsObject(),
            (next) =>
            {
                _overrides = next.DeepClone().AsObject();
                Publish();
                return Task.CompletedTask;
            });
        return true;
    }

    private Task RestoreOverrides()
    {
        _overrides = new JsonObject();
        Publish();
        return Task.CompletedTask;
    }

    private void Publish()
    {
        RefreshOverrideButton();
        var serialized = _overrides.ToJsonString();
        ValueChanged?.Invoke(this, serialized);
        ValueCommitted?.Invoke(this, serialized);
    }

    private void RefreshOverrideButton() =>
        _variantControl.SetOverrideHighlighted(
            OverrideDocumentContract.HasAuthoredValues(_overrides));

    private JsonObject Parse(string value) =>
        JsonPath.ParseRequiredObject(
            value,
            $"Dictionary field '{_definition.Id}'");
}
