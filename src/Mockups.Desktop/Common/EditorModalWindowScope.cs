using Avalonia.Controls;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.Common;

internal static class EditorModalWindowScope
{
    public static async Task ShowDialog(Window dialog, Window owner)
    {
        var restore = Prepare(dialog);
        try
        {
            await dialog.ShowDialog(owner);
        }
        finally
        {
            restore();
        }
    }

    public static async Task<TResult?> ShowDialog<TResult>(
        Window dialog,
        Window owner)
    {
        var restore = Prepare(dialog);
        try
        {
            return await dialog.ShowDialog<TResult?>(owner);
        }
        finally
        {
            restore();
        }
    }

    private static Action Prepare(Window dialog)
    {
        var wasTopmost = dialog.Topmost;
        EventHandler? opened = null;
        opened = (_, _) =>
        {
            dialog.Opened -= opened;
            dialog.Topmost = true;
            MacOsModalWindowLevel.Raise(dialog);
            dialog.Activate();
        };

        dialog.ShowActivated = true;
        dialog.Opened += opened;
        return () =>
        {
            dialog.Opened -= opened;
            dialog.Topmost = wasTopmost;
        };
    }
}
