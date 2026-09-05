using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryStructuredCollectionControl : Border, IDictionaryValueControl,
    IDictionaryRuntimeContractValueControl, IDictionaryOverrideStateControl,
    IEditorAuthoringItemTarget
{
    private readonly FieldDefinition _definition;
    private readonly DictionaryFieldServices _services;
    private readonly EditorSessionUiState _uiState;
    private readonly List<DictionaryFieldControl> _overrideControls = [];
    private JsonArray _items;
    private bool _lastPublishedOverrideState;

    public DictionaryStructuredCollectionControl(
        FieldDefinition definition,
        string value,
        DictionaryFieldServices services)
    {
        _definition = definition;
        _services = services;
        _uiState = services.StructuredCollectionUiState ?? new EditorSessionUiState();
        _items = Parse(value);
        Rebuild();
    }

    public event EventHandler<string>? ValueChanged;
    public event EventHandler<string>? ValueCommitted;
    public event EventHandler? RuntimeContractChanged;
    public event EventHandler? OverrideStateChanged;
    public string FieldId => _definition.Id;
    public bool HasOverrides => _overrideControls.Any((control) => control.HasOverrides);

    public bool SelectItem(string itemId)
    {
        var items = _items.OfType<JsonObject>().ToList();
        var selected = items.FirstOrDefault((item) =>
            (item["id"]?.GetValue<string>() ?? "").Equals(itemId, StringComparison.Ordinal));
        if (selected is null) return false;
        var activeKey = $"{_definition.Id}:{itemId}:expanded";
        _uiState.SetOnlyExpanded(
            items.Select((item, index) =>
                $"{_definition.Id}:{ItemId(item, index)}:expanded"),
            activeKey);
        _uiState.RequestReveal(activeKey);
        Rebuild();
        return true;
    }

    public void SetValue(string value)
    {
        _items = Parse(value);
        Rebuild();
    }

    private void Rebuild()
    {
        _overrideControls.Clear();
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
        StructuredCollectionDocumentContract.ValidateEffective(
            _items,
            collection,
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
            var stored = collection.StructureOwnedFieldJsonKeys
                    is { Count: > 0 }
                ? StructuredCollectionDocumentContract.StoredClone(
                    _items,
                    collection,
                    $"Structured collection '{collection.Id}'")
                : _items.DeepClone().AsArray();
            var json = stored.ToJsonString();
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
            return StructuredCollectionItemFactory.Create(
                collection,
                (field) => DefaultValue(collection, field),
                (reference) => _services.GetComponentVariantRuntimeValues?.Invoke(reference)
                    ?? throw new InvalidOperationException(
                        $"Component Variant '{reference}' has no Runtime values provider."));
        }
        var address = new StructuredCollectionAddress(
            collection.JsonKey,
            [],
            collection.JsonKey);
        async Task<StructuredCollectionMutationResult> Mutate(
            StructuredCollectionMutation mutation)
        {
            if (_services.MutateStructuredCollection is { } persistedMutation)
            {
                return await persistedMutation(mutation);
            }
            var content = new JsonObject
            {
                [collection.JsonKey] = _items.DeepClone(),
            };
            return StructuredCollectionMutationEngine.Apply(
                content,
                new JsonObject
                {
                    ["schemaVersion"] = 2,
                    ["tracks"] = new JsonArray(),
                },
                collection,
                mutation);
        }
        void ApplyMutationResult(
            StructuredCollectionMutationResult result,
            bool runtimeContractChanged)
        {
            _items = result.Collection.DeepClone().AsArray();
            if (_services.MutateStructuredCollection is null)
            {
                Commit(runtimeContractChanged);
                return;
            }
            ValueChanged?.Invoke(this, _items.ToJsonString());
            Rebuild();
            if (runtimeContractChanged)
            {
                RuntimeContractChanged?.Invoke(this, EventArgs.Empty);
            }
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
                AddFirst: async () =>
                {
                    var result = await Mutate(new AddStructuredCollectionItem(
                        address,
                        NewItem(),
                        items.Count == 0 ? null : ItemId(items[0], 0)));
                    editor!.ActivateOnly(
                        result.Item ?? throw new InvalidOperationException(
                            "Add structured collection mutation returned no item."),
                        result.Collection.Count);
                    ApplyMutationResult(result, runtimeContractChanged: true);
                },
                AddAfter: async (index) =>
                {
                    var beforeItemId = index + 1 < items.Count
                        ? ItemId(items[index + 1], index + 1)
                        : null;
                    var result = await Mutate(new AddStructuredCollectionItem(
                        address,
                        NewItem(),
                        beforeItemId));
                    editor!.ActivateOnly(
                        result.Item ?? throw new InvalidOperationException(
                            "Add structured collection mutation returned no item."),
                        result.Collection.Count);
                    ApplyMutationResult(result, runtimeContractChanged: true);
                },
                Duplicate: async (index) =>
                {
                    var source = items[index];
                    var result = await Mutate(new DuplicateStructuredCollectionItem(
                        address,
                        ItemId(source, index),
                        index + 1 < items.Count
                            ? ItemId(items[index + 1], index + 1)
                            : null));
                    editor!.ActivateOnly(
                        result.Item
                        ?? throw new InvalidOperationException(
                            "Duplicate structured collection mutation returned no item."),
                        result.Collection.Count);
                    ApplyMutationResult(
                        result,
                        runtimeContractChanged: true);
                },
                Move: async (index, delta) =>
                {
                    var target = index + delta;
                    if (target < 0 || target >= _items.Count) return;
                    var beforeItemId = delta < 0
                        ? ItemId(items[target], target)
                        : target + 1 < items.Count
                            ? ItemId(items[target + 1], target + 1)
                            : null;
                    var result = await Mutate(new MoveStructuredCollectionItem(
                        address,
                        ItemId(items[index], index),
                        beforeItemId));
                    ApplyMutationResult(result, runtimeContractChanged: false);
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
                    var result = await Mutate(new DeleteStructuredCollectionItem(
                        address,
                        ItemId(items[index], index)));
                    ApplyMutationResult(
                        result,
                        runtimeContractChanged: true);
                }),
            _uiState,
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
                RegisterOverrideControl(field);
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
        async Task PublishItemValuesAsync(
            IReadOnlyDictionary<string, JsonNode?> values)
        {
            if (_services.UpdateStructuredCollectionValues is { } update)
            {
                await update(
                    StructuredCollectionAddress.Root(collection.JsonKey),
                    ItemId(item, itemIndex),
                    values);
                return;
            }
            Publish(commit: true);
        }

        var componentItems = collection.ComponentItems;
        var fixedBoundary = collection.FixedComponentBoundary;
        var selectsRuntimeComponent = componentItems is not null
            && input.JsonKey.Equals(componentItems.VariantReferenceJsonKey, StringComparison.Ordinal);
        var selectsFixedComponent = fixedBoundary is not null
            && input.JsonKey.Equals(fixedBoundary.VariantReferenceJsonKey, StringComparison.Ordinal);
        var selectsComponent = selectsRuntimeComponent || selectsFixedComponent;
        var options = input.ValueKind switch
        {
            ValueKind.RecordReference =>
                DictionaryRecordReferenceOptions.Resolve(
                    _services,
                    input.TableId,
                    input.AllowEmpty,
                    $"Structured collection record reference '{input.Id}'"),
            ValueKind.ComponentVariant or ValueKind.ComponentVariantSlot
                when !string.IsNullOrWhiteSpace(input.ComponentType) =>
                ComponentVariantOptions(input, fixedBoundary),
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
            RecordReference: input.ValueKind == ValueKind.RecordReference
                ? new RecordReferenceDefinition(
                    input.TableId,
                    AllowEmpty: input.AllowEmpty)
                : null,
            SelectComponentClass: input.ValueKind is ValueKind.ComponentVariant or ValueKind.ComponentVariantSlot
                && ComponentVariantOptionContract.SelectsComponentClass(input.ComponentType),
            StructuredCollection: input.StructuredCollection,
            Unit: input.Unit,
            Animation: input.Animation,
            BehaviorTiming: input.BehaviorTiming);
        var overridesKey = selectsRuntimeComponent
            ? componentItems!.OverridesJsonKey
            : selectsFixedComponent
                ? fixedBoundary!.OverridesJsonKey
                : "";
        var overrides = overridesKey.Length == 0
            ? null
            : item[overridesKey] as JsonObject;
        var services = selectsComponent
            ? _services with
            {
                OpenEmbeddedComponent = async (_) =>
                {
                    if (_services.OpenRuntimeComponentOverrides is null) return;
                    var reference = JsonPath.RequiredString(
                        item,
                        input.JsonKey,
                        $"{collection.ItemLabel} '{ItemId(item, itemIndex)}'");
                    var currentOverrides = JsonPath.RequiredObject(
                        item,
                        overridesKey,
                        $"{collection.ItemLabel} '{ItemId(item, itemIndex)}'");
                    await _services.OpenRuntimeComponentOverrides(reference, currentOverrides, (next) =>
                    {
                        item[overridesKey] = next.DeepClone();
                        return PublishItemValuesAsync(
                            new Dictionary<string, JsonNode?>
                            {
                                [overridesKey] = next,
                            });
                    });
                },
                RestoreEmbeddedComponentOverrides = async (_) =>
                {
                    item[overridesKey] = new JsonObject();
                    await PublishItemValuesAsync(
                        new Dictionary<string, JsonNode?>
                        {
                            [overridesKey] = item[overridesKey],
                        });
                },
            }
            : _services;
        if (input.StructuredCollection is not null)
        {
            var parentMutation = services.MutateStructuredCollection;
            var parentUpdate = services.UpdateStructuredCollectionValues;
            services = services with
            {
                MutateStructuredCollection = parentMutation is null
                    ? null
                    : (mutation) =>
                    {
                        var address = mutation.Address with
                        {
                            RootStorageJsonKey = collection.JsonKey,
                            Owners =
                            [
                                new StructuredCollectionOwnerSegment(
                                    collection.JsonKey,
                                    ItemId(item, itemIndex)),
                                .. mutation.Address.Owners,
                            ],
                        };
                        return parentMutation(
                            StructuredCollectionMutationEngine.WithAddress(
                                mutation,
                                address));
                    },
                UpdateStructuredCollectionValues = parentUpdate is null
                    ? null
                    : (nestedAddress, nestedItemId, values) =>
                    {
                        var address = nestedAddress with
                        {
                            RootStorageJsonKey = collection.JsonKey,
                            Owners =
                            [
                                new StructuredCollectionOwnerSegment(
                                    collection.JsonKey,
                                    ItemId(item, itemIndex)),
                                .. nestedAddress.Owners,
                            ],
                        };
                        return parentUpdate(address, nestedItemId, values);
                    },
            };
        }
        var currentValue = DesignPreviewTestValues.CollectionValue(item, input);
        var control = new DictionaryFieldControl(
            new FieldValue(
                definition,
                currentValue,
                IsHighlighted: selectsComponent
                    && overrides is not null
                    && OverrideDocumentContract.HasAuthoredValues(overrides)),
            services);
        RegisterOverrideControl(control);
        control.ValueCommitted += async (_, next) =>
        {
            var previous = DesignPreviewTestValues.CollectionValue(item, input);
            var nextReference = next;
            var componentChanged = selectsComponent
                && !ComponentCategory(options, previous).Equals(
                    ComponentCategory(options, nextReference),
                    StringComparison.Ordinal);
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
            item[input.JsonKey] = DesignPreviewTestValues.ValueNode(input, nextReference);
            var updates = new Dictionary<string, JsonNode?>
            {
                [input.JsonKey] = item[input.JsonKey],
            };
            if (componentChanged && componentItems is not null)
            {
                item[componentItems.OverridesJsonKey] = new JsonObject();
                item[componentItems.InputsJsonKey] = string.IsNullOrWhiteSpace(next)
                    ? new JsonObject()
                    : _services.GetComponentVariantRuntimeValues?.Invoke(next)
                      ?? throw new InvalidOperationException(
                          $"Component Variant '{next}' has no Runtime values provider.");
                updates[componentItems.OverridesJsonKey] = item[componentItems.OverridesJsonKey];
                updates[componentItems.InputsJsonKey] = item[componentItems.InputsJsonKey];
            }
            await PublishItemValuesAsync(updates);
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

    private void RegisterOverrideControl(DictionaryFieldControl control)
    {
        _overrideControls.Add(control);
        control.OverrideStateChanged += (_, _) => PublishOverrideState();
        PublishOverrideState();
    }

    private void PublishOverrideState()
    {
        var hasOverrides = HasOverrides;
        if (_lastPublishedOverrideState == hasOverrides) return;
        _lastPublishedOverrideState = hasOverrides;
        OverrideStateChanged?.Invoke(this, EventArgs.Empty);
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
        var value = _services.GetFieldValue(
            _definition.RuntimeCollectionComponentVariantFieldId);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : _services.GetComponentVariantRuntimeCollections(
                DictionaryComponentVariantReference.Read(
                    value,
                    _definition
                        .RuntimeCollectionComponentVariantFieldId))
                .FirstOrDefault();
    }

    private string DefaultValue(
        RuntimeInputCollectionDefinition collection,
        ComponentInputDefinition input)
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
            ComponentVariantOptions(input, collection.FixedComponentBoundary),
            $"Structured collection field '{input.Id}'");
        return boundary.DefaultVariantReference;
    }

    private IReadOnlyList<FieldOption> ComponentVariantOptions(
        ComponentInputDefinition input,
        RuntimeFixedComponentBoundaryDefinition? fixedBoundary = null)
    {
        var options = (_services.GetComponentVariantOptions?.Invoke(input.ComponentType) ?? [])
            .Where((option) => fixedBoundary is null
                || option.GroupValue.Equals(
                    fixedBoundary.ComponentClassId,
                    StringComparison.Ordinal))
            .ToList();
        if (input.AllowEmpty && options.All((option) => !string.IsNullOrWhiteSpace(option.Value)))
        {
            options.Insert(0, new FieldOption("", "None"));
        }
        return options;
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
