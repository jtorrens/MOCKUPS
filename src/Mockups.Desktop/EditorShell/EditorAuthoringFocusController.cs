using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorAuthoringFocusRequest(
    string OwnerId,
    string RecordClassId,
    IReadOnlyList<string> SlotFieldIds,
    string FieldId,
    string ItemId = "",
    EditorAuthoringFocusSurface Surface = EditorAuthoringFocusSurface.Editor,
    PreviewAuthoringRuntimeComponentSlotTarget? RuntimeComponentSlot = null,
    IReadOnlyList<string>? RuntimeNestedSlotFieldIds = null);

internal enum EditorAuthoringFocusSurface
{
    Editor,
    PreviewAuthoring,
}

internal interface IEditorAuthoringItemTarget
{
    string FieldId { get; }

    bool SelectItem(string itemId);
}

internal interface IEditorAuthoringRuntimeComponentTarget
{
    string FieldId { get; }

    Task<bool> OpenRuntimeComponentOverridesAsync(
        string itemId,
        string slotFieldId);
}

internal interface IEditorAuthoringSectionTarget
{
    bool RevealAuthoringTarget(string fieldId, bool selectsItem);
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

    internal void Cancel() => _pending = null;

    internal bool ApplyRoot(
        ProjectTreeNode owner,
        IReadOnlyList<EditorPreparedLayoutCard> preparedCards,
        IReadOnlyList<InstantEditorCard> cards) =>
        ApplyEditor(
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
        ApplyEditor(
            context.OwnerNode.Id,
            context.RecordClassId,
            context.Slots.Select((slot) => slot.FieldId).ToArray(),
            "embedded:",
            preparedCards,
            cards);

    internal bool ApplyPreviewAuthoring(
        ProjectTreeNode owner,
        Control content)
    {
        if (_pending is not { } pending
            || pending.Surface != EditorAuthoringFocusSurface.PreviewAuthoring
            || !pending.OwnerId.Equals(owner.Id, StringComparison.Ordinal)
            || !pending.RecordClassId.Equals(owner.RecordClassId, StringComparison.Ordinal)
            || pending.SlotFieldIds.Count > 0)
        {
            return false;
        }

        _pending = null;
        if (pending.RuntimeComponentSlot is { } runtimeSlot)
        {
            return OpenRuntimeComponentTarget(
                owner,
                content,
                pending,
                runtimeSlot);
        }
        RevealPreviewAuthoringSection(
            content,
            pending.FieldId,
            !string.IsNullOrWhiteSpace(pending.ItemId));
        if (!string.IsNullOrWhiteSpace(pending.ItemId))
        {
            var itemTargets = content
                .GetLogicalDescendants()
                .OfType<IEditorAuthoringItemTarget>()
                .Where((target) => target.FieldId.Equals(
                    pending.FieldId,
                    StringComparison.Ordinal))
                .ToArray();
            if (itemTargets.Length != 1
                || !itemTargets[0].SelectItem(pending.ItemId))
            {
                _messages.Warning(
                    "Preview element",
                    $"Preview authoring field '{pending.FieldId}' has no item '{pending.ItemId}'.");
                return false;
            }
            if (itemTargets[0] is Control itemControl)
            {
                DeferredBringIntoView.Request(itemControl);
            }
            return true;
        }

        var fields = content
            .GetLogicalDescendants()
            .OfType<DictionaryFieldControl>()
            .Where((field) => field.FieldId.Equals(
                pending.FieldId,
                StringComparison.Ordinal))
            .ToArray();
        if (fields.Length != 1)
        {
            _messages.Warning(
                "Preview element",
                fields.Length == 0
                    ? $"Preview authoring field '{pending.FieldId}' is unavailable."
                    : $"More than one Preview authoring control owns '{pending.FieldId}'.");
            return false;
        }
        DeferredBringIntoView.Request(fields[0]);
        return true;
    }

    private bool OpenRuntimeComponentTarget(
        ProjectTreeNode owner,
        Control content,
        EditorAuthoringFocusRequest pending,
        PreviewAuthoringRuntimeComponentSlotTarget runtimeSlot)
    {
        RevealPreviewAuthoringSection(
            content,
            runtimeSlot.CollectionFieldId,
            selectsItem: true);
        var targets = content
            .GetLogicalDescendants()
            .OfType<IEditorAuthoringRuntimeComponentTarget>()
            .Where((target) => target.FieldId.Equals(
                runtimeSlot.CollectionFieldId,
                StringComparison.Ordinal))
            .ToArray();
        if (targets.Length != 1)
        {
            _messages.Warning(
                "Preview element",
                targets.Length == 0
                    ? $"Preview authoring field '{runtimeSlot.CollectionFieldId}' cannot open a Runtime Component slot."
                    : $"More than one Preview authoring control owns '{runtimeSlot.CollectionFieldId}'.");
            return false;
        }

        _pending = new EditorAuthoringFocusRequest(
            owner.Id,
            runtimeSlot.RecordClassId,
            pending.RuntimeNestedSlotFieldIds ?? [],
            pending.FieldId,
            pending.ItemId);
        var open = targets[0].OpenRuntimeComponentOverridesAsync(
            runtimeSlot.ItemId,
            runtimeSlot.SlotFieldId);
        if (!open.IsCompletedSuccessfully)
        {
            _ = ObserveRuntimeComponentOpenAsync(open);
            return true;
        }
        if (open.Result) return true;
        _pending = null;
        _messages.Warning(
            "Preview element",
            $"Runtime collection '{runtimeSlot.CollectionFieldId}' has no Component slot '{runtimeSlot.SlotFieldId}' on item '{runtimeSlot.ItemId}'.");
        return false;
    }

    private async Task ObserveRuntimeComponentOpenAsync(Task<bool> open)
    {
        try
        {
            if (await open) return;
            _pending = null;
            _messages.Warning(
                "Preview element",
                "The exact Runtime Component slot is unavailable.");
        }
        catch (Exception error)
        {
            _pending = null;
            _messages.Error("Preview element", error);
        }
    }

    private static void RevealPreviewAuthoringSection(
        Control content,
        string fieldId,
        bool selectsItem)
    {
        var navigators = content
            .GetLogicalDescendants()
            .OfType<IEditorAuthoringSectionTarget>()
            .ToArray();
        if (content is IEditorAuthoringSectionTarget root)
        {
            navigators = [root, .. navigators];
        }
        foreach (var navigator in navigators)
        {
            if (navigator.RevealAuthoringTarget(fieldId, selectsItem))
            {
                return;
            }
        }
    }

    private bool ApplyEditor(
        string ownerId,
        string recordClassId,
        IReadOnlyList<string> slotFieldIds,
        string cardPrefix,
        IReadOnlyList<EditorPreparedLayoutCard> preparedCards,
        IReadOnlyList<InstantEditorCard> cards)
    {
        if (_pending is not { } pending
            || pending.Surface != EditorAuthoringFocusSurface.Editor
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
                    .SelectMany((group) =>
                        group.VisibleFieldsFor(prepared.Fields))
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

        _cancelViewRestore();
        card.IsExpanded = true;
        DeferredBringIntoView.Request(card);

        if (!string.IsNullOrWhiteSpace(pending.ItemId))
        {
            var itemTargets = card
                .GetLogicalDescendants()
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

        return true;
    }
}
