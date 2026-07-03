using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorComboBoxBehavior
{
    public static ComboBox Configure(ComboBox comboBox)
    {
        comboBox.MinHeight = comboBox.MinHeight <= 0 ? 36 : comboBox.MinHeight;
        comboBox.LostFocus += (_, _) => comboBox.IsDropDownOpen = false;
        comboBox.SelectionChanged += (_, _) => comboBox.IsDropDownOpen = false;
        comboBox.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape) return;

            comboBox.IsDropDownOpen = false;
            args.Handled = true;
        };
        comboBox.AttachedToVisualTree += (_, _) =>
        {
            if (TopLevel.GetTopLevel(comboBox) is not { } topLevel) return;

            topLevel.AddHandler(
                InputElement.PointerPressedEvent,
                (_, args) =>
                {
                    if (!comboBox.IsDropDownOpen) return;
                    if (comboBox.IsPointerOver) return;
                    if (args.Source is Visual sourceVisual && comboBox.IsVisualAncestorOf(sourceVisual)) return;
                    if (args.Source is Visual popupVisual &&
                        popupVisual.FindAncestorOfType<ComboBoxItem>() is not null)
                    {
                        return;
                    }

                    comboBox.IsDropDownOpen = false;
                },
                RoutingStrategies.Tunnel);
        };
        return comboBox;
    }
}
