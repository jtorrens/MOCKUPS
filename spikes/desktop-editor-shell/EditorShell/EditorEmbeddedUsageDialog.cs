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

    public sealed record Selection(
        SpikeDatabase.EmbeddedComponentUsage? EmbeddedUsage,
        string TargetNodeId);

    public EditorEmbeddedUsageDialog(Window owner, bool isDark)
    {
        _owner = owner;
        _isDark = isDark;
    }

    public async Task<Selection?> Show(
        string componentName,
        string componentType,
        IReadOnlyList<SpikeDatabase.EmbeddedComponentUsage> classUsages,
        string? presetName = null,
        IReadOnlyList<SpikeDatabase.ComponentPresetReferenceUsage>? presetUsages = null)
    {
        Selection? selected = null;
        var dialog = new SukiWindow
        {
            Title = $"Embedded structure · {componentName}",
            Width = 620,
            Height = 460,
            MinWidth = 560,
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

        content.Children.Add(CreateUsageSwitch(
            classUsages,
            presetName,
            presetUsages ?? [],
            dialog,
            (selection) => selected = selection));

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

    private Control CreateUsageSwitch(
        IReadOnlyList<SpikeDatabase.EmbeddedComponentUsage> classUsages,
        string? presetName,
        IReadOnlyList<SpikeDatabase.ComponentPresetReferenceUsage> presetUsages,
        Window dialog,
        System.Action<Selection> select)
    {
        var classContent = classUsages.Count == 0
            ? EmptyText("No embedded usages.")
            : CreateUsageTree(classUsages, dialog, select);
        var presetContent = string.IsNullOrWhiteSpace(presetName)
            ? EmptyText("No active preset selected.")
            : presetUsages.Count == 0
                ? EmptyText($"No usages for preset {presetName}.")
                : CreatePresetUsageList(presetUsages, dialog, select);
        var contentHost = new ContentControl
        {
            Content = classContent,
        };
        var classButton = CreateSegmentButton("Class");
        var presetButton = CreateSegmentButton("Preset");
        void Select(bool preset)
        {
            contentHost.Content = preset ? presetContent : classContent;
            SetSegmentState(classButton, !preset);
            SetSegmentState(presetButton, preset);
        }

        classButton.Click += (_, _) => Select(false);
        presetButton.Click += (_, _) => Select(true);
        Select(false);

        var switchGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            MaxWidth = 280,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                classButton,
                presetButton,
            },
        };
        Grid.SetColumn(presetButton, 1);

        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                switchGrid,
                contentHost,
            },
        };
    }

    private Button CreateSegmentButton(string label)
    {
        return new Button
        {
            Content = label,
            Height = 32,
            Padding = new Avalonia.Thickness(12, 0),
            BorderThickness = new Avalonia.Thickness(1),
            FontWeight = FontWeight.SemiBold,
        };
    }

    private void SetSegmentState(Button button, bool selected)
    {
        button.Background = new SolidColorBrush(Color.Parse(selected
            ? (_isDark ? "#2AD6A638" : "#26D6A638")
            : (_isDark ? "#12FFFFFF" : "#0D000000")));
        button.BorderBrush = new SolidColorBrush(Color.Parse(selected ? "#D6A638" : (_isDark ? "#24FFFFFF" : "#20000000")));
        button.Foreground = new SolidColorBrush(Color.Parse(selected ? "#D6A638" : (_isDark ? "#F2F5FA" : "#172033")));
    }

    private static Control EmptyText(string text)
    {
        return new TextBlock
        {
            Text = text,
            Opacity = 0.75,
            Margin = new Avalonia.Thickness(0, 12, 0, 0),
        };
    }

    private Control CreateUsageTree(
        IReadOnlyList<SpikeDatabase.EmbeddedComponentUsage> usages,
        Window dialog,
        System.Action<Selection> select)
    {
        var root = new StackPanel
        {
            Spacing = 10,
        };
        foreach (var group in usages.GroupBy((usage) =>
                     string.IsNullOrWhiteSpace(usage.SourceNodeId)
                         ? usage.ParentComponentClassId
                         : usage.SourceNodeId))
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
        System.Action<Selection> select)
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
            select(new Selection(usage, string.IsNullOrWhiteSpace(usage.SourceNodeId) ? usage.ParentComponentClassId : usage.SourceNodeId));
            dialog.Close();
        };
        return button;
    }

    private Control CreatePresetUsageList(
        IReadOnlyList<SpikeDatabase.ComponentPresetReferenceUsage> usages,
        Window dialog,
        System.Action<Selection> select)
    {
        var root = new StackPanel
        {
            Spacing = 10,
            Margin = new Avalonia.Thickness(0, 12, 0, 0),
        };
        foreach (var group in usages.GroupBy((usage) => usage.SourceKind))
        {
            var branch = new StackPanel
            {
                Spacing = 5,
            };
            branch.Children.Add(new TextBlock
            {
                Text = group.Key,
                FontWeight = FontWeight.SemiBold,
            });
            foreach (var usage in group.OrderBy((item) => item.SourceName).ThenBy((item) => item.Detail))
            {
                var leaf = CreatePresetUsageButton(usage, dialog, select);
                leaf.Margin = new Avalonia.Thickness(18, 0, 0, 0);
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

    private static Button CreatePresetUsageButton(
        SpikeDatabase.ComponentPresetReferenceUsage usage,
        Window dialog,
        System.Action<Selection> select)
    {
        var label = string.IsNullOrWhiteSpace(usage.Detail)
            ? usage.SourceName
            : $"{usage.SourceName} · {usage.Detail}";
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
            select(new Selection(usage.EmbeddedUsage, usage.TargetNodeId));
            dialog.Close();
        };
        return button;
    }
}
