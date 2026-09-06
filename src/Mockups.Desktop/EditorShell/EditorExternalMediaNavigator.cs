using Mockups.DesktopEditorShell.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorExternalMediaNavigator
{
    private readonly Func<EditorWorkspace, string, Task<bool>>
        _selectNodeInWorkspace;
    private readonly Func<ProjectTreeNode?> _selectedNode;
    private readonly Action<EditorEmbeddedContext> _showEmbeddedContext;
    private readonly EditorAuthoringFocusController _focus;
    private readonly IEditorShellMessageSink _messages;

    public EditorExternalMediaNavigator(
        Func<EditorWorkspace, string, Task<bool>> selectNodeInWorkspace,
        Func<ProjectTreeNode?> selectedNode,
        Action<EditorEmbeddedContext> showEmbeddedContext,
        EditorAuthoringFocusController focus,
        IEditorShellMessageSink messages)
    {
        _selectNodeInWorkspace = selectNodeInWorkspace;
        _selectedNode = selectedNode;
        _showEmbeddedContext = showEmbeddedContext;
        _focus = focus;
        _messages = messages;
    }

    public async Task Navigate(ExternalMediaUsageDetail usage)
    {
        var slots = usage.SlotFieldIds
            .Select(EmbeddedComponentSlotCatalog.Get)
            .ToArray();
        var targetRecordClassId = slots.Length > 0
            ? slots[^1].RecordClassId
            : usage.SourceRecordClassId;
        _focus.Request(new EditorAuthoringFocusRequest(
            usage.SourceNodeId,
            targetRecordClassId,
            usage.SlotFieldIds,
            usage.FieldId,
            usage.ItemId,
            usage.AuthoringSurface == ExternalMediaAuthoringSurface.PreviewAuthoring
                ? EditorAuthoringFocusSurface.PreviewAuthoring
                : EditorAuthoringFocusSurface.Editor));

        var workspace = usage.IsProduction
            ? EditorWorkspace.Production
            : EditorWorkspace.Design;
        if (!await _selectNodeInWorkspace(workspace, usage.SourceNodeId))
        {
            _focus.Cancel();
            _messages.Warning(
                "External Media",
                $"Could not find {usage.SourceTypeLabel} '{usage.SourceName}' ({usage.SourceNodeId}).");
            return;
        }

        if (slots.Length == 0) return;
        if (usage.AuthoringSurface == ExternalMediaAuthoringSurface.PreviewAuthoring)
        {
            _focus.Cancel();
            _messages.Warning(
                "External Media",
                $"Preview authoring usage '{usage.FieldLabel}' cannot cross an embedded editor boundary.");
            return;
        }
        if (_selectedNode() is not { } owner
            || !owner.Id.Equals(usage.SourceNodeId, StringComparison.Ordinal))
        {
            _focus.Cancel();
            return;
        }
        _showEmbeddedContext(new EditorEmbeddedContext(owner, slots));
    }
}
