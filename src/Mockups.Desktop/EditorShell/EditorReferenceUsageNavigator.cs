using Mockups.DesktopEditorShell.Data;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorReferenceUsageNavigator
{
    private readonly Func<EditorWorkspace, string, bool> _selectNodeInWorkspace;
    private readonly Func<SpikeDatabase.EmbeddedComponentUsage, string, Task> _navigateToEmbeddedUsage;
    private readonly IEditorShellMessageSink _messages;

    public EditorReferenceUsageNavigator(
        Func<EditorWorkspace, string, bool> selectNodeInWorkspace,
        Func<SpikeDatabase.EmbeddedComponentUsage, string, Task> navigateToEmbeddedUsage,
        IEditorShellMessageSink messages)
    {
        _selectNodeInWorkspace = selectNodeInWorkspace;
        _navigateToEmbeddedUsage = navigateToEmbeddedUsage;
        _messages = messages;
    }

    public async Task Navigate(SpikeDatabase.ReferenceUsageDetail usage)
    {
        var workspace = usage.Scope switch
        {
            ReferenceUsageScope.Design => EditorWorkspace.Design,
            ReferenceUsageScope.Production => EditorWorkspace.Production,
            _ => throw new InvalidOperationException($"Unknown Usage scope '{usage.Scope}'."),
        };
        if (!_selectNodeInWorkspace(workspace, usage.SourceNodeId))
        {
            _messages.Warning(
                "Open Usage reference",
                $"Could not find {usage.SourceTypeLabel} '{usage.SourceName}' ({usage.SourceNodeId}).");
            return;
        }

        if (usage.EmbeddedUsage is not null)
        {
            await _navigateToEmbeddedUsage(usage.EmbeddedUsage, usage.SourceNodeId);
        }
    }
}
