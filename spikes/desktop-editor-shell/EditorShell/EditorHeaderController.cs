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
    private readonly EmbeddedComponentDocumentStore _embeddedDocuments;
    private readonly ProductionScreenPresentationDataSource _screenPresentation;
    private readonly Func<ProjectTreeNode?> _selectedNode;
    private readonly EditorNodeSelectionState _nodeSelection;
    private readonly EditorEmbeddedUsageNavigator _embeddedUsageNavigator;
    private readonly Action<ProjectTreeNode, bool> _showNode;
    private readonly Action<ProjectTreeNode> _returnToEmbeddedOwner;
    private readonly Action<EditorEmbeddedContext> _showEmbeddedContext;
    private readonly Func<ProjectTreeNode, Task> _saveVariant;
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
        Func<ProjectTreeNode, Task> saveVariant,
        Func<ProjectTreeNode, IReadOnlyList<EditorVariantHistorySnapshot>> variantHistory,
        Func<ProjectTreeNode, EditorVariantHistorySnapshot, Task> restoreVariantSnapshot,
        EditorActiveFieldControls activeFieldControls)
    {
        _breadcrumbPanel = breadcrumbPanel;
        _contextStripHost = contextStripHost;
        _actionsPanel = actionsPanel;
        _embeddedDocuments = new EmbeddedComponentDocumentStore(database);
        _screenPresentation = new ProductionScreenPresentationDataSource(database);
        _selectedNode = selectedNode;
        _nodeSelection = nodeSelection;
        _embeddedUsageNavigator = embeddedUsageNavigator;
        _showNode = showNode;
        _returnToEmbeddedOwner = returnToEmbeddedOwner;
        _showEmbeddedContext = showEmbeddedContext;
        _saveVariant = saveVariant;
        _variantHistory = variantHistory;
        _restoreVariantSnapshot = restoreVariantSnapshot;
        _activeFieldControls = activeFieldControls;
    }

    public void SetRootTitle(string title)
    {
        var selected = _selectedNode();
        var productionPath = ProductionPath(selected);
        var breadcrumbItems = VariantPath(selected)
            ?? productionPath
                .Select((node, index) => new EditorBreadcrumbItem(
                    node.Name,
                    index == productionPath.Count - 1 ? null : () => _showNode(node, false)))
                .ToList();
        EditorBreadcrumbBar.Render(
            _breadcrumbPanel,
            productionPath.Count > 0
                ? breadcrumbItems
                : breadcrumbItems.Count > 0
                    ? breadcrumbItems
                    : [new EditorBreadcrumbItem(title)],
            CreateStructureButtonForSelectedComponent());
        SetHeaderActions(CreateHeaderActionsForSelectedComponent());
        SetContextStrip(ContextMetadataForSelection());
    }

    private static List<EditorBreadcrumbItem>? VariantPath(ProjectTreeNode? selected)
    {
        if (selected is not
            {
                Kind: ProjectTreeNodeKind.ComponentVariant or ProjectTreeNodeKind.ModuleVariant,
                Parent: { } parent,
            })
        {
            return null;
        }

        return
        [
            new EditorBreadcrumbItem(parent.Name),
            new EditorBreadcrumbItem(selected.Name),
        ];
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
        var activeVariantName = _embeddedDocuments.ActiveVariantName(context);
        var items = new List<EditorBreadcrumbItem>
        {
            new(context.OwnerNode.Name, () => _returnToEmbeddedOwner(context.OwnerNode)),
        };
        if (context.RuntimeSource is not null)
        {
            var rootVariantName = _embeddedDocuments.ActiveVariantName(context.Ancestor(0));
            items.Add(context.Slots.Count == 0
                ? new EditorBreadcrumbItem($"Component: {rootVariantName}")
                : new EditorBreadcrumbItem(
                    $"Component: {rootVariantName}",
                    () => _showEmbeddedContext(context.Ancestor(0))));
        }
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
                () => _showEmbeddedContext(context.Ancestor(slotIndex))));
        }

        EditorBreadcrumbBar.Render(
            _breadcrumbPanel,
            items,
            context.RuntimeSource is not null
                ? null
                : EditorStructureButton.Create(async () => await _embeddedUsageNavigator.ShowForEmbedded(context.OwnerNode, context.Slot)));
        SetHeaderActions(null);
        SetContextStrip(ContextMetadataForEmbedded(context, activeVariantName));
    }

    private EditorContextStripMetadata? ContextMetadataForSelection()
    {
        var selected = _selectedNode();
        if (selected is null) return null;
        if (selected.Kind is ProjectTreeNodeKind.Episode or ProjectTreeNodeKind.Shot)
        {
            return null;
        }
        var variantNode = SelectedVariantNode(selected);
        var identities = variantNode is not null
            ? []
            : selected.Kind switch
        {
            ProjectTreeNodeKind.ComponentClass =>
                new[] { new EditorContextIdentity("Component", selected.Name) },
            ProjectTreeNodeKind.Module => [new EditorContextIdentity("Module", selected.Name)],
            ProjectTreeNodeKind.ModuleInstance => ScreenContextIdentities(selected.Id),
            ProjectTreeNodeKind.App => [new EditorContextIdentity("App", selected.Name)],
            _ => [new EditorContextIdentity(EditorUiText.IdentifierLabel(selected.Kind.ToString()), selected.Name)],
        };
        var statusNode = variantNode ?? selected;
        return new EditorContextStripMetadata(
            identities,
            VariantSelectorFor(variantNode),
            OverrideCount(),
            statusNode.IsUsed,
            statusNode.IsProtected,
            statusNode.IsLocked);
    }

    private IReadOnlyList<EditorContextIdentity> ScreenContextIdentities(string moduleInstanceId)
    {
        var context = _screenPresentation.Load(moduleInstanceId);
        return
        [
            new EditorContextIdentity("Module", context.Module),
            new EditorContextIdentity("Variant", context.Variant),
            new EditorContextIdentity("Duration", $"{context.DurationFrames} frames"),
            new EditorContextIdentity("Transition", EditorUiText.IdentifierLabel(context.Transition)),
        ];
    }

    private EditorContextStripMetadata ContextMetadataForEmbedded(EditorEmbeddedContext context, string activeVariantName)
    {
        var component = EditorUiText.IdentifierLabel(context.ComponentType);
        var identities = new List<EditorContextIdentity> { new("Component", component) };
        if (!string.IsNullOrWhiteSpace(activeVariantName)) identities.Add(new EditorContextIdentity("Variant", activeVariantName));
        return new EditorContextStripMetadata(identities, null, OverrideCount());
    }

    private ProjectTreeNode? SelectedVariantNode(ProjectTreeNode selected)
    {
        return selected.Kind switch
        {
            ProjectTreeNodeKind.ComponentVariant or ProjectTreeNodeKind.ModuleVariant => selected,
            ProjectTreeNodeKind.ComponentClass => _nodeSelection.PreferredVariantNode(selected) is { Kind: ProjectTreeNodeKind.ComponentVariant } variant
                ? variant
                : null,
            ProjectTreeNodeKind.Module => _nodeSelection.PreferredModuleVariantNode(selected) is { Kind: ProjectTreeNodeKind.ModuleVariant } variant
                ? variant
                : null,
            _ => null,
        };
    }

    private EditorContextVariantSelector? VariantSelectorFor(ProjectTreeNode? selectedVariant)
    {
        if (selectedVariant?.Parent is not { } parent)
        {
            return null;
        }

        var variants = parent.Children
            .Where((child) => child.Kind == selectedVariant.Kind)
            .ToList();
        var options = variants
            .Select((variant) => new FieldOption(variant.Id, variant.Name))
            .ToList();
        return new EditorContextVariantSelector(
            options,
            selectedVariant.Id,
            (variantId) =>
            {
                var next = variants.FirstOrDefault((variant) => variant.Id.Equals(variantId, StringComparison.Ordinal));
                if (next is not null && next.Id != selectedVariant.Id)
                {
                    _showNode(next, true);
                }
            });
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
        if (_selectedNode() is { Kind: ProjectTreeNodeKind.ModuleVariant } moduleVariant)
        {
            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    CreateHistoryComboBox(moduleVariant),
                    CreateNewVariantButton(moduleVariant),
                },
            };
        }
        var node = EditorNodeSelectionState.SelectedComponentClassNode(_selectedNode());
        if (node is null)
        {
            return null;
        }

        var selected = _selectedNode();
        var variantSourceNode = selected?.Kind == ProjectTreeNodeKind.ComponentVariant
            ? selected
            : _nodeSelection.PreferredVariantNode(node);
        if (variantSourceNode.Kind != ProjectTreeNodeKind.ComponentVariant)
        {
            return null;
        }

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                CreateHistoryComboBox(variantSourceNode),
                CreateNewVariantButton(variantSourceNode),
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

    private Button CreateNewVariantButton(ProjectTreeNode node)
    {
        var icon = EditorIcons.CreateSemantic("New Variant", EditorIcons.Add, 15);
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
                        Text = "New Variant…",
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
        ToolTip.SetTip(button, $"Create a new Variant by cloning {node.Name}.");
        button.Click += async (_, _) => await _saveVariant(node);
        return button;
    }
}
