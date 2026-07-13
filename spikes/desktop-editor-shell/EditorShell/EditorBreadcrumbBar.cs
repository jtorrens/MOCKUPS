using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorBreadcrumbItem(string Label, Action? Activate = null);

internal static class EditorBreadcrumbBar
{
    public static void Render(Panel target, IEnumerable<EditorBreadcrumbItem> items, Control? leading = null)
    {
        target.Children.Clear();
        if (leading is not null)
        {
            target.Children.Add(leading);
        }

        var itemList = new List<EditorBreadcrumbItem>(items);
        var isFirst = true;
        for (var index = 0; index < itemList.Count; index++)
        {
            var item = itemList[index];
            if (!isFirst)
            {
                target.Children.Add(new TextBlock
                {
                    Text = "›",
                    FontSize = 17,
                    FontWeight = FontWeight.Bold,
                    Opacity = 0.55,
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }

            target.Children.Add(CreateCrumb(item, index == itemList.Count - 1));
            isFirst = false;
        }
    }

    private static Control CreateCrumb(EditorBreadcrumbItem item, bool isCurrent)
    {
        if (item.Activate is null)
        {
            return new TextBlock
            {
                Text = item.Label,
                FontSize = 17,
                FontWeight = FontWeight.Bold,
                Foreground = isCurrent
                    ? new SolidColorBrush(Color.Parse("#D6A638"))
                    : EditorNavigationVisuals.TextBrush(false, EditorSukiWindowTheme.IsDark(null)),
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        var button = new Button
        {
            Content = item.Label,
            MinHeight = 34,
            Padding = new Avalonia.Thickness(8, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            FontSize = 17,
            FontWeight = FontWeight.Bold,
            Foreground = EditorNavigationVisuals.TextBrush(false, EditorSukiWindowTheme.IsDark(null)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Click += (_, _) => item.Activate();
        return EditorAccessibility.Describe(button, $"Open {item.Label}");
    }
}
