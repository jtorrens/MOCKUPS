using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorCollectionItemControls
{
    public static Button CreateMoveButton(bool up, bool enabled)
    {
        var button = CreateChromeFreeButton(
            EditorIcons.Create(up ? EditorIcons.Up : EditorIcons.Down, 14),
            up ? "Move up" : "Move down");
        button.IsEnabled = enabled;
        return button;
    }

    public static Button CreateDeleteButton()
    {
        return CreateChromeFreeButton(
            EditorIcons.Create(EditorIcons.Delete, 14),
            "Delete");
    }

    public static Button CreateAddButton(string accessibleName = "Add item")
    {
        return CreateChromeFreeButton(
            EditorIcons.Create(EditorIcons.Add, 14),
            accessibleName);
    }

    public static Button CreateDuplicateButton(string accessibleName = "Duplicate item")
    {
        return CreateChromeFreeButton(
            EditorIcons.Create(EditorIcons.Duplicate, 14),
            accessibleName);
    }

    private static Button CreateChromeFreeButton(Control content, string accessibleName)
    {
        var button = new Button
        {
            Content = content,
            Width = 30,
            Height = 28,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        EditorAccessibility.Describe(button, accessibleName);
        return button;
    }
}
