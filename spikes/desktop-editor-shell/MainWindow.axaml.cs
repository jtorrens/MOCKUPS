using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell;

public partial class MainWindow : Window
{
    private bool _isDark = true;
    private readonly SpikeDatabase _database = new(SpikeDatabase.DefaultDatabasePath());
    private readonly List<EditorAccordionCard> _editorCards = [];
    private readonly HashSet<string> _expandedNodeIds = [];
    private List<ProjectTreeNode> _treeRoots = [];
    private ProjectTreeNode? _selectedNode;

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
        if (_isDark)
        {
            SetBrush("AppShellBackground", "#25272b");
            SetBrush("ToolbarBackground", "#2b2d30");
            SetBrush("PanelBackground", "#2f3136");
            SetBrush("PanelBorderBrush", "#555861");
            SetBrush("SplitterBackground", "#383a3f");
            SetBrush("CardBackground", "#373a40");
            SetBrush("CardBorderBrush", "#646a76");
            SetShadow("CardShadow", "0 5 18 0 #66000000");
            SetBrush("InputBackground", "#383a3f");
            SetBrush("InputBorderBrush", "#77b86a");
            SetBrush("ButtonBackground", "#383a3f");
            SetBrush("ButtonBorderBrush", "#565a62");
            SetBrush("TextBrush", "#e0ded8");
            SetBrush("MutedTextBrush", "#aaa69d");
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
            SetBrush("TreeSelectionBackground", "#3d4548");
            SetBrush("TreeSelectedBorderBrush", "#5aa7ff");
            SetBrush("TreeActionHoverBackground", "#404248");
            SetBrush("TreeRowBackground", "#373a40");
            SetBrush("TreeRowLevel1Background", "#34373d");
            SetBrush("TreeRowLevel2Background", "#30343a");
            SetBrush("TreeRowBorderBrush", "#646a76");
            SetShadow("TreeRowShadow", "0 5 18 0 #66000000");
            SetBrush("DictionaryLabelBrush", "#aaa69d");
            SetBrush("DictionaryControlBorderBrush", "#565a62");
            SetBrush("DictionaryControlFocusBorderBrush", "#8b8f99");
            SetBrush("DictionaryControlSelectionBrush", "#5f6c72");
            SetBrush("DictionaryControlSelectionTextBrush", "#f4f1eb");
            SetBrush("ReadonlyInputBackground", "#323439");
            SetBrush("OverrideBorderBrush", "#d4b45f");
            SetBrush("OverrideBackground", "#40392b");
            SetBrush("OverrideCardBackground", "#373428");
            SetTextControlBrushes();
            ApplyShellSurfaceStyles();

            ThemeLabel.Text = "Dark mode";
            ThemeToggleButton.Content = "Switch to light";
            return;
        }

        SetBrush("AppShellBackground", "#ffffff");
        SetBrush("ToolbarBackground", "#ffffff");
        SetBrush("PanelBackground", "#fbfcfd");
        SetBrush("PanelBorderBrush", "#dde1e8");
        SetBrush("SplitterBackground", "#f0f2f5");
        SetBrush("CardBackground", "#f3f5f8");
        SetBrush("CardBorderBrush", "#c3ccd9");
        SetShadow("CardShadow", "0 3 12 0 #22000000");
        SetBrush("InputBackground", "#f7f8fb");
        SetBrush("InputBorderBrush", "#77b86a");
        SetBrush("ButtonBackground", "#ffffff");
        SetBrush("ButtonBorderBrush", "#d9dee7");
        SetBrush("TextBrush", "#15171c");
        SetBrush("MutedTextBrush", "#667085");
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
        SetBrush("TreeSelectionBackground", "#eef2ff");
        SetBrush("TreeSelectedBorderBrush", "#2f6bff");
        SetBrush("TreeActionHoverBackground", "#f2f4f7");
        SetBrush("TreeRowBackground", "#f3f5f8");
        SetBrush("TreeRowLevel1Background", "#eef2f6");
        SetBrush("TreeRowLevel2Background", "#e9eef4");
        SetBrush("TreeRowBorderBrush", "#c3ccd9");
        SetShadow("TreeRowShadow", "0 4 14 0 #1f000000");
        SetBrush("DictionaryLabelBrush", "#475467");
        SetBrush("DictionaryControlBorderBrush", "#cfd6e2");
        SetBrush("DictionaryControlFocusBorderBrush", "#aab4c3");
        SetBrush("DictionaryControlSelectionBrush", "#dce7f2");
        SetBrush("DictionaryControlSelectionTextBrush", "#15171c");
        SetBrush("ReadonlyInputBackground", "#fbfcfd");
        SetBrush("OverrideBorderBrush", "#e4ad68");
        SetBrush("OverrideBackground", "#fff7ed");
        SetBrush("OverrideCardBackground", "#fffbf4");
        SetTextControlBrushes();
        ApplyShellSurfaceStyles();

        ThemeLabel.Text = "Light mode";
        ThemeToggleButton.Content = "Switch to dark";
    }

    private void SetBrush(string key, string hex)
    {
        Resources[key] = new SolidColorBrush(Color.Parse(hex));
    }

    private void SetShadow(string key, string value)
    {
        Resources[key] = BoxShadows.Parse(value);
    }

    private void ApplyShellSurfaceStyles()
    {
        foreach (var card in EditorCardsPanel.Children.OfType<EditorAccordionCard>())
        {
            ApplyCardSurface(card);
        }

        ApplyTreeSurfaceStyles(ProjectTreeHost);
    }

    private void ApplyCardSurface(EditorAccordionCard card)
    {
        card.Background = (IBrush)Resources[card.IsChanged ? "OverrideCardBackground" : "CardBackground"]!;
        card.BorderBrush = (IBrush)Resources[card.IsChanged ? "OverrideBorderBrush" : "CardBorderBrush"]!;
        card.BorderThickness = new Avalonia.Thickness(2);
        card.CornerRadius = new CornerRadius(14);
        card.BoxShadow = (BoxShadows)Resources["CardShadow"]!;
    }

    private void ApplyTreeSurfaceStyles(Panel panel)
    {
        foreach (var child in panel.Children)
        {
            if (child is Border border && border.Classes.Contains("tree-row"))
            {
                ApplyTreeRowSurface(border);
            }

            if (child is Panel childPanel)
            {
                ApplyTreeSurfaceStyles(childPanel);
            }
        }
    }

    private void ApplyTreeRowSurface(Border row)
    {
        row.Background = (IBrush)Resources[
            row.Classes.Contains("tree-row-level-2")
                ? "TreeRowLevel2Background"
                : row.Classes.Contains("tree-row-level-1")
                    ? "TreeRowLevel1Background"
                    : "TreeRowBackground"]!;
        row.BorderBrush = row.Classes.Contains("selected")
            ? (IBrush)Resources["TreeSelectedBorderBrush"]!
            : (IBrush)Resources["TreeRowBorderBrush"]!;
        row.BorderThickness = new Avalonia.Thickness(2);
        row.CornerRadius = new CornerRadius(13);
        row.BoxShadow = (BoxShadows)Resources["TreeRowShadow"]!;
    }

    private void SetTextControlBrushes()
    {
        Resources["TextControlBackground"] = Resources["InputBackground"]!;
        Resources["TextControlBackgroundPointerOver"] = Resources["InputBackground"]!;
        Resources["TextControlBackgroundFocused"] = Resources["InputBackground"]!;
        Resources["TextControlBackgroundDisabled"] = Resources["ReadonlyInputBackground"]!;

        Resources["TextControlBorderBrush"] = Resources["DictionaryControlBorderBrush"]!;
        Resources["TextControlBorderBrushPointerOver"] = Resources["DictionaryControlBorderBrush"]!;
        Resources["TextControlBorderBrushFocused"] = Resources["DictionaryControlFocusBorderBrush"]!;
        Resources["TextControlBorderBrushDisabled"] = Resources["DictionaryControlBorderBrush"]!;

        Resources["TextControlForeground"] = Resources["TextBrush"]!;
        Resources["TextControlForegroundPointerOver"] = Resources["TextBrush"]!;
        Resources["TextControlForegroundFocused"] = Resources["TextBrush"]!;
        Resources["TextControlForegroundDisabled"] = Resources["MutedTextBrush"]!;

        Resources["TextControlSelectionHighlightColor"] = Resources["DictionaryControlSelectionBrush"]!;
        Resources["TextControlSelectionHighlightColorFocused"] = Resources["DictionaryControlSelectionBrush"]!;
        Resources["TextControlSelectionForeground"] = Resources["DictionaryControlSelectionTextBrush"]!;
        Resources["TextControlSelectionForegroundFocused"] = Resources["DictionaryControlSelectionTextBrush"]!;
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

            if (state.LeftPanelWidth > 0 && state.EditorPanelWidth > 0 && state.RightPanelWidth > 0)
            {
                ShellColumns.ColumnDefinitions[0].Width = new GridLength(state.LeftPanelWidth);
                ShellColumns.ColumnDefinitions[2].Width = new GridLength(state.EditorPanelWidth);
                ShellColumns.ColumnDefinitions[4].Width = new GridLength(state.RightPanelWidth);
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
        ProjectTreeHost.Children.Clear();
        _treeRoots = _database.LoadProjectTree();
        if (_expandedNodeIds.Count == 0 && _treeRoots.Count > 0)
        {
            _expandedNodeIds.Add(_treeRoots[0].Id);
        }

        foreach (var project in _treeRoots)
        {
            ProjectTreeHost.Children.Add(CreateTreeNodeView(project));
        }

        if (_treeRoots.Count > 0)
        {
            var selected = _selectedNode is not null
                ? FindNodeById(_treeRoots, _selectedNode.Id)
                : null;
            selected ??= _treeRoots[0];

            ExpandAncestors(selected);
            ShowNode(selected, rebuildTree: false);
            RefreshTreeSelection(ProjectTreeHost);
        }
    }

    private Control CreateTreeNodeView(ProjectTreeNode node)
    {
        var stack = new StackPanel
        {
            Spacing = 0,
        };

        stack.Children.Add(CreateTreeHeader(node));

        if (node.Children.Count > 0 && _expandedNodeIds.Contains(node.Id))
        {
            var children = new StackPanel
            {
                Spacing = 0,
                Margin = new Avalonia.Thickness(18, 0, 0, 0),
            };

            foreach (var child in node.Children)
            {
                children.Children.Add(CreateTreeNodeView(child));
            }

            stack.Children.Add(children);
        }

        return stack;
    }

    private Control CreateTreeHeader(ProjectTreeNode node)
    {
        var row = new Border
        {
            MinWidth = 240,
            Tag = node.Id,
        };
        row.Classes.Add("tree-row");
        row.Classes.Add($"tree-row-level-{node.Level}");
        row.Classes.Set("selected", _selectedNode?.Id == node.Id);
        ApplyTreeRowSurface(row);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("24,*,Auto,Auto"),
            ColumnSpacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var glyph = EditorIcons.Create(NodeIcon(node.Kind), 19);
        glyph.Classes.Add("tree-glyph");
        Grid.SetColumn(glyph, 0);

        var labelStack = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
        };
        labelStack.Children.Add(new TextBlock
        {
            Text = node.Name,
            Classes = { "body" },
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        labelStack.Children.Add(new TextBlock
        {
            Text = node.Kind.ToString(),
            Classes = { "muted" },
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

        var chevron = node.Children.Count == 0
            ? new TextBlock { Width = 20, Height = 20 }
            : EditorIcons.Create(
                _expandedNodeIds.Contains(node.Id) ? EditorIcons.Collapse : EditorIcons.Expand,
                18);
        chevron.Classes.Add("tree-chevron");
        Grid.SetColumn(chevron, 3);

        grid.Children.Add(glyph);
        grid.Children.Add(labelStack);
        grid.Children.Add(actions);
        grid.Children.Add(chevron);
        row.Child = grid;
        row.PointerPressed += (_, e) =>
        {
            if (e.Source is Control source
                && (source is Button || source.FindAncestorOfType<Button>() is not null))
            {
                return;
            }

            e.Handled = true;
            SelectTreeNode(node);
            if (node.Children.Count > 0)
            {
                ToggleTreeNode(node);
            }
        };

        return row;
    }

    private Button CreateTreeActionButton(
        Control content,
        string tooltip,
        EventHandler<RoutedEventArgs> onClick)
    {
        var button = new Button
        {
            Content = content,
        };
        ToolTip.SetTip(button, tooltip);
        button.Classes.Add("tree-action");
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
        RefreshTreeSelection(ProjectTreeHost);
    }

    private void ToggleTreeNode(ProjectTreeNode node)
    {
        if (_expandedNodeIds.Contains(node.Id))
        {
            _expandedNodeIds.Remove(node.Id);
        }
        else
        {
            CollapseSiblingNodes(node);
            _expandedNodeIds.Add(node.Id);
        }

        RebuildProjectTreeView();
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
        ProjectTreeHost.Children.Clear();
        foreach (var project in _treeRoots)
        {
            ProjectTreeHost.Children.Add(CreateTreeNodeView(project));
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

    private EditorAccordionCard CreateLayoutCard(ProjectTreeNode node, EditorLayoutCard layoutCard)
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
                    Classes = { "editor-group-label" },
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
                Classes = { "muted" },
                TextWrapping = TextWrapping.Wrap,
            });
        }

        var card = new EditorAccordionCard(layoutCard.Label, layoutCard.Icon, body, isOpen: layoutCard.DefaultOpen);
        foreach (var control in controls)
        {
            control.ValueChanged += (_, _) =>
            {
                card.IsChanged = controls.Any((item) => !item.IsDefault);
                ApplyCardSurface(card);
            };
        }

        card.IsChanged = controls.Any((item) => !item.IsDefault);
        ApplyCardSurface(card);
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

    private void AddEditorCard(EditorAccordionCard card)
    {
        card.Opened += (_, _) =>
        {
            foreach (var other in _editorCards.Where((item) => item != card))
            {
                other.SetOpen(false, notify: false);
            }
        };
        _editorCards.Add(card);
        EditorCardsPanel.Children.Add(card);
        ApplyCardSurface(card);
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
        var dialog = new Window
        {
            Title = $"Delete {node.Kind}",
            Width = 420,
            Height = 220,
            MinWidth = 420,
            MinHeight = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (IBrush)Resources["PanelBackground"]!,
        };

        var root = new Border
        {
            Padding = new Avalonia.Thickness(22),
            BorderBrush = (IBrush)Resources["PanelBorderBrush"]!,
            BorderThickness = new Avalonia.Thickness(1),
            Background = (IBrush)Resources["PanelBackground"]!,
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
            Classes = { "title" },
        });
        content.Children.Add(new TextBlock
        {
            Text = node.Kind == ProjectTreeNodeKind.Episode
                ? "This will also remove the shots inside this episode in the current in-memory spike."
                : node.Kind == ProjectTreeNodeKind.App
                    ? "This will also remove the modules inside this app in the current spike database."
                    : "This removes this item from the current spike database.",
            Classes = { "muted" },
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
        cancelButton.Classes.Add("toolbar");
        cancelButton.Click += (_, _) => dialog.Close(false);

        var deleteButton = new Button
        {
            Content = "Delete",
            MinWidth = 92,
            Background = new SolidColorBrush(Color.Parse(_isDark ? "#5a3435" : "#fff1f3")),
            BorderBrush = new SolidColorBrush(Color.Parse(_isDark ? "#78565a" : "#ffd0d5")),
            Foreground = new SolidColorBrush(Color.Parse(_isDark ? "#e8a1a8" : "#b4232e")),
        };
        deleteButton.Classes.Add("toolbar");
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

    private void RefreshTreeSelection(Panel panel)
    {
        foreach (var child in panel.Children)
        {
            if (child is Border border && border.Tag is string nodeId)
            {
                border.Classes.Set("selected", _selectedNode?.Id == nodeId);
                ApplyTreeRowSurface(border);
            }
            else if (child is Panel childPanel)
            {
                RefreshTreeSelection(childPanel);
            }
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
