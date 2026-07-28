using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record PreviewAuthoringNavigationTarget(
    string OwnerId,
    IReadOnlyList<string> SlotFieldIds,
    string FocusFieldId = "");

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
            || document.Count is < 2 or > 3
            || document.Any((property) =>
                property.Key is not
                    "ownerId"
                    and not "slotFieldIds"
                    and not "focusFieldId")
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

        target = new PreviewAuthoringNavigationTarget(
            ownerId,
            slotFieldIds,
            focusFieldId);
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
            slots.Length > 0
                ? slots[^1].RecordClassId
                : owner.RecordClassId,
            target.SlotFieldIds,
            target.FocusFieldId));
        if (slots.Length > 0)
        {
            _showEmbeddedContext(new EditorEmbeddedContext(owner, slots));
        }
        return true;
    }
}
