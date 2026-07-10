using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Common;

internal static class CollapsibleTree
{
    public static Control Branch(string label, Control icon, IEnumerable<Control> children, bool isExpanded = true)
    {
        var content = new StackPanel
        {
            Spacing = 3,
            Margin = new Thickness(10, 0, 0, 0),
        };
        foreach (var child in children)
        {
            content.Children.Add(child);
        }

        var indicator = new TextBlock
        {
            Width = 16,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.72,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };
        var host = new ContentControl { Content = content, IsVisible = isExpanded };
        var header = new Border
        {
            Padding = new Thickness(4, 5),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new Grid
            {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"),
                ColumnSpacing = 4,
                Children =
                {
                    indicator,
                    icon,
                    new TextBlock
                    {
                        Text = label,
                        FontWeight = FontWeight.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                },
            },
        };
        Grid.SetColumn((Control)((Grid)header.Child).Children[1], 1);
        Grid.SetColumn((Control)((Grid)header.Child).Children[2], 2);

        void SetExpanded(bool expanded)
        {
            host.IsVisible = expanded;
            indicator.Text = expanded ? "v" : ">";
        }
        SetExpanded(isExpanded);
        header.PointerPressed += (_, args) =>
        {
            if (args.Source is Control source && source.FindAncestorOfType<Button>() is not null) return;
            SetExpanded(!host.IsVisible);
            args.Handled = true;
        };

        return new StackPanel
        {
            Spacing = 0,
            Children = { header, host },
        };
    }

    public static Control Leaf(Control icon, Control content, Control action, bool isLast)
    {
        var connector = new Grid { Width = 18, Margin = new Thickness(0, 0, 4, 0) };
        connector.Children.Add(new Border
        {
            Width = 1,
            Background = new SolidColorBrush(Color.FromArgb(58, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = isLast ? VerticalAlignment.Top : VerticalAlignment.Stretch,
            Height = isLast ? 12 : double.NaN,
        });
        connector.Children.Add(new Border
        {
            Height = 1,
            Width = 10,
            Background = new SolidColorBrush(Color.FromArgb(58, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 0, 0),
        });

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"),
            ColumnSpacing = 7,
            VerticalAlignment = VerticalAlignment.Center,
            MinHeight = 28,
            Children = { connector, icon, content, action },
        };
        Grid.SetColumn(icon, 1);
        Grid.SetColumn(content, 2);
        Grid.SetColumn(action, 3);
        return row;
    }
}
