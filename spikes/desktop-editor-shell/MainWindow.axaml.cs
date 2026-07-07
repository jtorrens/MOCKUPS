using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell;

public partial class MainWindow : SukiWindow
{
    private readonly SpikeDatabase _database = new(SpikeDatabase.DefaultDatabasePath());
    private readonly CoreFieldValueService _coreFieldValues;
    private readonly RecordClassFieldValueService _recordClassFieldValues;
    private readonly ComponentClassFieldValueService _componentClassFieldValues;
    private readonly ActorAvatarPreviewController _actorAvatarPreviews;
    private readonly EditorCollectionCardFactory _collectionCards;
    private readonly EditorPreviewController _previewController;
    private readonly IEditorShellMessageSink _messages;
    private readonly EditorThemeController _themeController;
    private readonly EditorNodeCommandController _nodeCommands;
    private readonly EditorShellStateService _shellState;
    private readonly EditorNavigationRenderer _navigationRenderer;
    private readonly EditorFieldPostCommitEffects _fieldPostCommitEffects;
    private readonly EditorPathBrowser _pathBrowser;
    private readonly EditorDomainDialogService _domainDialogs;
    private readonly EditorDictionaryFieldServices _dictionaryFieldServices;
    private readonly EditorViewStateController _editorViewState;
    private readonly EditorFieldValueRouter _fieldValues;
    private readonly EditorLayoutCardFactory _layoutCards;
    private readonly EditorContentController _editorContent;
    private readonly EditorEmbeddedEditorController _embeddedEditors;
    private readonly EditorEmbeddedUsageNavigator _embeddedUsageNavigator;
    private readonly EditorHeaderController _editorHeader;
    private readonly EditorTreeExpansionState _treeExpansion = new();
    private readonly EditorNodeSelectionState _nodeSelection = new();
    private readonly EditorFieldCommitCoordinator _fieldCommitCoordinator = new();
    private readonly EditorActiveFieldControls _activeFieldControls = new();
    private List<ProjectTreeNode> _treeRoots = [];
    private ProjectTreeNode? _selectedNode;

    public MainWindow()
    {
        InitializeComponent();
        _coreFieldValues = new CoreFieldValueService(_database);
        _recordClassFieldValues = new RecordClassFieldValueService(_database);
        _componentClassFieldValues = new ComponentClassFieldValueService(_database);
        _themeController = new EditorThemeController(this, RootShell, ThemeModeSwitch, SukiColorComboBox, RefreshShellTheme);
        _actorAvatarPreviews = new ActorAvatarPreviewController(_database, () => _themeController.IsDark);
        _messages = new EditorShellMessageSink(ShellMessagesTextBox);
        _previewController = new EditorPreviewController(
            _database,
            PreviewDeviceComboBox,
            PreviewThemeComboBox,
            PreviewModeComboBox,
            PreviewScaleComboBox,
            PreviewMarksToggle,
            _messages,
            PreviewSetupHost,
            PreviewInputsHost,
            RuntimePreviewHost,
            DesignPreviewHost,
            PreviewContextTextBlock,
            PreviewContextLockButton,
            () => _themeController.IsDark,
            () => _selectedNode,
            this);
        _nodeCommands = new EditorNodeCommandController(
            this,
            _database,
            () => _themeController.IsDark,
            () => _treeRoots,
            LoadProjectTree,
            ReloadAndSelect,
            _messages);
        _shellState = new EditorShellStateService(this, ShellColumns);
        _navigationRenderer = new EditorNavigationRenderer(
            () => _selectedNode,
            () => _themeController.IsDark,
            _treeExpansion.IsExpanded,
            SelectTreeNode,
            (node) => ShowNode(_nodeSelection.ResolveSelectionNode(node)),
            ToggleTreeGroup,
            _nodeCommands.AddChild,
            _nodeCommands.DuplicateNode,
            _nodeCommands.RenameNode,
            _nodeCommands.DeleteNode);
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
        _dictionaryFieldServices = new EditorDictionaryFieldServices(_database, _pathBrowser, _domainDialogs);
        _editorViewState = new EditorViewStateController(EditorScrollViewer);
        _embeddedEditors = new EditorEmbeddedEditorController(ShowEmbeddedContext, _messages);
        _fieldValues = new EditorFieldValueRouter(
            _coreFieldValues,
            _recordClassFieldValues,
            _componentClassFieldValues,
            _actorAvatarPreviews,
            _fieldPostCommitEffects);
        _layoutCards = new EditorLayoutCardFactory(
            _fieldValues,
            _componentClassFieldValues,
            _actorAvatarPreviews,
            _dictionaryFieldServices,
            _fieldCommitCoordinator,
            _activeFieldControls,
            _messages,
            _embeddedEditors.Open,
            _embeddedEditors.OpenNested,
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
            EditorPresetTextBlock,
            EditorHeaderActionsPanel,
            _database,
            () => _selectedNode,
            _nodeSelection,
            _embeddedUsageNavigator,
            ShowNode,
            ShowEmbeddedContext,
            _nodeCommands.SaveCurrentComponentPreset);
        _collectionCards = new EditorCollectionCardFactory(
            _database,
            () => _themeController.IsDark,
            _nodeCommands.ShowInfoDialog,
            _domainDialogs,
            ReloadAndSelect,
            _pathBrowser.BrowsePath,
            RefreshPreviewDevice);
        _editorContent = new EditorContentController(
            _database,
            EditorCardsPanel,
            _activeFieldControls,
            _actorAvatarPreviews,
            _layoutCards,
            _collectionCards);
        _shellState.Restore();
        Closing += (_, _) => _shellState.Save();
        _themeController.Apply();
        LoadProjectTree();
        InitializePreviewOptions();
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
        RebuildNavigationCards();
        RefreshPreviewDevice();
    }

    private void RefreshPreviewOptions()
    {
        _previewController.RefreshOptions(_treeRoots);
    }

    private void LoadProjectTree()
    {
        _treeRoots = _database.LoadProjectTree();
        _treeExpansion.EnsureInitial(_treeRoots);

        if (_treeRoots.Count > 0)
        {
            var selected = _selectedNode is not null
                ? EditorNodeSelectionState.FindNodeById(_treeRoots, _selectedNode.Id)
                : null;
            selected = selected is not null && EditorNodeSelectionState.CanSelectTreeNode(selected) ? selected : null;
            selected ??= _treeRoots.FirstOrDefault((node) => node.CanOpenEditor) ?? _treeRoots[0];
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
        if (!EditorNodeSelectionState.CanSelectTreeNode(node))
        {
            ToggleTreeGroup(node);
            return;
        }

        ShowNode(node);
    }

    private void ToggleTreeGroup(ProjectTreeNode node)
    {
        _treeExpansion.Toggle(node);
        RebuildNavigationCards();
    }

    private void RebuildNavigationCards()
    {
        _navigationRenderer.Rebuild(NavigationCardsPanel, _treeRoots);
    }

    private void ShowNode(ProjectTreeNode node, bool rebuildTree = true)
    {
        node = _nodeSelection.ResolveSelectionNode(node);
        var previousNode = _selectedNode;
        var keepEditorViewState = _editorViewState.ShouldPreserve(previousNode, node);
        if (keepEditorViewState)
        {
            _editorViewState.Capture(previousNode, _editorContent.Cards);
        }

        _selectedNode = node;
        _nodeSelection.RememberComponentPresetSelection(node);
        _treeExpansion.ExpandAncestors(node);
        var editorNode = EditorNodeSelectionState.EditorNodeForSelection(node);
        SetEditorRootTitle(editorNode.Name);
        _editorContent.Build(editorNode, node);
        if (keepEditorViewState)
        {
            _editorViewState.Restore(node, _editorContent.Cards);
        }

        RefreshPreviewDevice();
        if (rebuildTree)
        {
            RebuildNavigationCards();
        }
    }

    private void ShowEmbeddedContext(EditorEmbeddedContext context)
    {
        SetEditorEmbeddedTitle(context);
        _editorContent.BuildEmbedded(context);
        RefreshPreviewDevice();
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
        _selectedNode = node;
        LoadProjectTree();
        RefreshPreviewOptions();
        SelectNodeById(node.Id);
    }

    private bool SelectNodeById(string nodeId)
    {
        var node = EditorNodeSelectionState.FindNodeById(_treeRoots, nodeId);
        if (node is null)
        {
            return false;
        }

        var selectableNode = EditorNodeSelectionState.CanSelectTreeNode(node) ? node : EditorNodeSelectionState.ClosestEditableNode(node);
        selectableNode = _nodeSelection.ResolveSelectionNode(selectableNode);
        _treeExpansion.ExpandAncestors(selectableNode);
        ShowNode(selectableNode, rebuildTree: true);
        return true;
    }


}
