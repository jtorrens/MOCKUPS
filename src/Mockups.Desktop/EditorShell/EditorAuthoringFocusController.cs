using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.VisualTree;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorAuthoringFocusRequest(
    string OwnerId,
    string RecordClassId,
    IReadOnlyList<string> SlotFieldIds,
    string FieldId,
    string ItemId = "");

internal interface IEditorAuthoringItemTarget
{
    string FieldId { get; }

    bool SelectItem(string itemId);
}

internal sealed class EditorAuthoringFocusController
{
    private readonly Action _cancelViewRestore;
    private readonly IEditorShellMessageSink _messages;
    private EditorAuthoringFocusRequest? _pending;

    internal EditorAuthoringFocusController(
        Action cancelViewRestore,
        IEditorShellMessageSink messages)
    {
        _cancelViewRestore = cancelViewRestore;
        _messages = messages;
    }

    internal void Request(EditorAuthoringFocusRequest request)
    {
        _pending = string.IsNullOrWhiteSpace(request.FieldId)
            ? null
            : request;
    }

    internal bool ApplyRoot(
        ProjectTreeNode owner,
        IReadOnlyList<EditorPreparedLayoutCard> preparedCards,
        IReadOnlyList<InstantEditorCard> cards) =>
        Apply(
            owner.Id,
            owner.RecordClassId,
            [],
            "layout:",
            preparedCards,
            cards);

    internal bool ApplyEmbedded(
        EditorEmbeddedContext context,
        IReadOnlyList<EditorPreparedLayoutCard> preparedCards,
        IReadOnlyList<InstantEditorCard> cards) =>
        Apply(
            context.OwnerNode.Id,
            context.RecordClassId,
            context.Slots.Select((slot) => slot.FieldId).ToArray(),
            "embedded:",
            preparedCards,
            cards);

    private bool Apply(
        string ownerId,
        string recordClassId,
        IReadOnlyList<string> slotFieldIds,
        string cardPrefix,
        IReadOnlyList<EditorPreparedLayoutCard> preparedCards,
        IReadOnlyList<InstantEditorCard> cards)
    {
        if (_pending is not { } pending
            || !pending.OwnerId.Equals(ownerId, StringComparison.Ordinal)
            || !pending.RecordClassId.Equals(
                recordClassId,
                StringComparison.Ordinal)
            || !pending.SlotFieldIds.SequenceEqual(
                slotFieldIds,
                StringComparer.Ordinal))
        {
            return false;
        }

        _pending = null;
        var matchingLayouts = preparedCards
            .Where((prepared) =>
                prepared.Layout.Visible
                && prepared.Layout.VisibleGroups
                    .SelectMany((group) => group.VisibleFields)
                    .Any((field) => field.Id.Equals(
                        pending.FieldId,
                        StringComparison.Ordinal)))
            .Select((prepared) => prepared.Layout)
            .ToArray();
        if (matchingLayouts.Length != 1)
        {
            _messages.Warning(
                "Preview element",
                matchingLayouts.Length == 0
                    ? $"No visible editor card contains '{pending.FieldId}'."
                    : $"More than one editor card contains '{pending.FieldId}'.");
            return false;
        }

        var sessionStateId =
            $"{cardPrefix}{matchingLayouts[0].Id}";
        var card = cards.SingleOrDefault((candidate) =>
            candidate.SessionStateId.Equals(
                sessionStateId,
                StringComparison.Ordinal));
        if (card is null)
        {
            _messages.Warning(
                "Preview element",
                $"The editor card '{sessionStateId}' is unavailable.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(pending.ItemId))
        {
            var itemTargets = card
                .GetVisualDescendants()
                .OfType<IEditorAuthoringItemTarget>()
                .Where((target) => target.FieldId.Equals(
                    pending.FieldId,
                    StringComparison.Ordinal))
                .ToArray();
            if (itemTargets.Length != 1)
            {
                _messages.Warning(
                    "Preview element",
                    itemTargets.Length == 0
                        ? $"Editor field '{pending.FieldId}' cannot select an authored item."
                        : $"More than one editor control owns '{pending.FieldId}'.");
                return false;
            }
            if (!itemTargets[0].SelectItem(pending.ItemId))
            {
                _messages.Warning(
                    "Preview element",
                    $"Editor field '{pending.FieldId}' has no item '{pending.ItemId}'.");
                return false;
            }
        }

        _cancelViewRestore();
        card.IsExpanded = true;
        DeferredBringIntoView.Request(card);
        return true;
    }
}
