using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using SukiUI;
using SukiUI.Controls;
using SukiUI.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell;

public partial class MainWindow : SukiWindow
{
    private sealed record EmbeddedEditorContext(
        ProjectTreeNode OwnerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> Slots)
    {
        public EmbeddedComponentSlotDefinition Slot => Slots[^1];
    }

    private bool _isDark = true;
    private readonly SpikeDatabase _database = new(SpikeDatabase.DefaultDatabasePath());
    private readonly CoreFieldValueService _coreFieldValues;
    private readonly RecordClassFieldValueService _recordClassFieldValues;
    private readonly ComponentClassFieldValueService _componentClassFieldValues;
    private readonly ActorAvatarPreviewController _actorAvatarPreviews;
    private readonly EditorCollectionCardFactory _collectionCards;
    private readonly EditorPreviewController _previewController;
    private readonly IEditorShellMessageSink _messages;
    private readonly EditorShellStateService _shellState;
    private readonly EditorNavigationRenderer _navigationRenderer;
    private readonly EditorFieldPostCommitEffects _fieldPostCommitEffects;
    private readonly EditorPathBrowser _pathBrowser;
    private readonly EditorDomainDialogService _domainDialogs;
    private readonly EditorDictionaryFieldServices _dictionaryFieldServices;
    private readonly EditorViewStateController _editorViewState;
    private readonly EditorCardHostController _editorCardHost;
    private readonly EditorFieldValueRouter _fieldValues;
    private readonly EditorEmbeddedUsageNavigator _embeddedUsageNavigator;
    private readonly EditorFieldCommitCoordinator _fieldCommitCoordinator = new();
    private readonly EditorActiveFieldControls _activeFieldControls = new();
    private readonly HashSet<string> _expandedNodeIds = [];
    private readonly Dictionary<string, string> _lastComponentPresetNodeIds = new(StringComparer.Ordinal);
    private List<ProjectTreeNode> _treeRoots = [];
    private ProjectTreeNode? _selectedNode;
    private EmbeddedEditorContext? _embeddedEditorContext;

    public MainWindow()
    {
        InitializeComponent();
        _coreFieldValues = new CoreFieldValueService(_database);
        _recordClassFieldValues = new RecordClassFieldValueService(_database);
        _componentClassFieldValues = new ComponentClassFieldValueService(_database);
        _actorAvatarPreviews = new ActorAvatarPreviewController(_database, () => _isDark);
        _messages = new EditorShellMessageSink(ShellMessagesTextBox);
        _previewController = new EditorPreviewController(
            _database,
            PreviewDeviceComboBox,
            PreviewThemeComboBox,
            PreviewModeComboBox,
            PreviewScaleComboBox,
            PreviewMarksToggle,
            _messages,
            RuntimePreviewHost,
            DesignPreviewHost,
            () => _isDark,
            () => _selectedNode,
            this);
        PreviewDeviceComboBox.SelectionChanged += (_, _) => _previewController.OnDeviceChanged();
        PreviewThemeComboBox.SelectionChanged += (_, _) => _previewController.OnThemeChanged();
        PreviewModeComboBox.SelectionChanged += (_, _) => _previewController.OnModeChanged();
        PreviewScaleComboBox.SelectionChanged += (_, _) => _previewController.OnScaleChanged();
        PreviewMarksToggle.PropertyChanged += (_, change) =>
        {
            if (change.Property == ToggleSwitch.IsCheckedProperty)
            {
                _previewController.OnMarksChanged();
            }
        };
        _shellState = new EditorShellStateService(this, ShellColumns);
        _navigationRenderer = new EditorNavigationRenderer(
            () => _selectedNode,
            () => _isDark,
            (node) => _expandedNodeIds.Contains(node.Id),
            SelectTreeNode,
            (node) => ShowNode(ResolveSelectionNode(node)),
            ToggleTreeGroup,
            AddChild,
            DuplicateNode,
            RenameNode,
            DeleteNode);
        _fieldPostCommitEffects = new EditorFieldPostCommitEffects(
            _database,
            () => _previewController.SelectedDeviceId,
            SetEditorRootTitle,
            RebuildNavigationCards,
            RefreshPreviewDevice,
            RefreshPreviewOptions);
        _pathBrowser = new EditorPathBrowser(StorageProvider, _database, () => _selectedNode);
        _domainDialogs = new EditorDomainDialogService(
            this,
            _database,
            () => _isDark,
            ShowInfoDialog,
            _pathBrowser.BrowseSvgFile,
            ReloadAndSelect);
        _dictionaryFieldServices = new EditorDictionaryFieldServices(_database, _pathBrowser, _domainDialogs);
        _editorViewState = new EditorViewStateController(EditorScrollViewer);
        _editorCardHost = new EditorCardHostController(EditorCardsPanel);
        _fieldValues = new EditorFieldValueRouter(
            _coreFieldValues,
            _recordClassFieldValues,
            _componentClassFieldValues,
            _actorAvatarPreviews,
            _fieldPostCommitEffects);
        _embeddedUsageNavigator = new EditorEmbeddedUsageNavigator(
            _database,
            this,
            () => _isDark,
            SelectNodeById,
            LoadProjectTree,
            () => _selectedNode,
            OpenEmbeddedComponentEditor,
            _messages);
        _collectionCards = new EditorCollectionCardFactory(
            _database,
            () => _isDark,
            ShowInfoDialog,
            _domainDialogs.ConfirmIconTokenDelete,
            _domainDialogs.ShowIconThemeSearch,
            _domainDialogs.ShowIconThemeSvgReplace,
            ReloadAndSelect,
            _pathBrowser.BrowsePath,
            _domainDialogs.ShowIconTokenPicker,
            RefreshPreviewDevice);
        _shellState.Restore();
        Closing += (_, _) => _shellState.Save();
        ApplyTheme();
        LoadProjectTree();
        InitializePreviewOptions();
    }

    private void OnToggleThemeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isDark = !_isDark;
        ApplyTheme();
    }

    private void OnRefreshUsageClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedId = _selectedNode?.Id;
        LoadProjectTree();
        if (selectedId is not null)
        {
            SelectNodeById(selectedId);
        }
    }

    private void ApplyTheme()
    {
        var themeVariant = _isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        RequestedThemeVariant = themeVariant;
        Application.Current!.RequestedThemeVariant = themeVariant;
        SukiTheme.GetInstance().ChangeBaseTheme(themeVariant);
        SukiTheme.GetInstance().ChangeColorTheme(SukiColor.Blue);

        if (_isDark)
        {
            ThemeLabel.Text = "Dark mode";
            ThemeToggleButton.Content = "Switch to light";
            RefreshPreviewDevice();
            return;
        }

        ThemeLabel.Text = "Light mode";
        ThemeToggleButton.Content = "Switch to dark";
        RefreshPreviewDevice();
    }

    private void InitializePreviewOptions()
    {
        _previewController.Initialize(_treeRoots);
    }

    private void RefreshPreviewDevice()
    {
        _previewController.Refresh();
    }

    private void RefreshPreviewOptions()
    {
        _previewController.RefreshOptions(_treeRoots);
    }

    private void LoadProjectTree()
    {
        _treeRoots = _database.LoadProjectTree();
        if (_expandedNodeIds.Count == 0 && _treeRoots.Count > 0)
        {
            _expandedNodeIds.Add(_treeRoots[0].Id);
        }

        if (_treeRoots.Count > 0)
        {
            var selected = _selectedNode is not null
                ? FindNodeById(_treeRoots, _selectedNode.Id)
                : null;
            selected = selected is not null && CanSelectTreeNode(selected) ? selected : null;
            selected ??= _treeRoots.FirstOrDefault((node) => node.CanOpenEditor) ?? _treeRoots[0];
            selected = ResolveSelectionNode(selected);

            ExpandAncestors(selected);
            RebuildNavigationCards();
            ShowNode(selected, rebuildTree: false);
            return;
        }

        RebuildNavigationCards();
    }

    private void SelectTreeNode(ProjectTreeNode node)
    {
        if (!CanSelectTreeNode(node))
        {
            ToggleTreeGroup(node);
            return;
        }

        ShowNode(node);
    }

    private void ToggleTreeGroup(ProjectTreeNode node)
    {
        if (node.Children.Count == 0) return;

        if (_expandedNodeIds.Contains(node.Id))
        {
            CollapseNodeAndDescendants(node);
        }
        else
        {
            CollapseVisibleNavigationPeers(node);
            _expandedNodeIds.Add(node.Id);
        }

        RebuildNavigationCards();
    }

    private void CollapseSiblingNodes(ProjectTreeNode node)
    {
        if (node.Parent is null) return;

        foreach (var sibling in node.Parent.Children.Where((child) => child.Id != node.Id))
        {
            CollapseNodeAndDescendants(sibling);
        }
    }

    private void CollapseVisibleNavigationPeers(ProjectTreeNode node)
    {
        if (node.Kind == ProjectTreeNodeKind.Project)
        {
            foreach (var child in node.Children)
            {
                CollapseNodeAndDescendants(child);
            }
            return;
        }

        if (node.Parent?.Kind == ProjectTreeNodeKind.Project)
        {
            CollapseNodeAndDescendants(node.Parent);
            CollapseSiblingNodes(node);
            return;
        }

        CollapseSiblingNodes(node);
    }

    private void CollapseNodeAndDescendants(ProjectTreeNode node)
    {
        _expandedNodeIds.Remove(node.Id);
        foreach (var child in node.Children)
        {
            CollapseNodeAndDescendants(child);
        }
    }

    private void RebuildNavigationCards()
    {
        _navigationRenderer.Rebuild(NavigationCardsPanel, _treeRoots);
    }

    private void ShowNode(ProjectTreeNode node, bool rebuildTree = true)
    {
        node = ResolveSelectionNode(node);
        var previousNode = _selectedNode;
        var keepEditorViewState = _editorViewState.ShouldPreserve(previousNode, node);
        if (keepEditorViewState)
        {
            _editorViewState.Capture(previousNode, _editorCardHost.Cards);
        }

        _selectedNode = node;
        RememberComponentPresetSelection(node);
        ExpandAncestors(node);
        _embeddedEditorContext = null;
        var editorNode = EditorNodeForSelection(node);
        SetEditorRootTitle(editorNode.Name);
        BuildEditorCards(editorNode, node);
        if (keepEditorViewState)
        {
            _editorViewState.Restore(node, _editorCardHost.Cards);
        }

        RefreshPreviewDevice();
        if (rebuildTree)
        {
            RebuildNavigationCards();
        }
    }

    private void BuildEditorCards(ProjectTreeNode layoutNode, ProjectTreeNode dataNode)
    {
        _editorCardHost.Clear();
        _activeFieldControls.Clear();
        _actorAvatarPreviews.Reset();

        var layout = _database.LoadEditorLayout(layoutNode.RecordClassId);
        foreach (var layoutCard in layout.Cards
                     .Where((card) => card.Visible)
                     .OrderBy((card) => card.Order)
                     .ThenBy((card) => card.Label))
        {
            _editorCardHost.Add(CreateLayoutCard(dataNode, layoutCard));
        }

        foreach (var collectionCard in _collectionCards.Create(dataNode))
        {
            _editorCardHost.Add(collectionCard);
        }
    }

    private InstantEditorCard CreateLayoutCard(ProjectTreeNode node, EditorLayoutCard layoutCard)
    {
        var body = new StackPanel
        {
            Spacing = 12,
        };
        var controls = new List<DictionaryFieldControl>();
        var headerIcon = EditorIcons.Create(layoutCard.Icon, 18);
        var visibleGroups = layoutCard.VisibleGroups.ToList();
        var useSectionChrome = visibleGroups.Count > 1;

        foreach (var group in visibleGroups)
        {
            var groupPanel = new StackPanel
            {
                Spacing = 12,
            };

            _actorAvatarPreviews.AddIfNeeded(node, layoutCard, groupPanel);

            foreach (var layoutField in group.VisibleFields)
            {
                var field = _fieldValues.Create(node, layoutField.Id);
                var services = _dictionaryFieldServices.ForNode(
                    node,
                    (fieldId) => _activeFieldControls.ValueOrStored(fieldId, (id) => _fieldValues.CurrentStoredValue(node, id)),
                    (fieldId) => OpenEmbeddedComponentEditor(node, fieldId));
                var control = new DictionaryFieldControl(
                    field,
                    services);
                controls.Add(control);
                _activeFieldControls.Register(control);
                control.ValueCommitted += (_, value) =>
                {
                    try
                    {
                        _fieldCommitCoordinator.Commit(
                            control,
                            value,
                            (draftValue) => _fieldValues.ToStorageValue(node, field.Definition.Id, draftValue),
                            () => _fieldValues.CurrentStoredValue(node, field.Definition.Id),
                            (storedValue) => _fieldValues.Commit(node, field.Definition.Id, storedValue));
                        _actorAvatarPreviews.Refresh(node, _activeFieldControls.ControlsByFieldId);
                        _activeFieldControls.RefreshPreviews();
                        RefreshPreviewDevice();
                    }
                    catch (Exception exception)
                    {
                        _messages.Error($"Editor field {field.Definition.Id}", exception);
                    }
                };
                groupPanel.Children.Add(control);
            }

            if (groupPanel.Children.Count > 0)
            {
                body.Children.Add(useSectionChrome
                    ? EditorGroupBlock.Create(group, groupPanel)
                    : EditorGroupBlock.CreatePlain(group, groupPanel));
            }
        }

        if (body.Children.Count == 0)
        {
            body.Children.Add(new TextBlock
            {
                Text = "No fields in this card yet.",
                TextWrapping = TextWrapping.Wrap,
            });
        }

        var card = new InstantEditorCard(
            EditorCardHeader.Create(layoutCard.Label, EditorCardHeader.Subtitle(layoutCard), headerIcon),
            new Border
            {
                Padding = new Avalonia.Thickness(10),
                Child = body,
            },
            layoutCard.DefaultOpen)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        EditorCardHeader.SetOverrideState(headerIcon, controls);
        foreach (var control in controls)
        {
            control.ValueChanged += (_, _) =>
            {
                EditorCardHeader.SetOverrideState(headerIcon, controls);
                _actorAvatarPreviews.Refresh(node, _activeFieldControls.ControlsByFieldId);
            };
        }

        return card;
    }

    private Task OpenEmbeddedComponentEditor(ProjectTreeNode node, string slotFieldId)
    {
        try
        {
            if (node.Kind is not ProjectTreeNodeKind.ComponentClass and not ProjectTreeNodeKind.ComponentPreset)
            {
                return Task.CompletedTask;
            }

            if (!EmbeddedComponentSlotCatalog.TryGet(slotFieldId, out var slot))
            {
                return Task.CompletedTask;
            }

            ShowEmbeddedContext(new EmbeddedEditorContext(node, [slot]));
        }
        catch (Exception exception)
        {
            _messages.Error($"Embedded component {slotFieldId}", exception);
        }

        return Task.CompletedTask;
    }

    private Task OpenEmbeddedComponentEditor(EmbeddedEditorContext parentContext, string slotFieldId)
    {
        try
        {
            if (!EmbeddedComponentSlotCatalog.TryGet(slotFieldId, out var slot))
            {
                return Task.CompletedTask;
            }

            ShowEmbeddedContext(new EmbeddedEditorContext(parentContext.OwnerNode, [.. parentContext.Slots, slot]));
        }
        catch (Exception exception)
        {
            _messages.Error($"Embedded component {slotFieldId}", exception);
        }

        return Task.CompletedTask;
    }

    private void ShowEmbeddedContext(EmbeddedEditorContext context)
    {
        _embeddedEditorContext = context;
        SetEditorEmbeddedTitle(context);
        BuildEmbeddedComponentCards(context);
        RefreshPreviewDevice();
    }

    private void BuildEmbeddedComponentCards(EmbeddedEditorContext context)
    {
        _editorCardHost.Clear();
        _activeFieldControls.Clear();
        _actorAvatarPreviews.Reset();

        var layout = _database.LoadEditorLayout(context.Slot.RecordClassId);
        foreach (var layoutCard in layout.Cards
                     .Where((card) => card.Visible && EmbeddedCardHasFields(card))
                     .OrderBy((card) => card.Order)
                     .ThenBy((card) => card.Label))
        {
            _editorCardHost.Add(CreateEmbeddedLayoutCard(context, layoutCard));
        }
    }

    private static bool EmbeddedCardHasFields(EditorLayoutCard layoutCard)
    {
        return layoutCard.VisibleGroups
            .SelectMany((group) => group.VisibleFields)
            .Any((field) => field.Id.StartsWith("component.", StringComparison.Ordinal));
    }

    private InstantEditorCard CreateEmbeddedLayoutCard(EmbeddedEditorContext context, EditorLayoutCard layoutCard)
    {
        var body = new StackPanel
        {
            Spacing = 12,
        };
        var controls = new List<DictionaryFieldControl>();
        var headerIcon = EditorIcons.Create(layoutCard.Icon, 18);
        var visibleGroups = layoutCard.VisibleGroups.ToList();
        var useSectionChrome = visibleGroups.Count > 1;

        foreach (var group in visibleGroups)
        {
            var groupPanel = new StackPanel
            {
                Spacing = 12,
            };

            foreach (var layoutField in group.VisibleFields
                         .Where((field) => field.Id.StartsWith("component.", StringComparison.Ordinal)))
            {
                var field = _componentClassFieldValues.CreateEmbeddedFieldValue(
                    context.OwnerNode,
                    context.Slots,
                    layoutField.Id);
                var services = _dictionaryFieldServices.ForNode(
                    context.OwnerNode,
                    (fieldId) => _activeFieldControls.ValueOrStored(fieldId, (id) =>
                        _componentClassFieldValues.CreateEmbeddedFieldValue(
                            context.OwnerNode,
                            context.Slots,
                            id).Value),
                    (fieldId) => OpenEmbeddedComponentEditor(context, fieldId));
                var control = new DictionaryFieldControl(field, services);
                controls.Add(control);
                _activeFieldControls.Register(control);
                control.ValueCommitted += (_, value) =>
                {
                    try
                    {
                        if (value == field.Definition.InheritedStorageValue)
                        {
                            _componentClassFieldValues.CommitEmbeddedFieldValue(
                                context.OwnerNode,
                                context.Slots,
                                field.Definition.Id,
                                value);
                            control.AcceptInheritedValueAsDefault();
                            _activeFieldControls.RefreshPreviews();
                            RefreshPreviewDevice();
                            return;
                        }

                        _fieldCommitCoordinator.Commit(
                            control,
                            value,
                            (draftValue) => draftValue,
                            () =>
                            {
                                var current = _componentClassFieldValues.CreateEmbeddedFieldValue(
                                    context.OwnerNode,
                                    context.Slots,
                                    field.Definition.Id);
                                return current.IsInherited
                                    ? current.Definition.InheritedStorageValue
                                    : current.Value;
                            },
                            (storedValue) => _componentClassFieldValues.CommitEmbeddedFieldValue(
                                context.OwnerNode,
                                context.Slots,
                                field.Definition.Id,
                                storedValue));
                        _activeFieldControls.RefreshPreviews();
                        RefreshPreviewDevice();
                    }
                    catch (Exception exception)
                    {
                        _messages.Error($"Embedded field {field.Definition.Id}", exception);
                    }
                };
                groupPanel.Children.Add(control);
            }

            if (groupPanel.Children.Count > 0)
            {
                body.Children.Add(useSectionChrome
                    ? EditorGroupBlock.Create(group, groupPanel)
                    : EditorGroupBlock.CreatePlain(group, groupPanel));
            }
        }

        var embeddedBody = new Border
        {
            Padding = new Thickness(10),
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 214, 166, 56)),
            Child = body,
        };
        var card = new InstantEditorCard(
            EditorCardHeader.Create(layoutCard.Label, $"Embedded override · {context.OwnerNode.Name}", headerIcon),
            embeddedBody,
            layoutCard.DefaultOpen)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        EditorCardHeader.SetOverrideState(headerIcon, controls);
        foreach (var control in controls)
        {
            control.ValueChanged += (_, _) =>
            {
                EditorCardHeader.SetOverrideState(headerIcon, controls);
            };
        }

        return card;
    }

    private void SetEditorRootTitle(string title)
    {
        SetEditorPresetText(EditorPresetTextForSelection());
        EditorBreadcrumbBar.Render(EditorBreadcrumbPanel, [
            new EditorBreadcrumbItem(title),
        ], CreateStructureButtonForSelectedComponent());
        SetEditorHeaderActions(CreateHeaderActionsForSelectedComponent());
    }

    private string? EditorPresetTextForSelection()
    {
        return _selectedNode?.Kind switch
        {
            ProjectTreeNodeKind.ComponentPreset => $"Preset: {_selectedNode.Name}",
            _ => null,
        };
    }

    private void SetEditorEmbeddedTitle(EmbeddedEditorContext context)
    {
        var activePresetName = _database.GetEmbeddedComponentPresetName(context.OwnerNode, context.Slots);
        SetEditorPresetText(string.IsNullOrWhiteSpace(activePresetName) ? null : $"Preset: {activePresetName}");
        var items = new List<EditorBreadcrumbItem>
        {
            new(context.OwnerNode.Name, () => ShowNode(context.OwnerNode, rebuildTree: false)),
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
                () => ShowEmbeddedContext(new EmbeddedEditorContext(context.OwnerNode, context.Slots.Take(slotIndex).ToArray()))));
        }

        EditorBreadcrumbBar.Render(
            EditorBreadcrumbPanel,
            items,
            EditorStructureButton.Create(async () => await _embeddedUsageNavigator.ShowForEmbedded(context.OwnerNode, context.Slot)));
        SetEditorHeaderActions(null);
    }

    private void SetEditorPresetText(string? text)
    {
        EditorPresetTextBlock.Text = text ?? "";
        EditorPresetTextBlock.IsVisible = !string.IsNullOrWhiteSpace(text);
    }

    private void SetEditorHeaderActions(Control? content)
    {
        EditorHeaderActionsPanel.Children.Clear();
        if (content is not null)
        {
            EditorHeaderActionsPanel.Children.Add(content);
        }
    }

    private Control? CreateStructureButtonForSelectedComponent()
    {
        var node = SelectedComponentClassNode();
        if (node is null)
        {
            return null;
        }

        return EditorStructureButton.Create(async () => await _embeddedUsageNavigator.ShowForComponent(node));
    }

    private Control? CreateHeaderActionsForSelectedComponent()
    {
        var node = SelectedComponentClassNode();
        if (node is null)
        {
            return null;
        }

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                CreateSavePresetButton(node),
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
                        Text = "Save preset",
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
        ToolTip.SetTip(button, "Save preset");
        button.Click += async (_, _) => await SaveCurrentComponentPreset(node);
        return button;
    }

    private async Task SaveCurrentComponentPreset(ProjectTreeNode node)
    {
        var presetName = await new EditorDialogService(this, _isDark).PromptText(
            "Save preset",
            "Preset name",
            $"{node.Name} preset");
        if (string.IsNullOrWhiteSpace(presetName))
        {
            return;
        }

        try
        {
            var preset = _database.SaveComponentPreset(node, presetName);
            ReloadAndSelect(preset);
        }
        catch (Exception exception)
        {
            _messages.Error($"Save preset {node.Name}", exception);
        }
    }

    private async Task AddChild(ProjectTreeNode parent)
    {
        var workflow = new EditorAddChildWorkflow(this, _database, ShowInfoDialog);
        var child = await workflow.TryAdd(parent);
        if (child is null) return;

        if (parent.Kind == ProjectTreeNodeKind.IconThemesRoot)
        {
            LoadProjectTree();
            return;
        }

        ReloadAndSelect(child);
    }

    private async Task ShowInfoDialog(string title, string message)
    {
        await new EditorDialogService(this, _isDark).ShowInfo(title, message);
    }

    private void DuplicateNode(ProjectTreeNode node)
    {
        if (node.Parent is null) return;

        var copy = _database.Duplicate(node);
        ReloadAndSelect(copy);
    }

    private async Task RenameNode(ProjectTreeNode node)
    {
        if (!node.CanRenameDirectly)
        {
            return;
        }

        var nextName = await new EditorDialogService(this, _isDark).PromptText(
            "Rename",
            "Name",
            node.Name);
        if (string.IsNullOrWhiteSpace(nextName) || nextName.Trim().Equals(node.Name, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var renamed = _database.RenameDirectNode(node, nextName);
            ReloadAndSelect(renamed);
        }
        catch (Exception exception)
        {
            _messages.Error($"Rename {node.Name}", exception);
        }
    }

    private async Task DeleteNode(ProjectTreeNode node)
    {
        if (node.Parent is null) return;

        var deleteNodeId = node.Id;
        LoadProjectTree();
        node = FindNodeById(_treeRoots, deleteNodeId) ?? node;
        if (node.Parent is null) return;

        var usages = _database.GetReferenceUsages(node);
        if (usages.Count > 0)
        {
            await ShowInfoDialog(
                "Cannot delete used item",
                $"{node.Name} is still used in:\n\n{string.Join(Environment.NewLine, usages.Take(12))}\n\nClean these references first, then delete it.");
            return;
        }

        var confirmed = await ConfirmDelete(node);
        if (!confirmed) return;

        var nextSelectionId = node.Parent.Id;
        try
        {
            _database.Delete(node);
        }
        catch (Exception exception)
        {
            await ShowInfoDialog("Delete failed", exception.Message);
            return;
        }

        var nextSelection = new ProjectTreeNode(
            node.Parent.Kind,
            nextSelectionId,
            node.Parent.Name,
            node.Parent.Notes,
            node.Parent.RecordClassId);
        ReloadAndSelect(nextSelection);
    }

    private async Task<bool> ConfirmDelete(ProjectTreeNode node)
    {
        return await new EditorDialogService(this, _isDark).ConfirmDelete(node);
    }

    private void ReloadAndSelect(ProjectTreeNode node)
    {
        _selectedNode = node;
        LoadProjectTree();
        RefreshPreviewOptions();
        SelectNodeById(node.Id);
    }

    private bool SelectNodeById(string nodeId)
    {
        var node = FindNodeById(_treeRoots, nodeId);
        if (node is null)
        {
            return false;
        }

        var selectableNode = CanSelectTreeNode(node) ? node : ClosestEditableNode(node);
        selectableNode = ResolveSelectionNode(selectableNode);
        ExpandAncestors(selectableNode);
        ShowNode(selectableNode, rebuildTree: true);
        return true;
    }

    private static bool CanSelectTreeNode(ProjectTreeNode node)
    {
        return node.CanOpenEditor || node.Kind == ProjectTreeNodeKind.ComponentPreset;
    }

    private ProjectTreeNode ResolveSelectionNode(ProjectTreeNode node)
    {
        return node.Kind == ProjectTreeNodeKind.ComponentClass
            ? PreferredPresetNode(node)
            : node;
    }

    private ProjectTreeNode PreferredPresetNode(ProjectTreeNode componentClassNode)
    {
        if (_lastComponentPresetNodeIds.TryGetValue(componentClassNode.Id, out var presetNodeId)
            && componentClassNode.Children.FirstOrDefault((child) => child.Id.Equals(presetNodeId, StringComparison.Ordinal)) is { } rememberedPreset)
        {
            return rememberedPreset;
        }

        return componentClassNode.Children.FirstOrDefault((child) =>
                child.Kind == ProjectTreeNodeKind.ComponentPreset
                && child.Id.EndsWith("::preset::default", StringComparison.Ordinal))
            ?? componentClassNode.Children.FirstOrDefault((child) => child.Kind == ProjectTreeNodeKind.ComponentPreset)
            ?? componentClassNode;
    }

    private void RememberComponentPresetSelection(ProjectTreeNode node)
    {
        if (node.Kind == ProjectTreeNodeKind.ComponentPreset
            && node.Parent?.Kind == ProjectTreeNodeKind.ComponentClass)
        {
            _lastComponentPresetNodeIds[node.Parent.Id] = node.Id;
        }
    }

    private static ProjectTreeNode EditorNodeForSelection(ProjectTreeNode node)
    {
        return node.Kind == ProjectTreeNodeKind.ComponentPreset && node.Parent is not null
            ? node.Parent
            : node;
    }

    private ProjectTreeNode? SelectedComponentClassNode()
    {
        var node = _selectedNode is null ? null : EditorNodeForSelection(_selectedNode);
        return node?.Kind == ProjectTreeNodeKind.ComponentClass ? node : null;
    }

    private static ProjectTreeNode ClosestEditableNode(ProjectTreeNode node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current.CanOpenEditor)
            {
                return current;
            }
        }

        return node;
    }

    private static ProjectTreeNode? FindNodeById(IEnumerable<ProjectTreeNode> nodes, string nodeId)
    {
        foreach (var node in nodes)
        {
            if (node.Id == nodeId) return node;

            var child = FindNodeById(node.Children, nodeId);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private void ExpandAncestors(ProjectTreeNode node)
    {
        var parent = node.Parent;
        while (parent is not null)
        {
            _expandedNodeIds.Add(parent.Id);
            parent = parent.Parent;
        }
    }
}
