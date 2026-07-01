using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using SukiUI;
using SukiUI.Controls;
using SukiUI.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell;

public partial class MainWindow : SukiWindow
{
    private bool _isDark = true;
    private readonly SpikeDatabase _database = new(SpikeDatabase.DefaultDatabasePath());
    private readonly List<Expander> _editorCards = [];
    private readonly HashSet<string> _expandedNodeIds = [];
    private List<ProjectTreeNode> _treeRoots = [];
    private ProjectTreeNode? _selectedNode;
    private bool _isRebuildingProjectTree;

    public MainWindow()
    {
        InitializeComponent();
        RestoreShellState();
        Closing += (_, _) => SaveShellState();
        ApplyTheme();
        LoadProjectTree();
    }

    private void OnToggleThemeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isDark = !_isDark;
        ApplyTheme();
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
            SetBrush("PreviewDeviceBorderBrush", "#111318");
            SetBrush("PreviewDeviceBackground", "#ece9e2");
            SetBrush("PreviewHeaderBackground", "#d7b25c");
            SetBrush("PreviewHeaderTextBrush", "#302a20");
            SetBrush("PreviewHeaderMutedBrush", "#5e523d");
            SetBrush("PreviewScreenBackground", "#f1f3ef");
            SetBrush("PreviewOutgoingBubble", "#9fcfb3");
            SetBrush("PreviewIncomingBubble", "#f6f4ef");
            SetBrush("PreviewBubbleTextBrush", "#fff8ee");
            SetBrush("PreviewIncomingTextBrush", "#302f2d");
            ThemeLabel.Text = "Dark mode";
            ThemeToggleButton.Content = "Switch to light";
            return;
        }

        SetBrush("PreviewDeviceBorderBrush", "#111318");
        SetBrush("PreviewDeviceBackground", "#ffffff");
        SetBrush("PreviewHeaderBackground", "#d7b25c");
        SetBrush("PreviewHeaderTextBrush", "#302a20");
        SetBrush("PreviewHeaderMutedBrush", "#5e523d");
        SetBrush("PreviewScreenBackground", "#f7f9fc");
        SetBrush("PreviewOutgoingBubble", "#9fcfb3");
        SetBrush("PreviewIncomingBubble", "#ffffff");
        SetBrush("PreviewBubbleTextBrush", "#fff8ee");
        SetBrush("PreviewIncomingTextBrush", "#302f2d");
        ThemeLabel.Text = "Light mode";
        ThemeToggleButton.Content = "Switch to dark";
    }

    private void SetBrush(string key, string hex)
    {
        Resources[key] = new SolidColorBrush(Color.Parse(hex));
    }

    private static string ShellStatePath()
    {
        var root = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(root, "..", "..", "..", "data", "window-state.json"));
    }

    private void RestoreShellState()
    {
        var path = ShellStatePath();
        if (!File.Exists(path)) return;

        try
        {
            var state = JsonSerializer.Deserialize<ShellWindowState>(File.ReadAllText(path));
            if (state is null) return;

            if (state.Width >= MinWidth)
            {
                Width = state.Width;
            }

            if (state.Height >= MinHeight)
            {
                Height = state.Height;
            }

            if (state.LeftPanelWidth > 0 && state.EditorPanelWidth > 0)
            {
                ShellColumns.ColumnDefinitions[0].Width = new GridLength(state.LeftPanelWidth);
                ShellColumns.ColumnDefinitions[2].Width = new GridLength(state.EditorPanelWidth);
                ShellColumns.ColumnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);
            }
        }
        catch
        {
            // Local UI state should never block opening the editor shell.
        }
    }

    private void SaveShellState()
    {
        try
        {
            var path = ShellStatePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var state = new ShellWindowState
            {
                Width = Width,
                Height = Height,
                LeftPanelWidth = ShellColumns.ColumnDefinitions[0].ActualWidth,
                EditorPanelWidth = ShellColumns.ColumnDefinitions[2].ActualWidth,
                RightPanelWidth = ShellColumns.ColumnDefinitions[4].ActualWidth,
            };

            File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
        }
        catch
        {
            // Same rule as restore: UI state is nice-to-have, not project data.
        }
    }

    private void LoadProjectTree()
    {
        _treeRoots = _database.LoadProjectTree();
        if (_expandedNodeIds.Count == 0 && _treeRoots.Count > 0)
        {
            _expandedNodeIds.Add(_treeRoots[0].Id);
        }

        RebuildProjectTreeView();

        if (_treeRoots.Count > 0)
        {
            var selected = _selectedNode is not null
                ? FindNodeById(_treeRoots, _selectedNode.Id)
                : null;
            selected ??= _treeRoots[0];

            ExpandAncestors(selected);
            ShowNode(selected, rebuildTree: false);
        }
    }

    private TreeViewItem CreateTreeNodeItem(ProjectTreeNode node)
    {
        var item = new TreeViewItem
        {
            Header = CreateTreeHeader(node),
            Tag = node.Id,
            IsExpanded = _expandedNodeIds.Contains(node.Id),
            IsSelected = _selectedNode?.Id == node.Id,
        };

        if (node.Children.Count > 0)
        {
            item.ItemsSource = node.Children.Select(CreateTreeNodeItem).ToList();
        }

        item.PropertyChanged += (_, change) =>
        {
            if (_isRebuildingProjectTree || change.Property != TreeViewItem.IsExpandedProperty) return;

            if (item.IsExpanded)
            {
                CollapseSiblingNodes(node);
                _expandedNodeIds.Add(node.Id);
                RebuildProjectTreeView();
                return;
            }

            CollapseNodeAndDescendants(node);
        };

        return item;
    }

    private Control CreateTreeHeader(ProjectTreeNode node)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("24,*,Auto"),
            ColumnSpacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var glyph = EditorIcons.Create(NodeIcon(node.Kind), 19);
        Grid.SetColumn(glyph, 0);

        var labelStack = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
        };
        labelStack.Children.Add(new TextBlock
        {
            Text = node.Name,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        labelStack.Children.Add(new TextBlock
        {
            Text = node.Kind.ToString(),
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetColumn(labelStack, 1);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (node.CanAddChild)
        {
            actions.Children.Add(CreateTreeActionButton(EditorIcons.Create(EditorIcons.Add, 15), "Add child", (_, e) =>
            {
                e.Handled = true;
                AddChild(node);
            }));
        }

        if (node.CanDuplicate)
        {
            actions.Children.Add(CreateTreeActionButton(EditorIcons.Create(EditorIcons.Duplicate, 15), "Duplicate", (_, e) =>
            {
                e.Handled = true;
                DuplicateNode(node);
            }));
        }

        if (node.CanDelete)
        {
            actions.Children.Add(CreateTreeActionButton(EditorIcons.Create(EditorIcons.Delete, 15), "Delete", async (_, e) =>
            {
                e.Handled = true;
                await DeleteNode(node);
            }));
        }

        Grid.SetColumn(actions, 2);

        grid.Children.Add(glyph);
        grid.Children.Add(labelStack);
        grid.Children.Add(actions);
        grid.PointerPressed += (_, e) =>
        {
            if (e.Source is Control source
                && (source is Button || source.FindAncestorOfType<Button>() is not null))
            {
                return;
            }

            e.Handled = true;
            SelectTreeNode(node);
        };

        return grid;
    }

    private Button CreateTreeActionButton(
        Control content,
        string tooltip,
        EventHandler<RoutedEventArgs> onClick)
    {
        var button = new Button
        {
            Content = content,
            Width = 28,
            Height = 28,
            Padding = new Avalonia.Thickness(0),
        };
        ToolTip.SetTip(button, tooltip);
        button.Click += onClick;
        return button;
    }

    private static string NodeIcon(ProjectTreeNodeKind kind)
    {
        return EditorIcons.ForTreeNode(kind);
    }

    private void SelectTreeNode(ProjectTreeNode node)
    {
        ShowNode(node);
    }

    private void CollapseSiblingNodes(ProjectTreeNode node)
    {
        if (node.Parent is null) return;

        foreach (var sibling in node.Parent.Children.Where((child) => child.Id != node.Id))
        {
            CollapseNodeAndDescendants(sibling);
        }
    }

    private void CollapseNodeAndDescendants(ProjectTreeNode node)
    {
        _expandedNodeIds.Remove(node.Id);
        foreach (var child in node.Children)
        {
            CollapseNodeAndDescendants(child);
        }
    }

    private void RebuildProjectTreeView()
    {
        _isRebuildingProjectTree = true;
        try
        {
            ProjectTreeView.ItemsSource = _treeRoots.Select(CreateTreeNodeItem).ToList();
        }
        finally
        {
            _isRebuildingProjectTree = false;
        }
    }

    private void ShowNode(ProjectTreeNode node, bool rebuildTree = false)
    {
        _selectedNode = node;
        EditorTitle.Text = node.Name;
        BuildEditorCards(node);
        if (rebuildTree)
        {
            RebuildProjectTreeView();
        }
    }

    private void BuildEditorCards(ProjectTreeNode node)
    {
        _editorCards.Clear();
        EditorCardsPanel.Children.Clear();

        var layout = _database.LoadEditorLayout(node.RecordClassId);
        foreach (var layoutCard in layout.Cards
                     .Where((card) => card.Visible)
                     .OrderBy((card) => card.Order)
                     .ThenBy((card) => card.Label))
        {
            AddEditorCard(CreateLayoutCard(node, layoutCard));
        }
    }

    private Expander CreateLayoutCard(ProjectTreeNode node, EditorLayoutCard layoutCard)
    {
        var body = new StackPanel
        {
            Spacing = 12,
        };
        var controls = new List<DictionaryFieldControl>();

        foreach (var group in layoutCard.VisibleGroups)
        {
            var groupPanel = new StackPanel
            {
                Spacing = 12,
            };

            if (!string.IsNullOrWhiteSpace(group.Label))
            {
                groupPanel.Children.Add(new TextBlock
                {
                    Text = group.Label,
                    FontSize = 12,
                    Opacity = 0.72,
                });
            }

            foreach (var layoutField in group.VisibleFields)
            {
                var field = CreateFieldValue(node, layoutField.Id);
                var control = new DictionaryFieldControl(field);
                controls.Add(control);
                control.ValueChanged += (_, value) =>
                {
                    ApplyFieldValue(node, field.Definition.Id, value);
                };
                groupPanel.Children.Add(control);
            }

            if (groupPanel.Children.Count > 0)
            {
                body.Children.Add(groupPanel);
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

        var card = new Expander
        {
            Header = layoutCard.Label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ExpandDirection = ExpandDirection.Down,
            IsExpanded = layoutCard.DefaultOpen,
            Content = new Border
            {
                Padding = new Avalonia.Thickness(10),
                Child = body,
            },
        };
        foreach (var control in controls)
        {
            control.ValueChanged += (_, _) =>
            {
                // Visual override badges will be handled by semantic controls later.
            };
        }

        return card;
    }

    private FieldValue CreateFieldValue(ProjectTreeNode node, string fieldId)
    {
        var persisted = node.Kind is ProjectTreeNodeKind.Project
            or ProjectTreeNodeKind.App
            or ProjectTreeNodeKind.Module
            or ProjectTreeNodeKind.Episode
            or ProjectTreeNodeKind.Shot;

        return fieldId switch
        {
            "core.name" => new FieldValue(
                new FieldDefinition(
                    "core.name",
                    "Name",
                    ValueKind.StringSingleLine,
                    IsEditable: persisted,
                    DefaultValue: node.Name),
                node.Name),
            "core.kind" => new FieldValue(
                new FieldDefinition(
                    "core.kind",
                    "Class",
                    ValueKind.StringReadOnly,
                    IsEditable: false,
                    DefaultValue: node.RecordClassId),
                node.RecordClassId),
            "core.notes" => new FieldValue(
                new FieldDefinition(
                    "core.notes",
                    "Notes",
                    ValueKind.StringMultiline,
                    IsEditable: persisted,
                    DefaultValue: node.Notes),
                node.Notes),
            _ => throw new InvalidOperationException($"Unknown field '{fieldId}' for record class '{node.RecordClassId}'."),
        };
    }

    private void ApplyFieldValue(ProjectTreeNode node, string fieldId, string value)
    {
        var persisted = node.Kind is ProjectTreeNodeKind.Project
            or ProjectTreeNodeKind.App
            or ProjectTreeNodeKind.Module
            or ProjectTreeNodeKind.Episode
            or ProjectTreeNodeKind.Shot;

        if (fieldId == "core.name")
        {
            node.Name = value;
            EditorTitle.Text = value;
        }
        else if (fieldId == "core.notes")
        {
            node.Notes = value;
        }

        if (persisted && fieldId is "core.name" or "core.notes")
        {
            _database.UpdateNode(node);
        }
    }

    private void AddEditorCard(Expander card)
    {
        card.PropertyChanged += (_, change) =>
        {
            if (change.Property != Expander.IsExpandedProperty || !card.IsExpanded) return;

            foreach (var other in _editorCards.Where((item) => item != card))
            {
                other.IsExpanded = false;
            }
        };
        _editorCards.Add(card);
        EditorCardsPanel.Children.Add(new Border
        {
            Margin = new Avalonia.Thickness(0, 0, 0, 12),
            Child = new GlassCard
            {
                Content = card,
            },
        });
    }

    private void AddChild(ProjectTreeNode parent)
    {
        var child = _database.AddChild(parent);
        ReloadAndSelect(child);
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

        var confirmed = await ConfirmDelete(node);
        if (!confirmed) return;

        var nextSelectionId = node.Parent.Id;
        _database.Delete(node);
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
        var dialog = new SukiWindow
        {
            Title = $"Delete {node.Kind}",
            Width = 420,
            Height = 220,
            MinWidth = 420,
            MinHeight = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
        };

        var root = new Border
        {
            Padding = new Avalonia.Thickness(22),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                RowSpacing = 18,
            },
        };

        var content = new StackPanel
        {
            Spacing = 8,
        };
        content.Children.Add(new TextBlock
        {
            Text = $"Delete {node.Name}?",
            FontSize = 17,
            FontWeight = Avalonia.Media.FontWeight.Bold,
        });
        content.Children.Add(new TextBlock
        {
            Text = node.Kind == ProjectTreeNodeKind.Episode
                ? "This will also remove the shots inside this episode in the current in-memory spike."
                : node.Kind == ProjectTreeNodeKind.App
                    ? "This will also remove the modules inside this app in the current spike database."
                    : "This removes this item from the current spike database.",
            TextWrapping = TextWrapping.Wrap,
        });

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 92,
        };
        cancelButton.Click += (_, _) => dialog.Close(false);

        var deleteButton = new Button
        {
            Content = "Delete",
            MinWidth = 92,
            Background = new SolidColorBrush(Color.Parse(_isDark ? "#5a3435" : "#fff1f3")),
            BorderBrush = new SolidColorBrush(Color.Parse(_isDark ? "#78565a" : "#ffd0d5")),
            Foreground = new SolidColorBrush(Color.Parse(_isDark ? "#e8a1a8" : "#b4232e")),
        };
        deleteButton.Click += (_, _) => dialog.Close(true);

        actions.Children.Add(cancelButton);
        actions.Children.Add(deleteButton);

        Grid.SetRow(content, 0);
        Grid.SetRow(actions, 1);
        ((Grid)root.Child).Children.Add(content);
        ((Grid)root.Child).Children.Add(actions);
        dialog.Content = root;

        return await dialog.ShowDialog<bool>(this);
    }

    private void ReloadAndSelect(ProjectTreeNode node)
    {
        _selectedNode = node;
        LoadProjectTree();
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

internal sealed class ShellWindowState
{
    public double Width { get; init; }
    public double Height { get; init; }
    public double LeftPanelWidth { get; init; }
    public double EditorPanelWidth { get; init; }
    public double RightPanelWidth { get; init; }
}

internal enum ProjectTreeNodeKind
{
    Project,
    AppsRoot,
    EpisodesRoot,
    App,
    Module,
    Episode,
    Shot,
}

internal sealed class ProjectTreeNode
{
    public ProjectTreeNode(
        ProjectTreeNodeKind kind,
        string id,
        string name,
        string notes,
        string recordClassId,
        ProjectTreeNode? parent = null)
    {
        Kind = kind;
        Id = id;
        Name = name;
        Notes = notes;
        RecordClassId = recordClassId;
        Parent = parent;
    }

    public ProjectTreeNodeKind Kind { get; }
    public string Id { get; }
    public string Name { get; set; }
    public string Notes { get; set; }
    public string RecordClassId { get; }
    public ProjectTreeNode? Parent { get; private set; }
    public List<ProjectTreeNode> Children { get; } = [];

    public int Level => Parent is null ? 0 : Parent.Level + 1;
    public bool CanAddChild => Kind is ProjectTreeNodeKind.AppsRoot
        or ProjectTreeNodeKind.App
        or ProjectTreeNodeKind.EpisodesRoot
        or ProjectTreeNodeKind.Episode;
    public bool CanDuplicate => Kind is ProjectTreeNodeKind.App
        or ProjectTreeNodeKind.Module
        or ProjectTreeNodeKind.Episode
        or ProjectTreeNodeKind.Shot;
    public bool CanDelete => Kind is ProjectTreeNodeKind.App
        or ProjectTreeNodeKind.Module
        or ProjectTreeNodeKind.Episode
        or ProjectTreeNodeKind.Shot;

    public string Display => Name;

    public void AddChild(ProjectTreeNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public static string DefaultRecordClassId(ProjectTreeNodeKind kind)
    {
        return kind switch
        {
            ProjectTreeNodeKind.Project => "project",
            ProjectTreeNodeKind.AppsRoot => "navigation.apps",
            ProjectTreeNodeKind.EpisodesRoot => "navigation.episodes",
            ProjectTreeNodeKind.App => "app.generic",
            ProjectTreeNodeKind.Module => "module.generic",
            ProjectTreeNodeKind.Episode => "episode",
            ProjectTreeNodeKind.Shot => "shot",
            _ => throw new InvalidOperationException($"No record class for {kind}."),
        };
    }
}
