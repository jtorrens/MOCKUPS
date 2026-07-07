using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorTextBoxBehavior
{
    private static readonly FontFamily TextFontFamily = new(
        "Inter, Apple Color Emoji, Segoe UI Emoji, Noto Color Emoji, sans-serif");

    public static TextBox Configure(TextBox textBox)
    {
        textBox.ClearSelectionOnLostFocus = false;
        textBox.ContextFlyout = null;
        textBox.ContextMenu = null;
        textBox.FontFamily = TextFontFamily;
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
