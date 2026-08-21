using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorEmbeddedEditorController
{
    private readonly Action<EditorEmbeddedContext> _showContext;
    private readonly Func<IReadOnlyList<ProjectTreeNode>>
        _treeRoots;
    private readonly IEditorShellMessageSink _messages;

    public EditorEmbeddedEditorController(
        Action<EditorEmbeddedContext> showContext,
        Func<IReadOnlyList<ProjectTreeNode>> treeRoots,
        IEditorShellMessageSink messages)
    {
        _showContext = showContext;
        _treeRoots = treeRoots;
        _messages = messages;
    }

    public Task OpenRecordReferenceOverrides(
        ProjectTreeNode ownerNode,
        FieldDefinition definition,
        string referenceId)
    {
        try
        {
            var contract = definition.RecordReference
                ?? throw new InvalidOperationException(
                    $"Field '{definition.Id}' is not a RecordReference.");
            if (string.IsNullOrWhiteSpace(
                    contract.OverrideRecordClassId)
                || string.IsNullOrWhiteSpace(
                    contract.OverrideDocumentFieldId)
                || contract.OverrideFieldIds is null
                || contract.OverrideFieldIds.Count == 0)
            {
                throw new InvalidOperationException(
                    $"RecordReference '{definition.Id}' does not declare a complete Overrides contract.");
            }
            var referenceNode =
                EditorNodeSelectionState.FindNodeById(
                    _treeRoots(),
                    referenceId)
                ?? throw new InvalidOperationException(
                    $"Record reference '{referenceId}' is not present in the current project tree.");
            if (!referenceNode.RecordClassId.Equals(
                    contract.OverrideRecordClassId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Record reference '{referenceId}' has class '{referenceNode.RecordClassId}', expected '{contract.OverrideRecordClassId}'.");
            }
            _showContext(
                EditorEmbeddedContext
                    .ForRecordReferenceOverride(
                        ownerNode,
                        referenceNode,
                        definition.Id,
                        contract.OverrideDocumentFieldId));
        }
        catch (Exception exception)
        {
            _messages.Error(
                $"RecordReference Overrides {definition.Id}",
                exception);
        }
        return Task.CompletedTask;
    }

    public Task Open(ProjectTreeNode node, string slotFieldId)
    {
        try
        {
            if (node.Kind is not ProjectTreeNodeKind.ComponentClass and not ProjectTreeNodeKind.ComponentVariant and not ProjectTreeNodeKind.Module and not ProjectTreeNodeKind.ModuleVariant)
            {
                return Task.CompletedTask;
            }

            if (!EmbeddedComponentSlotCatalog.TryGet(slotFieldId, out var slot))
            {
                return Task.CompletedTask;
            }

            return OpenSlot(node, slot);
        }
        catch (Exception exception)
        {
            _messages.Error($"Embedded component {slotFieldId}", exception);
        }

        return Task.CompletedTask;
    }

    public Task OpenNested(EditorEmbeddedContext parentContext, string slotFieldId)
    {
        try
        {
            if (!EmbeddedComponentSlotCatalog.TryGet(slotFieldId, out var slot))
            {
                return Task.CompletedTask;
            }

            return OpenNestedSlot(parentContext, slot);
        }
        catch (Exception exception)
        {
            _messages.Error($"Embedded component {slotFieldId}", exception);
        }

        return Task.CompletedTask;
    }

    public Task OpenSlot(ProjectTreeNode node, EmbeddedComponentSlotDefinition slot)
    {
        try
        {
            if (node.Kind is ProjectTreeNodeKind.ComponentClass or ProjectTreeNodeKind.ComponentVariant or ProjectTreeNodeKind.Module or ProjectTreeNodeKind.ModuleVariant)
            {
                _showContext(new EditorEmbeddedContext(node, [slot]));
            }
        }
        catch (Exception exception)
        {
            _messages.Error($"Embedded component {slot.FieldId}", exception);
        }

        return Task.CompletedTask;
    }

    public Task OpenNestedSlot(EditorEmbeddedContext parentContext, EmbeddedComponentSlotDefinition slot)
    {
        try
        {
            _showContext(parentContext.Nested(slot));
        }
        catch (Exception exception)
        {
            _messages.Error($"Embedded component {slot.FieldId}", exception);
        }

        return Task.CompletedTask;
    }
}
