using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System.Collections.Generic;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ProductionPreviewPathItem(string Label, Action? Activate = null);

internal sealed record ProductionPreviewContextMetadata(
    IReadOnlyList<ProductionPreviewPathItem> Path,
    string Actor,
    string Device,
    string Theme,
    string Mode,
    bool HasShotContext = true);

internal static class ProductionPreviewContextStrip
{
    public static void Render(StackPanel host, ProductionPreviewContextMetadata metadata, Control? trailing = null)
    {
        host.Children.Clear();
        var breadcrumb = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };
        for (var index = 0; index < metadata.Path.Count; index++)
        {
            if (index > 0)
            {
                breadcrumb.Children.Add(new TextBlock { Text = "›", Opacity = 0.52, VerticalAlignment = VerticalAlignment.Center });
            }
            var pathItem = metadata.Path[index];
            Control crumb = pathItem.Activate is null
                ? new TextBlock
                {
                    Text = pathItem.Label,
                    FontSize = 13,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = index == metadata.Path.Count - 1
                        ? new SolidColorBrush(Color.Parse("#D6A638"))
                        : EditorNavigationVisuals.TextBrush(false, EditorSukiWindowTheme.IsDark(null)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 180,
                }
                : CreateCrumbButton(pathItem);
            breadcrumb.Children.Add(crumb);
        }
        if (breadcrumb.Children.Count == 0)
        {
            breadcrumb.Children.Add(new TextBlock { Text = "No Episode selected", FontSize = 13, Opacity = 0.68 });
        }
        var context = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            IsVisible = metadata.HasShotContext,
        };
        if (metadata.HasShotContext)
        {
            context.Children.Add(Item("Actor", metadata.Actor));
            context.Children.Add(Item("Device", metadata.Device));
            context.Children.Add(Item("Theme", metadata.Theme));
            context.Children.Add(Item("Mode", metadata.Mode));
        }
        if (trailing is null)
        {
            host.Children.Add(breadcrumb);
            if (metadata.HasShotContext) host.Children.Add(context);
            return;
        }
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            RowDefinitions = new RowDefinitions(metadata.HasShotContext ? "Auto,Auto" : "Auto"),
            ColumnSpacing = 10,
            RowSpacing = 7,
        };
        row.Children.Add(breadcrumb);
        if (metadata.HasShotContext)
        {
            Grid.SetRow(context, 1);
            row.Children.Add(context);
        }
        Grid.SetColumn(trailing, 1);
        Grid.SetRowSpan(trailing, metadata.HasShotContext ? 2 : 1);
        trailing.HorizontalAlignment = HorizontalAlignment.Right;
        trailing.VerticalAlignment = VerticalAlignment.Stretch;
        row.Children.Add(trailing);
        host.Children.Add(row);
    }

    private static Control CreateCrumbButton(ProductionPreviewPathItem item)
    {
        var button = new Button
        {
            Content = item.Label,
            Padding = new Thickness(4, 0),
            MinHeight = 26,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = EditorNavigationVisuals.TextBrush(false, EditorSukiWindowTheme.IsDark(null)),
        };
        button.Click += (_, _) => item.Activate?.Invoke();
        ToolTip.SetTip(button, $"Open {item.Label}");
        return button;
    }

    private static Control Item(string label, string value)
    {
        return new Border
        {
            Padding = new Thickness(8, 5),
            Margin = new Thickness(0, 0, 7, 0),
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#554B525D")),
            Background = new SolidColorBrush(Color.Parse("#18252A31")),
            Child = new TextBlock
            {
                Text = $"{label}: {value}",
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 190,
            },
        };
    }
}
