using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell;

public partial class MainWindow : Window
{
    private bool _isDark = true;
    private readonly SpikeDatabase _database = new(SpikeDatabase.DefaultDatabasePath());
    private readonly List<EditorAccordionCard> _editorCards = [];
    private ProjectTreeNode? _selectedNode;

    public MainWindow()
    {
        InitializeComponent();
        LoadProjectTree();
        ApplyTheme();
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
            SetBrush("PanelBackground", "#2b2d30");
            SetBrush("PanelBorderBrush", "#50535b");
            SetBrush("SplitterBackground", "#383a3f");
            SetBrush("CardBackground", "#34363b");
            SetBrush("CardBorderBrush", "#50535b");
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
            SetBrush("TreeActionHoverBackground", "#404248");
            SetBrush("TreeRowBackground", "#34363b");
            SetBrush("TreeRowLevel1Background", "#383a3f");
            SetBrush("TreeRowLevel2Background", "#3b3d42");
            SetBrush("TreeRowBorderBrush", "#50535b");
            SetBrush("DictionaryLabelBrush", "#aaa69d");
            SetBrush("DictionaryControlBorderBrush", "#565a62");
            SetBrush("ReadonlyInputBackground", "#323439");
            SetBrush("OverrideBorderBrush", "#d4b45f");
            SetBrush("OverrideBackground", "#40392b");
            SetBrush("OverrideCardBackground", "#373428");

            ThemeLabel.Text = "Dark mode";
            ThemeToggleButton.Content = "Switch to light";
            return;
        }

        SetBrush("AppShellBackground", "#ffffff");
        SetBrush("ToolbarBackground", "#ffffff");
        SetBrush("PanelBackground", "#fbfcfd");
        SetBrush("PanelBorderBrush", "#dde1e8");
        SetBrush("SplitterBackground", "#f0f2f5");
        SetBrush("CardBackground", "#ffffff");
        SetBrush("CardBorderBrush", "#dfe3eb");
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
        SetBrush("TreeActionHoverBackground", "#f2f4f7");
        SetBrush("TreeRowBackground", "#ffffff");
        SetBrush("TreeRowLevel1Background", "#f8fafc");
        SetBrush("TreeRowLevel2Background", "#f5f7fb");
        SetBrush("TreeRowBorderBrush", "#e1e5ec");
        SetBrush("DictionaryLabelBrush", "#475467");
        SetBrush("DictionaryControlBorderBrush", "#cfd6e2");
        SetBrush("ReadonlyInputBackground", "#fbfcfd");
        SetBrush("OverrideBorderBrush", "#e4ad68");
        SetBrush("OverrideBackground", "#fff7ed");
        SetBrush("OverrideCardBackground", "#fffbf4");

        ThemeLabel.Text = "Light mode";
        ThemeToggleButton.Content = "Switch to dark";
    }

    private void SetBrush(string key, string hex)
    {
        Resources[key] = new SolidColorBrush(Color.Parse(hex));
    }

    private void LoadProjectTree()
    {
        ProjectTree.Items.Clear();

        foreach (var project in _database.LoadProjectTree())
        {
            ProjectTree.Items.Add(CreateTreeItem(project));
        }

        if (ProjectTree.Items.Count > 0 && ProjectTree.Items[0] is TreeViewItem firstItem)
        {
            firstItem.IsExpanded = true;
            firstItem.IsSelected = true;
            ShowNode((ProjectTreeNode)firstItem.Tag!);
        }
    }

    private TreeViewItem CreateTreeItem(ProjectTreeNode node)
    {
        var item = new TreeViewItem
        {
            Header = CreateTreeHeader(node),
            Tag = node,
            IsExpanded = node.Kind is ProjectTreeNodeKind.Project
                or ProjectTreeNodeKind.Episode
                or ProjectTreeNodeKind.App
                or ProjectTreeNodeKind.AppsRoot
                or ProjectTreeNodeKind.EpisodesRoot,
        };

        foreach (var child in node.Children)
        {
            item.Items.Add(CreateTreeItem(child));
        }

        return item;
    }

    private Control CreateTreeHeader(ProjectTreeNode node)
    {
        var row = new Border
        {
            MinWidth = 240,
        };
        row.Classes.Add("tree-row");
        row.Classes.Add($"tree-row-level-{node.Level}");

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var glyph = new TextBlock
        {
            Text = NodeGlyph(node.Kind),
            Foreground = (IBrush)Resources["MutedTextBrush"]!,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 14,
            FontWeight = FontWeight.Bold,
        };
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
            actions.Children.Add(CreateTreeActionButton("+", "Add child", (_, e) =>
            {
                e.Handled = true;
                AddChild(node);
            }));
        }

        if (node.CanDuplicate)
        {
            actions.Children.Add(CreateTreeActionButton("⧉", "Duplicate", (_, e) =>
            {
                e.Handled = true;
                DuplicateNode(node);
            }));
        }

        if (node.CanDelete)
        {
            actions.Children.Add(CreateTreeActionButton("×", "Delete", async (_, e) =>
            {
                e.Handled = true;
                await DeleteNode(node);
            }));
        }

        Grid.SetColumn(actions, 2);

        grid.Children.Add(glyph);
        grid.Children.Add(labelStack);
        grid.Children.Add(actions);
        row.Child = grid;

        return row;
    }

    private Button CreateTreeActionButton(
        string text,
        string tooltip,
        EventHandler<RoutedEventArgs> onClick)
    {
        var button = new Button
        {
            Content = text,
        };
        ToolTip.SetTip(button, tooltip);
        button.Classes.Add("tree-action");
        button.Click += onClick;
        return button;
    }

    private static string NodeGlyph(ProjectTreeNodeKind kind)
    {
        return kind switch
        {
            ProjectTreeNodeKind.Project => "▣",
            ProjectTreeNodeKind.AppsRoot => "▣",
            ProjectTreeNodeKind.EpisodesRoot => "▤",
            ProjectTreeNodeKind.Episode => "▤",
            ProjectTreeNodeKind.Shot => "◈",
            ProjectTreeNodeKind.App => "▣",
            ProjectTreeNodeKind.Module => "▧",
            _ => "•",
        };
    }

    private void OnProjectTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProjectTree.SelectedItem is TreeViewItem item && item.Tag is ProjectTreeNode node)
        {
            ShowNode(node);
        }
    }

    private void ShowNode(ProjectTreeNode node)
    {
        _selectedNode = node;
        EditorTitle.Text = node.Name;
        BuildEditorCards(node);
    }

    private void BuildEditorCards(ProjectTreeNode node)
    {
        _editorCards.Clear();
        EditorCardsPanel.Children.Clear();

        var generalCard = CreateGeneralCard(node);
        AddEditorCard(generalCard);

        var styleCard = new EditorAccordionCard(
            "Style",
            "◒",
            new TextBlock
            {
                Text = "Style fields will be added only when their FieldDefinitions exist.",
                Classes = { "muted" },
                TextWrapping = TextWrapping.Wrap,
            });
        AddEditorCard(styleCard);

        var behaviorCard = new EditorAccordionCard(
            "Behavior",
            "⚙",
            new TextBlock
            {
                Text = "Behavior fields will follow the same dictionary route.",
                Classes = { "muted" },
                TextWrapping = TextWrapping.Wrap,
            });
        AddEditorCard(behaviorCard);
    }

    private EditorAccordionCard CreateGeneralCard(ProjectTreeNode node)
    {
        var persisted = node.Kind is ProjectTreeNodeKind.Project
            or ProjectTreeNodeKind.App
            or ProjectTreeNodeKind.Module
            or ProjectTreeNodeKind.Episode
            or ProjectTreeNodeKind.Shot;

        var fields = new[]
        {
            new FieldValue(
                new FieldDefinition(
                    "core.name",
                    "Name",
                    ValueKind.StringSingleLine,
                    IsEditable: persisted,
                    DefaultValue: node.Name),
                node.Name),
            new FieldValue(
                new FieldDefinition(
                    "core.kind",
                    "Kind",
                    ValueKind.StringReadOnly,
                    IsEditable: false,
                    DefaultValue: node.Kind.ToString()),
                node.Kind.ToString()),
            new FieldValue(
                new FieldDefinition(
                    "core.notes",
                    "Notes",
                    ValueKind.StringMultiline,
                    IsEditable: persisted,
                    DefaultValue: node.Notes),
                node.Notes),
        };

        var body = new StackPanel
        {
            Spacing = 12,
        };
        var controls = new List<DictionaryFieldControl>();

        foreach (var field in fields)
        {
            var control = new DictionaryFieldControl(field);
            controls.Add(control);
            control.ValueChanged += (_, value) =>
            {
                if (field.Definition.Id == "core.name")
                {
                    node.Name = value;
                    EditorTitle.Text = value;
                }
                else if (field.Definition.Id == "core.notes")
                {
                    node.Notes = value;
                }

                if (persisted && field.Definition.Id != "core.kind")
                {
                    _database.UpdateNode(node);
                }
            };
            body.Children.Add(control);
        }

        var card = new EditorAccordionCard("General", "⌘", body, isOpen: true);
        foreach (var control in controls)
        {
            control.ValueChanged += (_, _) =>
            {
                card.IsChanged = controls.Any((item) => !item.IsDefault);
            };
        }

        card.IsChanged = controls.Any((item) => !item.IsDefault);
        return card;
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
        var nextSelection = new ProjectTreeNode(node.Parent.Kind, nextSelectionId, node.Parent.Name, node.Parent.Notes);
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
        LoadProjectTree();
        SelectNodeById(node.Id);
    }

    private bool SelectNodeById(string nodeId)
    {
        foreach (var item in ProjectTree.Items.OfType<TreeViewItem>())
        {
            if (SelectNodeById(item, nodeId))
            {
                return true;
            }
        }

        return false;
    }

    private bool SelectNodeById(TreeViewItem item, string nodeId)
    {
        if (item.Tag is ProjectTreeNode node && node.Id == nodeId)
        {
            item.IsSelected = true;
            ShowNode(node);
            return true;
        }

        foreach (var child in item.Items.OfType<TreeViewItem>())
        {
            if (SelectNodeById(child, nodeId))
            {
                item.IsExpanded = true;
                return true;
            }
        }

        return false;
    }
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
        ProjectTreeNode? parent = null)
    {
        Kind = kind;
        Id = id;
        Name = name;
        Notes = notes;
        Parent = parent;
    }

    public ProjectTreeNodeKind Kind { get; }
    public string Id { get; }
    public string Name { get; set; }
    public string Notes { get; set; }
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

    public string Display => Kind switch
    {
        ProjectTreeNodeKind.Project => $"▣ {Name}",
        ProjectTreeNodeKind.AppsRoot => $"▣ {Name}",
        ProjectTreeNodeKind.EpisodesRoot => $"▤ {Name}",
        ProjectTreeNodeKind.App => $"▣ {Name}",
        ProjectTreeNodeKind.Module => $"▧ {Name}",
        ProjectTreeNodeKind.Episode => $"▤ {Name}",
        ProjectTreeNodeKind.Shot => $"◈ {Name}",
        _ => Name,
    };

    public void AddChild(ProjectTreeNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }
}
