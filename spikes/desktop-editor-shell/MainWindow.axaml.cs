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
    private readonly EditorShellStateService _shellState;
    private readonly EditorNavigationRenderer _navigationRenderer;
    private readonly EditorFieldPostCommitEffects _fieldPostCommitEffects;
    private readonly EditorPathBrowser _pathBrowser;
    private readonly EditorFieldCommitCoordinator _fieldCommitCoordinator = new();
    private readonly List<InstantEditorCard> _editorCards = [];
    private readonly Dictionary<string, DictionaryFieldControl> _activeEditorControlsByFieldId = [];
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
        _previewController = new EditorPreviewController(
            _database,
            PreviewDeviceComboBox,
            PreviewThemeComboBox,
            PreviewModeComboBox,
            RuntimePreviewHost,
            DesignPreviewHost,
            () => _isDark,
            () => _selectedNode);
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
        _collectionCards = new EditorCollectionCardFactory(
            _database,
            () => _isDark,
            ShowInfoDialog,
            ConfirmIconTokenDelete,
            ShowIconThemeSearchDialog,
            ReloadAndSelect,
            _pathBrowser.BrowsePath,
            ShowIconTokenPicker,
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

    private void OnPreviewDeviceChanged(object? sender, SelectionChangedEventArgs e)
    {
        _previewController.OnDeviceChanged();
    }

    private void OnPreviewThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        _previewController.OnThemeChanged();
    }

    private void OnPreviewModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        _previewController.OnModeChanged();
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
        _selectedNode = node;
        EditorTitle.Text = node.Name;
        BuildEditorCards(node);
        RefreshPreviewDevice();
        if (rebuildTree)
        {
            RebuildNavigationCards();
        }
    }

    private void BuildEditorCards(ProjectTreeNode node)
    {
        _editorCards.Clear();
        _activeEditorControlsByFieldId.Clear();
        _actorAvatarPreviews.Reset();
        EditorCardsPanel.Children.Clear();

        var layout = _database.LoadEditorLayout(node.RecordClassId);
        foreach (var layoutCard in layout.Cards
                     .Where((card) => card.Visible)
                     .OrderBy((card) => card.Order)
                     .ThenBy((card) => card.Label))
        {
            AddEditorCard(CreateLayoutCard(node, layoutCard));
        }

        foreach (var collectionCard in _collectionCards.Create(node))
        {
            AddEditorCard(collectionCard);
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
                var field = CreateFieldValue(node, layoutField.Id);
                var control = new DictionaryFieldControl(
                    field,
                    _pathBrowser.BrowsePath,
                    (currentValue, allowMultiple) => ShowIconTokenPicker(ProjectAncestor(node).Id, currentValue, allowMultiple),
                    (currentValue, allowedOptions) => ShowThemeTokenPicker(ProjectAncestor(node).Id, currentValue, allowedOptions),
                    (token) => SvgIconPreview.CreateProjectIconTokenPreview(_database, ProjectAncestor(node).Id, token, 18));
                controls.Add(control);
                _activeEditorControlsByFieldId[field.Definition.Id] = control;
                control.ValueCommitted += (_, value) =>
                {
                    _fieldCommitCoordinator.Commit(
                        control,
                        value,
                        (draftValue) => StoredFieldValue(node, field.Definition.Id, draftValue),
                        () => CurrentStoredFieldValue(node, field.Definition.Id),
                        (storedValue) => PersistFieldValue(node, field.Definition.Id, storedValue));
                    if (node.Kind == ProjectTreeNodeKind.Actor)
                    {
                        _actorAvatarPreviews.Refresh(node, _activeEditorControlsByFieldId);
                    }
                    RefreshPreviewDevice();
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
                if (node.Kind == ProjectTreeNodeKind.Actor)
                {
                    _actorAvatarPreviews.Refresh(node, _activeEditorControlsByFieldId);
                }
            };
        }

        return card;
    }

    private FieldValue CreateFieldValue(ProjectTreeNode node, string fieldId)
    {
        if (_recordClassFieldValues.CanHandle(node.Kind, fieldId))
        {
            return _recordClassFieldValues.CreateFieldValue(node, fieldId);
        }

        if (_componentClassFieldValues.CanHandle(node.Kind, fieldId))
        {
            return _componentClassFieldValues.CreateFieldValue(node, fieldId);
        }

        if (_coreFieldValues.CanHandle(fieldId))
        {
            return _coreFieldValues.CreateFieldValue(node, fieldId);
        }

        throw new InvalidOperationException($"Unknown field '{fieldId}' for record class '{node.RecordClassId}'.");
    }

    private void PersistFieldValue(ProjectTreeNode node, string fieldId, string value)
    {
        if (_recordClassFieldValues.CanHandle(node.Kind, fieldId))
        {
            _recordClassFieldValues.CommitFieldValue(node, fieldId, value);
            _fieldPostCommitEffects.Apply(node, fieldId, value);
            return;
        }

        if (_componentClassFieldValues.CanHandle(node.Kind, fieldId))
        {
            _componentClassFieldValues.CommitFieldValue(node, fieldId, value);
            return;
        }

        if (_coreFieldValues.CanHandle(fieldId))
        {
            _coreFieldValues.CommitFieldValue(node, fieldId, value);
            _fieldPostCommitEffects.Apply(node, fieldId, value);
        }
    }

    private string StoredFieldValue(ProjectTreeNode node, string fieldId, string value)
    {
        if (node.Kind == ProjectTreeNodeKind.Actor && fieldId == "actor.avatar.filePath")
        {
            return _actorAvatarPreviews.RelativeActorMediaPath(node.Id, value) ?? value;
        }

        return value;
    }

    private string CurrentStoredFieldValue(ProjectTreeNode node, string fieldId)
    {
        return fieldId switch
        {
            "core.name" => node.Name,
            "core.notes" => node.Notes,
            _ => CreateFieldValue(node, fieldId).Value,
        };
    }

    private void AddEditorCard(InstantEditorCard card)
    {
        card.Expanded += (_, _) =>
        {
            foreach (var other in _editorCards.Where((item) => item != card))
            {
                other.IsExpanded = false;
            }
        };
        _editorCards.Add(card);
        EditorCardsPanel.Children.Add(new Border
        {
            Margin = new Avalonia.Thickness(0, 0, 0, 12),
            CornerRadius = new CornerRadius(14),
            BoxShadow = BoxShadows.Parse("0 6 14 0 #22000000"),
            Child = new GlassCard
            {
                Content = card,
            },
        });
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

    private static ProjectTreeNode ProjectAncestor(ProjectTreeNode node)
    {
        var current = node;
        while (current.Kind != ProjectTreeNodeKind.Project)
        {
            current = current.Parent ?? throw new InvalidOperationException($"{node.Kind} has no project ancestor.");
        }

        return current;
    }

    private async Task ShowInfoDialog(string title, string message)
    {
        await new EditorDialogService(this, _isDark).ShowInfo(title, message);
    }

    private async Task<bool> ConfirmIconTokenDelete(string token)
    {
        return await new EditorDialogService(this, _isDark).ConfirmIconTokenDelete(token);
    }

    private Task ShowIconThemeSearchDialog(ProjectTreeNode node)
    {
        return new IconThemeSearchDialog(this, _database, ShowInfoDialog, ReloadAndSelect).Show(node);
    }

    private Task<string?> ShowIconTokenPicker(string projectId, string currentValue, bool allowMultiple)
    {
        return new IconTokenPickerDialog(this, _database).Show(projectId, currentValue, allowMultiple);
    }

    private Task<string?> ShowThemeTokenPicker(string projectId, string currentValue, IReadOnlyList<FieldOption>? allowedOptions)
    {
        return new ThemeTokenPickerDialog(this, _database).Show(projectId, currentValue, allowedOptions);
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
