using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using SukiUI;
using SukiUI.Controls;
using SukiUI.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell;

public partial class MainWindow : SukiWindow
{
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
    private readonly EditorLayoutCardFactory _layoutCards;
    private readonly EditorEmbeddedUsageNavigator _embeddedUsageNavigator;
    private readonly EditorHeaderController _editorHeader;
    private readonly EditorTreeExpansionState _treeExpansion = new();
    private readonly EditorNodeSelectionState _nodeSelection = new();
    private readonly EditorFieldCommitCoordinator _fieldCommitCoordinator = new();
    private readonly EditorActiveFieldControls _activeFieldControls = new();
    private List<ProjectTreeNode> _treeRoots = [];
    private ProjectTreeNode? _selectedNode;
    private EditorEmbeddedContext? _embeddedEditorContext;

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
            PreviewContextTextBlock,
            PreviewContextLockButton,
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
            _treeExpansion.IsExpanded,
            SelectTreeNode,
            (node) => ShowNode(_nodeSelection.ResolveSelectionNode(node)),
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
        _layoutCards = new EditorLayoutCardFactory(
            _fieldValues,
            _componentClassFieldValues,
            _actorAvatarPreviews,
            _dictionaryFieldServices,
            _fieldCommitCoordinator,
            _activeFieldControls,
            _messages,
            OpenEmbeddedComponentEditor,
            OpenEmbeddedComponentEditor,
            RefreshPreviewDevice);
        _embeddedUsageNavigator = new EditorEmbeddedUsageNavigator(
            _database,
            this,
            () => _isDark,
            SelectNodeById,
            LoadProjectTree,
            () => _selectedNode,
            OpenEmbeddedComponentEditor,
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
            SaveCurrentComponentPreset);
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
            _editorViewState.Capture(previousNode, _editorCardHost.Cards);
        }

        _selectedNode = node;
        _nodeSelection.RememberComponentPresetSelection(node);
        _treeExpansion.ExpandAncestors(node);
        _embeddedEditorContext = null;
        var editorNode = EditorNodeSelectionState.EditorNodeForSelection(node);
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
            _editorCardHost.Add(_layoutCards.Create(dataNode, layoutCard));
        }

        foreach (var collectionCard in _collectionCards.Create(dataNode))
        {
            _editorCardHost.Add(collectionCard);
        }
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

            ShowEmbeddedContext(new EditorEmbeddedContext(node, [slot]));
        }
        catch (Exception exception)
        {
            _messages.Error($"Embedded component {slotFieldId}", exception);
        }

        return Task.CompletedTask;
    }

    private Task OpenEmbeddedComponentEditor(EditorEmbeddedContext parentContext, string slotFieldId)
    {
        try
        {
            if (!EmbeddedComponentSlotCatalog.TryGet(slotFieldId, out var slot))
            {
                return Task.CompletedTask;
            }

            ShowEmbeddedContext(new EditorEmbeddedContext(parentContext.OwnerNode, [.. parentContext.Slots, slot]));
        }
        catch (Exception exception)
        {
            _messages.Error($"Embedded component {slotFieldId}", exception);
        }

        return Task.CompletedTask;
    }

    private void ShowEmbeddedContext(EditorEmbeddedContext context)
    {
        _embeddedEditorContext = context;
        SetEditorEmbeddedTitle(context);
        BuildEmbeddedComponentCards(context);
        RefreshPreviewDevice();
    }

    private void BuildEmbeddedComponentCards(EditorEmbeddedContext context)
    {
        _editorCardHost.Clear();
        _activeFieldControls.Clear();
        _actorAvatarPreviews.Reset();

        var layout = _database.LoadEditorLayout(context.Slot.RecordClassId);
        foreach (var layoutCard in layout.Cards
                     .Where((card) => card.Visible && EditorLayoutCardFactory.EmbeddedCardHasFields(card))
                     .OrderBy((card) => card.Order)
                     .ThenBy((card) => card.Label))
        {
            _editorCardHost.Add(_layoutCards.CreateEmbedded(context, layoutCard));
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
        node = EditorNodeSelectionState.FindNodeById(_treeRoots, deleteNodeId) ?? node;
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
