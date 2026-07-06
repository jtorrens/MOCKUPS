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
            if (node.Kind is not ProjectTreeNodeKind.ComponentClass and not ProjectTreeNodeKind.ComponentPreset)
            {
                return Task.CompletedTask;
            }

            if (!EmbeddedComponentSlotCatalog.TryGet(slotFieldId, out var slot))
            {
                return Task.CompletedTask;
            }

            _showContext(new EditorEmbeddedContext(node, [slot]));
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

            _showContext(new EditorEmbeddedContext(parentContext.OwnerNode, [.. parentContext.Slots, slot]));
        }
        catch (Exception exception)
        {
            _messages.Error($"Embedded component {slotFieldId}", exception);
        }

        return Task.CompletedTask;
    }
}
