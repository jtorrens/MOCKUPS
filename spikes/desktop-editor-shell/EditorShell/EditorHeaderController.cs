using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorHeaderController
{
    private readonly Panel _breadcrumbPanel;
    private readonly TextBlock _presetTextBlock;
    private readonly Panel _actionsPanel;
    private readonly SpikeDatabase _database;
    private readonly Func<ProjectTreeNode?> _selectedNode;
    private readonly EditorNodeSelectionState _nodeSelection;
    private readonly EditorEmbeddedUsageNavigator _embeddedUsageNavigator;
    private readonly Action<ProjectTreeNode, bool> _showNode;
    private readonly Action<EditorEmbeddedContext> _showEmbeddedContext;
    private readonly Func<ProjectTreeNode, Task> _savePreset;

    public EditorHeaderController(
        Panel breadcrumbPanel,
        TextBlock presetTextBlock,
        Panel actionsPanel,
        SpikeDatabase database,
        Func<ProjectTreeNode?> selectedNode,
        EditorNodeSelectionState nodeSelection,
        EditorEmbeddedUsageNavigator embeddedUsageNavigator,
        Action<ProjectTreeNode, bool> showNode,
        Action<EditorEmbeddedContext> showEmbeddedContext,
        Func<ProjectTreeNode, Task> savePreset)
    {
        _breadcrumbPanel = breadcrumbPanel;
        _presetTextBlock = presetTextBlock;
        _actionsPanel = actionsPanel;
        _database = database;
        _selectedNode = selectedNode;
        _nodeSelection = nodeSelection;
        _embeddedUsageNavigator = embeddedUsageNavigator;
        _showNode = showNode;
        _showEmbeddedContext = showEmbeddedContext;
        _savePreset = savePreset;
    }

    public void SetRootTitle(string title)
    {
        SetPresetText(EditorPresetTextForSelection());
        EditorBreadcrumbBar.Render(_breadcrumbPanel, [
            new EditorBreadcrumbItem(title),
        ], CreateStructureButtonForSelectedComponent());
        SetHeaderActions(CreateHeaderActionsForSelectedComponent());
    }

    public void SetEmbeddedTitle(EditorEmbeddedContext context)
    {
        var activePresetName = _database.GetEmbeddedComponentPresetName(context.OwnerNode, context.Slots);
        SetPresetText(string.IsNullOrWhiteSpace(activePresetName) ? null : $"Variant: {activePresetName}");
        var items = new List<EditorBreadcrumbItem>
        {
            new(context.OwnerNode.Name, () => _showNode(context.OwnerNode, false)),
        };
        for (var index = 0; index < context.Slots.Count; index++)
        {
            var slot = context.Slots[index];
            var slotPresetName = _database.GetEmbeddedComponentPresetName(
                context.OwnerNode,
                context.Slots.Take(index + 1).ToArray());
            var label = string.IsNullOrWhiteSpace(slotPresetName)
                ? slot.Label
                : $"{slot.Label} · {slotPresetName}";
            if (index == context.Slots.Count - 1)
            {
                items.Add(new EditorBreadcrumbItem(label));
                continue;
            }

            var slotIndex = index + 1;
            items.Add(new EditorBreadcrumbItem(
                label,
                () => _showEmbeddedContext(new EditorEmbeddedContext(context.OwnerNode, context.Slots.Take(slotIndex).ToArray()))));
        }

        EditorBreadcrumbBar.Render(
            _breadcrumbPanel,
            items,
            EditorStructureButton.Create(async () => await _embeddedUsageNavigator.ShowForEmbedded(context.OwnerNode, context.Slot)));
        SetHeaderActions(null);
    }

    private string? EditorPresetTextForSelection()
    {
        return _selectedNode()?.Kind switch
        {
            ProjectTreeNodeKind.ComponentPreset => $"Variant: {_selectedNode()!.Name}",
            _ => null,
        };
    }

    private void SetPresetText(string? text)
    {
        _presetTextBlock.Text = text ?? "";
        _presetTextBlock.IsVisible = !string.IsNullOrWhiteSpace(text);
    }

    private void SetHeaderActions(Control? content)
    {
        _actionsPanel.Children.Clear();
        if (content is not null)
        {
            _actionsPanel.Children.Add(content);
        }
    }

    private Control? CreateStructureButtonForSelectedComponent()
    {
        var node = EditorNodeSelectionState.SelectedComponentClassNode(_selectedNode());
        if (node is null)
        {
            return null;
        }

        return EditorStructureButton.Create(async () => await _embeddedUsageNavigator.ShowForComponent(node));
    }

    private Control? CreateHeaderActionsForSelectedComponent()
    {
        var node = EditorNodeSelectionState.SelectedComponentClassNode(_selectedNode());
        if (node is null)
        {
            return null;
        }

        var selected = _selectedNode();
        var presetSourceNode = selected?.Kind == ProjectTreeNodeKind.ComponentPreset
            ? selected
            : _nodeSelection.PreferredPresetNode(node);
        if (presetSourceNode.Kind != ProjectTreeNodeKind.ComponentPreset)
        {
            return null;
        }

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                CreateSavePresetButton(presetSourceNode),
            },
        };
    }

    private Button CreateSavePresetButton(ProjectTreeNode node)
    {
        var icon = EditorIcons.Create(EditorIcons.Add, 15);
        EditorIcons.ApplyBrush(icon, new SolidColorBrush(Color.Parse("#D6A638")));
        var button = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                Children =
                {
                    icon,
                    new TextBlock
                    {
                        Text = "Save variant",
                        FontWeight = FontWeight.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                },
            },
            Height = 34,
            MinWidth = 126,
            Padding = new Thickness(10, 0),
            Background = Brushes.Transparent,
            BorderBrush = new SolidColorBrush(Color.Parse("#80D6A638")),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, "Save variant");
        button.Click += async (_, _) => await _savePreset(node);
        return button;
    }
}
