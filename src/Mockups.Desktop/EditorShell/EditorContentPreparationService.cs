using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorPreparedLayoutCard(
    EditorLayoutCard Layout,
    IReadOnlyDictionary<string, FieldValue> Fields);

internal sealed record EditorPreparedRootContent(
    IReadOnlyList<EditorPreparedLayoutCard> Cards,
    EditorDictionaryContextSnapshot DictionaryContext,
    EditorPreparedHeader Header);

internal sealed record EditorPreparedOverrideGroup(
    string Id,
    string PathLabel,
    EditorEmbeddedContext Context,
    IReadOnlyDictionary<string, FieldValue> Fields,
    IReadOnlyList<string> OverrideFieldIds);

internal sealed record EditorPreparedOverrideProjection(
    IReadOnlyList<EditorPreparedOverrideGroup> Groups,
    IReadOnlyDictionary<string, FieldValue> RootFields,
    EditorDictionaryContextSnapshot DictionaryContext)
{
    public int Count => Groups.Sum((group) =>
        group.OverrideFieldIds.Count);
}

internal sealed record EditorPreparedEmbeddedContent(
    EditorPreparedLayoutCard? OwnerCard,
    IReadOnlyList<EditorPreparedLayoutCard> Cards,
    EditorDictionaryContextSnapshot DictionaryContext,
    EditorPreparedHeader Header);

internal sealed class EditorContentPreparationService : IDisposable
{
    private readonly IEditorLayoutStore _layouts;
    private readonly EditorFieldValueRouter _fieldValues;
    private readonly ComponentClassFieldValueService _componentFields;
    private readonly EditorDictionaryFieldServices _dictionaryFields;
    private readonly EditorHeaderPreparationService _header;
    private readonly EditorOperationCoordinator _operations;
    private CancellationTokenSource? _activePreparation;
    private bool _disposed;

    public EditorContentPreparationService(
        IEditorLayoutStore layouts,
        EditorFieldValueRouter fieldValues,
        ComponentClassFieldValueService componentFields,
        EditorDictionaryFieldServices dictionaryFields,
        EditorHeaderPreparationService header,
        EditorOperationCoordinator operations)
    {
        _layouts = layouts;
        _fieldValues = fieldValues;
        _componentFields = componentFields;
        _dictionaryFields = dictionaryFields;
        _header = header;
        _operations = operations;
    }

    public Task<EditorPreparedRootContent> PrepareRootAsync(
        ProjectTreeNode layoutNode,
        ProjectTreeNode dataNode)
    {
        var selectedThemeId =
            _dictionaryFields.CaptureSelectedThemeId();
        var cancellationToken = BeginPreparation();
        return _operations.ExecuteAsync(
            () =>
            {
                var layout = _layouts.LoadEditorLayout(
                    layoutNode.RecordClassId);
                var allFields = PrepareDirectFields(
                    dataNode,
                    layout.Cards.SelectMany(AllFieldIds),
                    cancellationToken);
                var cards = VisibleCards(layout)
                    .Select((card) => new EditorPreparedLayoutCard(
                        card,
                        allFields))
                    .ToList();
                return new EditorPreparedRootContent(
                    cards,
                    _dictionaryFields.PrepareContext(
                        dataNode,
                        selectedThemeId,
                        allFields,
                        cancellationToken),
                    _header.PrepareRoot(
                        dataNode,
                        cancellationToken));
            },
            cancellationToken);
    }

    public Task<EditorPreparedOverrideProjection> PrepareOverridesAsync(
        ProjectTreeNode layoutNode,
        ProjectTreeNode dataNode)
    {
        if (dataNode.Kind is not (
                ProjectTreeNodeKind.ComponentVariant
                or ProjectTreeNodeKind.ModuleVariant))
        {
            throw new InvalidOperationException(
                $"Flat Overrides are not available for '{dataNode.Kind}'.");
        }
        var selectedThemeId =
            _dictionaryFields.CaptureSelectedThemeId();
        var cancellationToken = BeginPreparation();
        return _operations.ExecuteAsync(
            () =>
            {
                var layout = _layouts.LoadEditorLayout(
                    layoutNode.RecordClassId);
                var rootFields = PrepareDirectFields(
                    dataNode,
                    layout.Cards.SelectMany(AllFieldIds),
                    cancellationToken);
                return PrepareOverrideProjection(
                    dataNode,
                    selectedThemeId,
                    rootFields,
                    cancellationToken);
            },
            cancellationToken);
    }

    public Task<EditorPreparedEmbeddedContent> PrepareEmbeddedAsync(
        EditorEmbeddedContext context)
    {
        var selectedThemeId =
            _dictionaryFields.CaptureSelectedThemeId();
        var cancellationToken = BeginPreparation();
        return _operations.ExecuteAsync(
            () =>
            {
                if (context.IsRecordReferenceOverride)
                {
                    var referenceLayout =
                        _layouts.LoadEditorLayout(
                            context.RecordClassId);
                    var referenceFields = _fieldValues
                        .CreateRecordReferenceOverrideFields(
                            context,
                            referenceLayout.Cards
                                .SelectMany(AllFieldIds));
                    var referenceCards =
                        VisibleCards(referenceLayout)
                            .Where((card) =>
                                card.VisibleGroups
                                    .SelectMany((group) =>
                                        group.VisibleFieldsFor(
                                            referenceFields))
                                    .Any((field) =>
                                        referenceFields
                                            .ContainsKey(
                                                field.Id)))
                            .Select((card) =>
                                new EditorPreparedLayoutCard(
                                    card,
                                    referenceFields))
                            .ToList();
                    return new EditorPreparedEmbeddedContent(
                        null,
                        referenceCards,
                        _dictionaryFields.PrepareContext(
                            context.OwnerNode,
                            selectedThemeId,
                            referenceFields,
                            cancellationToken),
                        _header.PrepareEmbedded(
                            context,
                            cancellationToken));
                }
                EditorPreparedLayoutCard? ownerCard = null;
                if (!context.IsRuntimeRoot
                    && EmbeddedOwnerSettingsCatalog.TryGet(
                        context.Slot.FieldId,
                        out var ownerSettings))
                {
                    var ownerLayout = OwnerSettingsLayout(
                        context,
                        ownerSettings);
                    ownerCard = new EditorPreparedLayoutCard(
                        ownerLayout,
                        PrepareDirectFields(
                            context.OwnerNode,
                            AllFieldIds(ownerLayout),
                            cancellationToken));
                }

                var layout = _layouts.LoadEditorLayout(
                    context.RecordClassId);
                var embeddedFields = PrepareEmbeddedFields(
                    context,
                    layout.Cards.SelectMany(AllFieldIds),
                    cancellationToken);
                var cards = VisibleCards(layout)
                    .Where((card) =>
                        EditorLayoutCardFactory.EmbeddedCardHasFields(
                            card,
                            embeddedFields))
                    .Select((card) => new EditorPreparedLayoutCard(
                        card,
                        embeddedFields))
                    .ToList();
                return new EditorPreparedEmbeddedContent(
                    ownerCard,
                    cards,
                    _dictionaryFields.PrepareContext(
                        context.OwnerNode,
                        selectedThemeId,
                        ownerCard is null
                            ? embeddedFields
                            : MergeFields(
                                ownerCard.Fields,
                                embeddedFields),
                        cancellationToken),
                    _header.PrepareEmbedded(
                        context,
                        cancellationToken));
            },
            cancellationToken);
    }

    public void Cancel()
    {
        _activePreparation?.Cancel();
        _activePreparation?.Dispose();
        _activePreparation = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cancel();
    }

    private CancellationToken BeginPreparation()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Cancel();
        _activePreparation = new CancellationTokenSource();
        return _activePreparation.Token;
    }

    private IReadOnlyDictionary<string, FieldValue> PrepareDirectFields(
        ProjectTreeNode node,
        IEnumerable<string> fieldIds,
        CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, FieldValue>(
            StringComparer.Ordinal);
        var pending = new Queue<string>(fieldIds.Distinct(StringComparer.Ordinal));
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fieldId = pending.Dequeue();
            if (fields.ContainsKey(fieldId)) continue;
            var field = _fieldValues.Create(node, fieldId);
            fields[fieldId] = field;
            EnqueueRuntimeDependencies(field.Definition, pending, fields);
        }
        return fields;
    }

    private IReadOnlyDictionary<string, FieldValue> PrepareEmbeddedFields(
        EditorEmbeddedContext context,
        IEnumerable<string> fieldIds,
        CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, FieldValue>(
            StringComparer.Ordinal);
        var pending = new Queue<string>(fieldIds
            .Where((fieldId) =>
                fieldId.StartsWith("component.", StringComparison.Ordinal)
                && !fieldId.Equals("component.type", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal));
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fieldId = pending.Dequeue();
            if (fields.ContainsKey(fieldId)) continue;
            var field = _componentFields.CreateEmbeddedFieldValue(context, fieldId);
            fields[fieldId] = field;
            EnqueueRuntimeDependencies(field.Definition, pending, fields);
        }
        return fields;
    }

    private static void EnqueueRuntimeDependencies(
        FieldDefinition definition,
        Queue<string> pending,
        IReadOnlyDictionary<string, FieldValue> prepared)
    {
        foreach (var fieldId in new[]
                 {
                     definition.RuntimeInputComponentVariantFieldId,
                     definition.RuntimeCollectionComponentVariantFieldId,
                 })
        {
            if (!string.IsNullOrWhiteSpace(fieldId)
                && !prepared.ContainsKey(fieldId))
            {
                pending.Enqueue(fieldId);
            }
        }
    }

    private EditorPreparedOverrideProjection PrepareOverrideProjection(
        ProjectTreeNode node,
        string? selectedThemeId,
        IReadOnlyDictionary<string, FieldValue> rootFields,
        CancellationToken cancellationToken)
    {
        var groups = new List<EditorPreparedOverrideGroup>();
        var fieldSets =
            new List<IReadOnlyDictionary<string, FieldValue>>
            {
                rootFields,
            };
        var collectionOwners = new List<PreparedBoundaryCollectionOwner>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var field in rootFields.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (EmbeddedComponentSlotCatalog.TryGet(
                    field.Definition.Id,
                    out var slot))
            {
                PrepareOverrideContext(
                    new EditorEmbeddedContext(node, [slot]),
                    slot.Label,
                    groups,
                    fieldSets,
                    collectionOwners,
                    visited,
                    cancellationToken);
            }
            if (field.Definition.StructuredCollection is { } collection
                && ContainsComponentBoundaries(collection))
            {
                collectionOwners.Add(new PreparedBoundaryCollectionOwner(
                    null,
                    field.Definition.Id,
                    field.Value,
                    field.Definition.DisplayLabel,
                    collection));
            }
        }

        var ownerIndex = 0;
        while (ownerIndex < collectionOwners.Count)
        {
            var dictionaryContext = _dictionaryFields.PrepareContext(
                node,
                selectedThemeId,
                fieldSets,
                cancellationToken);
            PrepareCollectionOverrides(
                node,
                collectionOwners[ownerIndex++],
                dictionaryContext,
                groups,
                fieldSets,
                collectionOwners,
                visited,
                cancellationToken);
        }

        return new EditorPreparedOverrideProjection(
            groups,
            rootFields,
            _dictionaryFields.PrepareContext(
                node,
                selectedThemeId,
                fieldSets,
                cancellationToken));
    }

    private void PrepareOverrideContext(
        EditorEmbeddedContext context,
        string pathLabel,
        List<EditorPreparedOverrideGroup> groups,
        List<IReadOnlyDictionary<string, FieldValue>> fieldSets,
        List<PreparedBoundaryCollectionOwner> collectionOwners,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        var occurrenceId = OverrideOccurrenceId(context);
        if (!visited.Add(occurrenceId))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var layout = _layouts.LoadEditorLayout(
            context.RecordClassId);
        var fields = PrepareEmbeddedFields(
            context,
            layout.Cards.SelectMany(AllFieldIds),
            cancellationToken);
        fieldSets.Add(fields);
        var overrideFieldIds = VisibleCards(layout)
            .SelectMany((card) => card.VisibleGroups)
            .SelectMany((group) => group.VisibleFieldsFor(fields))
            .Select((field) => field.Id)
            .Where((fieldId) =>
                fields.TryGetValue(fieldId, out var field)
                && field.HasLocalOverride)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (overrideFieldIds.Count > 0)
        {
            groups.Add(new EditorPreparedOverrideGroup(
                occurrenceId,
                pathLabel,
                context,
                fields,
                overrideFieldIds));
        }

        foreach (var field in fields.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (EmbeddedComponentSlotCatalog.TryGet(
                    field.Definition.Id,
                    out var nestedSlot)
                && (field.IsHighlighted
                    || field.HasLocalOverride))
            {
                PrepareOverrideContext(
                    context.Nested(nestedSlot),
                    $"{pathLabel} · {nestedSlot.Label}",
                    groups,
                    fieldSets,
                    collectionOwners,
                    visited,
                    cancellationToken);
            }
            if (field.Definition.StructuredCollection is { } collection
                && ContainsComponentBoundaries(collection))
            {
                collectionOwners.Add(new PreparedBoundaryCollectionOwner(
                    context,
                    field.Definition.Id,
                    field.Value,
                    $"{pathLabel} · {field.Definition.DisplayLabel}",
                    collection));
            }
        }
    }

    private void PrepareCollectionOverrides(
        ProjectTreeNode node,
        PreparedBoundaryCollectionOwner owner,
        EditorDictionaryContextSnapshot dictionaryContext,
        List<EditorPreparedOverrideGroup> groups,
        List<IReadOnlyDictionary<string, FieldValue>> fieldSets,
        List<PreparedBoundaryCollectionOwner> collectionOwners,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        var items = JsonNode.Parse(owner.Value) as JsonArray
            ?? throw new InvalidOperationException(
                $"Structured collection field '{owner.FieldId}' must be an array.");
        StructuredCollectionDocumentContract.ValidateEffective(
            items,
            owner.Collection,
            $"Flat Overrides '{owner.FieldId}'");
        PrepareCollectionItems(
            node,
            owner,
            owner.Collection,
            items,
            [],
            owner.PathLabel,
            dictionaryContext,
            groups,
            fieldSets,
            collectionOwners,
            visited,
            cancellationToken);
    }

    private void PrepareCollectionItems(
        ProjectTreeNode node,
        PreparedBoundaryCollectionOwner owner,
        RuntimeInputCollectionDefinition collection,
        JsonArray items,
        IReadOnlyList<PreparedCollectionItemPathSegment> parentPath,
        string pathLabel,
        EditorDictionaryContextSnapshot dictionaryContext,
        List<EditorPreparedOverrideGroup> groups,
        List<IReadOnlyDictionary<string, FieldValue>> fieldSets,
        List<PreparedBoundaryCollectionOwner> collectionOwners,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[itemIndex]!.AsObject();
            var itemId = JsonPath.RequiredString(
                item,
                "id",
                $"Flat Overrides '{owner.FieldId}'");
            var presentation = RuntimeCollectionItemPresentation.Resolve(
                collection,
                item,
                itemIndex,
                $"{collection.ItemLabel} {itemIndex + 1}",
                $"{collection.ItemLabel} {itemIndex + 1}",
                EditorIcons.Component);
            var itemPathLabel = $"{pathLabel} · {presentation.Title}";
            var itemPath = parentPath
                .Append(new PreparedCollectionItemPathSegment(itemId, ""))
                .ToList();
            foreach (var boundary in ComponentBoundaries(collection))
            {
                var boundaryOwner = $"Flat Overrides '{owner.FieldId}' item '{itemId}'";
                var variantReference = boundary.ReadVariantReference(item, boundaryOwner);
                if (string.IsNullOrWhiteSpace(variantReference)) continue;
                if (!dictionaryContext.TryVariantSelection(variantReference, out var selection))
                {
                    throw new InvalidOperationException(
                        $"Component Variant '{variantReference}' was not included in the prepared dictionary context.");
                }
                var overrides = boundary.ReadOverrides(item, boundaryOwner)
                    .DeepClone()
                    .AsObject();
                var runtimeSource = new RuntimeComponentOverrideSource(
                    selection.ProjectId,
                    variantReference,
                    selection.ComponentType,
                    selection.RecordClassId,
                    selection.ConfigJson,
                    overrides,
                    (changed) => UpdateCollectionOverridesAsync(
                        node,
                        owner,
                        itemPath,
                        boundary,
                        changed));
                PrepareOverrideContext(
                    new EditorEmbeddedContext(node, [], runtimeSource),
                    $"{pathLabel} · {boundary.Label} {presentation.Title}",
                    groups,
                    fieldSets,
                    collectionOwners,
                    visited,
                    cancellationToken);
            }

            foreach (var input in collection.Fields.Where((field) =>
                         field.StructuredCollection is { } nested
                         && ContainsComponentBoundaries(nested)))
            {
                var nested = JsonPath.RequiredArray(
                    item,
                    input.JsonKey,
                    $"Flat Overrides '{owner.FieldId}' item '{itemId}'");
                var nestedCollection = input.StructuredCollection!;
                StructuredCollectionDocumentContract.ValidateEffective(
                    nested,
                    nestedCollection,
                    $"Flat Overrides '{owner.FieldId}' nested collection '{input.Id}'");
                var nestedParentPath = parentPath
                    .Append(new PreparedCollectionItemPathSegment(itemId, input.JsonKey))
                    .ToList();
                PrepareCollectionItems(
                    node,
                    owner,
                    nestedCollection,
                    nested,
                    nestedParentPath,
                    $"{itemPathLabel} · {input.Label}",
                    dictionaryContext,
                    groups,
                    fieldSets,
                    collectionOwners,
                    visited,
                    cancellationToken);
            }
        }
    }

    private Task UpdateCollectionOverridesAsync(
        ProjectTreeNode node,
        PreparedBoundaryCollectionOwner owner,
        IReadOnlyList<PreparedCollectionItemPathSegment> itemPath,
        PreparedCollectionComponentBoundary boundary,
        JsonObject overrides) =>
        _operations.ExecuteAsync(() =>
        {
            var current = owner.Context is null
                ? _fieldValues.Create(node, owner.FieldId)
                : _componentFields.CreateEmbeddedFieldValue(
                    owner.Context,
                    owner.FieldId);
            var items = JsonNode.Parse(current.Value) as JsonArray
                ?? throw new InvalidOperationException(
                    $"Structured collection field '{owner.FieldId}' must be an array.");
            var item = FindCollectionItem(items, owner.FieldId, itemPath);
            boundary.WriteOverrides(item, overrides);
            var value = items.ToJsonString();
            if (owner.Context is null)
            {
                _fieldValues.Persist(
                    node,
                    owner.FieldId,
                    value);
            }
            else
            {
                _componentFields.CommitEmbeddedFieldValue(
                    owner.Context,
                    owner.FieldId,
                    value);
            }
        });

    private static JsonObject FindCollectionItem(
        JsonArray root,
        string fieldId,
        IReadOnlyList<PreparedCollectionItemPathSegment> path)
    {
        var items = root;
        JsonObject? item = null;
        foreach (var segment in path)
        {
            item = items
                .Select((candidate) => candidate!.AsObject())
                .Single((candidate) => JsonPath.RequiredString(
                        candidate,
                        "id",
                        $"Structured collection field '{fieldId}'")
                    .Equals(segment.ItemId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(segment.ChildCollectionJsonKey))
            {
                items = JsonPath.RequiredArray(
                    item,
                    segment.ChildCollectionJsonKey,
                    $"Structured collection field '{fieldId}' item '{segment.ItemId}'");
            }
        }
        return item ?? throw new InvalidOperationException(
            $"Structured collection field '{fieldId}' requires a non-empty item path.");
    }

    private static bool ContainsComponentBoundaries(
        RuntimeInputCollectionDefinition collection) =>
        ComponentBoundaries(collection).Any()
        || collection.Fields.Any((field) => field.StructuredCollection is { } nested
            && ContainsComponentBoundaries(nested));

    private static IEnumerable<PreparedCollectionComponentBoundary> ComponentBoundaries(
        RuntimeInputCollectionDefinition collection)
    {
        if (collection.FixedComponentBoundary is { } fixedBoundary)
        {
            yield return PreparedCollectionComponentBoundary.Separate(
                collection.ItemLabel,
                fixedBoundary.VariantReferenceJsonKey,
                fixedBoundary.OverridesJsonKey);
        }
        if (collection.ComponentItems is { } componentItems)
        {
            yield return PreparedCollectionComponentBoundary.Separate(
                collection.ItemLabel,
                componentItems.VariantReferenceJsonKey,
                componentItems.OverridesJsonKey);
        }
        foreach (var input in collection.Fields.Where((field) =>
                     field.ValueKind == ValueKind.ComponentVariantSlot))
        {
            yield return PreparedCollectionComponentBoundary.Slot(
                input.Label,
                input.JsonKey);
        }
    }

    private static string OverrideOccurrenceId(
        EditorEmbeddedContext context)
    {
        var slots = string.Join(
            "/",
            context.Slots.Select((slot) => slot.FieldId));
        if (context.RuntimeSource is { } runtime)
        {
            return $"runtime:{runtime.VariantReference}:"
                + RuntimeHelpers.GetHashCode(runtime.Overrides)
                + (string.IsNullOrWhiteSpace(slots)
                    ? ""
                    : $"/{slots}");
        }
        return slots;
    }

    private sealed record PreparedBoundaryCollectionOwner(
        EditorEmbeddedContext? Context,
        string FieldId,
        string Value,
        string PathLabel,
        RuntimeInputCollectionDefinition Collection);

    private sealed record PreparedCollectionItemPathSegment(
        string ItemId,
        string ChildCollectionJsonKey);

    private sealed record PreparedCollectionComponentBoundary(
        string Label,
        string SlotJsonKey,
        string VariantReferenceJsonKey,
        string OverridesJsonKey)
    {
        public static PreparedCollectionComponentBoundary Separate(
            string label,
            string variantReferenceJsonKey,
            string overridesJsonKey) =>
            new(label, "", variantReferenceJsonKey, overridesJsonKey);

        public static PreparedCollectionComponentBoundary Slot(
            string label,
            string slotJsonKey) =>
            new(label, slotJsonKey, "", "");

        public string ReadVariantReference(JsonObject item, string owner) =>
            string.IsNullOrWhiteSpace(SlotJsonKey)
                ? JsonPath.RequiredString(item, VariantReferenceJsonKey, owner, allowEmpty: true)
                : ComponentVariantSlotDocumentContract.VariantReference(
                    JsonPath.RequiredObject(item, SlotJsonKey, owner),
                    $"{owner}.{SlotJsonKey}");

        public JsonObject ReadOverrides(JsonObject item, string owner) =>
            string.IsNullOrWhiteSpace(SlotJsonKey)
                ? JsonPath.RequiredObject(item, OverridesJsonKey, owner)
                : ComponentVariantSlotDocumentContract.Overrides(
                    JsonPath.RequiredObject(item, SlotJsonKey, owner),
                    $"{owner}.{SlotJsonKey}");

        public void WriteOverrides(JsonObject item, JsonObject overrides)
        {
            if (string.IsNullOrWhiteSpace(SlotJsonKey))
            {
                item[OverridesJsonKey] = overrides.DeepClone();
                return;
            }
            var owner = $"Structured collection Component Variant Slot '{SlotJsonKey}'";
            var slot = JsonPath.RequiredObject(item, SlotJsonKey, owner);
            item[SlotJsonKey] = ComponentVariantSlotDocumentContract.Create(
                ComponentVariantSlotDocumentContract.VariantReference(slot, owner),
                overrides,
                owner);
        }
    }

    private static IEnumerable<EditorLayoutCard> VisibleCards(
        EditorLayout layout) =>
        layout.Cards
            .Where((card) => card.Visible)
            .OrderBy((card) => card.Order)
            .ThenBy((card) => card.Label);

    private static IReadOnlyDictionary<string, FieldValue> MergeFields(
        IReadOnlyDictionary<string, FieldValue> first,
        IReadOnlyDictionary<string, FieldValue> second)
    {
        var fields = new Dictionary<string, FieldValue>(
            first,
            StringComparer.Ordinal);
        foreach (var (fieldId, field) in second)
        {
            fields[fieldId] = field;
        }
        return fields;
    }

    private static IEnumerable<string> AllFieldIds(
        EditorLayoutCard card) =>
        card.Groups.SelectMany(
            (group) => group.Fields.Select((field) => field.Id));

    private static EditorLayoutCard OwnerSettingsLayout(
        EditorEmbeddedContext context,
        EmbeddedOwnerSettingsDefinition ownerSettings) => new()
    {
        Id = $"{context.Slot.FieldId}.ownerSettings",
        Label = ownerSettings.Label,
        Subtitle = ownerSettings.Subtitle,
        Icon = ownerSettings.Icon,
        Order = 0,
        Visible = true,
        DefaultOpen = false,
        Groups =
        [
            new EditorLayoutGroup
            {
                Id = "content",
                Label = "Content",
                Order = 0,
                Visible = true,
                Fields = ownerSettings.FieldIds
                    .Select((fieldId, index) => new EditorLayoutField
                    {
                        Id = fieldId,
                        Order = index,
                        Visible = true,
                    })
                    .ToList(),
            },
        ],
    };
}
