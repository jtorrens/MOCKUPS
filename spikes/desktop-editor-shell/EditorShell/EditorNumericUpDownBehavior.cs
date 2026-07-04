using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorNumericUpDownBehavior
{
    public static NumericUpDown Configure(NumericUpDown numeric)
    {
        numeric.ContextFlyout = null;
        numeric.ContextMenu = null;
        numeric.AttachedToVisualTree += (_, _) =>
        {
            Dispatcher.UIThread.Post(() => ConfigureInnerTextBoxes(numeric), DispatcherPriority.Loaded);
        };
        numeric.Loaded += (_, _) => ConfigureInnerTextBoxes(numeric);
        return numeric;
    }

    private static void ConfigureInnerTextBoxes(NumericUpDown numeric)
    {
        foreach (var textBox in numeric.GetVisualDescendants().OfType<TextBox>())
        {
            EditorTextBoxBehavior.Configure(textBox);
        }
    }
}
