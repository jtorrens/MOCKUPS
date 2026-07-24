using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Threading.Tasks;

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

    public static Button CreateDeleteButton(string accessibleName = "Delete")
    {
        return CreateChromeFreeButton(
            EditorIcons.Create(EditorIcons.Delete, 14),
            accessibleName);
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

    public static Control CreateActions(
        string itemLabel,
        int itemIndex,
        int itemCount,
        Action<int> addAfter,
        Action<int> duplicate,
        Action<int, int> move,
        Func<int, Task> delete)
    {
        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
        };
        var normalizedLabel = itemLabel.ToLowerInvariant();
        var add = CreateAddButton($"Add {normalizedLabel} after this item");
        add.Click += (_, args) =>
        {
            args.Handled = true;
            addAfter(itemIndex);
        };
        controls.Children.Add(add);

        var duplicateButton = CreateDuplicateButton($"Duplicate {normalizedLabel}");
        duplicateButton.Click += (_, args) =>
        {
            args.Handled = true;
            duplicate(itemIndex);
        };
        controls.Children.Add(duplicateButton);

        var moveUp = CreateMoveButton(up: true, enabled: itemIndex > 0);
        moveUp.Click += (_, args) =>
        {
            args.Handled = true;
            move(itemIndex, -1);
        };
        controls.Children.Add(moveUp);

        var moveDown = CreateMoveButton(up: false, enabled: itemIndex < itemCount - 1);
        moveDown.Click += (_, args) =>
        {
            args.Handled = true;
            move(itemIndex, 1);
        };
        controls.Children.Add(moveDown);

        var deleteButton = CreateDeleteButton();
        deleteButton.Click += async (_, args) =>
        {
            args.Handled = true;
            await delete(itemIndex);
        };
        controls.Children.Add(deleteButton);
        return controls;
    }

    public static Control CreateFooter(
        string itemLabel,
        int itemCount,
        bool canEditStructure,
        Action addFirst,
        Action<int> addAfter)
    {
        var footer = new StackPanel { Spacing = 8 };
        if (itemCount == 0)
        {
            footer.Children.Add(new TextBlock
            {
                Text = "No active instances in this design.",
                Opacity = 0.68,
            });
        }
        if (canEditStructure)
        {
            var add = CreateAddButton($"Add {itemLabel.ToLowerInvariant()}");
            add.HorizontalAlignment = HorizontalAlignment.Left;
            add.Click += (_, args) =>
            {
                args.Handled = true;
                if (itemCount == 0) addFirst();
                else addAfter(itemCount - 1);
            };
            footer.Children.Add(add);
        }
        return footer;
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
