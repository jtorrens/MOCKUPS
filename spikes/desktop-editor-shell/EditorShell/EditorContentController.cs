using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorContentController
{
    private readonly SpikeDatabase _database;
    private readonly EditorCardHostController _cardHost;
    private readonly EditorActiveFieldControls _activeFieldControls;
    private readonly IEditorInlinePreviewController _inlinePreviews;
    private readonly EditorLayoutCardFactory _layoutCards;
    private readonly EditorCollectionCardFactory _collectionCards;

    public EditorContentController(
        SpikeDatabase database,
        Panel host,
        Func<double>? availableWidth,
        Control? widthObserver,
        EditorActiveFieldControls activeFieldControls,
        IEditorInlinePreviewController inlinePreviews,
        EditorLayoutCardFactory layoutCards,
        EditorCollectionCardFactory collectionCards)
    {
        _database = database;
        _cardHost = new EditorCardHostController(host, availableWidth, widthObserver);
        _activeFieldControls = activeFieldControls;
        _inlinePreviews = inlinePreviews;
        _layoutCards = layoutCards;
        _collectionCards = collectionCards;
    }

    public IReadOnlyList<InstantEditorCard> Cards => _cardHost.Cards;

    public void Build(ProjectTreeNode layoutNode, ProjectTreeNode dataNode)
    {
        var layout = _database.LoadEditorLayout(layoutNode.RecordClassId);
        ResetRegistries();
        var cards = layout.Cards
            .Where((card) => card.Visible)
            .OrderBy((card) => card.Order)
            .ThenBy((card) => card.Label)
            .Select((layoutCard) => _layoutCards.Create(
                dataNode,
                layoutCard,
                layoutNode.RecordClassId))
            .Concat(_collectionCards.Create(dataNode))
            .ToList();
        _cardHost.Replace(cards);
    }

    public void BuildEmbedded(EditorEmbeddedContext context)
    {
        ResetRegistries();
        var cards = new List<InstantEditorCard>();
        var ownerLayoutRecordClassId = OwnerLayoutRecordClassId(context.OwnerNode);

        if (!context.IsRuntimeRoot
            && EmbeddedOwnerSettingsCatalog.TryGet(context.Slot.FieldId, out var ownerSettings))
        {
            cards.Add(_layoutCards.Create(context.OwnerNode, new EditorLayoutCard
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
                            .Select((fieldId, index) => new EditorLayoutField { Id = fieldId, Order = index, Visible = true })
                            .ToList(),
                    },
                ],
            }, ownerLayoutRecordClassId));
        }

        var layout = _database.LoadEditorLayout(context.RecordClassId);
        foreach (var layoutCard in layout.Cards
                     .Where((card) => card.Visible && EditorLayoutCardFactory.EmbeddedCardHasFields(card))
                     .OrderBy((card) => card.Order)
                     .ThenBy((card) => card.Label))
        {
            cards.Add(_layoutCards.CreateEmbedded(context, layoutCard));
        }
        _cardHost.Replace(cards);
    }

    internal static string OwnerLayoutRecordClassId(ProjectTreeNode ownerNode) =>
        ownerNode.Kind is ProjectTreeNodeKind.ComponentPreset or ProjectTreeNodeKind.ModuleVariant
            ? ownerNode.Parent?.RecordClassId
                ?? throw new InvalidOperationException("A component Variant must have its parent component class.")
            : ownerNode.RecordClassId;

    private void ResetRegistries()
    {
        _activeFieldControls.Clear();
        _inlinePreviews.Reset();
    }

}
