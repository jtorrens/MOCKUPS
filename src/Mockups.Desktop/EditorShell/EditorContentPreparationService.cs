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
                            card))
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
        var fixedBoundaryOwners = new List<PreparedFixedBoundaryCollectionOwner>();
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
                    fixedBoundaryOwners,
                    visited,
                    cancellationToken);
            }
            if (field.Definition.StructuredCollection is { FixedComponentBoundary: not null } collection)
            {
                fixedBoundaryOwners.Add(new PreparedFixedBoundaryCollectionOwner(
                    null,
                    field.Definition.Id,
                    field.Value,
                    field.Definition.DisplayLabel,
                    collection));
            }
        }

        var dictionaryContext = _dictionaryFields.PrepareContext(
            node,
            selectedThemeId,
            fieldSets,
            cancellationToken);
        foreach (var owner in fixedBoundaryOwners)
        {
            PrepareFixedBoundaryOverrides(
                node,
                owner,
                dictionaryContext,
                groups,
                fieldSets,
                visited,
                cancellationToken);
        }

        return new EditorPreparedOverrideProjection(
            groups,
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
        List<PreparedFixedBoundaryCollectionOwner> fixedBoundaryOwners,
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
            .SelectMany((group) => group.VisibleFields)
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
                    fixedBoundaryOwners,
                    visited,
                    cancellationToken);
            }
            if (field.Definition.StructuredCollection is { FixedComponentBoundary: not null } collection
                && field.HasLocalOverride)
            {
                fixedBoundaryOwners.Add(new PreparedFixedBoundaryCollectionOwner(
                    context,
                    field.Definition.Id,
                    field.Value,
                    $"{pathLabel} · {field.Definition.DisplayLabel}",
                    collection));
            }
        }
    }

    private void PrepareFixedBoundaryOverrides(
        ProjectTreeNode node,
        PreparedFixedBoundaryCollectionOwner owner,
        EditorDictionaryContextSnapshot dictionaryContext,
        List<EditorPreparedOverrideGroup> groups,
        List<IReadOnlyDictionary<string, FieldValue>> fieldSets,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        var items = JsonNode.Parse(owner.Value) as JsonArray
            ?? throw new InvalidOperationException(
                $"Structured collection field '{owner.FieldId}' must be an array.");
        StructuredCollectionDocumentContract.Validate(
            items,
            owner.Collection,
            $"Flat Overrides '{owner.FieldId}'");
        var boundary = owner.Collection.FixedComponentBoundary
            ?? throw new InvalidOperationException(
                $"Structured collection '{owner.Collection.Id}' requires a fixed Component boundary.");
        foreach (var itemNode in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = itemNode!.AsObject();
            var itemId = JsonPath.RequiredString(
                item,
                "id",
                $"Flat Overrides '{owner.FieldId}'");
            var variantReference = JsonPath.RequiredString(
                item,
                boundary.VariantReferenceJsonKey,
                $"Flat Overrides '{owner.FieldId}' item '{itemId}'");
            if (!dictionaryContext.TryVariantSelection(
                    variantReference,
                    out var selection))
            {
                throw new InvalidOperationException(
                    $"Component Variant '{variantReference}' was not included in the prepared dictionary context.");
            }
            var overrides = JsonPath.RequiredObject(
                    item,
                    boundary.OverridesJsonKey,
                    $"Flat Overrides '{owner.FieldId}' item '{itemId}'")
                .DeepClone()
                .AsObject();
            var runtimeSource = new RuntimeComponentOverrideSource(
                selection.ProjectId,
                variantReference,
                selection.ComponentType,
                selection.RecordClassId,
                selection.ConfigJson,
                overrides,
                (changed) => UpdateFixedBoundaryOverridesAsync(
                    node,
                    owner,
                    itemId,
                    changed));
            var context = new EditorEmbeddedContext(
                node,
                [],
                runtimeSource);
            var titleKey = owner.Collection.Fields.FirstOrDefault((field) =>
                    field.Id.Equals(
                        owner.Collection.ItemPresentation?.TitleFieldId,
                        StringComparison.Ordinal))?.JsonKey ?? "";
            var itemLabel = titleKey.Length == 0
                ? ""
                : JsonPath.RequiredString(
                    item,
                    titleKey,
                    $"Flat Overrides '{owner.FieldId}' item '{itemId}'",
                    allowEmpty: true);
            PrepareOverrideContext(
                context,
                $"{owner.PathLabel} · {owner.Collection.ItemLabel} "
                    + (string.IsNullOrWhiteSpace(itemLabel)
                        ? itemId
                        : itemLabel),
                groups,
                fieldSets,
                [],
                visited,
                cancellationToken);
        }
    }

    private Task UpdateFixedBoundaryOverridesAsync(
        ProjectTreeNode node,
        PreparedFixedBoundaryCollectionOwner owner,
        string itemId,
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
            var item = items
                .Select((candidate) => candidate!.AsObject())
                .Single((candidate) => JsonPath.RequiredString(
                        candidate,
                        "id",
                        $"Structured collection field '{owner.FieldId}'")
                    .Equals(itemId, StringComparison.Ordinal));
            var boundary = owner.Collection.FixedComponentBoundary
                ?? throw new InvalidOperationException(
                    $"Structured collection '{owner.Collection.Id}' requires a fixed Component boundary.");
            item[boundary.OverridesJsonKey] = overrides.DeepClone();
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

    private sealed record PreparedFixedBoundaryCollectionOwner(
        EditorEmbeddedContext? Context,
        string FieldId,
        string Value,
        string PathLabel,
        RuntimeInputCollectionDefinition Collection);

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
