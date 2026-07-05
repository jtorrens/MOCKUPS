using Avalonia.Controls;
using Avalonia;
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
}
