using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell.Common;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorNumericUpDownBehavior
{
    public const double CompactHorizontalPadding = 2;

    public static NumericUpDown Configure(NumericUpDown numeric) =>
        Configure(numeric, compact: false);

    public static NumericUpDown ConfigureCompact(NumericUpDown numeric) =>
        Configure(numeric, compact: true);

    private static NumericUpDown Configure(NumericUpDown numeric, bool compact)
    {
        EditorNumericTextStyle.Apply(numeric);
        EditorContextMenuBehavior.Configure(numeric);
        numeric.ContextFlyout = null;
        numeric.ContextMenu = null;
        if (compact)
        {
            numeric.Padding = new Thickness(0);
        }
        numeric.AttachedToVisualTree += (_, _) =>
        {
            Dispatcher.UIThread.Post(
                () => ConfigureInnerTextBoxes(numeric, compact),
                DispatcherPriority.Loaded);
        };
        numeric.Loaded += (_, _) => ConfigureInnerTextBoxes(numeric, compact);
        return numeric;
    }

    private static void ConfigureInnerTextBoxes(NumericUpDown numeric, bool compact)
    {
        if (compact)
        {
            var spinner = numeric
                .GetVisualDescendants()
                .OfType<Control>()
                .FirstOrDefault((control) => control.Name == "PART_Spinner");
            if (spinner is not null)
            {
                spinner.Margin = new Thickness(0);
            }
        }
        foreach (var textBox in numeric.GetVisualDescendants().OfType<TextBox>())
        {
            EditorNumericTextStyle.Apply(textBox);
            if (compact)
            {
                textBox.Padding = new Thickness(
                    CompactHorizontalPadding,
                    textBox.Padding.Top,
                    CompactHorizontalPadding,
                    textBox.Padding.Bottom);
            }
        }
    }
}
