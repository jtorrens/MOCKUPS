using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell.Common;
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
    private readonly Action<ProjectTreeNode> _toggleGroup;
    private readonly Func<ProjectTreeNode, Task> _addChild;
    private readonly Action<ProjectTreeNode> _duplicateNode;
    private readonly Func<ProjectTreeNode, Task> _renameNode;
    private readonly Func<ProjectTreeNode, Task> _deleteNode;
    private readonly Func<ProjectTreeNode, Task> _toggleVariantLock;
    private readonly Func<ProjectTreeNode, bool> _canExposeChildren;
    private readonly Func<ProjectTreeNode, bool> _isNodeEnabled;

    public EditorNavigationRenderer(
        Func<ProjectTreeNode?> selectedNode,
        Func<bool> isDark,
        Func<ProjectTreeNode, bool> isExpanded,
        Action<ProjectTreeNode> selectNode,
        Action<ProjectTreeNode> toggleGroup,
        Func<ProjectTreeNode, Task> addChild,
        Action<ProjectTreeNode> duplicateNode,
        Func<ProjectTreeNode, Task> renameNode,
        Func<ProjectTreeNode, Task> deleteNode,
        Func<ProjectTreeNode, Task> toggleVariantLock,
        Func<ProjectTreeNode, bool> canExposeChildren,
        Func<ProjectTreeNode, bool> isNodeEnabled)
    {
        _selectedNode = selectedNode;
        _isDark = isDark;
        _isExpanded = isExpanded;
        _selectNode = selectNode;
        _toggleGroup = toggleGroup;
        _addChild = addChild;
        _duplicateNode = duplicateNode;
        _renameNode = renameNode;
        _deleteNode = deleteNode;
        _toggleVariantLock = toggleVariantLock;
        _canExposeChildren = canExposeChildren;
        _isNodeEnabled = isNodeEnabled;
    }

    public void Rebuild(
        StackPanel target,
        IReadOnlyList<ProjectTreeNode> treeRoots,
        EditorWorkspace workspace,
        string productionId)
    {
        var candidate = new StackPanel();

        foreach (var project in treeRoots)
        {
            if (workspace == EditorWorkspace.Production
                && !string.Equals(project.Id, productionId, StringComparison.Ordinal))
            {
                continue;
            }

            if (workspace != EditorWorkspace.Production)
            {
                candidate.Children.Add(CreateNavigationRow(project, EditorIcons.ForNavigationTreeNode(project)));
            }

            foreach (var root in EditorWorkspaceNavigation.SectionRoots(project, workspace))
            {
                AddNavigationSection(candidate, root);
            }
        }

        var replacement = candidate.Children.ToList();
        candidate.Children.Clear();
        target.Children.Clear();
        foreach (var child in replacement) target.Children.Add(child);
    }

    private void AddNavigationSection(StackPanel parent, ProjectTreeNode sectionRoot)
    {
        var content = new StackPanel
        {
            Spacing = 1,
            Margin = new Thickness(2, 5, 0, 0),
        };

        for (var index = 0; index < sectionRoot.Children.Count; index++)
        {
            var child = sectionRoot.Children[index];
            AddHierarchicalNode(content, child, 0, index, index == sectionRoot.Children.Count - 1);
        }

        AddNavigationCard(parent, sectionRoot, content, EditorNavigationMetadata.SectionIcon(sectionRoot));
    }

    private void AddHierarchicalNode(StackPanel parent, ProjectTreeNode node, int depth, int siblingIndex, bool isLastSibling)
    {
        var exposeChildren = _canExposeChildren(node);
        var visibleChildren = node.Children;
        var hasChildren = visibleChildren.Count > 0 || node.CanAddChild;
        var nodeEnabled = _isNodeEnabled(node);
        var expanded = hasChildren && _isExpanded(node);
        var options = new List<EditorNavigationRowAction>();
        if (node.CanRenameDirectly)
        {
            options.Add(new($"Rename {EditorNavigationMetadata.Title(node)}", EditorIcons.Edit, () => _ = _renameNode(node)));
        }
        EditorNavigationRowAction? lockedAction = null;
        if (node.Kind == ProjectTreeNodeKind.ComponentPreset && node.IsLocked)
        {
            lockedAction = new($"Unlock {EditorNavigationMetadata.Title(node)} variant editing", EditorIcons.Lock, () => _ = _toggleVariantLock(node));
        }
        else if (node.Kind == ProjectTreeNodeKind.ComponentPreset)
        {
            options.Add(new($"Lock {EditorNavigationMetadata.Title(node)} variant editing", EditorIcons.Unlock, () => _ = _toggleVariantLock(node)));
        }
        if (node.CanDuplicate)
        {
            options.Add(new($"Duplicate {EditorNavigationMetadata.Title(node)}", EditorIcons.Duplicate, () => _duplicateNode(node)));
        }
        if (node.CanDelete || node.IsProtected)
        {
            options.Add(new($"Delete {EditorNavigationMetadata.Title(node)}", EditorIcons.Delete, () => _ = _deleteNode(node), node.CanDelete));
        }
        var add = node.CanAddChild
            ? new EditorNavigationRowAction(EditorNavigationMetadata.AddChildLabel(node), EditorIcons.Add, () => _ = _addChild(node), exposeChildren)
            : null;
        var status = !nodeEnabled ? "Unavailable" : node.IsProtected ? "Protected" : node.IsLocked ? "Locked" : node.IsUsed ? "Used" : "";
        var metadata = new EditorHierarchicalNavigationMetadata(
            node.Id,
            depth,
            nodeEnabled,
            nodeEnabled ? "" : "Assign an Actor to the Shot to enable this Screen",
            IsSelected(node),
            hasChildren,
            expanded,
            hasChildren ? EditorNavigationMetadata.HierarchicalIcon(node) : "",
            node.ColorHex,
            EditorNavigationMetadata.Title(node),
            EditorNavigationMetadata.Subtitle(node),
            status,
            EditorNavigationMetadata.IsUsed(node),
            !hasChildren,
            node.Kind == ProjectTreeNodeKind.ComponentClassGroup,
            node.Kind == ProjectTreeNodeKind.ComponentClassGroup && siblingIndex > 0,
            isLastSibling,
            node.Kind == ProjectTreeNodeKind.ComponentClassGroup ? 46 : node.Kind == ProjectTreeNodeKind.ComponentPreset ? 40 : 42,
            lockedAction,
            add,
            options);
        parent.Children.Add(EditorHierarchicalNavigationRow.Create(
            metadata,
            _isDark(),
            () => { if (nodeEnabled) ActivateNavigationNode(node); },
            hasChildren ? () => _toggleGroup(node) : null));
        if (!expanded) return;
        for (var index = 0; index < visibleChildren.Count; index++)
        {
            AddHierarchicalNode(parent, visibleChildren[index], depth + 1, index, index == visibleChildren.Count - 1);
        }
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

            AddNavigationCard(parent, node, content, EditorIcons.ForNavigationTreeNode(node));
            return;
        }

        parent.Children.Add(node.Kind == ProjectTreeNodeKind.PaletteColor
            ? CreatePaletteNavigationRow(node)
            : CreateNavigationRow(node, EditorIcons.ForNavigationTreeNode(node)));
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
            $"{(isExpanded ? "Collapse" : "Expand")} {EditorNavigationMetadata.Title(node)}",
            (_, e) =>
            {
                e.Handled = true;
                _toggleGroup(node);
            });
        Grid.SetColumn(toggle, contentColumn + 2);

        grid.Children.Add(titleButton);
        grid.Children.Add(actions);
        grid.Children.Add(toggle);
        grid.PointerPressed += (_, args) =>
        {
            if (args.Source is not Visual source
                || source.FindAncestorOfType<Button>() is not null)
            {
                return;
            }

            ActivateNavigationNode(node);
            args.Handled = true;
        };

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
            ActivateNavigationNode(node);
        };
        return button;
    }

    private void ActivateNavigationNode(ProjectTreeNode node)
    {
        _selectNode(node);
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

        if (node.CanRenameDirectly)
        {
            actions.Children.Add(CreateTreeActionButton(EditorIcons.Create(EditorIcons.Edit, 14), $"Rename {EditorNavigationMetadata.Title(node)}", async (_, e) =>
            {
                e.Handled = true;
                await _renameNode(node);
            }));
        }

        if (node.Kind == ProjectTreeNodeKind.ComponentPreset)
        {
            actions.Children.Add(CreateVariantLockButton(node));
        }

        if (node.CanAddChild)
        {
            actions.Children.Add(CreateTreeActionButton(EditorIcons.Create(EditorIcons.Add, 14), EditorNavigationMetadata.AddChildLabel(node), async (_, e) =>
            {
                e.Handled = true;
                await _addChild(node);
            }));
        }

        if (node.CanDuplicate)
        {
            actions.Children.Add(CreateTreeActionButton(EditorIcons.Create(EditorIcons.Duplicate, 14), $"Duplicate {EditorNavigationMetadata.Title(node)}", (_, e) =>
            {
                e.Handled = true;
                _duplicateNode(node);
            }));
        }

        if (node.CanDelete)
        {
            actions.Children.Add(CreateTreeActionButton(EditorIcons.Create(EditorIcons.Delete, 14), $"Delete {EditorNavigationMetadata.Title(node)}", async (_, e) =>
            {
                e.Handled = true;
                await _deleteNode(node);
            }));
        }

        return actions;
    }

    private Button CreateVariantLockButton(ProjectTreeNode node)
    {
        var icon = EditorIcons.Create(node.IsLocked ? EditorIcons.Lock : EditorIcons.Unlock, 14);
        EditorIcons.ApplyBrush(icon, EditorNavigationVisuals.VariantLockBrush(node.IsLocked));
        return CreateTreeActionButton(icon, $"{(node.IsLocked ? "Unlock" : "Lock")} {EditorNavigationMetadata.Title(node)} variant editing", async (_, e) =>
        {
            e.Handled = true;
            await _toggleVariantLock(node);
        });
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
        button.Click += onClick;
        return EditorAccessibility.Describe(button, tooltip);
    }

    private bool IsSelected(ProjectTreeNode node)
    {
        return _selectedNode()?.Id == node.Id;
    }
}
