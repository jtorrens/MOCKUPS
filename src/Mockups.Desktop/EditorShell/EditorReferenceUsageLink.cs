using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorReferenceUsageLink
{
    public static Button Create(
        ReferenceUsageDetail usage,
        bool isDark,
        Func<Task> activate,
        bool includeKindIcon = false)
    {
        var content = new Grid
        {
            ColumnDefinitions = includeKindIcon
                ? new ColumnDefinitions("18,*,18")
                : new ColumnDefinitions("*,18"),
            ColumnSpacing = 7,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var textColumn = 0;
        if (includeKindIcon)
        {
            var icon = EditorIcons.Create(EditorIcons.ForTreeNode(usage.SourceKind), 15);
            Grid.SetColumn(icon, 0);
            content.Children.Add(icon);
            textColumn = 1;
        }

        var labels = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = usage.Label,
                    FontWeight = FontWeight.SemiBold,
                    TextDecorations = TextDecorations.Underline,
                    Foreground = new SolidColorBrush(Color.Parse(isDark ? "#D6A638" : "#A56600")),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                new TextBlock
                {
                    Text = usage.Field,
                    FontSize = 11,
                    Opacity = 0.68,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            },
        };
        if (usage.SourceBreadcrumb.Length > 0)
        {
            labels.Children.Insert(1, new TextBlock
            {
                Text = usage.SourceBreadcrumb,
                FontSize = 11,
                Opacity = 0.8,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        Grid.SetColumn(labels, textColumn);
        content.Children.Add(labels);

        var openIcon = EditorIcons.CreateSemantic("Open Usage reference", EditorIcons.Edit, 14);
        Grid.SetColumn(openIcon, textColumn + 1);
        content.Children.Add(openIcon);

        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(4, 3),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = content,
        };
        EditorAccessibility.Describe(button, $"Open {usage.Label}",
            usage.SourceBreadcrumb.Length > 0
                ? $"{usage.SourceBreadcrumb} · {usage.Field}"
                : usage.Field);
        ToolTip.SetTip(button, $"Open in {(usage.IsProduction ? "Production" : "Design")}");
        button.Click += async (_, args) =>
        {
            args.Handled = true;
            await activate();
        };
        return button;
    }
}
