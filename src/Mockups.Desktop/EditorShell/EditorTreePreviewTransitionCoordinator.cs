using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorTreePreviewTransitionCoordinator
{
    private readonly EditorWorkspaceCoordinator _workspace;
    private readonly EditorPreviewController _preview;

    public EditorTreePreviewTransitionCoordinator(
        EditorWorkspaceCoordinator workspace,
        EditorPreviewController preview)
    {
        _workspace = workspace
            ?? throw new ArgumentNullException(nameof(workspace));
        _preview = preview
            ?? throw new ArgumentNullException(nameof(preview));
    }

    public Task<EditorSessionTransition?> ReloadAsync(
        string source = "tree-load",
        EditorTreeLoadIntent intent =
            EditorTreeLoadIntent.Workspace)
    {
        return PrepareAndCommitAsync(
            _workspace.PrepareTreeReloadAsync(intent),
            source);
    }

    public Task<EditorSessionTransition?> SwitchWorkspaceAsync(
        EditorWorkspace workspace,
        string source = "workspace")
    {
        return PrepareAndCommitAsync(
            _workspace.PrepareWorkspaceSwitchAsync(
                workspace),
            source);
    }

    private async Task<EditorSessionTransition?>
        PrepareAndCommitAsync(
            Task<EditorTreeLoadPreparation?> treeTask,
            string source)
    {
        var tree = await treeTask;
        if (tree is null)
        {
            return null;
        }

        PreviewOptionsPreparation? preview = null;
        try
        {
            preview = await _preview.PrepareOptionsAsync(
                tree.TreeRoots,
                tree.Token);
            if (preview is null)
            {
                _workspace.DiscardTreeLoad(tree);
                return null;
            }
            if (!_workspace.IsCurrentTreeLoad(tree)
                || !_preview.IsCurrentOptionsPreparation(
                    preview))
            {
                _preview.DiscardOptions(preview);
                _workspace.DiscardTreeLoad(tree);
                return null;
            }

            if (!_preview.TryCommitOptions(
                    preview,
                    applyVisualState: false))
            {
                _workspace.DiscardTreeLoad(tree);
                return null;
            }
            if (!_workspace.TryCommitTreeLoad(
                    tree,
                    source,
                    out var transition))
            {
                _preview.RestoreCommittedOptions(preview);
                return null;
            }

            _preview.ApplyCommittedOptions(preview);
            return transition;
        }
        catch
        {
            if (preview is not null
                && _preview.IsCurrentOptionsPreparation(
                    preview))
            {
                _preview.DiscardOptions(preview);
            }
            _workspace.DiscardTreeLoad(tree);
            throw;
        }
    }
}
