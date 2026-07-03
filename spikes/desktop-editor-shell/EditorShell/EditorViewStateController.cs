using Avalonia.Controls;
using Avalonia.Threading;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorViewStateController
{
    private readonly ScrollViewer _scrollViewer;
    private readonly Dictionary<string, EditorViewState> _statesByRecordClassId = [];

    public EditorViewStateController(ScrollViewer scrollViewer)
    {
        _scrollViewer = scrollViewer;
    }

    public bool ShouldPreserve(ProjectTreeNode? previousNode, ProjectTreeNode nextNode)
    {
        return previousNode?.RecordClassId == nextNode.RecordClassId;
    }

    public void Capture(ProjectTreeNode? node, IReadOnlyList<InstantEditorCard> cards)
    {
        if (node is null || cards.Count == 0) return;

        _statesByRecordClassId[node.RecordClassId] = new EditorViewState(
            cards.Select((card) => card.IsExpanded).ToArray(),
            _scrollViewer.Offset);
    }

    public void Restore(ProjectTreeNode node, IReadOnlyList<InstantEditorCard> cards)
    {
        if (!_statesByRecordClassId.TryGetValue(node.RecordClassId, out var state))
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
