using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorComboBoxBehavior
{
    public static ComboBox Configure(ComboBox comboBox)
    {
        comboBox.MinHeight = comboBox.MinHeight <= 0 ? 36 : comboBox.MinHeight;
        comboBox.PointerPressed += (_, args) =>
        {
            if (!comboBox.IsEnabled) return;
            if (comboBox.IsDropDownOpen) return;
            if (!args.GetCurrentPoint(comboBox).Properties.IsLeftButtonPressed) return;

            comboBox.Focus();
            comboBox.IsDropDownOpen = true;
            ClearPopupMotion(comboBox);
            args.Handled = true;
        };
        comboBox.DropDownOpened += (_, _) => ClearPopupMotion(comboBox);
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

    private static void ClearPopupMotion(ComboBox comboBox)
    {
        Dispatcher.UIThread.Post(
            () =>
            {
                if (!comboBox.IsDropDownOpen) return;
                if (TopLevel.GetTopLevel(comboBox) is not Visual topLevel) return;

                foreach (var visual in topLevel.GetVisualDescendants())
                {
                    if (visual is not Control control) continue;
                    if (!IsComboPopupPart(control)) continue;

                    control.Transitions = null;
                    control.Opacity = 1;
                }
            },
            DispatcherPriority.Render);
    }

    private static bool IsComboPopupPart(Control control)
    {
        if (control is ComboBoxItem) return true;
        if (control.FindAncestorOfType<ComboBoxItem>() is not null) return true;

        var typeName = control.GetType().Name;
        return typeName.Contains("Popup", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("Overlay", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("Presenter", StringComparison.OrdinalIgnoreCase);
    }
}
