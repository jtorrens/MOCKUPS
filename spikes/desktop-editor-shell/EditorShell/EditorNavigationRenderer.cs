using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorNavigationRenderer
{
    private readonly Func<ProjectTreeNode?> _selectedNode;
    private readonly Func<bool> _isDark;
    private readonly Func<ProjectTreeNode, bool> _isExpanded;
    private readonly Action<ProjectTreeNode> _selectNode;
    private readonly Action<ProjectTreeNode> _showNode;
    private readonly Action<ProjectTreeNode> _toggleGroup;
    private readonly Func<ProjectTreeNode, Task> _addChild;
    private readonly Action<ProjectTreeNode> _duplicateNode;
    private readonly Func<ProjectTreeNode, Task> _renameNode;
    private readonly Func<ProjectTreeNode, Task> _deleteNode;

    public EditorNavigationRenderer(
        Func<ProjectTreeNode?> selectedNode,
        Func<bool> isDark,
        Func<ProjectTreeNode, bool> isExpanded,
        Action<ProjectTreeNode> selectNode,
        Action<ProjectTreeNode> showNode,
        Action<ProjectTreeNode> toggleGroup,
        Func<ProjectTreeNode, Task> addChild,
        Action<ProjectTreeNode> duplicateNode,
        Func<ProjectTreeNode, Task> renameNode,
        Func<ProjectTreeNode, Task> deleteNode)
    {
        _selectedNode = selectedNode;
        _isDark = isDark;
        _isExpanded = isExpanded;
        _selectNode = selectNode;
        _showNode = showNode;
        _toggleGroup = toggleGroup;
        _addChild = addChild;
        _duplicateNode = duplicateNode;
        _renameNode = renameNode;
        _deleteNode = deleteNode;
    }

    public void Rebuild(StackPanel target, IReadOnlyList<ProjectTreeNode> treeRoots)
    {
        target.Children.Clear();

        foreach (var project in treeRoots)
        {
            AddNavigationCard(target, project, CreateProjectNavigationContent(project), EditorIcons.ForTreeNode(project.Kind));

            foreach (var root in project.Children
                         .Where(EditorNavigationMetadata.IsTopLevelSection)
                         .OrderBy(EditorNavigationMetadata.RootOrder))
            {
                AddNavigationSection(target, root);
            }
        }
    }

    private Control CreateProjectNavigationContent(ProjectTreeNode project)
    {
        var panel = new StackPanel
        {
            Spacing = 7,
            Margin = new Thickness(0, 6, 0, 0),
        };

        var episodesRoot = project.Children.FirstOrDefault((child) => child.Kind == ProjectTreeNodeKind.EpisodesRoot);
        foreach (var episode in episodesRoot?.Children ?? [])
        {
            AddNavigationNode(panel, episode);
        }

        return panel;
    }

    private void AddNavigationSection(StackPanel parent, ProjectTreeNode sectionRoot)
    {
        var content = new StackPanel
        {
            Spacing = 5,
            Margin = new Thickness(6, 5, 0, 0),
        };

        foreach (var child in sectionRoot.Children)
        {
            AddNavigationNode(content, child);
        }

        AddNavigationCard(parent, sectionRoot, content, EditorNavigationMetadata.SectionIcon(sectionRoot));
    }

    private void AddNavigationNode(StackPanel parent, ProjectTreeNode node)
    {
        if (node.Children.Count > 0 || node.CanAddChild)
        {
            var content = new StackPanel
            {
                Spacing = 5,
                Margin = new Thickness(6, 5, 0, 0),
            };
            foreach (var child in node.Children)
            {
                AddNavigationNode(content, child);
            }

            AddNavigationCard(parent, node, content, EditorIcons.ForTreeNode(node.Kind));
            return;
        }

        parent.Children.Add(node.Kind == ProjectTreeNodeKind.PaletteColor
            ? CreatePaletteNavigationRow(node)
            : CreateNavigationRow(node, EditorIcons.ForTreeNode(node.Kind)));
    }

    private void AddNavigationCard(
        StackPanel parent,
        ProjectTreeNode node,
        Control content,
        string? iconName)
    {
        var isExpanded = _isExpanded(node);
        parent.Children.Add(new InstantNavigationCard(
            CreateNavigationHeader(node, iconName, isExpanded),
            content,
            isExpanded));
    }

    private Control CreateNavigationHeader(ProjectTreeNode node, string? iconName, bool isExpanded)
    {
        var grid = new Grid
        {
            ColumnDefinitions = iconName is null
                ? new ColumnDefinitions("*,Auto,24")
                : new ColumnDefinitions("20,*,Auto,24"),
            ColumnSpacing = 6,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var contentColumn = 0;
        if (iconName is not null)
        {
            var icon = EditorIcons.Create(iconName, 16);
            ApplyNavigationSelectionBrush(icon, node);
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);
            contentColumn = 1;
        }

        var titleButton = CreateNavigationSelectButton(node, includeSubtitle: true);
        Grid.SetColumn(titleButton, contentColumn);

        var actions = CreateNavigationActions(node);
        Grid.SetColumn(actions, contentColumn + 1);

        var toggle = EditorNavigationVisuals.ToggleButton(
            isExpanded,
            isExpanded ? "Collapse" : "Expand",
            (_, e) =>
            {
                e.Handled = true;
                _toggleGroup(node);
            });
        Grid.SetColumn(toggle, contentColumn + 2);

        grid.Children.Add(titleButton);
        grid.Children.Add(actions);
        grid.Children.Add(toggle);

        return grid;
    }

    private Control CreateNavigationRow(ProjectTreeNode node, string? iconName)
    {
        var row = new Border
        {
            Padding = new Thickness(6, 4),
            CornerRadius = new CornerRadius(8),
            Background = EditorNavigationVisuals.RowBackground(IsSelected(node), _isDark()),
        };

        var grid = new Grid
        {
            ColumnDefinitions = iconName is null
                ? new ColumnDefinitions("10,*,Auto")
                : new ColumnDefinitions("10,20,*,Auto"),
            ColumnSpacing = 6,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var usedDot = EditorNavigationVisuals.UsedDot(node, _isDark());
        Grid.SetColumn(usedDot, 0);
        grid.Children.Add(usedDot);

        var contentColumn = 1;
        if (iconName is not null)
        {
            var icon = EditorIcons.Create(iconName, 16);
            ApplyNavigationSelectionBrush(icon, node);
            Grid.SetColumn(icon, 1);
            grid.Children.Add(icon);
            contentColumn = 2;
        }

        var titleButton = CreateNavigationSelectButton(node, includeSubtitle: true);
        Grid.SetColumn(titleButton, contentColumn);

        var actions = CreateNavigationActions(node);
        Grid.SetColumn(actions, contentColumn + 1);

        grid.Children.Add(titleButton);
        grid.Children.Add(actions);
        row.Child = grid;
        return row;
    }

    private Control CreatePaletteNavigationRow(ProjectTreeNode node)
    {
        var row = new Border
        {
            Padding = new Thickness(6, 4),
            CornerRadius = new CornerRadius(8),
            Background = EditorNavigationVisuals.RowBackground(IsSelected(node), _isDark()),
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("10,18,*,Auto"),
            ColumnSpacing = 7,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var usedDot = EditorNavigationVisuals.UsedDot(node, _isDark());
        Grid.SetColumn(usedDot, 0);

        var swatch = EditorNavigationVisuals.PaletteSwatch(node.ColorHex, _isDark());
        Grid.SetColumn(swatch, 1);

        var titleButton = CreateNavigationSelectButton(node, includeSubtitle: true);
        Grid.SetColumn(titleButton, 2);

        var actions = CreateNavigationActions(node);
        Grid.SetColumn(actions, 3);

        grid.Children.Add(usedDot);
        grid.Children.Add(swatch);
        grid.Children.Add(titleButton);
        grid.Children.Add(actions);
        row.Child = grid;
        return row;
    }

    private Button CreateNavigationSelectButton(ProjectTreeNode node, bool includeSubtitle)
    {
        var textPanel = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
        };
        textPanel.Children.Add(new TextBlock
        {
            Text = EditorNavigationMetadata.Title(node),
            FontWeight = FontWeight.SemiBold,
            Foreground = EditorNavigationVisuals.TextBrush(IsSelected(node), _isDark()),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var subtitle = EditorNavigationMetadata.Subtitle(node);
        if (includeSubtitle && !string.IsNullOrWhiteSpace(subtitle))
        {
            textPanel.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                Opacity = 0.72,
                Foreground = EditorNavigationVisuals.MutedTextBrush(IsSelected(node), _isDark()),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        var button = new Button
        {
            Content = textPanel,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        button.Click += (_, e) =>
        {
            e.Handled = true;
            if (node.Children.Count > 0)
            {
                var wasExpanded = _isExpanded(node);
                if (node.CanOpenEditor)
                {
                    _showNode(node);
                }

                if (!node.CanOpenEditor || wasExpanded)
                {
                    _toggleGroup(node);
                }
                return;
            }

            _selectNode(node);
        };
        return button;
    }

    private void ApplyNavigationSelectionBrush(Control control, ProjectTreeNode node)
    {
        if (IsSelected(node))
        {
            EditorIcons.ApplyBrush(control, EditorNavigationVisuals.TextBrush(true, _isDark()));
        }
    }

    private StackPanel CreateNavigationActions(ProjectTreeNode node)
    {
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        if (node.CanAddChild)
        {
            actions.Children.Add(CreateTreeActionButton(EditorIcons.Create(EditorIcons.Add, 14), "Add child", async (_, e) =>
            {
                e.Handled = true;
                await _addChild(node);
            }));
        }

        if (node.CanDuplicate)
        {
            actions.Children.Add(CreateTreeActionButton(EditorIcons.Create(EditorIcons.Duplicate, 14), "Duplicate", (_, e) =>
            {
                e.Handled = true;
                _duplicateNode(node);
            }));
        }

        if (node.CanRenameDirectly)
        {
            actions.Children.Add(CreateTreeActionButton(EditorIcons.Create(EditorIcons.Edit, 14), "Rename", async (_, e) =>
            {
                e.Handled = true;
                await _renameNode(node);
            }));
        }

        if (node.CanDelete)
        {
            actions.Children.Add(CreateTreeActionButton(EditorIcons.Create(EditorIcons.Delete, 14), "Delete", async (_, e) =>
            {
                e.Handled = true;
                await _deleteNode(node);
            }));
        }

        return actions;
    }

    private static Button CreateTreeActionButton(
        Control content,
        string tooltip,
        EventHandler<RoutedEventArgs> onClick)
    {
        var button = new Button
        {
            Content = content,
            Width = 25,
            Height = 25,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        ToolTip.SetTip(button, tooltip);
        button.Click += onClick;
        return button;
    }

    private bool IsSelected(ProjectTreeNode node)
    {
        return _selectedNode()?.Id == node.Id;
    }
}
