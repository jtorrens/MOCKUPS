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
        RuntimeCollectionDocumentContract.Validate(
            _items,
            $"Structured collection '{collection.Id}'");
        var items = _items.Select((node, index) => node as JsonObject
                ?? throw new InvalidOperationException(
                    $"Structured collection '{collection.Id}' item at index {index} must be an object."))
            .ToList();
        if (collection.ComponentItems is { } componentItemDefinition)
        {
            for (var index = 0; index < items.Count; index++)
            {
                RuntimeComponentCollectionItemDocumentContract.ValidateItem(
                    items[index],
                    componentItemDefinition.DocumentKeys,
                    $"Structured collection '{collection.Id}' item at index {index}");
            }
        }
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
                index,
                $"{collection.ItemLabel} {index + 1}",
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
                    var copy = source.DeepClone().AsObject();
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
                    var title = RuntimeCollectionItemPresentation.Resolve(
                        collection,
                        items[index],
                        index,
                        $"{collection.ItemLabel} {index + 1}",
                        $"Variant item {index + 1}",
                        EditorIcons.Component).Title;
                    var forwardedLabels = RuntimeInputForwardingContract.Labels(items[index]);
                    var confirmed = forwardedLabels.Count > 0
                        ? _services.ConfirmDiscardForwardedRuntimeInputs is null
                          || await _services.ConfirmDiscardForwardedRuntimeInputs(
                              $"Delete {title}",
                              forwardedLabels)
                        : _services.ConfirmStructuredCollectionItemDelete is null
                          || await _services.ConfirmStructuredCollectionItemDelete(title);
                    if (!confirmed) return;
                    _services.RemoveStructuredCollectionAnimationTargets?.Invoke(
                        StructuredCollectionItemIdentity.TargetIds(items[index]));
                    _items.RemoveAt(index);
                    Commit(runtimeContractChanged: true);
                }),
            _services.StructuredCollectionUiState ?? new EditorSessionUiState(),
            canEditStructure: _definition.IsEditable && collection.CanEditStructure);
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
            if (!input.ShowInEditor) continue;
            if (!CollectionFieldAvailability.IsEnabled(item, input, itemIndex)) continue;
            content.Children.Add(CreateItemField(collection, item, itemIndex, input));
        }

        var subcards = new List<EditorInternalNavigationSection>();
        if (collection.ComponentItems is { } componentItems)
        {
            var variantReference = RuntimeComponentCollectionItemDocumentContract.RequireVariantReference(
                item,
                componentItems.DocumentKeys,
                $"{collection.ItemLabel} '{ItemId(item, itemIndex)}'");
            var bindings = variantReference.Length == 0
                ? []
                : _services.GetComponentVariantRuntimeInputs?.Invoke(variantReference) ?? [];
            if (bindings.Count > 0)
            {
                var itemId = ItemId(item, itemIndex);
                var inputs = RuntimeComponentCollectionItemDocumentContract.RequireInputs(
                    item,
                    componentItems.DocumentKeys,
                    $"{collection.ItemLabel} '{itemId}'");
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
            && input.JsonKey.Equals(componentItems.VariantReferenceJsonKey, StringComparison.Ordinal);
        var options = input.ValueKind switch
        {
            ValueKind.ComponentVariant or ValueKind.ComponentVariantSlot
                when !string.IsNullOrWhiteSpace(input.ComponentType) =>
                ComponentVariantOptions(input),
            ValueKind.PaletteColorToken => _services.GetPaletteColorOptions?.Invoke() ?? [],
            _ => input.Options ?? [],
        };
        var definition = new FieldDefinition(
            $"{_definition.Id}.{ItemId(item, 0)}.{input.Id}",
            input.Label,
            input.ValueKind,
            IsEditable: _definition.IsEditable,
            DefaultValue: input.DefaultValue,
            Options: options,
            PairLabels: input.PairLabels,
            Number: input.ValueKind is ValueKind.Integer or ValueKind.Decimal or ValueKind.Alpha
                ? new NumberDefinition(input.Minimum, input.Maximum, input.Increment, input.ValueKind == ValueKind.Integer ? 0 : 2)
                : null,
            SelectComponentClass: input.ValueKind is ValueKind.ComponentVariant or ValueKind.ComponentVariantSlot
                && ComponentVariantOptionContract.SelectsComponentClass(input.ComponentType),
            StructuredCollection: input.StructuredCollection);
        var overrides = componentItems is null
            ? null
            : item[componentItems.OverridesJsonKey] as JsonObject;
        var services = selectsComponent && componentItems is not null
            ? _services with
            {
                OpenEmbeddedComponent = async (_) =>
                {
                    if (_services.OpenRuntimeComponentOverrides is null) return;
                    var reference = RuntimeComponentCollectionItemDocumentContract.RequireVariantReference(
                        item,
                        componentItems.DocumentKeys,
                        $"{collection.ItemLabel} '{ItemId(item, itemIndex)}'");
                    var currentOverrides = RuntimeComponentCollectionItemDocumentContract.RequireOverrides(
                        item,
                        componentItems.DocumentKeys,
                        $"{collection.ItemLabel} '{ItemId(item, itemIndex)}'");
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
                    : _services.GetComponentVariantRuntimeValues?.Invoke(next)
                      ?? throw new InvalidOperationException(
                          $"Component Variant '{next}' has no Runtime values provider.");
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
        return decorated;
    }

    private void SetComponentInputs(
        JsonObject item,
        RuntimeComponentCollectionItemDefinition componentItems,
        string value,
        bool commit)
    {
        item[componentItems.InputsJsonKey] = RuntimeInputValueKindContract.ParseValue(
            ValueKind.ComponentInputBindings,
            value,
            $"Structured collection component inputs '{componentItems.InputsJsonKey}'").AsObject();
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
        if (string.IsNullOrWhiteSpace(_definition.RuntimeCollectionComponentVariantFieldId)
            || _services.GetFieldValue is null
            || _services.GetComponentVariantRuntimeCollections is null)
        {
            return null;
        }
        var reference = _services.GetFieldValue(_definition.RuntimeCollectionComponentVariantFieldId);
        return string.IsNullOrWhiteSpace(reference)
            ? null
            : _services.GetComponentVariantRuntimeCollections(reference).FirstOrDefault();
    }

    private string DefaultValue(ComponentInputDefinition input)
    {
        if (input.ValueKind != ValueKind.ComponentVariant
            || input.AllowEmpty
            || !string.IsNullOrWhiteSpace(input.DefaultValue))
        {
            return input.DefaultValue;
        }
        if (ComponentVariantOptionContract.SelectsComponentClass(input.ComponentType))
        {
            return "";
        }
        var boundary = ComponentVariantOptionContract.RequireFixedBoundary(
            _services.GetComponentVariantOptions?.Invoke(input.ComponentType) ?? [],
            $"Structured collection field '{input.Id}'");
        return boundary.DefaultVariantReference;
    }

    private IReadOnlyList<FieldOption> ComponentVariantOptions(ComponentInputDefinition input)
    {
        var options = (_services.GetComponentVariantOptions?.Invoke(input.ComponentType) ?? []).ToList();
        if (input.AllowEmpty && options.All((option) => !string.IsNullOrWhiteSpace(option.Value)))
        {
            options.Insert(0, new FieldOption("", "None"));
        }
        return options;
    }

    private void InitializeComponentItem(RuntimeInputCollectionDefinition collection, JsonObject item)
    {
        if (collection.ComponentItems is not { } componentItems) return;
        var variantField = collection.Fields.Single((field) =>
            field.JsonKey.Equals(componentItems.VariantReferenceJsonKey, StringComparison.Ordinal));
        var reference = DesignPreviewTestValues.CollectionValue(item, variantField);
        item[componentItems.OverridesJsonKey] = new JsonObject();
        item[componentItems.InputsJsonKey] = reference.Length == 0
            ? new JsonObject()
            : _services.GetComponentVariantRuntimeValues?.Invoke(reference)
              ?? throw new InvalidOperationException(
                  $"Component Variant '{reference}' has no Runtime values provider.");
    }

    private static string ItemId(JsonObject item, int index) =>
        JsonPath.RequiredString(item, "id", $"Structured collection item at index {index}");

    private static string ComponentCategory(IReadOnlyList<FieldOption> options, string reference)
    {
        var group = options.FirstOrDefault((option) => option.Value.Equals(reference, StringComparison.Ordinal))?.GroupValue;
        if (!string.IsNullOrWhiteSpace(group)) return group;
        return VariantReferenceId.TryParse(reference, out var componentId, out _)
            ? componentId
            : reference;
    }

    private static JsonArray Parse(string value)
    {
        return RuntimeInputValueKindContract.ParseValue(
            ValueKind.StructuredCollection,
            value,
            "Structured collection value").AsArray();
    }
}
