using Avalonia.Controls;
using Avalonia.Threading;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorViewStateController
{
    private readonly ScrollViewer _scrollViewer;
    private readonly Dictionary<string, EditorViewState> _statesByNodeId = [];

    public EditorViewStateController(ScrollViewer scrollViewer)
    {
        _scrollViewer = scrollViewer;
    }

    public bool ShouldPreserve(ProjectTreeNode? previousNode, ProjectTreeNode nextNode)
    {
        return previousNode is not null;
    }

    public void Capture(ProjectTreeNode? node, IReadOnlyList<InstantEditorCard> cards)
    {
        if (node is null || cards.Count == 0) return;

        var state = CaptureState(cards);
        if (state is not null)
        {
            _statesByNodeId[node.Id] = state;
        }
    }

    public void Restore(ProjectTreeNode node, IReadOnlyList<InstantEditorCard> cards)
    {
        if (!_statesByNodeId.TryGetValue(node.Id, out var state))
        {
            return;
        }

        RestoreState(state, cards);
    }

    public EditorViewState? CaptureState(IReadOnlyList<InstantEditorCard> cards)
    {
        if (cards.Count == 0)
        {
            return null;
        }

        return new EditorViewState(
            cards.Select((card) => card.IsExpanded).ToArray(),
            _scrollViewer.Offset);
    }

    public void RestoreState(EditorViewState? state, IReadOnlyList<InstantEditorCard> cards)
    {
        if (state is null)
        {
            return;
        }

        for (var index = 0; index < cards.Count && index < state.ExpandedCards.Length; index++)
        {
            cards[index].IsExpanded = state.ExpandedCards[index];
        }

        Dispatcher.UIThread.Post(
            () => _scrollViewer.Offset = state.ScrollOffset,
            DispatcherPriority.Loaded);
    }
}
