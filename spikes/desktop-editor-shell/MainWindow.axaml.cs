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
    private readonly EditorFieldCommitCoordinator _fieldCommitCoordinator = new();
    private readonly EditorActiveFieldControls _activeFieldControls = new();
    private readonly HashSet<string> _expandedNodeIds = [];
    private List<ProjectTreeNode> _treeRoots = [];
    private ProjectTreeNode? _selectedNode;

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
            () => _selectedNode);
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
            (node) => ShowNode(node),
            ToggleTreeGroup,
            AddChild,
            DuplicateNode,
            DeleteNode);
        _fieldPostCommitEffects = new EditorFieldPostCommitEffects(
            _database,
            () => _previewController.SelectedDeviceId,
            (title) => EditorTitle.Text = title,
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

    private void OnRunIrDiagnosticsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var result = EditorIrDiagnosticsService.Run();
        IrDiagnosticsLabel.Text = result.Message;
        IrDiagnosticsLabel.Foreground = result.Success
            ? new SolidColorBrush(Color.Parse("#4ADE80"))
            : new SolidColorBrush(Color.Parse("#F87171"));
        if (result.Success)
        {
            _messages.Info("IR diagnostics", result.Message);
        }
        else
        {
            _messages.Error("IR diagnostics", result.Message);
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

        RebuildNavigationCards();

        if (_treeRoots.Count > 0)
        {
            var selected = _selectedNode is not null
                ? FindNodeById(_treeRoots, _selectedNode.Id)
                : null;
            selected = selected?.CanOpenEditor == true ? selected : null;
            selected ??= _treeRoots.FirstOrDefault((node) => node.CanOpenEditor) ?? _treeRoots[0];

            ExpandAncestors(selected);
            ShowNode(selected, rebuildTree: false);
        }
    }

    private void SelectTreeNode(ProjectTreeNode node)
    {
        if (!node.CanOpenEditor)
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
        var previousNode = _selectedNode;
        var keepEditorViewState = _editorViewState.ShouldPreserve(previousNode, node);
        if (keepEditorViewState)
        {
            _editorViewState.Capture(previousNode, _editorCardHost.Cards);
        }

        _selectedNode = node;
        EditorTitle.Text = node.Name;
        BuildEditorCards(node);
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

    private void BuildEditorCards(ProjectTreeNode node)
    {
        _editorCardHost.Clear();
        _activeFieldControls.Clear();
        _actorAvatarPreviews.Reset();

        var layout = _database.LoadEditorLayout(node.RecordClassId);
        foreach (var layoutCard in layout.Cards
                     .Where((card) => card.Visible)
                     .OrderBy((card) => card.Order)
                     .ThenBy((card) => card.Label))
        {
            _editorCardHost.Add(CreateLayoutCard(node, layoutCard));
        }

        foreach (var collectionCard in _collectionCards.Create(node))
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
                    (fieldId) => _activeFieldControls.ValueOrStored(fieldId, (id) => _fieldValues.CurrentStoredValue(node, id)));
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

        ExpandAncestors(node);
        ShowNode(node, rebuildTree: true);
        return true;
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
