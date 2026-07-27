using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
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
        foreach (var fieldId in fieldIds.Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            fields[fieldId] = _fieldValues.Create(node, fieldId);
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
        foreach (var fieldId in fieldIds
                     .Where((fieldId) =>
                         fieldId.StartsWith(
                             "component.",
                             StringComparison.Ordinal)
                         && !fieldId.Equals(
                             "component.type",
                             StringComparison.Ordinal))
                     .Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            fields[fieldId] =
                _componentFields.CreateEmbeddedFieldValue(
                    context,
                    fieldId);
        }
        return fields;
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
