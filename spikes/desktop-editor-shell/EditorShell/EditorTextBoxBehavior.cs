using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorTextBoxBehavior
{
    public static TextBox Configure(TextBox textBox)
    {
        textBox.ClearSelectionOnLostFocus = false;
        textBox.ContextFlyout = null;
        textBox.ContextMenu = null;
        textBox.Padding = new Thickness(6, textBox.Padding.Top, 6, textBox.Padding.Bottom);
        return textBox;
    }

    public static void AttachDeferredCommit(TextBox textBox, Action commit, bool commitOnEnter = true)
    {
        textBox.LostFocus += (_, _) => commit();
        textBox.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter || !commitOnEnter) return;

            commit();
            args.Handled = true;
        };
    }
}
