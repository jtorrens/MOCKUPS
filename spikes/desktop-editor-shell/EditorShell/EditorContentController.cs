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
    private readonly ActorAvatarPreviewController _actorAvatarPreviews;
    private readonly EditorLayoutCardFactory _layoutCards;
    private readonly EditorCollectionCardFactory _collectionCards;

    public EditorContentController(
        SpikeDatabase database,
        Panel host,
        Func<double>? availableWidth,
        Control? widthObserver,
        EditorActiveFieldControls activeFieldControls,
        ActorAvatarPreviewController actorAvatarPreviews,
        EditorLayoutCardFactory layoutCards,
        EditorCollectionCardFactory collectionCards)
    {
        _database = database;
        _cardHost = new EditorCardHostController(host, availableWidth, widthObserver);
        _activeFieldControls = activeFieldControls;
        _actorAvatarPreviews = actorAvatarPreviews;
        _layoutCards = layoutCards;
        _collectionCards = collectionCards;
    }

    public IReadOnlyList<InstantEditorCard> Cards => _cardHost.Cards;

    public void Build(ProjectTreeNode layoutNode, ProjectTreeNode dataNode)
    {
        Reset();

        var layout = _database.LoadEditorLayout(layoutNode.RecordClassId);
        foreach (var layoutCard in layout.Cards
                     .Where((card) => card.Visible)
                     .OrderBy((card) => card.Order)
                     .ThenBy((card) => card.Label))
        {
            _cardHost.Add(_layoutCards.Create(dataNode, layoutCard));
        }

        foreach (var collectionCard in _collectionCards.Create(dataNode))
        {
            _cardHost.Add(collectionCard);
        }
    }

    public void BuildEmbedded(EditorEmbeddedContext context)
    {
        Reset();

        var layout = _database.LoadEditorLayout(context.Slot.RecordClassId);
        foreach (var layoutCard in layout.Cards
                     .Where((card) => card.Visible && EditorLayoutCardFactory.EmbeddedCardHasFields(card))
                     .OrderBy((card) => card.Order)
                     .ThenBy((card) => card.Label))
        {
            _cardHost.Add(_layoutCards.CreateEmbedded(context, layoutCard));
        }
    }

    private void Reset()
    {
        _cardHost.Clear();
        _activeFieldControls.Clear();
        _actorAvatarPreviews.Reset();
    }
}
