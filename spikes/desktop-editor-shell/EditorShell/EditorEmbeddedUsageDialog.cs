using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorEmbeddedUsageDialog
{
    private readonly Window _owner;
    private readonly bool _isDark;

    public EditorEmbeddedUsageDialog(Window owner, bool isDark)
    {
        _owner = owner;
        _isDark = isDark;
    }

    public async Task<SpikeDatabase.EmbeddedComponentUsage?> Show(
        string componentName,
        string componentType,
        IReadOnlyList<SpikeDatabase.EmbeddedComponentUsage> usages)
    {
        SpikeDatabase.EmbeddedComponentUsage? selected = null;
        var dialog = new SukiWindow
        {
            Title = $"Embedded structure · {componentName}",
            Width = 520,
            Height = 460,
            MinWidth = 520,
            MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };

        var content = new StackPanel
        {
            Spacing = 12,
        };
        content.Children.Add(new TextBlock
        {
            Text = $"{componentName} · {componentType}",
            FontSize = 17,
            FontWeight = FontWeight.Bold,
        });

        if (usages.Count == 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = "No embedded usages.",
                Opacity = 0.75,
            });
        }
        else
        {
            content.Children.Add(CreateUsageTree(usages, dialog, (usage) => selected = usage));
        }

        var closeButton = new Button
        {
            Content = "Close",
            MinWidth = 92,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        closeButton.Click += (_, _) => dialog.Close();

        dialog.Content = new Border
        {
            Padding = new Avalonia.Thickness(22),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                RowSpacing = 16,
                Children =
                {
                    new ScrollViewer
                    {
                        Content = content,
                    },
                    closeButton,
                },
            },
        };
        Grid.SetRow(closeButton, 1);

        await dialog.ShowDialog(_owner);
        return selected;
    }

    private Control CreateUsageTree(
        IReadOnlyList<SpikeDatabase.EmbeddedComponentUsage> usages,
        Window dialog,
        System.Action<SpikeDatabase.EmbeddedComponentUsage> select)
    {
        var root = new StackPanel
        {
            Spacing = 10,
        };
        foreach (var group in usages.GroupBy((usage) => usage.ParentComponentClassId))
        {
            var first = group.First();
            var branchHeaderIcon = EditorIcons.Create(EditorIcons.Component, 16);
            var branchHeaderText = new TextBlock
            {
                Text = $"{first.ParentComponentName} · {first.ParentComponentType}",
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var branchHeader = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 8,
                Children =
                {
                    branchHeaderIcon,
                    branchHeaderText,
                },
            };
            Grid.SetColumn(branchHeaderText, 1);

            var branch = new StackPanel
            {
                Spacing = 5,
            };
            branch.Children.Add(branchHeader);

            foreach (var usage in group.OrderBy((item) => item.SlotLabel))
            {
                var leaf = CreateLeafButton(usage, dialog, select);
                leaf.Margin = new Avalonia.Thickness(24, 0, 0, 0);
                branch.Children.Add(leaf);
            }

            root.Children.Add(new Border
            {
                Padding = new Avalonia.Thickness(10),
                CornerRadius = new Avalonia.CornerRadius(8),
                BorderThickness = new Avalonia.Thickness(1),
                Background = new SolidColorBrush(Color.Parse(_isDark ? "#10FFFFFF" : "#10000000")),
                BorderBrush = new SolidColorBrush(Color.Parse(_isDark ? "#24FFFFFF" : "#20000000")),
                Child = branch,
            });
        }

        return root;
    }

    private static Button CreateLeafButton(
        SpikeDatabase.EmbeddedComponentUsage usage,
        Window dialog,
        System.Action<SpikeDatabase.EmbeddedComponentUsage> select)
    {
        var label = usage.HasOverrides
            ? $"{usage.SlotLabel} · overrides"
            : usage.SlotLabel;
        var text = new TextBlock
        {
            Text = label,
            TextDecorations = TextDecorations.Underline,
            Foreground = new SolidColorBrush(Color.Parse("#D6A638")),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var button = new Button
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Avalonia.Thickness(6, 2),
            Background = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
        };
        button.Click += (_, _) =>
        {
            select(usage);
            dialog.Close();
        };
        return button;
    }
}
