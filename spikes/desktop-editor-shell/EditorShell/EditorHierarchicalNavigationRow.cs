using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorNavigationRowAction(
    string Label,
    string Icon,
    Action Activate,
    bool IsEnabled = true);

internal sealed record EditorHierarchicalNavigationMetadata(
    string NodeId,
    int Depth,
    bool IsSelected,
    bool HasChildren,
    bool IsExpanded,
    string Icon,
    string? ColorHex,
    string Title,
    string Subtitle,
    string Status,
    bool IsUsed,
    bool ShowUsageIndicator,
    bool IsGroup,
    bool ShowTopSeparator,
    bool IsLastSibling,
    double Height,
    EditorNavigationRowAction? LockedAction,
    EditorNavigationRowAction? AddAction,
    IReadOnlyList<EditorNavigationRowAction> Options);

internal static class EditorHierarchicalNavigationRow
{
    public static Control Create(
        EditorHierarchicalNavigationMetadata metadata,
        bool isDark,
        Action select,
        Action? toggle)
    {
        var row = new Border
        {
            MinHeight = metadata.Height,
            Padding = new Thickness(5, 3),
            CornerRadius = new CornerRadius(5),
            Background = Brushes.Transparent,
            Focusable = true,
            Tag = metadata,
        };
        if (metadata.ShowTopSeparator)
        {
            row.BorderThickness = new Thickness(0, 1, 0, 0);
            row.BorderBrush = new SolidColorBrush(Color.Parse(isDark ? "#35404B" : "#CBD3DC"));
            row.Margin = new Thickness(0, 5, 0, 0);
            row.Padding = new Thickness(5, 8, 5, 3);
        }

        var grid = new Grid
        {
            ColumnDefinitions = metadata.ShowUsageIndicator
                ? new ColumnDefinitions("12,12,*,124")
                : new ColumnDefinitions("28,12,*,124"),
            ColumnSpacing = 5,
            Margin = new Thickness(metadata.Depth * 18, 0, 0, 0),
        };
        if (metadata.IsSelected)
        {
            var selection = new Border
            {
                Margin = new Thickness(-4, 0, -3, 0),
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(Color.Parse(isDark ? "#285F9E" : "#D7E9FF")),
            };
            Grid.SetColumn(selection, 1);
            Grid.SetColumnSpan(selection, 3);
            grid.Children.Add(selection);
        }
        var leading = new Grid { ColumnDefinitions = new ColumnDefinitions("12,16") };
        if (metadata.Depth > 0)
        {
            var vertical = new Border
            {
                Width = 2,
                Margin = new Thickness(6, -8, 0, -8),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.Parse(isDark ? "#35404B" : "#CBD3DC")),
            };
            if (metadata.IsLastSibling)
            {
                vertical.Height = metadata.Height / 2 + 8;
                vertical.VerticalAlignment = VerticalAlignment.Top;
                vertical.Margin = new Thickness(6, -8, 0, 0);
            }
            leading.Children.Add(vertical);
            leading.Children.Add(new Border
            {
                Height = 2,
                Width = 18,
                Margin = new Thickness(6, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.Parse(isDark ? "#35404B" : "#CBD3DC")),
            });
        }
        if (!string.IsNullOrWhiteSpace(metadata.ColorHex))
        {
            var swatch = EditorNavigationVisuals.PaletteSwatch(metadata.ColorHex, isDark);
            Grid.SetColumn(swatch, 0);
            leading.Children.Add(swatch);
        }
        else if (!string.IsNullOrWhiteSpace(metadata.Icon))
        {
            var icon = EditorIcons.Create(metadata.Icon, 15);
            Grid.SetColumn(icon, 0);
            leading.Children.Add(icon);
        }

        var title = new StackPanel
        {
            Spacing = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.Children.Add(new TextBlock
        {
            Text = metadata.Title,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var detail = !string.IsNullOrWhiteSpace(metadata.Status) ? metadata.Status : metadata.Subtitle;
        if (!metadata.IsGroup && !string.IsNullOrWhiteSpace(detail))
        {
            title.Children.Add(new TextBlock
            {
                Text = detail,
                FontSize = 10,
                Opacity = 0.68,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }
        ToolTip.SetTip(title, string.IsNullOrWhiteSpace(metadata.Subtitle)
            ? metadata.Title
            : $"{metadata.Title} · {metadata.Subtitle}");

        if (metadata.ShowUsageIndicator)
        {
            var usedDot = EditorNavigationVisuals.UsedDot(metadata.IsUsed, isDark);
            Grid.SetColumn(usedDot, 1);
            grid.Children.Add(usedDot);
        }

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (metadata.LockedAction is { } locked)
        {
            var lockButton = ActionButton(locked, "");
            if (lockButton.Content is Control lockIcon)
            {
                EditorIcons.ApplyBrush(lockIcon, EditorNavigationVisuals.VariantLockBrush(true));
            }
            actions.Children.Add(lockButton);
        }
        if (metadata.IsSelected)
        {
            if (metadata.AddAction is { } add)
            {
                actions.Children.Add(ActionButton(add, "+"));
            }
            foreach (var action in metadata.Options)
            {
                actions.Children.Add(ActionButton(action, ""));
            }
        }
        if (metadata.HasChildren && toggle is not null)
        {
            var chevron = EditorNavigationVisuals.ToggleButton(
                metadata.IsExpanded,
                metadata.IsExpanded ? "Collapse" : "Expand",
                (_, args) => { args.Handled = true; toggle(); });
            chevron.Width = 24;
            chevron.Height = 28;
            chevron.Padding = new Thickness(0);
            actions.Children.Add(chevron);
        }

        Grid.SetColumn(title, metadata.ShowUsageIndicator ? 2 : 1);
        if (!metadata.ShowUsageIndicator) Grid.SetColumnSpan(title, 2);
        Grid.SetColumn(actions, 3);
        grid.Children.Add(leading);
        grid.Children.Add(title);
        grid.Children.Add(actions);
        row.Child = grid;
        row.PointerPressed += (_, args) =>
        {
            if (args.Source is Avalonia.Visual source && source.FindAncestorOfType<Button>() is not null) return;
            select();
            args.Handled = true;
        };
        row.DoubleTapped += (_, args) => { select(); args.Handled = true; };
        row.KeyDown += (_, args) =>
        {
            var rows = VisibleRows(row);
            var index = rows.IndexOf(row);
            switch (args.Key)
            {
                case Key.Enter:
                    select();
                    args.Handled = true;
                    break;
                case Key.Up when index > 0:
                    rows[index - 1].Focus();
                    args.Handled = true;
                    break;
                case Key.Down when index >= 0 && index < rows.Count - 1:
                    rows[index + 1].Focus();
                    args.Handled = true;
                    break;
                case Key.Home when rows.Count > 0:
                    rows[0].Focus();
                    args.Handled = true;
                    break;
                case Key.End when rows.Count > 0:
                    rows[^1].Focus();
                    args.Handled = true;
                    break;
                case Key.Right when metadata.HasChildren && toggle is not null:
                    if (!metadata.IsExpanded)
                    {
                        var root = TopLevel.GetTopLevel(row);
                        toggle();
                        FocusAfterRebuild(root, metadata.NodeId, focusFirstChild: true);
                    }
                    else if (index >= 0 && index < rows.Count - 1
                             && RowMetadata(rows[index + 1])?.Depth == metadata.Depth + 1)
                    {
                        rows[index + 1].Focus();
                    }
                    args.Handled = true;
                    break;
                case Key.Left when metadata.HasChildren && metadata.IsExpanded && toggle is not null:
                    var collapseRoot = TopLevel.GetTopLevel(row);
                    toggle();
                    FocusAfterRebuild(collapseRoot, metadata.NodeId, focusFirstChild: false);
                    args.Handled = true;
                    break;
                case Key.Left:
                    var parent = rows.Take(index).LastOrDefault((candidate) => RowMetadata(candidate)?.Depth == metadata.Depth - 1);
                    if (parent is not null) parent.Focus();
                    args.Handled = parent is not null;
                    break;
            }
        };
        return row;
    }

    private static List<Border> VisibleRows(Border row)
    {
        return row.FindAncestorOfType<StackPanel>()?.Children
            .OfType<Border>()
            .Where((candidate) => candidate.Tag is EditorHierarchicalNavigationMetadata)
            .ToList() ?? [];
    }

    private static EditorHierarchicalNavigationMetadata? RowMetadata(Border row) =>
        row.Tag as EditorHierarchicalNavigationMetadata;

    private static void FocusAfterRebuild(Avalonia.Visual? root, string nodeId, bool focusFirstChild)
    {
        if (root is null) return;
        Dispatcher.UIThread.Post(() =>
        {
            var rows = root.GetVisualDescendants()
                .OfType<Border>()
                .Where((candidate) => candidate.Tag is EditorHierarchicalNavigationMetadata)
                .ToList();
            var index = rows.FindIndex((candidate) => RowMetadata(candidate)?.NodeId == nodeId);
            if (index < 0) return;
            var target = focusFirstChild && index < rows.Count - 1
                         && RowMetadata(rows[index + 1])?.Depth == RowMetadata(rows[index])?.Depth + 1
                ? rows[index + 1]
                : rows[index];
            target.Focus();
        }, DispatcherPriority.Loaded);
    }

    private static Button ActionButton(EditorNavigationRowAction action, string fallback)
    {
        var button = new Button
        {
            Content = string.IsNullOrWhiteSpace(action.Icon) ? fallback : EditorIcons.Create(action.Icon, 15),
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            IsEnabled = action.IsEnabled,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
        };
        button.Click += (_, args) => { args.Handled = true; action.Activate(); };
        ToolTip.SetTip(button, action.Label);
        return button;
    }
}
