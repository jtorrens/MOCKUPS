using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record PreviewAuthoringNavigationTarget(
    string OwnerId,
    IReadOnlyList<string> SlotFieldIds,
    string FocusFieldId = "",
    string FocusItemId = "",
    PreviewAuthoringRuntimeComponentSlotTarget? RuntimeComponentSlot = null);

internal sealed record PreviewAuthoringRuntimeComponentSlotTarget(
    string CollectionFieldId,
    string ItemId,
    string SlotFieldId,
    string RecordClassId);

internal static class PreviewAuthoringNavigationMessage
{
    internal const string Prefix = "mockups-preview-authoring:";

    internal static bool TryParse(
        string message,
        out PreviewAuthoringNavigationTarget target)
    {
        target = new PreviewAuthoringNavigationTarget("", []);
        if (!message.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        JsonObject? document;
        try
        {
            document = JsonNode.Parse(message[Prefix.Length..]) as JsonObject;
        }
        catch
        {
            return false;
        }

        if (document is null
            || document.Count is < 2 or > 5
            || document.Any((property) =>
                property.Key is not
                    "ownerId"
                    and not "slotFieldIds"
                    and not "focusFieldId"
                    and not "focusItemId"
                    and not "runtimeComponentSlot")
            || document["ownerId"] is not JsonValue ownerValue
            || !ownerValue.TryGetValue<string>(out var ownerId)
            || string.IsNullOrWhiteSpace(ownerId)
            || document["slotFieldIds"] is not JsonArray slotArray)
        {
            return false;
        }

        var slotFieldIds = new List<string>(slotArray.Count);
        foreach (var item in slotArray)
        {
            if (item is not JsonValue value
                || !value.TryGetValue<string>(out var fieldId)
                || string.IsNullOrWhiteSpace(fieldId))
            {
                return false;
            }
            slotFieldIds.Add(fieldId);
        }

        var focusFieldId = "";
        if (document.TryGetPropertyValue(
                "focusFieldId",
                out var focusNode)
            && (focusNode is not JsonValue focusValue
                || !focusValue.TryGetValue<string>(
                    out focusFieldId)
                || string.IsNullOrWhiteSpace(focusFieldId)))
        {
            return false;
        }

        var focusItemId = "";
        if (document.TryGetPropertyValue(
                "focusItemId",
                out var focusItemNode)
            && (string.IsNullOrWhiteSpace(focusFieldId)
                || focusItemNode is not JsonValue focusItemValue
                || !focusItemValue.TryGetValue<string>(
                    out focusItemId)
                || string.IsNullOrWhiteSpace(focusItemId)))
        {
            return false;
        }

        PreviewAuthoringRuntimeComponentSlotTarget? runtimeComponentSlot = null;
        if (document.TryGetPropertyValue(
                "runtimeComponentSlot",
                out var runtimeSlotNode))
        {
            if (runtimeSlotNode is not JsonObject runtimeSlot
                || runtimeSlot.Count != 4
                || !RequiredString(runtimeSlot, "collectionFieldId", out var collectionFieldId)
                || !RequiredString(runtimeSlot, "itemId", out var runtimeItemId)
                || !RequiredString(runtimeSlot, "slotFieldId", out var slotFieldId)
                || !RequiredString(runtimeSlot, "recordClassId", out var recordClassId))
            {
                return false;
            }
            runtimeComponentSlot = new(
                collectionFieldId,
                runtimeItemId,
                slotFieldId,
                recordClassId);
        }

        target = new PreviewAuthoringNavigationTarget(
            ownerId,
            slotFieldIds,
            focusFieldId,
            focusItemId,
            runtimeComponentSlot);
        return true;
    }

    private static bool RequiredString(
        JsonObject document,
        string key,
        out string value)
    {
        value = "";
        if (document[key] is not JsonValue node
            || !node.TryGetValue<string>(out var parsed)
            || string.IsNullOrWhiteSpace(parsed))
        {
            return false;
        }
        value = parsed;
        return true;
    }
}

internal sealed class PreviewAuthoringNavigator
{
    private readonly Func<ProjectTreeNode?> _selectedNode;
    private readonly Func<string, bool> _selectNodeById;
    private readonly Action<EditorEmbeddedContext> _showEmbeddedContext;
    private readonly Action<EditorAuthoringFocusRequest> _requestFocus;
    private readonly IEditorShellMessageSink _messages;

    internal PreviewAuthoringNavigator(
        Func<ProjectTreeNode?> selectedNode,
        Func<string, bool> selectNodeById,
        Action<EditorEmbeddedContext> showEmbeddedContext,
        Action<EditorAuthoringFocusRequest> requestFocus,
        IEditorShellMessageSink messages)
    {
        _selectedNode = selectedNode;
        _selectNodeById = selectNodeById;
        _showEmbeddedContext = showEmbeddedContext;
        _requestFocus = requestFocus;
        _messages = messages;
    }

    internal bool Navigate(PreviewAuthoringNavigationTarget target)
    {
        EmbeddedComponentSlotDefinition[] slots;
        try
        {
            slots = target.SlotFieldIds
                .Select(EmbeddedComponentSlotCatalog.Get)
                .ToArray();
        }
        catch (Exception error)
        {
            _messages.Error("Preview element", error);
            return false;
        }

        if (!_selectNodeById(target.OwnerId)
            || _selectedNode() is not { } owner
            || !owner.Id.Equals(target.OwnerId, StringComparison.Ordinal))
        {
            _messages.Warning(
                "Preview element",
                $"The exact authoring owner '{target.OwnerId}' is unavailable.");
            return false;
        }

        _requestFocus(new EditorAuthoringFocusRequest(
            owner.Id,
            target.RuntimeComponentSlot is not null
                ? owner.RecordClassId
                : slots.Length > 0
                ? slots[^1].RecordClassId
                : owner.RecordClassId,
            target.RuntimeComponentSlot is not null
                ? []
                : target.SlotFieldIds,
            target.FocusFieldId,
            target.FocusItemId,
            target.RuntimeComponentSlot is not null
                || owner.Kind == ProjectTreeNodeKind.ModuleInstance
                && slots.Length == 0
                ? EditorAuthoringFocusSurface.PreviewAuthoring
                : EditorAuthoringFocusSurface.Editor,
            target.RuntimeComponentSlot,
            target.SlotFieldIds));
        if (slots.Length > 0 && target.RuntimeComponentSlot is null)
        {
            _showEmbeddedContext(new EditorEmbeddedContext(owner, slots));
        }
        return true;
    }
}
