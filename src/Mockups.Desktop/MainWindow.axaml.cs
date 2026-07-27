using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using Mockups.DesktopEditorShell.Common;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell;

public partial class MainWindow : SukiWindow
{
    private const string PreviewUtilityAuthoringDataId = "authoring-data";
    private const string PreviewUtilitySetupId = "setup";
    private const string PreviewUtilityControlsId = "controls";
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
    private readonly EditorSessionUiState _editorSessionUiState = new();
    private readonly EditorFieldValueRouter _fieldValues;
    private readonly EditorLayoutCardFactory _layoutCards;
    private readonly EditorContentController _editorContent;
    private readonly EditorEmbeddedEditorController _embeddedEditors;
    private readonly EditorEmbeddedUsageNavigator _embeddedUsageNavigator;
    private readonly EditorReferenceUsageNavigator _referenceUsageNavigator;
    private readonly EditorHeaderController _editorHeader;
    private readonly EditorVariantHistoryService _variantHistory;
    private readonly EditorProductionNavigationActions _productionNavigationActions;
    private readonly EditorTreeExpansionState _treeExpansion = new();
    private readonly EditorFieldCommitCoordinator _fieldCommitCoordinator;
    private readonly EditorActiveFieldControls _activeFieldControls = new();
    private readonly EditorWorkspaceCoordinator _workspaceCoordinator;
    private bool _isUpdatingProductionPicker;
    private string _previewUtilityTabStateKey = "";
    private bool _isUpdatingPreviewUtilityTab;
    private string _renderedPreviewNavigationNodeId = "";
    private EditorSessionState Session => _workspaceCoordinator.State;

    [Obsolete("MainWindow must be created by Mockups.Desktop.Host.")]
    public MainWindow()
    {
        throw new InvalidOperationException(
            "MainWindow requires a validated desktop application session.");
    }

    internal MainWindow(
        DesktopApplicationServices application,
        IReadOnlyList<ProjectTreeNode> initialTreeRoots)
    {
        var data = application.Data;
        _variantHistory = application.VariantHistory;
        _coreFieldValues = application.CoreFieldValues;
        _recordClassFieldValues = application.RecordClassFieldValues;
        _componentClassFieldValues = application.ComponentClassFieldValues;
        _productionShotContext = application.ProductionShotContext;
        _workspaceCoordinator = application.WorkspaceCoordinator;
        _fieldCommitCoordinator = new EditorFieldCommitCoordinator(
            application.Operations);
        InitializeComponent();
        _themeController = new EditorThemeController(this, RootShell, RefreshShellTheme);
        _inlinePreviews = EditorInlinePreviewControllerFactory.Create(
            data.ActorPreview,
            data.ProjectPaths,
            () => _themeController.IsDark);
        EditorTextBoxBehavior.Configure(ShellMessagesTextBox);
        _messages = new EditorShellMessageSink(ShellMessagesTextBox);
        _editorViewState = new EditorViewStateController(EditorScrollViewer);
        _previewController = new EditorPreviewController(
            data.Preview,
            data.ComponentPreview,
            data.Timeline,
            data.ModuleInstanceThemes,
            data.Dictionary,
            data.ActorPreview,
            data.ProjectPaths,
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
            () => Session.SelectedNode,
            (nodeId) => SelectNodeById(nodeId, "preview-context"),
            this);
        _previewController.ThemeChanged += _activeFieldControls.RefreshPreviews;
        _nodeCommands = new EditorNodeCommandController(
            this,
            data.NodeCommands,
            data.ReferenceUsage,
            data.Children,
            data.ModuleInstances,
            data.ProjectPaths,
            application.Operations,
            () => _themeController.IsDark,
            () => Session.TreeRoots,
            LoadProjectTreeAsync,
            ReloadAndSelect,
            NavigateToReferenceUsage,
            _messages);
        _shellState = new EditorShellStateService(this, ShellColumns);
        _productionNavigationActions = new EditorProductionNavigationActions(
            this,
            ProductionActionButton,
            data.RenderSnapshots,
            data.ProjectPaths,
            application.ProductionOutputRoots,
            () => _themeController.IsDark,
            OpenSelectedProductionCard);
        _navigationRenderer = new EditorNavigationRenderer(
            () => Session.SelectedNode,
            () => _themeController.IsDark,
            _treeExpansion.IsExpanded,
            SelectTreeNode,
            ToggleTreeGroup,
            _nodeCommands.AddChild,
            _nodeCommands.DuplicateNode,
            _nodeCommands.RenameNode,
            _nodeCommands.DeleteNode,
            _nodeCommands.ToggleVariantLock,
            _productionShotContext.CanExposeChildren,
            _productionShotContext.IsNavigationNodeEnabled,
            () => _previewController.ActiveNavigationNodeId,
            _productionNavigationActions.NodeAction);
        _previewController.PlaybackState.Changed += RefreshPreviewNavigationState;
        _fieldPostCommitEffects = new EditorFieldPostCommitEffects(
            data.Presentation,
            () => _previewController.SelectedDeviceId,
            SetEditorRootTitle,
            RebuildNavigationCards,
            RefreshPreviewDevice,
            RefreshPreviewOptions,
            RefreshProductionPicker);
        _pathBrowser = new EditorPathBrowser(
            StorageProvider,
            data.Presentation,
            data.ProjectPaths,
            () => Session.SelectedNode);
        _domainDialogs = new EditorDomainDialogService(
            this,
            data.ModuleInstances,
            data.IconThemes,
            data.ThemeTokens,
            application.Operations,
            () => _themeController.IsDark,
            _nodeCommands.ShowInfoDialog,
            _pathBrowser.BrowseSvgFile,
            ReloadAndSelect);
        _dictionaryFieldServices = new EditorDictionaryFieldServices(
            data.Dictionary,
            data.Preview,
            data.Timeline,
            data.ModuleInstanceThemes,
            data.ActorPreview,
            data.ProjectPaths,
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
            OpenComponentVariantReference,
            _nodeCommands.ToggleVariantLock,
            ShowEmbeddedContext,
            ScheduleActiveEditorReload,
            RefreshPreviewDevice,
            _editorSessionUiState);
        _embeddedUsageNavigator = new EditorEmbeddedUsageNavigator(
            data.Components,
            this,
            () => _themeController.IsDark,
            SelectNodeById,
            LoadProjectTreeAsync,
            () => Session.SelectedNode,
            _embeddedEditors.Open,
            _messages);
        _referenceUsageNavigator = new EditorReferenceUsageNavigator(
            SelectReferenceNodeInWorkspaceAsync,
            _embeddedUsageNavigator.NavigateToEmbeddedUsage,
            _messages);
        _editorHeader = new EditorHeaderController(
            EditorBreadcrumbPanel,
            EditorContextStripHost,
            EditorHeaderActionsPanel,
            data.Components,
            data.Preview,
            data.Timeline,
            data.ModuleInstanceThemes,
            () => Session.SelectedNode,
            _workspaceCoordinator.PreferredVariantNode,
            _workspaceCoordinator.PreferredModuleVariantNode,
            _embeddedUsageNavigator,
            ShowNode,
            ReturnToEmbeddedOwner,
            ShowEmbeddedContext,
            _nodeCommands.SaveCurrentVariant,
            _variantHistory.Snapshots,
            _nodeCommands.RestoreVariantSnapshot,
            _activeFieldControls);
        _collectionCards = new EditorCollectionCardFactory(
            data.ModuleInstances,
            data.IconThemes,
            data.ComponentPreview,
            data.Dictionary,
            data.ActorPreview,
            data.RuntimeInputOwners,
            data.Timeline,
            data.RuntimeInputInstances,
            data.Animation,
            data.ModuleInstanceThemes,
            data.ReferenceUsage,
            application.Operations,
            () => _themeController.IsDark,
            _nodeCommands.ShowInfoDialog,
            _domainDialogs,
            ReloadAndSelect,
            RefreshPreviewDevice,
            _dictionaryFieldServices,
            _previewController.TriggerDesignPreviewAction,
            _previewController.RestoreDesignPreviewAction,
            _previewController.CanRestoreDesignPreviewAction,
            _previewController.SetDesignPreviewTestValue,
            _previewController.SetDesignPreviewCollectionItemValues,
            _previewController.SetDesignPreviewCollectionTestItems,
            _previewController.ApplyDesignPreviewTransientTestValues,
            _previewController.ResetDesignPreviewTestValues,
            _previewController.PlaybackState,
            SelectNodeById,
            NavigateToReferenceUsage,
            ShowEmbeddedContext,
            _previewController.ProductionShotFrame,
            _previewController.SetProductionShotFrame,
            _previewController.ToggleProductionPlayback,
            _editorSessionUiState);
        _editorContent = new EditorContentController(
            data.Layouts,
            EditorCardsPanel,
            () => Math.Max(1, EditorScrollViewer.Bounds.Width - EditorScrollViewer.Padding.Left - EditorScrollViewer.Padding.Right),
            EditorScrollViewer,
            _activeFieldControls,
            _inlinePreviews,
            _layoutCards,
            _collectionCards,
            _productionNavigationActions.EditorCards);
        PreviewUtilityTabs.SelectionChanged += (_, args) =>
        {
            if (_isUpdatingPreviewUtilityTab
                || !ReferenceEquals(args.Source, PreviewUtilityTabs)
                || string.IsNullOrWhiteSpace(_previewUtilityTabStateKey)
                || PreviewUtilityTabId(PreviewUtilityTabs.SelectedItem) is not { } tabId)
            {
                return;
            }

            _editorSessionUiState.Select(_previewUtilityTabStateKey, tabId);
        };
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
        _shellState.Restore();
        _workspaceCoordinator.Restore(new EditorSessionRestoreState(
            EditorWorkspaceNavigation.Parse(_shellState.Workspace),
            _shellState.ProductionId,
            _shellState.SessionHistory.LastComponentVariantSelections));
        var initialLoad = _workspaceCoordinator.BeginTreeLoad(
            Session.Workspace);
        if (!_workspaceCoordinator.TryCommitTreeLoad(
                initialLoad,
                initialTreeRoots,
                "startup",
                out var initialTransition))
        {
            throw new InvalidOperationException(
                "The prepared startup tree became obsolete before window initialization.");
        }
        DesignWorkspaceButton.Click += (_, _) => SetWorkspace(EditorWorkspace.Design);
        ProductionWorkspaceButton.Click += (_, _) => SetWorkspace(EditorWorkspace.Production);
        ProductionComboBox.SelectionChanged += (_, _) => SelectProductionFromPicker();
        UpdateWorkspaceButtons();
        _variantHistory.RestoreState(_shellState.SessionHistory.VariantHistory);
        _previewController.RestoreDesignHistoryState(_shellState.SessionHistory.DesignPreviewHistory);
        _previewController.RestoreProductionHistoryState(_shellState.SessionHistory.ProductionPreviewHistory);
        _previewController.SetWorkspaceWithoutRefresh(Session.Workspace);
        _themeController.SetState(_shellState.IsDark, _shellState.SukiColor);
        EditorUiDensity.Configure(_shellState.UiTextScale, _shellState.UiCardPaddingScale);
        Closing += (_, _) =>
        {
            _shellState.Save(CreateSessionHistoryState());
            _productionNavigationActions.Dispose();
            application.Operations.Dispose();
            _workspaceCoordinator.Dispose();
            _previewController.Dispose();
        };
        _themeController.Apply();
        ApplyTreeLoadTransition(initialTransition);
        InitializePreviewOptions();
        ApplyUiTextScale();
    }

    private async void OnRefreshUsageClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedId = Session.SelectedNode?.Id;
        if (await LoadProjectTreeAsync()
            && selectedId is not null)
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
        _previewController.Initialize(Session.TreeRoots);
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
        _previewController.RefreshOptions(Session.TreeRoots);
    }

    private async Task<bool> LoadProjectTreeAsync()
    {
        CaptureActiveEditorViewState();
        try
        {
            var transition =
                await _workspaceCoordinator.ReloadTreeAsync();
            if (transition is null)
            {
                return false;
            }
            ApplyTreeLoadTransition(transition);
            return true;
        }
        catch (Exception exception)
        {
            _messages.Error(
                "Load project tree",
                exception);
            return false;
        }
    }

    private void ApplyTreeLoadTransition(
        EditorSessionTransition transition)
    {
        _treeExpansion.EnsureInitial(transition.Current.TreeRoots);
        ApplyPersistedContext(transition);
        if (transition.Current.SelectedNode is { } selected)
        {
            _treeExpansion.ExpandAncestors(selected);
            RebuildNavigationCards();
            RenderRootSelection(transition, rebuildTree: false);
        }
        else
        {
            RebuildNavigationCards();
        }
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
        NavigationWorkspaceTextBlock.Text =
            EditorWorkspaceNavigation.Title(Session.Workspace);
        RefreshProductionPicker();
        _renderedPreviewNavigationNodeId = _previewController.ActiveNavigationNodeId;
        _navigationRenderer.Rebuild(
            NavigationCardsPanel,
            Session.TreeRoots,
            Session.Workspace,
            Session.ProductionId);
        ApplyUiTextScale();
    }

    private void RefreshPreviewNavigationState()
    {
        var activeNodeId = _previewController.ActiveNavigationNodeId;
        if (activeNodeId.Equals(_renderedPreviewNavigationNodeId, StringComparison.Ordinal))
        {
            return;
        }
        RebuildNavigationCards();
    }

    private void ShowNode(ProjectTreeNode node, bool rebuildTree = true)
    {
        ShowNode(node, rebuildTree, "selection");
    }

    private void ShowNode(ProjectTreeNode node, bool rebuildTree, string source)
    {
        CaptureActiveEditorViewState();
        using var transaction = BeginContextTransaction(source, node.Id);
        if (!_workspaceCoordinator.TrySelectNode(
                node,
                source,
                out var transition))
        {
            return;
        }
        RenderRootSelection(transition, rebuildTree, transaction);
    }

    private void RenderRootSelection(
        EditorSessionTransition transition,
        bool rebuildTree,
        EditorShellContextTransaction? transaction = null)
    {
        var node = transition.Current.SelectedNode
            ?? throw new InvalidOperationException(
                "A root editor transition requires a selected node.");
        _ = TrackVariantTransitionAsync(
            transition.Previous.SelectedNode,
            node);
        _treeExpansion.ExpandAncestors(node);
        _previewController.BeginSelectionTransition();
        var editorNode = EditorNodeSelectionState.EditorNodeForSelection(node);
        transaction?.Checkpoint("before-editor-candidate");
        _editorContent.Build(editorNode, node);
        RefreshPreviewAuthoringSurface(node);
        SetEditorRootTitle(editorNode.Name);
        transaction?.Checkpoint("after-editor-swap");
        _editorViewState.Restore(node, _editorContent.Cards);

        if (rebuildTree)
        {
            RebuildNavigationCards();
            transaction?.Checkpoint("after-navigation-swap");
        }
        ApplyUiTextScale();
        var revision = transition.Current.Revision;
        var selectedNodeId = node.Id;
        _previewController.ScheduleSelectionRefresh(() =>
            _workspaceCoordinator.IsCurrent(
                revision,
                selectedNodeId));
    }

    private async Task TrackVariantTransitionAsync(
        ProjectTreeNode? previousNode,
        ProjectTreeNode nextNode)
    {
        try
        {
            await _variantHistory.TrackTransitionAsync(
                previousNode,
                nextNode);
        }
        catch (OperationCanceledException)
        {
            // Closing the session cancels queued history reads.
        }
        catch (Exception exception)
        {
            _messages.Warning("Variant history", exception.Message);
        }
    }

    private void RefreshPreviewAuthoringSurface(ProjectTreeNode node)
    {
        var authoringSurface = _collectionCards.CreatePreviewAuthoringSurface(
            node,
            Session.Workspace);
        _previewUtilityTabStateKey =
            $"{EditorNodeSelectionState.EditorNodeForSelection(node).RecordClassId}:preview:utility-tab";
        var selectedId = _editorSessionUiState.Selection(_previewUtilityTabStateKey);
        var selectedTab = selectedId switch
        {
            PreviewUtilityControlsId => PreviewControlsTab,
            PreviewUtilitySetupId => PreviewSetupTab,
            PreviewUtilityAuthoringDataId when authoringSurface is not null => PreviewAuthoringDataTab,
            _ when authoringSurface is not null => PreviewAuthoringDataTab,
            _ => PreviewSetupTab,
        };

        _isUpdatingPreviewUtilityTab = true;
        try
        {
            PreviewAuthoringDataHost.Content = authoringSurface?.Content;
            PreviewAuthoringDataTab.Header = authoringSurface?.Header ?? "Authoring Data";
            PreviewAuthoringDataTab.IsVisible = authoringSurface is not null;
            PreviewUtilityTabs.SelectedItem = selectedTab;
        }
        finally
        {
            _isUpdatingPreviewUtilityTab = false;
        }
    }

    private string? PreviewUtilityTabId(object? selectedTab)
    {
        if (ReferenceEquals(selectedTab, PreviewAuthoringDataTab)) return PreviewUtilityAuthoringDataId;
        if (ReferenceEquals(selectedTab, PreviewSetupTab)) return PreviewUtilitySetupId;
        if (ReferenceEquals(selectedTab, PreviewControlsTab)) return PreviewUtilityControlsId;
        return null;
    }

    private void ShowEmbeddedContext(EditorEmbeddedContext context)
    {
        CaptureActiveEditorViewState();
        var transition = _workspaceCoordinator.ShowEmbeddedEditor(context);
        var embedded = transition.Current.EmbeddedEditor
            ?? throw new InvalidOperationException(
                "The embedded editor transition did not retain its context.");
        _editorContent.BuildEmbedded(embedded);
        SetEditorEmbeddedTitle(embedded);
        _editorViewState.Restore(
            embedded.RecordClassId,
            _editorContent.Cards);
        RefreshPreviewDevice();
        ApplyUiTextScale();
    }

    private void CaptureActiveEditorViewState()
    {
        if (Session.EmbeddedEditor is { } embedded)
        {
            _editorViewState.Capture(embedded.RecordClassId, _editorContent.Cards);
            return;
        }

        _editorViewState.Capture(Session.SelectedNode, _editorContent.Cards);
    }

    private void ScheduleActiveEditorReload(ProjectTreeNode ownerNode)
    {
        var scheduledRevision = Session.Revision;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_workspaceCoordinator.IsCurrent(
                    scheduledRevision,
                    ownerNode.Id))
            {
                return;
            }

            ReloadActiveEditor(ownerNode.Id);
        }, DispatcherPriority.Background);
    }

    private async void ReloadActiveEditor(string ownerNodeId)
    {
        var viewState = _editorViewState.CaptureState(_editorContent.Cards);
        EditorSessionTransition? transition;
        try
        {
            transition =
                await _workspaceCoordinator.ReloadTreeAsync(
                    "field-refresh",
                    EditorTreeLoadIntent.ActiveEditor);
        }
        catch (Exception exception)
        {
            _messages.Error(
                "Refresh active editor",
                exception);
            return;
        }
        if (transition is null)
        {
            return;
        }
        _treeExpansion.EnsureInitial(transition.Current.TreeRoots);
        ApplyPersistedContext(transition);
        var refreshedOwner = transition.Current.SelectedNode;
        if (refreshedOwner is null
            || !refreshedOwner.Id.Equals(
                ownerNodeId,
                StringComparison.Ordinal))
        {
            RebuildNavigationCards();
            if (refreshedOwner is not null)
            {
                RenderRootSelection(transition, rebuildTree: false);
            }
            RefreshPreviewOptions();
            return;
        }

        _treeExpansion.ExpandAncestors(refreshedOwner);
        RebuildNavigationCards();

        if (transition.Current.EmbeddedEditor is { } embeddedContext)
        {
            _editorContent.BuildEmbedded(embeddedContext);
            SetEditorEmbeddedTitle(embeddedContext);
            RefreshPreviewDevice();
            _editorViewState.RestoreState(viewState, _editorContent.Cards);
        }
        else
        {
            RenderRootSelection(transition, rebuildTree: false);
            _editorViewState.RestoreState(viewState, _editorContent.Cards);
        }

        RefreshPreviewOptions();
        ApplyUiTextScale();
    }

    private void ReturnToEmbeddedOwner(ProjectTreeNode ownerNode)
    {
        ShowNode(ownerNode, false, "breadcrumb");
    }

    private void SetEditorRootTitle(string title)
    {
        _editorHeader.SetRootTitle(title);
    }

    private void SetEditorEmbeddedTitle(EditorEmbeddedContext context)
    {
        _editorHeader.SetEmbeddedTitle(context);
    }

    private async void ReloadAndSelect(ProjectTreeNode node)
    {
        if (!await LoadProjectTreeAsync())
        {
            return;
        }
        RefreshPreviewOptions();
        SelectNodeById(node.Id);
    }

    private bool SelectNodeById(string nodeId)
    {
        return SelectNodeById(nodeId, "node-id");
    }

    private bool SelectNodeById(string nodeId, string source)
    {
        var node = EditorNodeSelectionState.FindNodeById(
            Session.TreeRoots,
            nodeId);
        if (node is null)
        {
            return false;
        }

        var selectableNode = EditorNodeSelectionState.CanSelectTreeNode(node)
            ? node
            : EditorNodeSelectionState.ClosestEditableNode(node);
        selectableNode = _workspaceCoordinator.ResolveSelectionNode(
            selectableNode);
        _treeExpansion.ExpandAncestors(selectableNode);
        ShowNode(selectableNode, rebuildTree: true, source);
        ApplyUiTextScale();
        return true;
    }

    private Task NavigateToReferenceUsage(ReferenceUsageDetail usage)
    {
        return _referenceUsageNavigator.Navigate(usage);
    }

    private async Task<bool> SelectReferenceNodeInWorkspaceAsync(
        EditorWorkspace workspace,
        string nodeId)
    {
        var node = EditorNodeSelectionState.FindNodeById(
            Session.TreeRoots,
            nodeId);
        if (node is null)
        {
            if (!await LoadProjectTreeAsync())
            {
                return false;
            }
            node = EditorNodeSelectionState.FindNodeById(
                Session.TreeRoots,
                nodeId);
        }
        if (node is null || !EditorWorkspaceNavigation.Contains(workspace, node))
        {
            return false;
        }

        CaptureActiveEditorViewState();
        using var transaction = BeginContextTransaction(
            "reference-usage",
            nodeId);
        if (!_workspaceCoordinator.TrySelectNodeInWorkspace(
                workspace,
                nodeId,
                "reference-usage",
                out var transition))
        {
            return false;
        }
        ApplyPersistedContext(transition);
        if (transition.Effects.HasFlag(EditorSessionEffects.Workspace))
        {
            _previewController.SetWorkspaceWithoutRefresh(workspace);
            UpdateWorkspaceButtons();
        }
        RenderRootSelection(transition, rebuildTree: true, transaction);
        _navigationRenderer.BringNodeIntoView(
            NavigationCardsPanel,
            transition.Current.SelectedNode?.Id ?? nodeId);
        return true;
    }

    private System.Threading.Tasks.Task OpenComponentVariantReference(string variantReference)
    {
        if (!SelectNodeById(variantReference))
        {
            _messages.Warning("Open component variant", $"Could not find variant '{variantReference}'.");
        }

        return System.Threading.Tasks.Task.CompletedTask;
    }

    private void ApplyUiDensity(bool rebuildCards)
    {
        EditorUiDensity.Configure(_shellState.UiTextScale, _shellState.UiCardPaddingScale);
        if (rebuildCards && Session.SelectedNode is { } selected)
        {
            ShowNode(selected, rebuildTree: true);
            return;
        }

        ApplyUiTextScale();
    }

    private void ApplyUiTextScale()
    {
        EditorUiTextScale.Apply(this, _shellState.UiTextScale, DesignPreviewHost);
    }

    private async void SetWorkspace(EditorWorkspace workspace)
    {
        using var transaction = BeginContextTransaction("workspace", workspace.ToString());
        CaptureActiveEditorViewState();
        EditorSessionTransition? transition;
        try
        {
            transition =
                await _workspaceCoordinator.SwitchWorkspaceAsync(
                    workspace);
        }
        catch (Exception exception)
        {
            _messages.Error(
                "Switch workspace",
                exception);
            return;
        }
        if (transition is null
            || transition.Effects == EditorSessionEffects.None)
        {
            return;
        }
        ApplyPersistedContext(transition);
        _previewController.SetWorkspaceWithoutRefresh(workspace);
        UpdateWorkspaceButtons();
        transaction.Checkpoint("workspace-state-ready");
        _treeExpansion.EnsureInitial(transition.Current.TreeRoots);
        if (transition.Current.SelectedNode is { } selected)
        {
            _treeExpansion.ExpandAncestors(selected);
            RebuildNavigationCards();
            RenderRootSelection(
                transition,
                rebuildTree: false,
                transaction);
        }
        else
        {
            RebuildNavigationCards();
        }
        transaction.Checkpoint("workspace-selection-committed");
    }

    private EditorShellContextTransaction BeginContextTransaction(string source, string targetId)
    {
        return new EditorShellContextTransaction(
            source,
            targetId,
            Session.SelectedNode?.Id ?? "",
            Session.Workspace.ToString(),
            this,
            NavigationCardsPanel,
            EditorCardsPanel,
            DesignPreviewHost,
            _previewController.NativeHostLifecycleState);
    }

    private void ApplyPersistedContext(EditorSessionTransition transition)
    {
        if (transition.Effects.HasFlag(EditorSessionEffects.Workspace))
        {
            _shellState.SetWorkspace(transition.Current.Workspace);
        }
        if (transition.Effects.HasFlag(EditorSessionEffects.Production))
        {
            _shellState.SetProductionId(transition.Current.ProductionId);
        }
    }

    private void UpdateWorkspaceButtons()
    {
        var activeBrush = new SolidColorBrush(Color.Parse(_themeController.IsDark ? "#F0B429" : "#A56600"));
        var inactiveBrush = new SolidColorBrush(Color.Parse(_themeController.IsDark ? "#9CA3AF" : "#6B7280"));
        var activeBackground = new SolidColorBrush(Color.Parse(_themeController.IsDark ? "#463711" : "#F2DEAA"));
        WorkspaceSwitcherBorder.BorderBrush = new SolidColorBrush(Color.Parse(_themeController.IsDark ? "#59616D" : "#AAB1BB"));
        WorkspaceSwitcherBorder.Background = new SolidColorBrush(Color.Parse(_themeController.IsDark ? "#16191F" : "#E3E5E8"));
        ApplyWorkspaceButton(
            DesignWorkspaceButton,
            Session.Workspace == EditorWorkspace.Design,
            activeBrush,
            inactiveBrush,
            activeBackground);
        ApplyWorkspaceButton(
            ProductionWorkspaceButton,
            Session.Workspace == EditorWorkspace.Production,
            activeBrush,
            inactiveBrush,
            activeBackground);
        ProductionPickerGrid.IsVisible =
            Session.Workspace == EditorWorkspace.Production;
    }

    private void RefreshProductionPicker()
    {
        _isUpdatingProductionPicker = true;
        try
        {
            var options = Session.TreeRoots
                .Select((project) => new FieldOption(project.Id, project.Name))
                .ToList();
            ProductionComboBox.ItemsSource = options;
            ProductionComboBox.SelectedItem = options.FirstOrDefault((option) =>
                    option.Value == Session.ProductionId)
                ?? options.FirstOrDefault();
            _productionNavigationActions.Refresh(
                ProductionComboBox.SelectedItem is null
                    ? null
                    : Session.ProductionId);
        }
        finally
        {
            _isUpdatingProductionPicker = false;
        }
    }

    private void SelectProductionFromPicker()
    {
        if (_isUpdatingProductionPicker || ProductionComboBox.SelectedItem is not { } selected) return;
        if (string.Equals(
                Session.ProductionId,
                selected.Value,
                StringComparison.Ordinal))
        {
            return;
        }

        CaptureActiveEditorViewState();
        using var transaction = BeginContextTransaction(
            "production",
            selected.Value);
        if (_workspaceCoordinator.TrySelectProduction(
                selected.Value,
                "production",
                out var transition))
        {
            ApplyPersistedContext(transition);
            if (transition.Effects.HasFlag(EditorSessionEffects.Workspace))
            {
                _previewController.SetWorkspaceWithoutRefresh(
                    transition.Current.Workspace);
                UpdateWorkspaceButtons();
            }
            RenderRootSelection(
                transition,
                rebuildTree: false,
                transaction);
        }

        RebuildNavigationCards();
    }

    private void OpenSelectedProductionCard(string cardSessionStateId)
    {
        var production = Session.TreeRoots.FirstOrDefault((project) =>
            project.Id == Session.ProductionId);
        if (production is null) return;

        ShowNode(production);
        var card = _editorContent.Cards.FirstOrDefault((candidate) =>
            candidate.SessionStateId.Equals(
                cardSessionStateId,
                StringComparison.Ordinal));
        if (card is not null)
        {
            card.IsExpanded = true;
        }
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
            LastComponentVariantSelections = Session.VariantSelections
                .ComponentVariantNodeIds
                .ToDictionary(
                (entry) => entry.Key,
                (entry) => entry.Value,
                StringComparer.Ordinal),
        };
    }

}
