using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorReferenceUsageDialog
{
    private readonly Window _owner;
    private readonly bool _isDark;

    public EditorReferenceUsageDialog(Window owner, bool isDark)
    {
        _owner = owner;
        _isDark = isDark;
    }

    public Task<SpikeDatabase.ReferenceUsageDetail?> Show(
        ProjectTreeNode node,
        IReadOnlyList<SpikeDatabase.ReferenceUsageDetail> usages)
    {
        var dialog = new SukiWindow
        {
            Title = "Cannot delete used item",
            Width = 540,
            Height = 460,
            MinWidth = 540,
            MinHeight = 360,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);

        var list = new StackPanel { Spacing = 14 };
        AddGroup(dialog, list, "Design usage", usages.Where((usage) => !usage.IsProduction));
        AddGroup(dialog, list, "Production usage", usages.Where((usage) => usage.IsProduction));

        var closeButton = new Button
        {
            Content = "Close",
            MinWidth = 92,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        closeButton.Click += (_, _) => dialog.Close(null);

        var header = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = $"{node.Name} is still used",
                    FontSize = 17,
                    FontWeight = FontWeight.Bold,
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text = "Open a reference to remove or replace it before deleting this item.",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.76,
                },
            },
        };

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 16,
            Children =
            {
                header,
                new ScrollViewer
                {
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    Content = list,
                },
                closeButton,
            },
        };
        Grid.SetRow(root.Children[1], 1);
        Grid.SetRow(closeButton, 2);
        dialog.Content = new Border { Padding = new Thickness(22), Child = root };

        return dialog.ShowDialog<SpikeDatabase.ReferenceUsageDetail?>(_owner);
    }

    private void AddGroup(
        Window dialog,
        Panel host,
        string label,
        IEnumerable<SpikeDatabase.ReferenceUsageDetail> usages)
    {
        var items = usages.ToList();
        if (items.Count == 0) return;

        var content = new StackPanel { Spacing = 4 };
        content.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(4, 0, 4, 2),
        });
        foreach (var usage in items)
        {
            content.Children.Add(EditorReferenceUsageLink.Create(
                usage,
                _isDark,
                () =>
                {
                    dialog.Close(usage);
                    return Task.CompletedTask;
                },
                includeKindIcon: true));
        }

        host.Children.Add(new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.Parse(_isDark ? "#10FFFFFF" : "#10000000")),
            BorderBrush = new SolidColorBrush(Color.Parse(_isDark ? "#24FFFFFF" : "#20000000")),
            Child = content,
        });
    }
}
