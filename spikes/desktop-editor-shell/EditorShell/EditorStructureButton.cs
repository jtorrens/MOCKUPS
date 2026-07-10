using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorStructureButton
{
    public static Button Create(Action activate)
    {
        var icon = EditorIcons.Create(EditorIcons.Structure, 18);
        EditorIcons.ApplyBrush(icon, new SolidColorBrush(Color.Parse("#D6A638")));
        var button = new Button
        {
            Content = icon,
            Width = 34,
            Height = 34,
            MinWidth = 34,
            Padding = new Avalonia.Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = new SolidColorBrush(Color.Parse("#80D6A638")),
            BorderThickness = new Avalonia.Thickness(1),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, "Embedded structure");
        button.Click += (_, _) => activate();
        return button;
    }
}
