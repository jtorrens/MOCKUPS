using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorHeaderController
{
    private readonly Panel _breadcrumbPanel;
    private readonly Panel _contextStripHost;
    private readonly Panel _actionsPanel;
    private readonly SpikeDatabase _database;
    private readonly Func<ProjectTreeNode?> _selectedNode;
    private readonly EditorNodeSelectionState _nodeSelection;
    private readonly EditorEmbeddedUsageNavigator _embeddedUsageNavigator;
    private readonly Action<ProjectTreeNode, bool> _showNode;
    private readonly Action<ProjectTreeNode> _returnToEmbeddedOwner;
    private readonly Action<EditorEmbeddedContext> _showEmbeddedContext;
    private readonly Func<ProjectTreeNode, Task> _savePreset;
    private readonly Func<ProjectTreeNode, IReadOnlyList<EditorVariantHistorySnapshot>> _variantHistory;
    private readonly Func<ProjectTreeNode, EditorVariantHistorySnapshot, Task> _restoreVariantSnapshot;
    private readonly EditorActiveFieldControls _activeFieldControls;

    public EditorHeaderController(
        Panel breadcrumbPanel,
        Panel contextStripHost,
        Panel actionsPanel,
        SpikeDatabase database,
        Func<ProjectTreeNode?> selectedNode,
        EditorNodeSelectionState nodeSelection,
        EditorEmbeddedUsageNavigator embeddedUsageNavigator,
        Action<ProjectTreeNode, bool> showNode,
        Action<ProjectTreeNode> returnToEmbeddedOwner,
        Action<EditorEmbeddedContext> showEmbeddedContext,
        Func<ProjectTreeNode, Task> savePreset,
        Func<ProjectTreeNode, IReadOnlyList<EditorVariantHistorySnapshot>> variantHistory,
        Func<ProjectTreeNode, EditorVariantHistorySnapshot, Task> restoreVariantSnapshot,
        EditorActiveFieldControls activeFieldControls)
    {
        _breadcrumbPanel = breadcrumbPanel;
        _contextStripHost = contextStripHost;
        _actionsPanel = actionsPanel;
        _database = database;
        _selectedNode = selectedNode;
        _nodeSelection = nodeSelection;
        _embeddedUsageNavigator = embeddedUsageNavigator;
        _showNode = showNode;
        _returnToEmbeddedOwner = returnToEmbeddedOwner;
        _showEmbeddedContext = showEmbeddedContext;
        _savePreset = savePreset;
        _variantHistory = variantHistory;
        _restoreVariantSnapshot = restoreVariantSnapshot;
        _activeFieldControls = activeFieldControls;
    }

    public void SetRootTitle(string title)
    {
        var selected = _selectedNode();
        var productionPath = ProductionPath(selected);
        var breadcrumbItems = productionPath
            .Select((node, index) => new EditorBreadcrumbItem(
                node.Name,
                index == productionPath.Count - 1 ? null : () => _showNode(node, false)))
            .ToList();
        EditorBreadcrumbBar.Render(
            _breadcrumbPanel,
            productionPath.Count > 0
                ? breadcrumbItems
                : [new EditorBreadcrumbItem(title)],
            CreateStructureButtonForSelectedComponent());
        SetHeaderActions(CreateHeaderActionsForSelectedComponent());
        SetContextStrip(ContextMetadataForSelection());
    }

    private static IReadOnlyList<ProjectTreeNode> ProductionPath(ProjectTreeNode? selected)
    {
        var result = new List<ProjectTreeNode>();
        var current = selected;
        while (current is not null)
        {
            if (current.Kind is ProjectTreeNodeKind.Episode or ProjectTreeNodeKind.Shot or ProjectTreeNodeKind.ModuleInstance)
            {
                result.Add(current);
            }
            current = current.Parent;
        }
        result.Reverse();
        return result;
    }

    public void SetEmbeddedTitle(EditorEmbeddedContext context)
    {
        var activePresetName = _database.GetEmbeddedComponentPresetName(context.OwnerNode, context.Slots);
        var items = new List<EditorBreadcrumbItem>
        {
            new(context.OwnerNode.Name, () => _returnToEmbeddedOwner(context.OwnerNode)),
        };
        for (var index = 0; index < context.Slots.Count; index++)
        {
            var slot = context.Slots[index];
            var label = $"Slot: {slot.Label}";
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
        SetContextStrip(ContextMetadataForEmbedded(context, activePresetName));
    }

    private EditorContextStripMetadata? ContextMetadataForSelection()
    {
        var selected = _selectedNode();
        if (selected is null) return null;
        var identities = selected.Kind switch
        {
            ProjectTreeNodeKind.ComponentPreset when selected.Parent is not null =>
                new[] { new EditorContextIdentity("Component", selected.Parent.Name), new EditorContextIdentity("Variant", selected.Name) },
            ProjectTreeNodeKind.ComponentClass =>
                new[] { new EditorContextIdentity("Component", selected.Name), new EditorContextIdentity("Variant", _nodeSelection.PreferredPresetNode(selected).Name) },
            ProjectTreeNodeKind.Module => [new EditorContextIdentity("Module", selected.Name)],
            ProjectTreeNodeKind.ModuleInstance => [new EditorContextIdentity("Screen", selected.Name)],
            ProjectTreeNodeKind.App => [new EditorContextIdentity("App", selected.Name)],
            _ => [new EditorContextIdentity(EditorUiText.IdentifierLabel(selected.Kind.ToString()), selected.Name)],
        };
        return new EditorContextStripMetadata(identities, OverrideCount(), EditorContextSaveState.Saved);
    }

    private EditorContextStripMetadata ContextMetadataForEmbedded(EditorEmbeddedContext context, string activePresetName)
    {
        var component = EditorUiText.IdentifierLabel(context.Slot.EmbeddedComponentType);
        var identities = new List<EditorContextIdentity> { new("Component", component) };
        if (!string.IsNullOrWhiteSpace(activePresetName)) identities.Add(new EditorContextIdentity("Variant", activePresetName));
        return new EditorContextStripMetadata(identities, OverrideCount(), EditorContextSaveState.Saved);
    }

    private int OverrideCount() => _activeFieldControls.ControlsByFieldId.Values.Count((control) => control.HasLocalOverride);

    private void SetContextStrip(EditorContextStripMetadata? metadata)
    {
        _contextStripHost.Children.Clear();
        _contextStripHost.IsVisible = metadata is not null;
        if (metadata is not null) _contextStripHost.Children.Add(EditorContextStrip.Create(metadata));
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
                CreateHistoryComboBox(presetSourceNode),
                CreateSavePresetButton(presetSourceNode),
            },
        };
    }

    private Control CreateHistoryComboBox(ProjectTreeNode node)
    {
        var snapshots = _variantHistory(node);
        var placeholder = new FieldOption("", snapshots.Count == 0 ? "No history" : "Restore...");
        var options = new List<FieldOption> { placeholder };
        options.AddRange(snapshots.Select((snapshot) => new FieldOption(snapshot.Id, snapshot.Label)));

        var combo = new EditorInstantComboBox
        {
            Width = 126,
            Height = 34,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = snapshots.Count > 0 && !node.IsLocked,
            Opacity = snapshots.Count > 0 && !node.IsLocked ? 1 : 0.42,
        };
        combo.ItemsSource = options;
        combo.SelectedItem = placeholder;

        var suppress = false;
        combo.SelectionChanged += async (_, _) =>
        {
            if (suppress || combo.SelectedItem is not { } selected || string.IsNullOrWhiteSpace(selected.Value))
            {
                return;
            }

            var snapshot = snapshots.FirstOrDefault((item) => item.Id == selected.Value);
            if (snapshot is null)
            {
                return;
            }

            await _restoreVariantSnapshot(node, snapshot);
            suppress = true;
            combo.SelectedItem = placeholder;
            suppress = false;
        };
        ToolTip.SetTip(combo, node.IsLocked ? "Variant is locked" : "Restore a previous in-session version");
        return combo;
    }

    private Button CreateSavePresetButton(ProjectTreeNode node)
    {
        var icon = EditorIcons.CreateSemantic("Save variant", EditorIcons.Add, 15);
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
