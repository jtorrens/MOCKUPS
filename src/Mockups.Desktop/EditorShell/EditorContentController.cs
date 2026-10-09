using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
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
    private readonly Panel _peerViewHost;
    private readonly Panel _flatOverrideHost;
    private readonly Control _editorViewport;
    private readonly Control _overrideViewport;
    private readonly EditorLoadingScrim _loading;
    private readonly Dictionary<string, bool> _overrideModeByLayout =
        new(StringComparer.Ordinal);
    private readonly Func<ProjectTreeNode, IReadOnlyList<InstantEditorCard>?>
        _specialCards;
    private string _activeRootLayoutId = "";

    public EditorContentController(
        EditorContentPreparationService preparation,
        Panel host,
        Func<double>? availableWidth,
        Control? widthObserver,
        Panel peerViewHost,
        Panel flatOverrideHost,
        Control editorViewport,
        Control overrideViewport,
        EditorLoadingScrim loading,
        EditorActiveFieldControls activeFieldControls,
        IEditorInlinePreviewController inlinePreviews,
        EditorLayoutCardFactory layoutCards,
        EditorCollectionCardFactory collectionCards,
        Func<ProjectTreeNode, IReadOnlyList<InstantEditorCard>?>? specialCards = null)
    {
        _preparation = preparation;
        _cardHost = new EditorCardHostController(host, availableWidth, widthObserver);
        _peerViewHost = peerViewHost;
        _flatOverrideHost = flatOverrideHost;
        _editorViewport = editorViewport;
        _overrideViewport = overrideViewport;
        _loading = loading;
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
        if (PrepareSpecial(dataNode) is { } specialCards)
        {
            CommitSpecial(dataNode, specialCards);
            return true;
        }

        return false;
    }

    public IReadOnlyList<InstantEditorCard>? PrepareSpecial(
        ProjectTreeNode dataNode) =>
        _specialCards(dataNode);

    public void CommitSpecial(
        ProjectTreeNode dataNode,
        IReadOnlyList<InstantEditorCard> specialCards)
    {
        _preparation.Cancel();
        HidePeerViews();
        ResetRegistries();
        _cardHost.Replace(specialCards, resetExpansion: false);
        CommittedOwnerId = dataNode.Id;
        _loading.Hide();
    }

    public IDisposable BeginNavigationLoading(string destination)
    {
        return _loading.BeginScope($"Loading {destination}…");
    }

    public IDisposable ShowLoading(string destination)
    {
        var loading = BeginNavigationLoading(destination);
        HidePeerViews();
        ResetRegistries();
        CommittedOwnerId = "";
        _cardHost.Replace([], resetExpansion: false);
        return loading;
    }

    public void ShowPreparationError(string message)
    {
        HidePeerViews();
        ResetRegistries();
        CommittedOwnerId = "";
        _cardHost.Replace(
        [
            new InstantEditorCard(
                EditorCardHeader.Create(
                    "Editor unavailable",
                    "The selected record could not be prepared",
                    EditorIcons.Create(EditorIcons.Structure, 18)),
                new Border
                {
                    Padding = EditorUiDensity.CardThickness(10),
                    Child = new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    },
                },
                isExpanded: true)
            {
                SessionStateId = "editor:preparation-error",
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
        EditorPreparedRootContent prepared,
        IReadOnlyCollection<string>? restoredExpandedCardIds = null)
    {
        ResetRegistries();
        var presentationControllerFieldIds =
            PresentationControllerFieldIds(prepared.Cards);
        var cards = prepared.Cards
            .Select((card) => _layoutCards.Create(
                dataNode,
                card.Layout,
                layoutNode.RecordClassId,
                prepared.DictionaryContext,
                card.Fields,
                presentationControllerFieldIds))
            .Concat(_collectionCards.Create(dataNode))
            .ToList();
        _cardHost.Replace(
            cards,
            restoredExpandedCardIds: restoredExpandedCardIds);
        HidePeerViews();
        CommittedOwnerId = dataNode.Id;
    }

    public Task<EditorPreparedOverrideProjection>
        PrepareOverridesAsync(
            ProjectTreeNode layoutNode,
            ProjectTreeNode dataNode) =>
        _preparation.PrepareOverridesAsync(
            layoutNode,
            dataNode);

    public void CommitOverrides(
        ProjectTreeNode layoutNode,
        ProjectTreeNode dataNode,
        EditorPreparedOverrideProjection overrides)
    {
        if (!CommittedOwnerId.Equals(
                dataNode.Id,
                StringComparison.Ordinal))
        {
            return;
        }
        _activeRootLayoutId = layoutNode.RecordClassId;
        _flatOverrideHost.Children.Clear();
        _flatOverrideHost.Children.Add(
            _layoutCards.CreateFlatOverrideContent(
                dataNode,
                overrides));
        ShowPeerViews(
            overrides.Count,
            _overrideModeByLayout.TryGetValue(
                _activeRootLayoutId,
                out var showOverrides)
                && showOverrides);
    }

    public Task<EditorPreparedEmbeddedContent> PrepareEmbeddedAsync(
        EditorEmbeddedContext context) =>
        _preparation.PrepareEmbeddedAsync(context);

    public void CommitEmbedded(
        EditorEmbeddedContext context,
        EditorPreparedEmbeddedContent prepared,
        IReadOnlyCollection<string>? restoredExpandedCardIds = null)
    {
        HidePeerViews();
        ResetRegistries();
        var cards = new List<InstantEditorCard>();
        var ownerLayoutRecordClassId = OwnerLayoutRecordClassId(context.OwnerNode);
        var preparedCards = prepared.OwnerCard is null
            ? prepared.Cards
            : [prepared.OwnerCard, .. prepared.Cards];
        var presentationControllerFieldIds =
            PresentationControllerFieldIds(preparedCards);

        if (context.IsRecordReferenceOverride)
        {
            cards.AddRange(prepared.Cards.Select((card) =>
                _layoutCards.CreateRecordReferenceOverrides(
                    context,
                    card.Layout,
                    prepared.DictionaryContext,
                    card.Fields,
                    _activeFieldControls)));
            _cardHost.Replace(
                cards,
                restoredExpandedCardIds:
                    restoredExpandedCardIds);
            CommittedOwnerId = context.OwnerNode.Id;
            return;
        }

        if (prepared.OwnerCard is { } ownerCard)
        {
            cards.Add(_layoutCards.Create(
                context.OwnerNode,
                ownerCard.Layout,
                ownerLayoutRecordClassId,
                prepared.DictionaryContext,
                ownerCard.Fields,
                presentationControllerFieldIds));
        }

        foreach (var card in prepared.Cards)
        {
            cards.Add(_layoutCards.CreateEmbedded(
                context,
                card.Layout,
                prepared.DictionaryContext,
                card.Fields,
                presentationControllerFieldIds));
        }
        _cardHost.Replace(
            cards,
            restoredExpandedCardIds: restoredExpandedCardIds);
        CommittedOwnerId = context.OwnerNode.Id;
    }

    public void CancelPreparation() =>
        _preparation.Cancel();

    public void Dispose()
    {
        _preparation.Dispose();
        _loading.Hide();
    }

    internal static string OwnerLayoutRecordClassId(ProjectTreeNode ownerNode) =>
        ownerNode.Kind is ProjectTreeNodeKind.ComponentVariant or ProjectTreeNodeKind.ModuleVariant
            ? ownerNode.Parent?.RecordClassId
                ?? throw new InvalidOperationException("A component Variant must have its parent component class.")
            : ownerNode.RecordClassId;

    private static IReadOnlySet<string> PresentationControllerFieldIds(
        IEnumerable<EditorPreparedLayoutCard> cards) =>
        cards
            .SelectMany((card) => card.Layout.Groups)
            .SelectMany((group) => group.Fields)
            .Where((field) => !string.IsNullOrWhiteSpace(
                field.VisibleWhenFieldId))
            .Select((field) => field.VisibleWhenFieldId)
            .ToHashSet(StringComparer.Ordinal);

    private void ResetRegistries()
    {
        _activeFieldControls.Clear();
        _inlinePreviews.Reset();
    }

    private void ShowPeerViews(
        int overrideCount,
        bool showOverrides)
    {
        _peerViewHost.Children.Clear();
        var editor = PeerViewButton(
            "Editor",
            !showOverrides,
            () => SelectPeerView(false));
        var overrides = PeerViewButton(
            $"Overrides ({overrideCount})",
            showOverrides,
            () => SelectPeerView(true));
        _peerViewHost.Children.Add(editor);
        _peerViewHost.Children.Add(overrides);
        _peerViewHost.IsVisible = true;
        SetPeerViewport(showOverrides);
    }

    private void SelectPeerView(bool showOverrides)
    {
        if (string.IsNullOrWhiteSpace(_activeRootLayoutId))
        {
            return;
        }
        _overrideModeByLayout[_activeRootLayoutId] =
            showOverrides;
        foreach (var toggle in
                 _peerViewHost.Children.OfType<ToggleButton>())
        {
            toggle.IsChecked =
                showOverrides
                    ? toggle.Tag as string == "overrides"
                    : toggle.Tag as string == "editor";
        }
        SetPeerViewport(showOverrides);
    }

    private void HidePeerViews()
    {
        _peerViewHost.Children.Clear();
        _peerViewHost.IsVisible = false;
        _flatOverrideHost.Children.Clear();
        SetPeerViewport(false);
        _activeRootLayoutId = "";
    }

    private void SetPeerViewport(bool showOverrides)
    {
        _editorViewport.IsVisible = !showOverrides;
        _overrideViewport.IsVisible = showOverrides;
    }

    private static ToggleButton PeerViewButton(
        string label,
        bool selected,
        Action selectedAction)
    {
        var id = label.StartsWith(
            "Overrides",
            StringComparison.Ordinal)
                ? "overrides"
                : "editor";
        var button = new ToggleButton
        {
            Content = label,
            Tag = id,
            IsChecked = selected,
            MinHeight = 30,
            Padding = new Avalonia.Thickness(12, 4),
            HorizontalContentAlignment =
                HorizontalAlignment.Center,
        };
        button.Click += (_, _) => selectedAction();
        return button;
    }

}
