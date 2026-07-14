using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using Mockups.DesktopEditorShell.Common;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell;

public partial class MainWindow : SukiWindow
{
    private readonly SpikeDatabase _database;
    private readonly CoreFieldValueService _coreFieldValues;
    private readonly RecordClassFieldValueService _recordClassFieldValues;
    private readonly ComponentClassFieldValueService _componentClassFieldValues;
    private readonly IEditorInlinePreviewController _inlinePreviews;
    private readonly EditorCollectionCardFactory _collectionCards;
    private readonly EditorPreviewController _previewController;
    private readonly IEditorShellMessageSink _messages;
    private readonly EditorThemeController _themeController;
    private readonly EditorNodeCommandController _nodeCommands;
    private readonly EditorShellStateService _shellState;
    private readonly EditorNavigationRenderer _navigationRenderer;
    private readonly ProductionShotContextService _productionShotContext;
    private readonly EditorFieldPostCommitEffects _fieldPostCommitEffects;
    private readonly EditorPathBrowser _pathBrowser;
    private readonly EditorDomainDialogService _domainDialogs;
    private readonly EditorDictionaryFieldServices _dictionaryFieldServices;
    private readonly EditorViewStateController _editorViewState;
    private readonly Dictionary<string, EditorViewState> _embeddedParentViewStates = new(StringComparer.Ordinal);
    private readonly EditorFieldValueRouter _fieldValues;
    private readonly EditorLayoutCardFactory _layoutCards;
    private readonly EditorContentController _editorContent;
    private readonly EditorEmbeddedEditorController _embeddedEditors;
    private readonly EditorEmbeddedUsageNavigator _embeddedUsageNavigator;
    private readonly EditorHeaderController _editorHeader;
    private readonly EditorVariantHistoryService _variantHistory;
    private readonly EditorTreeExpansionState _treeExpansion = new();
    private readonly EditorNodeSelectionState _nodeSelection = new();
    private readonly EditorFieldCommitCoordinator _fieldCommitCoordinator = new();
    private readonly EditorActiveFieldControls _activeFieldControls = new();
    private List<ProjectTreeNode> _treeRoots = [];
    private ProjectTreeNode? _selectedNode;
    private EditorWorkspace _workspace = EditorWorkspace.Design;
    private readonly Dictionary<EditorWorkspace, string> _workspaceSelections = [];
    private string _selectedProductionId = "";
    private bool _isUpdatingProductionPicker;

    public MainWindow()
        : this(SpikeDatabase.DefaultDatabasePath())
    {
    }

    public MainWindow(string databasePath)
    {
        _database = new SpikeDatabase(databasePath);
        InitializeComponent();
        _variantHistory = new EditorVariantHistoryService(_database);
        _coreFieldValues = new CoreFieldValueService(_database);
        _recordClassFieldValues = new RecordClassFieldValueService(_database);
        _componentClassFieldValues = new ComponentClassFieldValueService(_database);
        _themeController = new EditorThemeController(this, RootShell, RefreshShellTheme);
        _inlinePreviews = EditorInlinePreviewControllerFactory.Create(_database, () => _themeController.IsDark);
        EditorTextBoxBehavior.Configure(ShellMessagesTextBox);
        _messages = new EditorShellMessageSink(ShellMessagesTextBox);
        _editorViewState = new EditorViewStateController(EditorScrollViewer);
        _previewController = new EditorPreviewController(
            _database,
            PreviewDeviceComboBox,
            PreviewThemeComboBox,
            PreviewModeComboBox,
            PreviewOrientationComboBox,
            _messages,
            PreviewSetupHost,
            PreviewControlsHost,
            PreviewBusyHost,
            DesignPreviewHost,
            PreviewContextTextBlock,
            PreviewContextHistoryButton,
            PreviewContextAddHistoryButton,
            PreviewContextLockButton,
            PreviewTitlePanel,
            () => _themeController.IsDark,
            () => _selectedNode,
            () => _editorViewState.CaptureState(_editorContent!.Cards),
            (nodeId, viewState) => SelectNodeById(nodeId, viewState, "preview-context"),
            this);
        _previewController.ThemeChanged += _activeFieldControls.RefreshPreviews;
        _nodeCommands = new EditorNodeCommandController(
            this,
            _database,
            () => _themeController.IsDark,
            () => _treeRoots,
            LoadProjectTree,
            ReloadAndSelect,
            ReloadAndSelect,
            _messages);
        _shellState = new EditorShellStateService(this, ShellColumns);
        _productionShotContext = new ProductionShotContextService(_database);
        _navigationRenderer = new EditorNavigationRenderer(
            () => _selectedNode,
            () => _themeController.IsDark,
            _treeExpansion.IsExpanded,
            SelectTreeNode,
            ToggleTreeGroup,
            _nodeCommands.AddChild,
            _nodeCommands.DuplicateNode,
            _nodeCommands.RenameNode,
            _nodeCommands.DeleteNode,
            _nodeCommands.ToggleComponentPresetLock,
            _productionShotContext.CanExposeChildren,
            _productionShotContext.IsNavigationNodeEnabled);
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
            () => _themeController.IsDark,
            _nodeCommands.ShowInfoDialog,
            _pathBrowser.BrowseSvgFile,
            ReloadAndSelect);
        _dictionaryFieldServices = new EditorDictionaryFieldServices(
            _database,
            _pathBrowser,
            _domainDialogs,
            () => _previewController.SelectedThemeId,
            _previewController.SetDesignPreviewTestValue);
        _embeddedEditors = new EditorEmbeddedEditorController(ShowEmbeddedContext, _messages);
        _fieldValues = new EditorFieldValueRouter(
            _coreFieldValues,
            _recordClassFieldValues,
            _componentClassFieldValues,
            _inlinePreviews,
            _fieldPostCommitEffects);
        _layoutCards = new EditorLayoutCardFactory(
            _fieldValues,
            _componentClassFieldValues,
            _inlinePreviews,
            _dictionaryFieldServices,
            _fieldCommitCoordinator,
            _activeFieldControls,
            _messages,
            _embeddedEditors.Open,
            _embeddedEditors.OpenSlot,
            _embeddedEditors.OpenNested,
            _embeddedEditors.OpenNestedSlot,
            OpenComponentPresetReference,
            _nodeCommands.ToggleComponentPresetLock,
            ShowEmbeddedContext,
            ReloadAndSelect,
            RefreshPreviewDevice);
        _embeddedUsageNavigator = new EditorEmbeddedUsageNavigator(
            _database,
            this,
            () => _themeController.IsDark,
            SelectNodeById,
            LoadProjectTree,
            () => _selectedNode,
            _embeddedEditors.Open,
            _messages);
        _editorHeader = new EditorHeaderController(
            EditorBreadcrumbPanel,
            EditorContextStripHost,
            EditorHeaderActionsPanel,
            _database,
            () => _selectedNode,
            _nodeSelection,
            _embeddedUsageNavigator,
            ShowNode,
            ReturnToEmbeddedOwner,
            ShowEmbeddedContext,
            _nodeCommands.SaveCurrentComponentPreset,
            _variantHistory.Snapshots,
            _nodeCommands.RestoreComponentPresetSnapshot,
            _activeFieldControls);
        _collectionCards = new EditorCollectionCardFactory(
            _database,
            () => _themeController.IsDark,
            _nodeCommands.ShowInfoDialog,
            _domainDialogs,
            ReloadAndSelect,
            _pathBrowser.BrowsePath,
            RefreshPreviewDevice,
            _dictionaryFieldServices,
            _previewController.TriggerDesignPreviewAction,
            _previewController.SetDesignPreviewTestValue,
            _previewController.SetDesignPreviewCollectionTestValue,
            _previewController.SetDesignPreviewCollectionTestItems,
            _previewController.ApplyDesignPreviewTransientTestValues,
            _previewController.ResetDesignPreviewTestValues,
            _previewController.PlaybackState,
            SelectNodeById,
            ShowEmbeddedContext,
            _previewController.ProductionShotFrame,
            _previewController.SetProductionShotFrame,
            _previewController.ToggleProductionPlayback);
        _editorContent = new EditorContentController(
            _database,
            EditorCardsPanel,
            () => Math.Max(1, EditorScrollViewer.Bounds.Width - EditorScrollViewer.Padding.Left - EditorScrollViewer.Padding.Right),
            EditorScrollViewer,
            _activeFieldControls,
            _inlinePreviews,
            _layoutCards,
            _collectionCards);
        UsageRefreshButton.Content = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children =
            {
                EditorIcons.Create(EditorIcons.Refresh, 16),
                new TextBlock
                {
                    Text = "Update usage",
                    FontWeight = FontWeight.SemiBold,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                },
            },
        };
        ShellSettingsButton.Content = EditorIcons.Create(EditorIcons.Settings, 18);
        ApplyHeaderUtilityButton(UsageRefreshButton);
        ApplyHeaderUtilityButton(ShellSettingsButton);
        EditorAccessibility.Describe(UsageRefreshButton, "Update usage");
        EditorAccessibility.Describe(ShellSettingsButton, "Settings");
        ProductionAddButton.Content = EditorIcons.Create(EditorIcons.Add, 15);
        ProductionDuplicateButton.Content = EditorIcons.Create(EditorIcons.Duplicate, 15);
        ProductionDeleteButton.Content = EditorIcons.Create(EditorIcons.Delete, 15);
        ProductionEditButton.Content = EditorIcons.Create(EditorIcons.Edit, 15);
        _shellState.Restore();
        _workspace = EditorWorkspaceNavigation.Parse(_shellState.Workspace);
        _selectedProductionId = _shellState.ProductionId;
        DesignWorkspaceButton.Click += (_, _) => SetWorkspace(EditorWorkspace.Design);
        ProductionWorkspaceButton.Click += (_, _) => SetWorkspace(EditorWorkspace.Production);
        ProductionComboBox.SelectionChanged += (_, _) => SelectProductionFromPicker();
        ProductionEditButton.Click += (_, _) => OpenSelectedProduction();
        UpdateWorkspaceButtons();
        _variantHistory.RestoreState(_shellState.SessionHistory.VariantHistory);
        _previewController.RestoreDesignHistoryState(_shellState.SessionHistory.DesignPreviewHistory);
        _previewController.RestoreProductionHistoryState(_shellState.SessionHistory.ProductionPreviewHistory);
        _previewController.SetWorkspaceWithoutRefresh(_workspace);
        _nodeSelection.RestoreComponentPresetSelections(_shellState.SessionHistory.LastComponentVariantSelections);
        _themeController.SetState(_shellState.IsDark, _shellState.SukiColor);
        EditorUiDensity.Configure(_shellState.UiTextScale, _shellState.UiCardPaddingScale);
        Closing += (_, _) => _shellState.Save(CreateSessionHistoryState());
        _themeController.Apply();
        LoadProjectTree();
        InitializePreviewOptions();
        ApplyUiTextScale();
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

    private async void OnShellSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await new EditorShellSettingsDialog(
            this,
            _themeController,
            _shellState,
            ApplyUiDensity).Show();
    }

    private void InitializePreviewOptions()
    {
        _previewController.Initialize(_treeRoots);
    }

    private void RefreshPreviewDevice()
    {
        _previewController.Refresh();
    }

    private void RefreshShellTheme()
    {
        UpdateWorkspaceButtons();
        RebuildNavigationCards();
        RefreshPreviewDevice();
        ApplyUiTextScale();
    }

    private void RefreshPreviewOptions()
    {
        _previewController.RefreshOptions(_treeRoots);
    }

    private void LoadProjectTree()
    {
        _treeRoots = _database.LoadProjectTree();
        EnsureSelectedProductionExists();
        _treeExpansion.EnsureInitial(_treeRoots);

        if (_treeRoots.Count > 0)
        {
            var selected = _selectedNode is not null
                ? EditorNodeSelectionState.FindNodeById(_treeRoots, _selectedNode.Id)
                : null;
            selected = selected is not null
                && EditorNodeSelectionState.CanSelectTreeNode(selected)
                && EditorWorkspaceNavigation.Contains(_workspace, selected)
                ? selected
                : null;
            if (selected is null && _workspaceSelections.TryGetValue(_workspace, out var selectionId))
            {
                var remembered = EditorNodeSelectionState.FindNodeById(_treeRoots, selectionId);
                selected = remembered is not null
                    && EditorNodeSelectionState.CanSelectTreeNode(remembered)
                    && EditorWorkspaceNavigation.Contains(_workspace, remembered)
                    ? remembered
                    : null;
            }
            selected ??= EditorWorkspaceNavigation.FirstSelectable(_treeRoots, _workspace)
                ?? _treeRoots.FirstOrDefault((node) => node.CanOpenEditor)
                ?? _treeRoots[0];
            selected = _nodeSelection.ResolveSelectionNode(selected);

            _treeExpansion.ExpandAncestors(selected);
            RebuildNavigationCards();
            ShowNode(selected, rebuildTree: false);
            return;
        }

        RebuildNavigationCards();
    }

    private void SelectTreeNode(ProjectTreeNode node)
    {
        if (node.Children.Count > 0)
        {
            if (EditorNodeSelectionState.CanSelectTreeNode(node))
            {
                var wasExpanded = _treeExpansion.IsExpanded(node);
                ShowNode(node, rebuildTree: false);
                if (EditorNavigationMetadata.ExpandChildrenWhenOpened(node))
                {
                    if (!wasExpanded)
                    {
                        ToggleTreeGroup(node);
                    }
                    else
                    {
                        RebuildNavigationCards();
                    }
                    return;
                }

                if (!wasExpanded)
                {
                    RebuildNavigationCards();
                    return;
                }

                ToggleTreeGroup(node);
                return;
            }

            ToggleTreeGroup(node);
            return;
        }

        if (EditorNodeSelectionState.CanSelectTreeNode(node))
        {
            ShowNode(node);
        }
    }

    private void ToggleTreeGroup(ProjectTreeNode node)
    {
        _treeExpansion.Toggle(node);
        RebuildNavigationCards();
    }

    private void RebuildNavigationCards()
    {
        NavigationWorkspaceTextBlock.Text = EditorWorkspaceNavigation.Title(_workspace);
        RefreshProductionPicker();
        _navigationRenderer.Rebuild(NavigationCardsPanel, _treeRoots, _workspace, _selectedProductionId);
        ApplyUiTextScale();
    }

    private void ShowNode(ProjectTreeNode node, bool rebuildTree = true)
    {
        ShowNode(node, rebuildTree, "selection");
    }

    private void ShowNode(ProjectTreeNode node, bool rebuildTree, string source)
    {
        node = _nodeSelection.ResolveSelectionNode(node);
        using var transaction = BeginContextTransaction(source, node.Id);
        var previousNode = _selectedNode;
        var previousViewState = _editorViewState.CaptureState(_editorContent.Cards);
        var keepEditorViewState = _editorViewState.ShouldPreserve(previousNode, node);
        if (keepEditorViewState)
        {
            _editorViewState.Capture(previousNode, _editorContent.Cards);
        }

        try
        {
            _variantHistory.TrackTransition(previousNode, node, previousViewState);
        }
        catch (Exception exception)
        {
            _messages.Warning("Variant history", exception.Message);
        }
        _selectedNode = node;
        _workspaceSelections[_workspace] = node.Id;
        _nodeSelection.RememberComponentPresetSelection(node);
        _treeExpansion.ExpandAncestors(node);
        var editorNode = EditorNodeSelectionState.EditorNodeForSelection(node);
        transaction.Checkpoint("before-editor-candidate");
        _editorContent.Build(editorNode, node);
        SetEditorRootTitle(editorNode.Name);
        transaction.Checkpoint("after-editor-swap");
        if (keepEditorViewState)
        {
            _editorViewState.Restore(node, _editorContent.Cards);
        }

        RefreshPreviewDevice();
        if (rebuildTree)
        {
            RebuildNavigationCards();
            transaction.Checkpoint("after-navigation-swap");
        }
        ApplyUiTextScale();
    }

    private void ShowEmbeddedContext(EditorEmbeddedContext context)
    {
        if (context.IsNavigationRoot
            && _editorViewState.CaptureState(_editorContent.Cards) is { } parentState)
        {
            _embeddedParentViewStates[context.OwnerNode.Id] = parentState;
        }
        _editorContent.BuildEmbedded(context);
        SetEditorEmbeddedTitle(context);
        RefreshPreviewDevice();
        ApplyUiTextScale();
    }

    private void ReturnToEmbeddedOwner(ProjectTreeNode ownerNode)
    {
        ShowNode(ownerNode, false, "breadcrumb");
        if (_embeddedParentViewStates.Remove(ownerNode.Id, out var state))
        {
            _editorViewState.RestoreState(state, _editorContent.Cards);
        }
    }

    private void SetEditorRootTitle(string title)
    {
        _editorHeader.SetRootTitle(title);
    }

    private void SetEditorEmbeddedTitle(EditorEmbeddedContext context)
    {
        _editorHeader.SetEmbeddedTitle(context);
    }

    private void ReloadAndSelect(ProjectTreeNode node)
    {
        ReloadAndSelect(node, null);
    }

    private void ReloadAndSelect(ProjectTreeNode node, EditorViewState? viewState)
    {
        LoadProjectTree();
        RefreshPreviewOptions();
        SelectNodeById(node.Id, viewState);
    }

    private bool SelectNodeById(string nodeId)
    {
        return SelectNodeById(nodeId, null);
    }

    private bool SelectNodeById(string nodeId, EditorViewState? viewState)
    {
        return SelectNodeById(nodeId, viewState, "node-id");
    }

    private bool SelectNodeById(string nodeId, EditorViewState? viewState, string source)
    {
        var node = EditorNodeSelectionState.FindNodeById(_treeRoots, nodeId);
        if (node is null)
        {
            return false;
        }

        var selectableNode = EditorNodeSelectionState.CanSelectTreeNode(node) ? node : EditorNodeSelectionState.ClosestEditableNode(node);
        selectableNode = _nodeSelection.ResolveSelectionNode(selectableNode);
        _treeExpansion.ExpandAncestors(selectableNode);
        ShowNode(selectableNode, rebuildTree: true, source);
        _editorViewState.RestoreState(viewState, _editorContent.Cards);
        ApplyUiTextScale();
        return true;
    }

    private System.Threading.Tasks.Task OpenComponentPresetReference(string presetReference)
    {
        if (!SelectNodeById(presetReference))
        {
            _messages.Warning("Open component variant", $"Could not find variant '{presetReference}'.");
        }

        return System.Threading.Tasks.Task.CompletedTask;
    }

    private void ApplyUiDensity(bool rebuildCards)
    {
        EditorUiDensity.Configure(_shellState.UiTextScale, _shellState.UiCardPaddingScale);
        if (rebuildCards && _selectedNode is not null)
        {
            ShowNode(_selectedNode, rebuildTree: true);
            return;
        }

        ApplyUiTextScale();
    }

    private void ApplyUiTextScale()
    {
        EditorUiTextScale.Apply(this, _shellState.UiTextScale, DesignPreviewHost);
    }

    private void SetWorkspace(EditorWorkspace workspace)
    {
        if (_workspace == workspace) return;

        using var transaction = BeginContextTransaction("workspace", workspace.ToString());

        if (_selectedNode is not null)
        {
            _workspaceSelections[_workspace] = _selectedNode.Id;
        }

        _workspace = workspace;
        _shellState.SetWorkspace(workspace);
        _previewController.SetWorkspaceWithoutRefresh(workspace);
        UpdateWorkspaceButtons();
        transaction.Checkpoint("workspace-state-ready");
        LoadProjectTree();
        transaction.Checkpoint("workspace-selection-committed");
    }

    private EditorShellContextTransaction BeginContextTransaction(string source, string targetId)
    {
        return new EditorShellContextTransaction(
            source,
            targetId,
            _selectedNode?.Id ?? "",
            _workspace.ToString(),
            this,
            NavigationCardsPanel,
            EditorCardsPanel,
            DesignPreviewHost,
            _previewController.NativeHostLifecycleState);
    }

    private void UpdateWorkspaceButtons()
    {
        var activeBrush = new SolidColorBrush(Color.Parse(_themeController.IsDark ? "#F0B429" : "#A56600"));
        var inactiveBrush = new SolidColorBrush(Color.Parse(_themeController.IsDark ? "#9CA3AF" : "#6B7280"));
        var activeBackground = new SolidColorBrush(Color.Parse(_themeController.IsDark ? "#463711" : "#F2DEAA"));
        WorkspaceSwitcherBorder.BorderBrush = new SolidColorBrush(Color.Parse(_themeController.IsDark ? "#59616D" : "#AAB1BB"));
        WorkspaceSwitcherBorder.Background = new SolidColorBrush(Color.Parse(_themeController.IsDark ? "#16191F" : "#E3E5E8"));
        ApplyWorkspaceButton(DesignWorkspaceButton, _workspace == EditorWorkspace.Design, activeBrush, inactiveBrush, activeBackground);
        ApplyWorkspaceButton(ProductionWorkspaceButton, _workspace == EditorWorkspace.Production, activeBrush, inactiveBrush, activeBackground);
        ProductionPickerGrid.IsVisible = _workspace == EditorWorkspace.Production;
    }

    private void EnsureSelectedProductionExists()
    {
        if (_treeRoots.Any((project) => project.Id == _selectedProductionId)) return;

        _selectedProductionId = _treeRoots.FirstOrDefault()?.Id ?? "";
        _shellState.SetProductionId(_selectedProductionId);
    }

    private void RefreshProductionPicker()
    {
        _isUpdatingProductionPicker = true;
        try
        {
            var options = _treeRoots
                .Select((project) => new FieldOption(project.Id, project.Name))
                .ToList();
            ProductionComboBox.ItemsSource = options;
            ProductionComboBox.SelectedItem = options.FirstOrDefault((option) => option.Value == _selectedProductionId)
                ?? options.FirstOrDefault();
            ProductionEditButton.IsEnabled = ProductionComboBox.SelectedItem is not null;
        }
        finally
        {
            _isUpdatingProductionPicker = false;
        }
    }

    private void SelectProductionFromPicker()
    {
        if (_isUpdatingProductionPicker || ProductionComboBox.SelectedItem is not { } selected) return;
        if (string.Equals(_selectedProductionId, selected.Value, StringComparison.Ordinal)) return;

        _selectedProductionId = selected.Value;
        _shellState.SetProductionId(_selectedProductionId);
        var project = _treeRoots.FirstOrDefault((candidate) => candidate.Id == _selectedProductionId);
        var node = project is null
            ? null
            : EditorWorkspaceNavigation.FirstSelectable([project], EditorWorkspace.Production);
        if (node is not null)
        {
            ShowNode(_nodeSelection.ResolveSelectionNode(node), rebuildTree: false);
        }

        RebuildNavigationCards();
    }

    private void OpenSelectedProduction()
    {
        var production = _treeRoots.FirstOrDefault((project) => project.Id == _selectedProductionId);
        if (production is null) return;

        ShowNode(production);
    }

    private static void ApplyWorkspaceButton(Button button, bool isActive, IBrush activeBrush, IBrush inactiveBrush, IBrush activeBackground)
    {
        button.Foreground = isActive ? activeBrush : inactiveBrush;
        button.BorderBrush = Brushes.Transparent;
        button.BorderThickness = new Thickness(0);
        button.Background = isActive ? activeBackground : Brushes.Transparent;
    }

    private static void ApplyHeaderUtilityButton(Button button)
    {
        button.Background = Brushes.Transparent;
        button.BorderBrush = Brushes.Transparent;
        button.BorderThickness = new Thickness(0);
    }

    private EditorSessionHistoryState CreateSessionHistoryState()
    {
        return new EditorSessionHistoryState
        {
            VariantHistory = _variantHistory.ExportState(),
            DesignPreviewHistory = _previewController.ExportDesignHistoryState().ToList(),
            ProductionPreviewHistory = _previewController.ExportProductionHistoryState().ToList(),
            LastComponentVariantSelections = _nodeSelection.ExportComponentPresetSelections().ToDictionary(
                (entry) => entry.Key,
                (entry) => entry.Value,
                StringComparer.Ordinal),
        };
    }

}
