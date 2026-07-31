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
    private readonly EditorCollectionCardFactory _collectionCards;
    private readonly EditorPreviewController _previewController;
    private readonly IEditorShellMessageSink _messages;
    private readonly EditorThemeController _themeController;
    private readonly EditorNodeCommandController _nodeCommands;
    private readonly EditorShellStateService _shellState;
    private readonly EditorNavigationPanelController
        _navigationPanel;
    private readonly PreviewControlsDockController
        _previewControlsDock;
    private readonly EditorNavigationRenderer _navigationRenderer;
    private readonly EditorViewStateController _editorViewState;
    private readonly EditorAuthoringFocusController
        _authoringFocusController;
    private readonly EditorSessionUiState _editorSessionUiState = new();
    private readonly EditorContentController _editorContent;
    private readonly EditorEmbeddedEditorController _embeddedEditors;
    private readonly EditorEmbeddedUsageNavigator _embeddedUsageNavigator;
    private readonly EditorReferenceUsageNavigator _referenceUsageNavigator;
    private readonly EditorHeaderController _editorHeader;
    private readonly EditorVariantHistoryService _variantHistory;
    private readonly EditorProductionNavigationActions _productionNavigationActions;
    private readonly EditorTreeExpansionState _treeExpansion = new();
    private readonly EditorActiveFieldControls _activeFieldControls = new();
    private readonly EditorWorkspaceCoordinator _workspaceCoordinator;
    private readonly EditorTreePreviewTransitionCoordinator
        _treePreviewTransitions;
    private bool _isUpdatingProductionPicker;
    private string _previewUtilityTabStateKey = "";
    private bool _isUpdatingPreviewUtilityTab;
    private string _renderedPreviewNavigationNodeId = "";
    private (string NodeId, string CardId)? _pendingEditorCardExpansion;
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
        var coreFieldValues = application.CoreFieldValues;
        var recordClassFieldValues =
            application.RecordClassFieldValues;
        var componentClassFieldValues =
            application.ComponentClassFieldValues;
        var productionShotContext =
            application.ProductionShotContext;
        _workspaceCoordinator = application.WorkspaceCoordinator;
        var fieldCommitCoordinator = new EditorFieldCommitCoordinator(
            application.Operations);
        InitializeComponent();
        EditorContextMenuBehavior.Configure(this);
        _themeController = new EditorThemeController(this, RootShell, RefreshShellTheme);
        var inlinePreviews = EditorInlinePreviewControllerFactory.Create(
            data.ActorPreview,
            data.ProjectPaths,
            () => _themeController.IsDark);
        EditorTextBoxBehavior.Configure(ShellMessagesTextBox);
        _messages = new EditorShellMessageSink(ShellMessagesTextBox);
        _editorViewState = new EditorViewStateController(EditorScrollViewer);
        _authoringFocusController =
            new EditorAuthoringFocusController(
                _editorViewState.CancelPendingRestore,
                _messages);
        _embeddedEditors = new EditorEmbeddedEditorController(ShowEmbeddedContext, _messages);
        var previewAuthoringNavigator = new PreviewAuthoringNavigator(
            () => Session.SelectedNode,
            (nodeId) => NavigateToNodeById(
                nodeId,
                "preview-element"),
            ShowEmbeddedContext,
            _authoringFocusController.Request,
            _messages);
        _previewController = new EditorPreviewController(
            data.Preview,
            data.ComponentPreview,
            data.Timeline,
            data.ModuleInstanceThemes,
            data.Dictionary,
            data.ActorPreview,
            data.ProjectPaths,
            application.Operations,
            PreviewDeviceComboBox,
            PreviewThemeComboBox,
            PreviewModeComboBox,
            PreviewOrientationComboBox,
            _messages,
            PreviewSetupHost,
            PreviewCombinedControlsHost,
            PreviewBusyHost,
            DesignPreviewHost,
            PreviewContextTextBlock,
            PreviewContextHistoryButton,
            PreviewContextAddHistoryButton,
            PreviewContextLockButton,
            PreviewTitlePanel,
            () => _themeController.IsDark,
            () => Session.SelectedNode,
            (nodeId) => NavigateToNodeById(
                nodeId,
                "preview-context"),
            (target) => previewAuthoringNavigator.Navigate(target),
            this);
        _previewControlsDock =
            new PreviewControlsDockController(
                this,
                PreviewPanelDock,
                PreviewHeaderSurface,
                PreviewPanelGrid,
                PreviewUtilitySurface,
                PreviewPanelGrid,
                PreviewUtilitySplitter,
                PreviewControlsDetachButton,
                () => _themeController.IsDark);
        _treePreviewTransitions =
            new EditorTreePreviewTransitionCoordinator(
                _workspaceCoordinator,
                _previewController);
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
        _navigationPanel =
            new EditorNavigationPanelController(
                ShellColumns,
                NavigationPanelBorder,
                NavigationPanelSplitter,
                NavigationPanelToggleButton,
                () => Bounds.Width > 0
                    ? Bounds.Width
                    : Width);
        _shellState =
            new EditorShellStateService(this, ShellColumns);
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
            productionShotContext.CanExposeChildren,
            productionShotContext.IsNavigationNodeEnabled,
            () => _previewController.ActiveNavigationNodeId,
            _productionNavigationActions.NodeAction);
        _previewController.PlaybackState.Changed += RefreshPreviewNavigationState;
        var previewAuthoringRefresh =
            new PreviewAuthoringRefreshCoordinator(
                () => Session.Workspace,
                _previewController.NotifyAuthoredPreviewInputsChanged,
                RefreshPreviewOptions);
        var fieldPostCommitEffects = new EditorFieldPostCommitEffects(
            data.Presentation,
            application.Operations,
            () => _previewController.SelectedDeviceId,
            SetEditorRootTitle,
            RebuildNavigationCards,
            previewAuthoringRefresh.Notify,
            RefreshPreviewOptions,
            RefreshProductionPicker);
        var pathBrowser = new EditorPathBrowser(
            StorageProvider,
            data.Presentation,
            data.ProjectPaths,
            () => Session.SelectedNode);
        var domainDialogs = new EditorDomainDialogService(
            this,
            data.ModuleInstances,
            data.IconThemes,
            data.ThemeTokens,
            application.Operations,
            () => _themeController.IsDark,
            _nodeCommands.ShowInfoDialog,
            pathBrowser.BrowseSvgFile,
            ReloadAndSelect);
        var dictionaryFieldServices = new EditorDictionaryFieldServices(
            data.Dictionary,
            data.Preview,
            data.Timeline,
            data.ModuleInstanceThemes,
            data.ActorPreview,
            data.ProjectPaths,
            pathBrowser,
            domainDialogs,
            application.Operations,
            () => _previewController.SelectedThemeId,
            _previewController.SetDesignPreviewTestValue);
        var fieldValues = new EditorFieldValueRouter(
            coreFieldValues,
            recordClassFieldValues,
            componentClassFieldValues,
            inlinePreviews,
            fieldPostCommitEffects);
        var layoutCards = new EditorLayoutCardFactory(
            fieldValues,
            componentClassFieldValues,
            inlinePreviews,
            dictionaryFieldServices,
            fieldCommitCoordinator,
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
            previewAuthoringRefresh.Notify,
            _editorSessionUiState);
        _embeddedUsageNavigator = new EditorEmbeddedUsageNavigator(
            data.Components,
            this,
            () => _themeController.IsDark,
            (nodeId) => NavigateToNodeById(
                nodeId,
                "embedded-usage"),
            LoadProjectTreeAsync,
            () => Session.SelectedNode,
            _embeddedEditors.Open,
            _messages);
        _referenceUsageNavigator = new EditorReferenceUsageNavigator(
            SelectReferenceNodeInWorkspaceAsync,
            _embeddedUsageNavigator.NavigateToEmbeddedUsage,
            _messages);
        var headerPreparation =
            new EditorHeaderPreparationService(
                data.Components,
                data.Preview,
                data.Timeline,
                data.ModuleInstanceThemes);
        _editorHeader = new EditorHeaderController(
            EditorBreadcrumbPanel,
            EditorContextStripHost,
            EditorHeaderActionsPanel,
            () => Session.SelectedNode,
            _workspaceCoordinator.PreferredVariantNode,
            _workspaceCoordinator.PreferredModuleVariantNode,
            _embeddedUsageNavigator,
            (node, rebuildTree) =>
                ShowRoutedNode(
                    node,
                    rebuildTree,
                    "editor-header"),
            ReturnToEmbeddedOwner,
            ShowEmbeddedContext,
            _nodeCommands.SaveCurrentVariant,
            _variantHistory.Snapshots,
            _nodeCommands.RestoreVariantSnapshot,
            () => _workspaceCoordinator
                .DesignNavigationAvailability,
            NavigateDesignHistory,
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
            domainDialogs,
            ReloadAndSelect,
            previewAuthoringRefresh.Notify,
            dictionaryFieldServices,
            _messages,
            _previewController.TriggerDesignPreviewAction,
            _previewController.RestoreDesignPreviewAction,
            _previewController.CanRestoreDesignPreviewAction,
            _previewController.StepDesignPreviewAction,
            _previewController.CanStepDesignPreviewAction,
            _previewController.SetDesignPreviewActionFrame,
            _previewController.CurrentDesignPreviewActionFrame,
            _previewController.MaximumDesignPreviewActionFrame,
            _previewController.SetDesignPreviewTestValue,
            _previewController.SetDesignPreviewCollectionItemValues,
            _previewController.SetDesignPreviewCollectionTestItems,
            _previewController.ResetDesignPreviewTestValues,
            _previewController.PlaybackState,
            (nodeId) => NavigateToNodeById(
                nodeId,
                "collection-navigation"),
            NavigateToReferenceUsage,
            ShowEmbeddedContext,
            _previewController.ProductionShotFrame,
            _previewController.SetProductionShotFrame,
            _previewController.ToggleProductionPlayback,
            _editorSessionUiState);
        _editorContent = new EditorContentController(
            new EditorContentPreparationService(
                data.Layouts,
                fieldValues,
                componentClassFieldValues,
                dictionaryFieldServices,
                headerPreparation,
                application.Operations),
            EditorCardsPanel,
            () => Math.Max(1, EditorScrollViewer.Bounds.Width - EditorScrollViewer.Padding.Left - EditorScrollViewer.Padding.Right),
            EditorScrollViewer,
            EditorPeerViewHost,
            EditorOverridesPanel,
            EditorScrollViewer,
            EditorOverridesScrollViewer,
            _activeFieldControls,
            inlinePreviews,
            layoutCards,
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
        ApplyHeaderUtilityButton(
            NavigationPanelToggleButton);
        ApplyHeaderUtilityButton(UsageRefreshButton);
        ApplyHeaderUtilityButton(ShellSettingsButton);
        EditorAccessibility.Describe(UsageRefreshButton, "Update usage");
        EditorAccessibility.Describe(ShellSettingsButton, "Settings");
        _shellState.Restore();
        _navigationPanel.Restore(
            _shellState.IsNavigationPanelCollapsed,
            _shellState.NavigationPanelExpandedWidth,
            _shellState.NavigationPanelExpandedEditorWidth,
            _shellState.NavigationPanelExpandedPreviewWidth);
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
            _shellState.Save(
                CreateSessionHistoryState(),
                _navigationPanel.Snapshot());
            _productionNavigationActions.Dispose();
            _previewControlsDock.Dispose();
            _editorContent.Dispose();
            _collectionCards.Dispose();
            _previewController.Dispose();
            application.Operations.Dispose();
            _workspaceCoordinator.Dispose();
        };
        _themeController.Apply();
        InitializePreviewOptions();
        ApplyTreeLoadTransition(initialTransition);
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
        RefreshPreviewOptions();
    }

    private void RefreshPreviewDevice()
    {
        _previewController.Refresh();
    }

    private void RefreshShellTheme()
    {
        _previewControlsDock.RefreshTheme();
        UpdateWorkspaceButtons();
        RebuildNavigationCards();
        _previewController
            .NotifyAuthoredPreviewInputsChanged();
        ApplyUiTextScale();
    }

    private async void RefreshPreviewOptions()
    {
        await RefreshPreviewOptionsAsync();
    }

    private async Task<bool> RefreshPreviewOptionsAsync()
    {
        try
        {
            return await _previewController.RefreshOptionsAsync(
                Session.TreeRoots);
        }
        catch (Exception exception)
        {
            _messages.Error(
                "Preview options",
                exception);
            return false;
        }
    }

    private async Task<bool> LoadProjectTreeAsync()
    {
        CaptureActiveEditorViewState();
        try
        {
            var transition =
                await _treePreviewTransitions.ReloadAsync();
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
        EditorShellContextTransaction? transaction = null,
        EditorViewState? restoreState = null)
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
        if (_editorContent.TryBuildSpecial(node))
        {
            RestoreRootEditorViewState(
                node,
                restoreState);
            ApplyPendingEditorCardExpansion(node.Id);
        }
        else
        {
            _editorContent.ShowLoading();
            _ = PrepareRootEditorAsync(
                editorNode,
                node,
                transition.Current.Revision,
                restoreState);
        }
        _ = RefreshPreviewAuthoringSurfaceAsync(
            node,
            transition.Current.Revision);
        _editorHeader.SetRootTitle(
            editorNode.Name,
            EditorPreparedHeader.Loading(node.Id));
        transaction?.Checkpoint("after-editor-swap");

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

    private async Task PrepareRootEditorAsync(
        ProjectTreeNode layoutNode,
        ProjectTreeNode dataNode,
        long revision,
        EditorViewState? restoreState)
    {
        try
        {
            var prepared = await _editorContent.PrepareRootAsync(
                layoutNode,
                dataNode);
            if (!_workspaceCoordinator.IsCurrent(
                    revision,
                    dataNode.Id)
                || Session.EmbeddedEditor is not null)
            {
                return;
            }

            _editorContent.CommitRoot(
                layoutNode,
                dataNode,
                prepared);
            _editorHeader.SetRootTitle(
                layoutNode.Name,
                prepared.Header);
            if (dataNode.Kind is
                ProjectTreeNodeKind.ComponentVariant
                or ProjectTreeNodeKind.ModuleVariant)
            {
                _ = PrepareRootOverridesAsync(
                    layoutNode,
                    dataNode,
                    revision);
            }
            RestoreRootEditorViewState(
                dataNode,
                restoreState);
            _authoringFocusController.ApplyRoot(
                dataNode,
                prepared.Cards,
                _editorContent.Cards);
            ApplyPendingEditorCardExpansion(dataNode.Id);
            ApplyUiTextScale();
        }
        catch (OperationCanceledException)
        {
            // A newer root or embedded editor owns the visual surface.
        }
        catch (Exception exception)
        {
            if (_workspaceCoordinator.IsCurrent(
                    revision,
                    dataNode.Id))
            {
                _messages.Error(
                    "Prepare editor",
                    exception);
            }
        }
    }

    private async Task PrepareRootOverridesAsync(
        ProjectTreeNode layoutNode,
        ProjectTreeNode dataNode,
        long revision)
    {
        try
        {
            var prepared =
                await _editorContent.PrepareOverridesAsync(
                    layoutNode,
                    dataNode);
            if (!_workspaceCoordinator.IsCurrent(
                    revision,
                    dataNode.Id)
                || Session.EmbeddedEditor is not null)
            {
                return;
            }
            _editorContent.CommitOverrides(
                layoutNode,
                dataNode,
                prepared);
            ApplyUiTextScale();
        }
        catch (OperationCanceledException)
        {
            // A newer root or embedded editor owns the visual surface.
        }
        catch (Exception exception)
        {
            if (_workspaceCoordinator.IsCurrent(
                    revision,
                    dataNode.Id))
            {
                _messages.Error(
                    "Prepare flat Overrides",
                    exception);
            }
        }
    }

    private void RestoreRootEditorViewState(
        ProjectTreeNode node,
        EditorViewState? restoreState)
    {
        if (restoreState is not null)
        {
            _editorViewState.RestoreState(
                restoreState,
                _editorContent.Cards);
            return;
        }
        _editorViewState.Restore(
            node,
            _editorContent.Cards);
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

    private async Task RefreshPreviewAuthoringSurfaceAsync(
        ProjectTreeNode node,
        long revision)
    {
        var workspace = Session.Workspace;
        if (!EditorCollectionCardFactory
                .SupportsPreviewAuthoringSurface(
                    node,
                    workspace))
        {
            _collectionCards
                .CancelPreviewAuthoringPreparation();
            RenderPreviewAuthoringSurface(
                node,
                null);
            return;
        }

        var header = workspace == EditorWorkspace.Production
            ? "Screen Payload"
            : "Test Values";
        RenderPreviewAuthoringSurface(
            node,
            new EditorPreviewAuthoringSurface(
                header,
                new Border
                {
                    Padding = new Thickness(4),
                    Child = new EditorLoadingScrim(),
                }));
        try
        {
            var transientState = _previewController
                .CaptureDesignPreviewTransientState(node);
            var prepared = await _collectionCards
                .PreparePreviewAuthoringSurfaceAsync(
                    node,
                    workspace,
                    transientState);
            if (!_workspaceCoordinator.IsCurrent(
                    revision,
                    node.Id))
            {
                return;
            }

            RenderPreviewAuthoringSurface(
                node,
                prepared is null
                    ? null
                    : EditorCollectionCardFactory
                        .CreatePreparedPreviewAuthoringSurface(
                            prepared));
        }
        catch (OperationCanceledException)
        {
            // A newer selection owns Preview authoring.
        }
        catch (Exception exception)
        {
            if (_workspaceCoordinator.IsCurrent(
                    revision,
                    node.Id))
            {
                RenderPreviewAuthoringSurface(
                    node,
                    null);
                _messages.Error(
                    "Prepare Preview authoring",
                    exception);
            }
        }
    }

    private void RenderPreviewAuthoringSurface(
        ProjectTreeNode node,
        EditorPreviewAuthoringSurface? authoringSurface)
    {
        _previewUtilityTabStateKey =
            $"{EditorNodeSelectionState.EditorNodeForSelection(node).RecordClassId}:preview:utility-tab";
        var selectedId = _editorSessionUiState.Selection(_previewUtilityTabStateKey);
        var selectedTab = selectedId switch
        {
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
        return null;
    }

    private void ShowEmbeddedContext(EditorEmbeddedContext context)
    {
        CaptureActiveEditorViewState();
        var transition = _workspaceCoordinator.ShowEmbeddedEditor(context);
        var embedded = transition.Current.EmbeddedEditor
            ?? throw new InvalidOperationException(
                "The embedded editor transition did not retain its context.");
        _editorContent.ShowLoading();
        _ = PrepareEmbeddedEditorAsync(
            embedded,
            transition.Current.Revision);
        _editorHeader.SetEmbeddedTitle(
            embedded,
            EditorPreparedHeader.Loading(
                embedded.OwnerNode.Id));
        RefreshPreviewDevice();
        ApplyUiTextScale();
    }

    private async Task PrepareEmbeddedEditorAsync(
        EditorEmbeddedContext context,
        long revision,
        EditorViewState? restoreState = null)
    {
        try
        {
            var prepared = await _editorContent.PrepareEmbeddedAsync(
                context);
            if (Session.Revision != revision
                || Session.EmbeddedEditor is not { } current
                || !current.OwnerNode.Id.Equals(
                    context.OwnerNode.Id,
                    StringComparison.Ordinal)
                || !current.RecordClassId.Equals(
                    context.RecordClassId,
                    StringComparison.Ordinal))
            {
                return;
            }

            _editorContent.CommitEmbedded(
                current,
                prepared);
            _editorHeader.SetEmbeddedTitle(
                current,
                prepared.Header);
            if (restoreState is not null)
            {
                _editorViewState.RestoreState(
                    restoreState,
                    _editorContent.Cards);
            }
            else
            {
                _editorViewState.Restore(
                    current.RecordClassId,
                    _editorContent.Cards);
            }
            _authoringFocusController.ApplyEmbedded(
                current,
                prepared.Cards,
                _editorContent.Cards);
            ApplyUiTextScale();
        }
        catch (OperationCanceledException)
        {
            // A newer root or embedded editor owns the visual surface.
        }
        catch (Exception exception)
        {
            if (Session.Revision == revision)
            {
                _messages.Error(
                    "Prepare embedded editor",
                    exception);
            }
        }
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
                await _treePreviewTransitions.ReloadAsync(
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
            return;
        }

        _treeExpansion.ExpandAncestors(refreshedOwner);
        RebuildNavigationCards();

        if (transition.Current.EmbeddedEditor is { } embeddedContext)
        {
            _editorContent.ShowLoading();
            _ = PrepareEmbeddedEditorAsync(
                embeddedContext,
                transition.Current.Revision,
                viewState);
            _editorHeader.SetEmbeddedTitle(
                embeddedContext,
                EditorPreparedHeader.Loading(
                    embeddedContext.OwnerNode.Id));
            RefreshPreviewDevice();
        }
        else
        {
            RenderRootSelection(
                transition,
                rebuildTree: false,
                restoreState: viewState);
        }

        ApplyUiTextScale();
    }

    private void ReturnToEmbeddedOwner(ProjectTreeNode ownerNode)
    {
        ShowRoutedNode(
            ownerNode,
            rebuildTree: false,
            source: "breadcrumb");
    }

    private void ShowRoutedNode(
        ProjectTreeNode node,
        bool rebuildTree,
        string source)
    {
        _navigationPanel.EnsureVisible();
        ShowNode(node, rebuildTree, source);
        BringSelectedNavigationNodeIntoView();
    }

    private void NavigateDesignHistory(
        int direction)
    {
        CaptureActiveEditorViewState();
        using var transaction =
            BeginContextTransaction(
                direction < 0
                    ? "design-history-back"
                    : "design-history-forward",
                direction.ToString());
        if (!_workspaceCoordinator
                .TryNavigateDesignHistory(
                    direction,
                    out var transition))
        {
            return;
        }

        _navigationPanel.EnsureVisible();
        ApplyPersistedContext(
            transition);
        if (transition.Effects.HasFlag(
                EditorSessionEffects.Workspace))
        {
            _previewController
                .SetWorkspaceWithoutRefresh(
                    EditorWorkspace.Design);
            UpdateWorkspaceButtons();
        }
        if (transition.Current.EmbeddedEditor
            is { } embedded)
        {
            RenderEmbeddedHistorySelection(
                transition,
                embedded);
            BringSelectedNavigationNodeIntoView();
            return;
        }

        RenderRootSelection(
            transition,
            rebuildTree: true,
            transaction);
        BringSelectedNavigationNodeIntoView();
    }

    private void RenderEmbeddedHistorySelection(
        EditorSessionTransition transition,
        EditorEmbeddedContext embedded)
    {
        var node =
            transition.Current.SelectedNode
            ?? throw new InvalidOperationException(
                "Embedded history requires an exact owner.");
        _ = TrackVariantTransitionAsync(
            transition.Previous.SelectedNode,
            node);
        _treeExpansion.ExpandAncestors(
            node);
        _previewController
            .BeginSelectionTransition();
        _editorContent.ShowLoading();
        _ = PrepareEmbeddedEditorAsync(
            embedded,
            transition.Current.Revision);
        _ = RefreshPreviewAuthoringSurfaceAsync(
            node,
            transition.Current.Revision);
        _editorHeader.SetEmbeddedTitle(
            embedded,
            EditorPreparedHeader.Loading(
                embedded.OwnerNode.Id));
        RebuildNavigationCards();
        ApplyUiTextScale();
        var revision =
            transition.Current.Revision;
        _previewController
            .ScheduleSelectionRefresh(
                () => _workspaceCoordinator
                    .IsCurrent(
                        revision,
                        node.Id));
    }

    private void SetEditorRootTitle(string title)
    {
        _editorHeader.RefreshRootTitle(title);
    }

    private async void ReloadAndSelect(ProjectTreeNode node)
    {
        try
        {
            if (!await LoadProjectTreeAsync())
            {
                return;
            }
            NavigateToNodeById(
                node.Id,
                "reload-select");
        }
        catch (Exception exception)
        {
            _messages.Error(
                $"Reload and select {node.Name}",
                exception);
        }
    }

    private bool SelectNodeById(string nodeId)
    {
        return SelectNodeById(nodeId, "node-id");
    }

    private bool SelectNodeById(string nodeId, string source)
    {
        return SelectNodeByIdCore(
            nodeId,
            source,
            revealNavigation: false);
    }

    private bool NavigateToNodeById(
        string nodeId,
        string source)
    {
        return SelectNodeByIdCore(
            nodeId,
            source,
            revealNavigation: true);
    }

    private bool SelectNodeByIdCore(
        string nodeId,
        string source,
        bool revealNavigation)
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
        if (revealNavigation)
        {
            _navigationPanel.EnsureVisible();
        }
        _treeExpansion.ExpandAncestors(selectableNode);
        ShowNode(selectableNode, rebuildTree: true, source);
        if (revealNavigation)
        {
            BringSelectedNavigationNodeIntoView();
        }
        ApplyUiTextScale();
        return true;
    }

    private void BringSelectedNavigationNodeIntoView()
    {
        if (Session.SelectedNode is { } selected)
        {
            _navigationRenderer.BringNodeIntoView(
                NavigationCardsPanel,
                selected.Id);
        }
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

        _navigationPanel.EnsureVisible();
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
        if (!NavigateToNodeById(
                variantReference,
                "component-reference"))
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
        _previewControlsDock.ApplyTextScale(
            _shellState.UiTextScale);
    }

    private async void SetWorkspace(EditorWorkspace workspace)
    {
        using var transaction = BeginContextTransaction("workspace", workspace.ToString());
        CaptureActiveEditorViewState();
        EditorSessionTransition? transition;
        try
        {
            transition =
                await _treePreviewTransitions.SwitchWorkspaceAsync(
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
        _pendingEditorCardExpansion = (
            production.Id,
            cardSessionStateId);
        ApplyPendingEditorCardExpansion(
            production.Id);
    }

    private void ApplyPendingEditorCardExpansion(string nodeId)
    {
        if (_pendingEditorCardExpansion is not { } pending
            || !pending.NodeId.Equals(
                nodeId,
                StringComparison.Ordinal))
        {
            return;
        }

        var card = _editorContent.Cards.FirstOrDefault((candidate) =>
            candidate.SessionStateId.Equals(
                pending.CardId,
                StringComparison.Ordinal));
        if (card is not null)
        {
            card.IsExpanded = true;
            _pendingEditorCardExpansion = null;
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
