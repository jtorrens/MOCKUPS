using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorEmbeddedEditorController
{
    private readonly Action<EditorEmbeddedContext> _showContext;
    private readonly IEditorShellMessageSink _messages;

    public EditorEmbeddedEditorController(
        Action<EditorEmbeddedContext> showContext,
        IEditorShellMessageSink messages)
    {
        _showContext = showContext;
        _messages = messages;
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
