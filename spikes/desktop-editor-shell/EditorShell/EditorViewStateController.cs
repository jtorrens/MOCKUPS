using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorViewStateController
{
    private readonly ScrollViewer _scrollViewer;
    private readonly EditorSessionViewStateStore _sessionStates;

    public EditorViewStateController(
        ScrollViewer scrollViewer,
        EditorSessionViewStateStore? sessionStates = null)
    {
        _scrollViewer = scrollViewer;
        _sessionStates = sessionStates ?? new EditorSessionViewStateStore();
    }

    public void Capture(ProjectTreeNode? node, IReadOnlyList<InstantEditorCard> cards)
    {
        if (node is null || cards.Count == 0) return;

        var state = CaptureState(cards);
        if (state is not null)
        {
            _sessionStates.Set(StateKey(node), state);
        }
    }

    public void Restore(ProjectTreeNode node, IReadOnlyList<InstantEditorCard> cards)
    {
        var state = _sessionStates.Get(StateKey(node));
        if (state is null)
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

        ValidateStableCardIds(cards);
        return new EditorViewState(
            cards.Where((card) => card.IsExpanded)
                .Select((card) => card.SessionStateId)
                .ToArray(),
            _scrollViewer.Offset);
    }

    public void RestoreState(EditorViewState? state, IReadOnlyList<InstantEditorCard> cards)
    {
        if (state is null)
        {
            return;
        }

        ValidateStableCardIds(cards);
        var expandedCardIds = state.ExpandedCardIds.ToHashSet(StringComparer.Ordinal);
        foreach (var card in cards)
        {
            card.RestoreExpansion(expandedCardIds.Contains(card.SessionStateId));
        }

        Dispatcher.UIThread.Post(
            () => _scrollViewer.Offset = ClampOffset(
                state.ScrollOffset,
                _scrollViewer.Extent,
                _scrollViewer.Viewport),
            DispatcherPriority.Loaded);
    }

    internal static string StateKey(ProjectTreeNode node)
    {
        return EditorNodeSelectionState.EditorNodeForSelection(node).RecordClassId;
    }

    internal static Vector ClampOffset(Vector requested, Size extent, Size viewport)
    {
        return new Vector(
            Math.Clamp(requested.X, 0, Math.Max(0, extent.Width - viewport.Width)),
            Math.Clamp(requested.Y, 0, Math.Max(0, extent.Height - viewport.Height)));
    }

    private static void ValidateStableCardIds(IReadOnlyList<InstantEditorCard> cards)
    {
        if (cards.Any((card) => string.IsNullOrWhiteSpace(card.SessionStateId)))
        {
            throw new InvalidOperationException("Every top-level editor card requires a stable session state id.");
        }

        if (cards.Select((card) => card.SessionStateId).Distinct(StringComparer.Ordinal).Count() != cards.Count)
        {
            throw new InvalidOperationException("Top-level editor card session state ids must be unique.");
        }
    }
}
