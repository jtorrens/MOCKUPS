using Avalonia.Controls;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorContentController : IDisposable
{
    private readonly EditorContentPreparationService _preparation;
    private readonly EditorCardHostController _cardHost;
    private readonly EditorActiveFieldControls _activeFieldControls;
    private readonly IEditorInlinePreviewController _inlinePreviews;
    private readonly EditorLayoutCardFactory _layoutCards;
    private readonly EditorCollectionCardFactory _collectionCards;
    private readonly Func<ProjectTreeNode, IReadOnlyList<InstantEditorCard>?>
        _specialCards;

    public EditorContentController(
        EditorContentPreparationService preparation,
        Panel host,
        Func<double>? availableWidth,
        Control? widthObserver,
        EditorActiveFieldControls activeFieldControls,
        IEditorInlinePreviewController inlinePreviews,
        EditorLayoutCardFactory layoutCards,
        EditorCollectionCardFactory collectionCards,
        Func<ProjectTreeNode, IReadOnlyList<InstantEditorCard>?>? specialCards = null)
    {
        _preparation = preparation;
        _cardHost = new EditorCardHostController(host, availableWidth, widthObserver);
        _activeFieldControls = activeFieldControls;
        _inlinePreviews = inlinePreviews;
        _layoutCards = layoutCards;
        _collectionCards = collectionCards;
        _specialCards = specialCards ?? (_ => null);
    }

    public IReadOnlyList<InstantEditorCard> Cards => _cardHost.Cards;
    public string CommittedOwnerId { get; private set; } = "";

    public bool TryBuildSpecial(ProjectTreeNode dataNode)
    {
        if (_specialCards(dataNode) is { } specialCards)
        {
            _preparation.Cancel();
            ResetRegistries();
            _cardHost.Replace(specialCards, resetExpansion: false);
            CommittedOwnerId = dataNode.Id;
            return true;
        }

        return false;
    }

    public void ShowLoading()
    {
        ResetRegistries();
        CommittedOwnerId = "";
        _cardHost.Replace(
        [
            new InstantEditorCard(
                EditorCardHeader.Create(
                    "Editor",
                    "Preparing data",
                    EditorIcons.Create(
                        EditorIcons.Structure,
                        18)),
                new Border
                {
                    Padding = EditorUiDensity.CardThickness(10),
                    Child = new TextBlock
                    {
                        Text = "Loading editor data…",
                        Opacity = 0.72,
                    },
                },
                isExpanded: true)
            {
                SessionStateId = "editor:loading",
            },
        ], resetExpansion: false);
    }

    public Task<EditorPreparedRootContent> PrepareRootAsync(
        ProjectTreeNode layoutNode,
        ProjectTreeNode dataNode) =>
        _preparation.PrepareRootAsync(
            layoutNode,
            dataNode);

    public void CommitRoot(
        ProjectTreeNode layoutNode,
        ProjectTreeNode dataNode,
        EditorPreparedRootContent prepared)
    {
        ResetRegistries();
        var cards = prepared.Cards
            .Select((card) => _layoutCards.Create(
                dataNode,
                card.Layout,
                layoutNode.RecordClassId,
                prepared.DictionaryContext,
                card.Fields))
            .Concat(_collectionCards.Create(dataNode))
            .ToList();
        _cardHost.Replace(cards);
        CommittedOwnerId = dataNode.Id;
    }

    public Task<EditorPreparedEmbeddedContent> PrepareEmbeddedAsync(
        EditorEmbeddedContext context) =>
        _preparation.PrepareEmbeddedAsync(context);

    public void CommitEmbedded(
        EditorEmbeddedContext context,
        EditorPreparedEmbeddedContent prepared)
    {
        ResetRegistries();
        var cards = new List<InstantEditorCard>();
        var ownerLayoutRecordClassId = OwnerLayoutRecordClassId(context.OwnerNode);

        if (prepared.OwnerCard is { } ownerCard)
        {
            cards.Add(_layoutCards.Create(
                context.OwnerNode,
                ownerCard.Layout,
                ownerLayoutRecordClassId,
                prepared.DictionaryContext,
                ownerCard.Fields));
        }

        foreach (var card in prepared.Cards)
        {
            cards.Add(_layoutCards.CreateEmbedded(
                context,
                card.Layout,
                prepared.DictionaryContext,
                card.Fields));
        }
        _cardHost.Replace(cards);
        CommittedOwnerId = context.OwnerNode.Id;
    }

    public void Dispose() => _preparation.Dispose();

    internal static string OwnerLayoutRecordClassId(ProjectTreeNode ownerNode) =>
        ownerNode.Kind is ProjectTreeNodeKind.ComponentVariant or ProjectTreeNodeKind.ModuleVariant
            ? ownerNode.Parent?.RecordClassId
                ?? throw new InvalidOperationException("A component Variant must have its parent component class.")
            : ownerNode.RecordClassId;

    private void ResetRegistries()
    {
        _activeFieldControls.Clear();
        _inlinePreviews.Reset();
    }

}
