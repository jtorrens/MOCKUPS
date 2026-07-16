using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryStructuredCollectionControl : Border, IDictionaryValueControl, IDictionaryRuntimeContractValueControl
{
    private readonly FieldDefinition _definition;
    private readonly DictionaryFieldServices _services;
    private JsonArray _items;

    public DictionaryStructuredCollectionControl(
        FieldDefinition definition,
        string value,
        DictionaryFieldServices services)
    {
        _definition = definition;
        _services = services;
        _items = Parse(value);
        Rebuild();
    }

    public event EventHandler<string>? ValueChanged;
    public event EventHandler<string>? ValueCommitted;
    public event EventHandler? RuntimeContractChanged;

    public void SetValue(string value)
    {
        _items = Parse(value);
        Rebuild();
    }

    private void Rebuild()
    {
        var collection = CollectionDefinition();
        if (collection is null)
        {
            Child = new TextBlock
            {
                Text = "Select a component Variant with a collection contract.",
                Opacity = 0.68,
            };
            return;
        }
        var items = _items.OfType<JsonObject>().ToList();
        StructuredCollectionEditor? editor = null;
        void Commit(bool runtimeContractChanged = false)
        {
            var json = _items.ToJsonString();
            ValueChanged?.Invoke(this, json);
            ValueCommitted?.Invoke(this, json);
            Rebuild();
            if (runtimeContractChanged)
            {
                RuntimeContractChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        JsonObject NewItem()
        {
            var item = new JsonObject { ["id"] = $"{collection.Id}_{Guid.NewGuid():N}" };
            foreach (var field in collection.Fields)
            {
                item[field.JsonKey] = DesignPreviewTestValues.ValueNode(field, DefaultValue(field));
            }
            StructuredCollectionItemIdentity.RebaseNestedItems(item, collection);
            InitializeComponentItem(collection, item);
            return item;
        }
        editor = new StructuredCollectionEditor(
            StructuredCollectionEditingContext.VariantAuthoring,
            _definition.Id,
            collection.ItemLabel,
            items,
            ItemId,
            (item, index) => RuntimeCollectionItemPresentation.Resolve(
                collection,
                item,
                $"Variant item {index + 1}",
                EditorIcons.Component),
            (item, index) => ItemContent(collection, item, index),
            new StructuredCollectionActions(
                AddFirst: () =>
                {
                    var item = NewItem();
                    editor!.ActivateOnly(item, items.Count);
                    _items.Add(item);
                    Commit();
                },
                AddAfter: (index) =>
                {
                    var item = NewItem();
                    editor!.ActivateOnly(item, items.Count);
                    _items.Insert(index + 1, item);
                    Commit();
                },
                Duplicate: (index) =>
                {
                    var source = items[index];
                    var copy = source.DeepClone() as JsonObject ?? new JsonObject();
                    var oldId = ItemId(source, index);
                    var newId = $"{collection.Id}_{Guid.NewGuid():N}";
                    copy["id"] = newId;
                    RuntimeInputForwardingContract.RebaseIds(copy, oldId, newId);
                    var mappings = StructuredCollectionItemIdentity.RebaseNestedItems(copy, collection)
                        .ToDictionary((entry) => entry.Key, (entry) => entry.Value, StringComparer.Ordinal);
                    mappings[oldId] = newId;
                    _services.DuplicateStructuredCollectionAnimationTargets?.Invoke(mappings);
                    editor!.ActivateOnly(copy, items.Count);
                    _items.Insert(index + 1, copy);
                    Commit(runtimeContractChanged: true);
                },
                Move: (index, delta) =>
                {
                    var target = index + delta;
                    if (target < 0 || target >= _items.Count) return;
                    var item = _items[index];
                    _items.RemoveAt(index);
                    _items.Insert(target, item);
                    Commit();
                },
                Delete: async (index) =>
                {
                    var forwardedLabels = RuntimeInputForwardingContract.Labels(items[index]);
                    var confirmed = forwardedLabels.Count > 0
                        ? _services.ConfirmDiscardForwardedRuntimeInputs is null
                          || await _services.ConfirmDiscardForwardedRuntimeInputs(
                              $"Delete {collection.ItemLabel} {index + 1}",
                              forwardedLabels)
                        : _services.ConfirmStructuredCollectionItemDelete is null
                          || await _services.ConfirmStructuredCollectionItemDelete($"{collection.ItemLabel} {index + 1}");
                    if (!confirmed) return;
                    _services.RemoveStructuredCollectionAnimationTargets?.Invoke(
                        StructuredCollectionItemIdentity.TargetIds(items[index]));
                    _items.RemoveAt(index);
                    Commit(runtimeContractChanged: true);
                }),
            _services.StructuredCollectionUiState ?? new EditorSessionUiState());
        Child = editor.Create();
    }

    private StructuredCollectionItemContent ItemContent(
        RuntimeInputCollectionDefinition collection,
        JsonObject item,
        int itemIndex)
    {
        var content = new StackPanel { Spacing = 8 };
        foreach (var input in collection.Fields)
        {
            if (!CollectionFieldAvailability.IsEnabled(item, input, itemIndex)) continue;
            content.Children.Add(CreateItemField(collection, item, itemIndex, input));
        }

        var subcards = new List<EditorInternalNavigationSection>();
        if (collection.ComponentItems is { } componentItems)
        {
            var presetReference = JsonText(item[componentItems.PresetJsonKey]);
            var bindings = string.IsNullOrWhiteSpace(presetReference)
                ? []
                : _services.GetComponentPresetRuntimeInputs?.Invoke(presetReference) ?? [];
            if (bindings.Count > 0)
            {
                var itemId = ItemId(item, itemIndex);
                var inputs = item[componentItems.InputsJsonKey] as JsonObject ?? new JsonObject();
                item[componentItems.InputsJsonKey] = inputs;
                var field = new DictionaryFieldControl(
                    new FieldValue(
                        new FieldDefinition(
                            $"{_definition.Id}.{itemId}.inputs",
                            "Component inputs",
                            ValueKind.ComponentInputBindings,
                            ComponentInputBindings: bindings),
                        inputs.ToJsonString()),
                    _services,
                    valueOnly: true);
                field.ValueChanged += (_, next) => SetComponentInputs(item, componentItems, next, commit: false);
                field.ValueCommitted += (_, next) => SetComponentInputs(item, componentItems, next, commit: true);
                field.RuntimeContractChanged += (_, _) => RuntimeContractChanged?.Invoke(this, EventArgs.Empty);
                content.Children.Add(field);
            }
        }
        return new StructuredCollectionItemContent(content, subcards);
    }

    private Control CreateItemField(
        RuntimeInputCollectionDefinition collection,
        JsonObject item,
        int itemIndex,
        ComponentInputDefinition input)
    {
        var componentItems = collection.ComponentItems;
        var selectsComponent = componentItems is not null
            && input.JsonKey.Equals(componentItems.PresetJsonKey, StringComparison.Ordinal);
        var options = input.ValueKind switch
        {
            ValueKind.ComponentPreset when !string.IsNullOrWhiteSpace(input.ComponentType) =>
                ComponentPresetOptions(input),
            ValueKind.PaletteColorToken => _services.GetPaletteColorOptions?.Invoke() ?? [],
            _ => input.Options ?? [],
        };
        var definition = new FieldDefinition(
            $"{_definition.Id}.{ItemId(item, 0)}.{input.Id}",
            input.Label,
            input.ValueKind,
            DefaultValue: input.DefaultValue,
            Options: options,
            PairLabels: input.PairLabels,
            Number: input.ValueKind is ValueKind.Integer or ValueKind.Decimal or ValueKind.Alpha
                ? new NumberDefinition(input.Minimum, input.Maximum, input.Increment, input.ValueKind == ValueKind.Integer ? 0 : 2)
                : null,
            SelectComponentClass: input.ValueKind == ValueKind.ComponentPreset
                && input.ComponentType.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains("*", StringComparer.Ordinal),
            StructuredCollection: input.StructuredCollection);
        var overrides = componentItems is null
            ? null
            : item[componentItems.OverridesJsonKey] as JsonObject;
        var services = selectsComponent && componentItems is not null
            ? _services with
            {
                OpenEmbeddedComponent = async (_) =>
                {
                    var reference = JsonText(item[componentItems.PresetJsonKey]);
                    if (string.IsNullOrWhiteSpace(reference) || _services.OpenRuntimeComponentOverrides is null) return;
                    var currentOverrides = item[componentItems.OverridesJsonKey] as JsonObject ?? new JsonObject();
                    item[componentItems.OverridesJsonKey] = currentOverrides;
                    await _services.OpenRuntimeComponentOverrides(reference, currentOverrides, (next) =>
                    {
                        item[componentItems.OverridesJsonKey] = next.DeepClone();
                        Publish(commit: true);
                    });
                },
            }
            : _services;
        var control = new DictionaryFieldControl(
            new FieldValue(
                definition,
                DesignPreviewTestValues.CollectionValue(item, input),
                IsHighlighted: selectsComponent && overrides is { Count: > 0 }),
            services);
        control.ValueCommitted += async (_, next) =>
        {
            var previous = DesignPreviewTestValues.CollectionValue(item, input);
            var componentChanged = selectsComponent
                && !ComponentCategory(options, previous).Equals(ComponentCategory(options, next), StringComparison.Ordinal);
            if (componentChanged)
            {
                var forwardedLabels = RuntimeInputForwardingContract.Labels(item);
                var confirmed = forwardedLabels.Count == 0
                    || _services.ConfirmDiscardForwardedRuntimeInputs is null
                    || await _services.ConfirmDiscardForwardedRuntimeInputs(
                        $"Change {collection.ItemLabel} component",
                        forwardedLabels);
                if (!confirmed)
                {
                    control.SetValue(previous);
                    return;
                }
            }
            item[input.JsonKey] = DesignPreviewTestValues.ValueNode(input, next);
            if (componentChanged && componentItems is not null)
            {
                item[componentItems.OverridesJsonKey] = new JsonObject();
                item[componentItems.InputsJsonKey] = string.IsNullOrWhiteSpace(next)
                    ? new JsonObject()
                    : _services.GetComponentPresetRuntimeValues?.Invoke(next) ?? new JsonObject();
            }
            Publish(commit: true);
            if (collection.Fields.Any((candidate) =>
                    candidate.EnabledWhenItemJsonKey.Equals(input.JsonKey, StringComparison.Ordinal)))
            {
                Rebuild();
            }
            if (componentChanged)
            {
                RuntimeContractChanged?.Invoke(this, EventArgs.Empty);
            }
        };
        Control decorated = _services.DecorateStructuredCollectionField?.Invoke(
            input,
            ItemId(item, itemIndex),
            control) ?? control;
        return EditorSimplifiedPromotionControl.Wrap(
            decorated,
            _services.SimplifiedProjection,
            EditorSimplifiedFieldReference.Collection(
                _definition.Id,
                ItemId(item, itemIndex),
                input.Id,
                [
                    new EditorSimplifiedGroupIdentity(
                        collection.Id,
                        collection.Label,
                        EditorIcons.Component),
                    new EditorSimplifiedGroupIdentity(
                        ItemId(item, itemIndex),
                        $"{collection.ItemLabel} {itemIndex + 1}",
                        EditorIcons.Component),
                ],
                _services.SimplifiedSlotFieldIds));
    }

    private void SetComponentInputs(
        JsonObject item,
        RuntimeComponentCollectionItemDefinition componentItems,
        string value,
        bool commit)
    {
        item[componentItems.InputsJsonKey] = JsonNode.Parse(value) as JsonObject ?? new JsonObject();
        Publish(commit);
    }

    private void Publish(bool commit)
    {
        var json = _items.ToJsonString();
        ValueChanged?.Invoke(this, json);
        if (commit) ValueCommitted?.Invoke(this, json);
    }

    private RuntimeInputCollectionDefinition? CollectionDefinition()
    {
        if (_definition.StructuredCollection is not null)
        {
            return _definition.StructuredCollection;
        }
        if (string.IsNullOrWhiteSpace(_definition.RuntimeCollectionComponentPresetFieldId)
            || _services.GetFieldValue is null
            || _services.GetComponentPresetRuntimeCollections is null)
        {
            return null;
        }
        var reference = _services.GetFieldValue(_definition.RuntimeCollectionComponentPresetFieldId);
        return string.IsNullOrWhiteSpace(reference)
            ? null
            : _services.GetComponentPresetRuntimeCollections(reference).FirstOrDefault();
    }

    private string DefaultValue(ComponentInputDefinition input)
    {
        if (input.ValueKind != ValueKind.ComponentPreset
            || input.AllowEmptyComponentPreset
            || !string.IsNullOrWhiteSpace(input.DefaultValue))
        {
            return input.DefaultValue;
        }
        return _services.GetComponentPresetOptions?.Invoke(input.ComponentType)
            .FirstOrDefault((option) => !string.IsNullOrWhiteSpace(option.Value))?.Value ?? "";
    }

    private IReadOnlyList<FieldOption> ComponentPresetOptions(ComponentInputDefinition input)
    {
        var options = (_services.GetComponentPresetOptions?.Invoke(input.ComponentType) ?? []).ToList();
        if (input.AllowEmptyComponentPreset && options.All((option) => !string.IsNullOrWhiteSpace(option.Value)))
        {
            options.Insert(0, new FieldOption("", "None"));
        }
        return options;
    }

    private void InitializeComponentItem(RuntimeInputCollectionDefinition collection, JsonObject item)
    {
        if (collection.ComponentItems is not { } componentItems) return;
        var reference = JsonText(item[componentItems.PresetJsonKey]);
        item[componentItems.OverridesJsonKey] = new JsonObject();
        item[componentItems.InputsJsonKey] = string.IsNullOrWhiteSpace(reference)
            ? new JsonObject()
            : _services.GetComponentPresetRuntimeValues?.Invoke(reference) ?? new JsonObject();
    }

    private static string ItemId(JsonObject item, int index) =>
        item["id"] is JsonValue value && value.TryGetValue<string>(out var id) && !string.IsNullOrWhiteSpace(id)
            ? id
            : $"item-{index}";

    private static string JsonText(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static string ComponentCategory(IReadOnlyList<FieldOption> options, string reference)
    {
        var group = options.FirstOrDefault((option) => option.Value.Equals(reference, StringComparison.Ordinal))?.GroupValue;
        if (!string.IsNullOrWhiteSpace(group)) return group;
        var separator = reference.IndexOf("::preset::", StringComparison.Ordinal);
        return separator > 0 ? reference[..separator] : reference;
    }

    private static JsonArray Parse(string value)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value) as JsonArray ?? new JsonArray();
        }
        catch
        {
            return new JsonArray();
        }
    }
}
