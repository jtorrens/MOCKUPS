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
    private EventHandler? _pendingLayoutUpdated;
    private Vector? _pendingScrollOffset;
    private long _scrollRestoreRevision;

    public EditorViewStateController(
        ScrollViewer scrollViewer,
        EditorSessionViewStateStore? sessionStates = null)
    {
        _scrollViewer = scrollViewer;
        _sessionStates = sessionStates ?? new EditorSessionViewStateStore();
    }

    public void Capture(ProjectTreeNode? node, IReadOnlyList<InstantEditorCard> cards)
    {
        if (node is null) return;
        Capture(StateKey(node), cards);
    }

    public void Capture(string recordClassId, IReadOnlyList<InstantEditorCard> cards)
    {
        var state = CaptureState(cards);
        if (state is not null)
        {
            _sessionStates.Set(recordClassId, state);
        }
    }

    public void Restore(ProjectTreeNode node, IReadOnlyList<InstantEditorCard> cards)
    {
        Restore(StateKey(node), cards);
    }

    public void Restore(string recordClassId, IReadOnlyList<InstantEditorCard> cards)
    {
        var state = _sessionStates.Get(recordClassId);
        if (state is null)
        {
            ScheduleScrollRestore(default);
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
            _pendingScrollOffset ?? _scrollViewer.Offset);
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

        ScheduleScrollRestore(state.ScrollOffset);
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

    internal void CancelPendingRestore() =>
        CancelPendingScrollRestore();

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

    private void ScheduleScrollRestore(Vector requested)
    {
        CancelPendingScrollRestore();
        var revision = ++_scrollRestoreRevision;
        _pendingScrollOffset = requested;

        EventHandler? layoutUpdated = null;
        layoutUpdated = (_, _) =>
        {
            if (revision != _scrollRestoreRevision
                || !ReferenceEquals(_pendingLayoutUpdated, layoutUpdated)
                || _scrollViewer.Viewport.Height <= 0)
            {
                return;
            }

            _scrollViewer.LayoutUpdated -= layoutUpdated;
            _pendingLayoutUpdated = null;
            _pendingScrollOffset = null;
            _scrollViewer.Offset = ClampOffset(
                requested,
                _scrollViewer.Extent,
                _scrollViewer.Viewport);
        };
        _pendingLayoutUpdated = layoutUpdated;
        _scrollViewer.LayoutUpdated += layoutUpdated;

        Dispatcher.UIThread.Post(
            () =>
            {
                if (revision == _scrollRestoreRevision)
                {
                    _scrollViewer.InvalidateMeasure();
                }
            },
            DispatcherPriority.Loaded);
    }

    private void CancelPendingScrollRestore()
    {
        _scrollRestoreRevision++;
        if (_pendingLayoutUpdated is not null)
        {
            _scrollViewer.LayoutUpdated -= _pendingLayoutUpdated;
        }
        _pendingLayoutUpdated = null;
        _pendingScrollOffset = null;
    }
}
