using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorResourceCleanupWorkflow(
    Window owner, IResourceAssetCleanupStore store, EditorOperationCoordinator operations,
    Func<bool> isDark, IEditorShellMessageSink messages)
{
    public async Task Show()
    {
        var dialogs = new EditorDialogService(owner, isDark());
        try
        {
            var pending = await operations.ExecuteAsync(store.GetPending);
            if (pending.Count == 0)
            {
                await dialogs.ShowInfo("Resource cleanup", "There are no pending resource file deletions.");
                return;
            }
            foreach (var item in pending)
            {
                if (!await dialogs.ConfirmAction("Resource cleanup", item.Label,
                        $"The record deletion was committed. Retry deleting its retained files?\n\n{item.Path}\n\n{item.Error}",
                        "Retry cleanup", width: 640, height: 400)) return;
                await operations.ExecuteAsync(() => store.Retry(item.Id));
            }
            var remaining = await operations.ExecuteAsync(store.GetPending);
            await dialogs.ShowInfo("Resource cleanup", remaining.Count == 0
                ? "All pending resource files were cleaned up."
                : $"{remaining.Count} cleanup operations remain pending. Files that changed or are in use were retained. Review Resource cleanup again to see the current errors.");
        }
        catch (Exception exception)
        {
            messages.Error("Resource cleanup", exception);
        }
    }
}
