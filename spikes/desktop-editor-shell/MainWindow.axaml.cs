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
    private readonly EditorFieldCommitCoordinator _fieldCommitCoordinator = new();
    private readonly List<InstantEditorCard> _editorCards = [];
    private readonly Dictionary<string, DictionaryFieldControl> _activeEditorControlsByFieldId = [];
    private readonly HashSet<string> _expandedNodeIds = [];
    private readonly RuntimeWebPreviewPane _runtimePreviewPane = new();
    private readonly DesignWebPreviewPane _designPreviewPane = new();
    private List<ProjectTreeNode> _treeRoots = [];
    private ProjectTreeNode? _selectedNode;
    private string? _selectedPreviewDeviceId;
    private string? _selectedPreviewThemeId;
    private string _selectedPreviewMode = "light";

    public MainWindow()
    {
        InitializeComponent();
        _coreFieldValues = new CoreFieldValueService(_database);
        _recordClassFieldValues = new RecordClassFieldValueService(_database);
        _componentClassFieldValues = new ComponentClassFieldValueService(_database);
        _actorAvatarPreviews = new ActorAvatarPreviewController(_database, () => _isDark);
        _collectionCards = new EditorCollectionCardFactory(
            _database,
            () => _isDark,
            ShowInfoDialog,
            ConfirmIconTokenDelete,
            ShowIconThemeSearchDialog,
            ReloadAndSelect,
            BrowsePath,
            ShowIconTokenPicker,
            RefreshPreviewDevice);
        RuntimePreviewHost.Content = _runtimePreviewPane;
        DesignPreviewHost.Content = _designPreviewPane;
        RestoreShellState();
        Closing += (_, _) => SaveShellState();
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
        EditorComboBoxBehavior.Configure(PreviewDeviceComboBox);
        EditorComboBoxBehavior.Configure(PreviewThemeComboBox);
        EditorComboBoxBehavior.Configure(PreviewModeComboBox);

        var project = _treeRoots.FirstOrDefault((node) => node.Kind == ProjectTreeNodeKind.Project);
        if (project is null) return;

        var deviceOptions = _database.GetDeviceOptions(project.Id);
        PreviewDeviceComboBox.ItemsSource = deviceOptions;
        var selected = !string.IsNullOrWhiteSpace(_selectedPreviewDeviceId)
            ? deviceOptions.FirstOrDefault((option) => option.Value == _selectedPreviewDeviceId)
            : null;
        selected ??= deviceOptions.FirstOrDefault();
        PreviewDeviceComboBox.SelectedItem = selected;
        _selectedPreviewDeviceId = selected?.Value;

        var themeOptions = _database.GetThemeOptions(project.Id);
        PreviewThemeComboBox.ItemsSource = themeOptions;
        var selectedTheme = !string.IsNullOrWhiteSpace(_selectedPreviewThemeId)
            ? themeOptions.FirstOrDefault((option) => option.Value == _selectedPreviewThemeId)
            : null;
        selectedTheme ??= themeOptions.FirstOrDefault();
        PreviewThemeComboBox.SelectedItem = selectedTheme;
        _selectedPreviewThemeId = selectedTheme?.Value;

        var modeOptions = new[]
        {
            new FieldOption("light", "Light"),
            new FieldOption("dark", "Dark"),
        };
        PreviewModeComboBox.ItemsSource = modeOptions;
        PreviewModeComboBox.SelectedItem = modeOptions.FirstOrDefault((option) => option.Value == _selectedPreviewMode) ?? modeOptions[0];
        _selectedPreviewMode = ((FieldOption)PreviewModeComboBox.SelectedItem).Value;

        RefreshPreviewDevice();
    }

    private void OnPreviewDeviceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PreviewDeviceComboBox.SelectedItem is not FieldOption option) return;

        _selectedPreviewDeviceId = option.Value;
        RefreshPreviewDevice();
    }

    private void OnPreviewThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PreviewThemeComboBox.SelectedItem is not FieldOption option) return;

        _selectedPreviewThemeId = option.Value;
        RefreshPreviewDevice();
    }

    private void OnPreviewModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PreviewModeComboBox.SelectedItem is not FieldOption option) return;

        _selectedPreviewMode = option.Value;
        RefreshPreviewDevice();
    }

    private void RefreshPreviewDevice()
    {
        if (string.IsNullOrWhiteSpace(_selectedPreviewDeviceId)) return;

        var metrics = _database.GetDevicePreviewMetrics(_selectedPreviewDeviceId);
        var themeName = SelectedPreviewThemeName();
        _runtimePreviewPane.Update(metrics, _isDark, themeName, _selectedPreviewMode);
        _designPreviewPane.Update(
            metrics,
            _isDark,
            themeName,
            _selectedPreviewMode,
            DesignPreviewPayloadFactory.Create(_database, _selectedNode, _selectedPreviewThemeId));
    }

    private string SelectedPreviewThemeName()
    {
        return PreviewThemeComboBox.SelectedItem is FieldOption option ? option.Label : "No theme";
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

    private Button CreateTreeActionButton(
        Control content,
        string tooltip,
        EventHandler<RoutedEventArgs> onClick)
    {
        var button = new Button
        {
            Content = content,
            Width = 25,
            Height = 25,
            Padding = new Avalonia.Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
        };
        ToolTip.SetTip(button, tooltip);
        button.Click += onClick;
        return button;
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
        NavigationCardsPanel.Children.Clear();

        foreach (var project in _treeRoots)
        {
            AddNavigationCard(NavigationCardsPanel, project, CreateProjectNavigationContent(project), EditorIcons.ForTreeNode(project.Kind));

            foreach (var root in project.Children
                .Where(EditorNavigationMetadata.IsTopLevelSection)
                .OrderBy(EditorNavigationMetadata.RootOrder))
            {
                AddNavigationSection(NavigationCardsPanel, root);
            }
        }
    }

    private Control CreateProjectNavigationContent(ProjectTreeNode project)
    {
        var panel = new StackPanel
        {
            Spacing = 7,
            Margin = new Avalonia.Thickness(0, 6, 0, 0),
        };

        var episodesRoot = project.Children.FirstOrDefault((child) => child.Kind == ProjectTreeNodeKind.EpisodesRoot);
        foreach (var episode in episodesRoot?.Children ?? [])
        {
            AddNavigationNode(panel, episode, level: 1);
        }

        return panel;
    }

    private void AddNavigationSection(StackPanel parent, ProjectTreeNode sectionRoot)
    {
        var content = new StackPanel
        {
            Spacing = 5,
            Margin = new Avalonia.Thickness(6, 5, 0, 0),
        };

        foreach (var child in sectionRoot.Children)
        {
            AddNavigationNode(content, child, level: 1);
        }

        AddNavigationCard(parent, sectionRoot, content, EditorNavigationMetadata.SectionIcon(sectionRoot));
    }

    private void AddNavigationNode(StackPanel parent, ProjectTreeNode node, int level)
    {
        if (node.Children.Count > 0 || node.CanAddChild)
        {
            var content = new StackPanel
            {
                Spacing = 5,
                Margin = new Avalonia.Thickness(6, 5, 0, 0),
            };
            foreach (var child in node.Children)
            {
                AddNavigationNode(content, child, level + 1);
            }

            AddNavigationCard(parent, node, content, EditorIcons.ForTreeNode(node.Kind));
            return;
        }

        parent.Children.Add(node.Kind == ProjectTreeNodeKind.PaletteColor
            ? CreatePaletteNavigationRow(node)
            : CreateNavigationRow(node, EditorIcons.ForTreeNode(node.Kind)));
    }

    private void AddNavigationCard(
        StackPanel parent,
        ProjectTreeNode node,
        Control content,
        string? iconName)
    {
        var isExpanded = _expandedNodeIds.Contains(node.Id);
        parent.Children.Add(new InstantNavigationCard(
            CreateNavigationHeader(node, iconName, isExpanded),
            content,
            isExpanded));
    }

    private Control CreateNavigationHeader(ProjectTreeNode node, string? iconName, bool isExpanded)
    {
        var grid = new Grid
        {
            ColumnDefinitions = iconName is null
                ? new ColumnDefinitions("*,Auto,24")
                : new ColumnDefinitions("20,*,Auto,24"),
            ColumnSpacing = 6,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var contentColumn = 0;
        if (iconName is not null)
        {
            var icon = EditorIcons.Create(iconName, 16);
            ApplyNavigationSelectionBrush(icon, node);
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);
            contentColumn = 1;
        }

        var titleButton = CreateNavigationSelectButton(node, includeSubtitle: true);
        Grid.SetColumn(titleButton, contentColumn);

        var actions = CreateNavigationActions(node);
        Grid.SetColumn(actions, contentColumn + 1);

        var toggle = CreateNavigationToggleButton(
            isExpanded,
            isExpanded ? "Collapse" : "Expand",
            (_, e) =>
            {
                e.Handled = true;
                ToggleTreeGroup(node);
            });
        Grid.SetColumn(toggle, contentColumn + 2);

        grid.Children.Add(titleButton);
        grid.Children.Add(actions);
        grid.Children.Add(toggle);

        return grid;
    }

    private Control CreateNavigationRow(ProjectTreeNode node, string? iconName)
    {
        var row = new Border
        {
            Padding = new Avalonia.Thickness(6, 4),
            CornerRadius = new CornerRadius(8),
            Background = _selectedNode?.Id == node.Id
                ? new SolidColorBrush(Color.Parse(_isDark ? "#253f5f" : "#dfefff"))
                : Brushes.Transparent,
        };

        var grid = new Grid
        {
            ColumnDefinitions = iconName is null
                ? new ColumnDefinitions("10,*,Auto")
                : new ColumnDefinitions("10,20,*,Auto"),
            ColumnSpacing = 6,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var usedDot = CreateUsedDot(node);
        Grid.SetColumn(usedDot, 0);
        grid.Children.Add(usedDot);

        var contentColumn = 1;
        if (iconName is not null)
        {
            var icon = EditorIcons.Create(iconName, 16);
            ApplyNavigationSelectionBrush(icon, node);
            Grid.SetColumn(icon, 1);
            grid.Children.Add(icon);
            contentColumn = 2;
        }

        var titleButton = CreateNavigationSelectButton(node, includeSubtitle: true);
        Grid.SetColumn(titleButton, contentColumn);

        var actions = CreateNavigationActions(node);
        Grid.SetColumn(actions, contentColumn + 1);

        grid.Children.Add(titleButton);
        grid.Children.Add(actions);
        row.Child = grid;
        return row;
    }

    private Control CreatePaletteNavigationRow(ProjectTreeNode node)
    {
        var row = new Border
        {
            Padding = new Avalonia.Thickness(6, 4),
            CornerRadius = new CornerRadius(8),
            Background = _selectedNode?.Id == node.Id
                ? new SolidColorBrush(Color.Parse(_isDark ? "#253f5f" : "#dfefff"))
                : Brushes.Transparent,
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("10,18,*,Auto"),
            ColumnSpacing = 7,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var usedDot = CreateUsedDot(node);
        Grid.SetColumn(usedDot, 0);

        var swatch = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(4),
            Background = SafeColorBrush(node.ColorHex, "#808080"),
            BorderBrush = new SolidColorBrush(Color.Parse(_isDark ? "#B7C0D2" : "#667085")),
            BorderThickness = new Avalonia.Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        Grid.SetColumn(swatch, 1);

        var titleButton = CreateNavigationSelectButton(node, includeSubtitle: true);
        Grid.SetColumn(titleButton, 2);

        var actions = CreateNavigationActions(node);
        Grid.SetColumn(actions, 3);

        grid.Children.Add(usedDot);
        grid.Children.Add(swatch);
        grid.Children.Add(titleButton);
        grid.Children.Add(actions);
        row.Child = grid;
        return row;
    }

    private Avalonia.Controls.Shapes.Ellipse CreateUsedDot(ProjectTreeNode node)
    {
        return new Avalonia.Controls.Shapes.Ellipse
        {
            Width = 7,
            Height = 7,
            Fill = node.IsUsed ? new SolidColorBrush(Color.Parse("#D6A638")) : Brushes.Transparent,
            Stroke = new SolidColorBrush(Color.Parse(_isDark ? "#8d96a6" : "#667085")),
            StrokeThickness = 1,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
    }

    private Button CreateNavigationToggleButton(
        bool isExpanded,
        string tooltip,
        EventHandler<RoutedEventArgs> onClick)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = isExpanded ? "v" : ">",
                FontSize = 18,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Width = 22,
            Height = 22,
            Padding = new Avalonia.Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
        };
        ToolTip.SetTip(button, tooltip);
        button.Click += onClick;
        return button;
    }

    private Button CreateNavigationSelectButton(ProjectTreeNode node, bool includeSubtitle)
    {
        var textPanel = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
        };
        textPanel.Children.Add(new TextBlock
        {
            Text = EditorNavigationMetadata.Title(node),
            FontWeight = FontWeight.SemiBold,
            Foreground = NavigationTextBrush(node),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var subtitle = EditorNavigationMetadata.Subtitle(node);
        if (includeSubtitle && !string.IsNullOrWhiteSpace(subtitle))
        {
            textPanel.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                Opacity = 0.72,
                Foreground = NavigationMutedTextBrush(node),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        var button = new Button
        {
            Content = textPanel,
            Padding = new Avalonia.Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
        };
        button.Click += (_, e) =>
        {
            e.Handled = true;
            if (node.Children.Count > 0)
            {
                if (node.CanOpenEditor)
                {
                    ShowNode(node);
                }
                ToggleTreeGroup(node);
                return;
            }

            SelectTreeNode(node);
        };
        return button;
    }

    private IBrush NavigationTextBrush(ProjectTreeNode node)
    {
        return _selectedNode?.Id == node.Id
            ? new SolidColorBrush(Color.Parse(_isDark ? "#7DB7FF" : "#1368CE"))
            : new SolidColorBrush(Color.Parse(_isDark ? "#F1F5F9" : "#1F2937"));
    }

    private IBrush NavigationMutedTextBrush(ProjectTreeNode node)
    {
        return _selectedNode?.Id == node.Id
            ? new SolidColorBrush(Color.Parse(_isDark ? "#A8CEFF" : "#2F7EDB"))
            : new SolidColorBrush(Color.Parse(_isDark ? "#B8C0CE" : "#667085"));
    }

    private void ApplyNavigationSelectionBrush(Control control, ProjectTreeNode node)
    {
        if (_selectedNode?.Id == node.Id)
        {
            EditorIcons.ApplyBrush(control, NavigationTextBrush(node));
        }
    }

    private StackPanel CreateNavigationActions(ProjectTreeNode node)
    {
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        if (node.CanAddChild)
        {
            actions.Children.Add(CreateTreeActionButton(EditorIcons.Create(EditorIcons.Add, 14), "Add child", async (_, e) =>
            {
                e.Handled = true;
                await AddChild(node);
            }));
        }

        if (node.CanDuplicate)
        {
            actions.Children.Add(CreateTreeActionButton(EditorIcons.Create(EditorIcons.Duplicate, 14), "Duplicate", (_, e) =>
            {
                e.Handled = true;
                DuplicateNode(node);
            }));
        }

        if (node.CanDelete)
        {
            actions.Children.Add(CreateTreeActionButton(EditorIcons.Create(EditorIcons.Delete, 14), "Delete", async (_, e) =>
            {
                e.Handled = true;
                await DeleteNode(node);
            }));
        }

        return actions;
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
                    BrowsePath,
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
            if (node.Kind == ProjectTreeNodeKind.PaletteColor && fieldId == "palette.token")
            {
                node.Name = value;
                EditorTitle.Text = value;
                RebuildNavigationCards();
            }
            else if (node.Kind == ProjectTreeNodeKind.PaletteColor && fieldId == "palette.valueHex")
            {
                node.ColorHex = value;
                RebuildNavigationCards();
            }
            else if (node.Kind == ProjectTreeNodeKind.Device && _selectedPreviewDeviceId == node.Id)
            {
                RefreshPreviewDevice();
            }
            else if (node.Kind == ProjectTreeNodeKind.Actor && fieldId == "actor.shortName")
            {
                node.Notes = value;
                RebuildNavigationCards();
            }
            else if (node.Kind == ProjectTreeNodeKind.Theme &&
                     fieldId is "theme.family" or "theme.iconThemeId" or "theme.statusBarId" or "theme.navigationBarId")
            {
                var settings = _database.GetThemeSettings(node.Id);
                var linkedCount = new[] { settings.IconThemeId, settings.StatusBarId, settings.NavigationBarId }.Count((id) => !string.IsNullOrWhiteSpace(id));
                node.Notes = $"{settings.Family} · {linkedCount}/3 refs";
                RebuildNavigationCards();
            }
            else if (node.Kind == ProjectTreeNodeKind.ProductionFont && fieldId == "font.category")
            {
                node.Notes = $"{value} · {_database.GetProductionFontFieldValue(node.Id, "font.files").Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length} files";
                RebuildNavigationCards();
            }
            else if (node.Kind == ProjectTreeNodeKind.StatusBar && fieldId == "statusBar.family")
            {
                var itemCount = _database.GetStatusBarItems(node.Id).Count;
                node.Notes = $"{value} · {itemCount} items";
                RebuildNavigationCards();
            }
            else if (node.Kind == ProjectTreeNodeKind.NavigationBar && fieldId == "navigationBar.family")
            {
                var itemCount = _database.GetNavigationBarItems(node.Id).Count;
                node.Notes = $"{value} · {itemCount} buttons";
                RebuildNavigationCards();
            }
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
            if (fieldId == "core.name")
            {
                EditorTitle.Text = node.Name;
            }
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

    private async Task<string?> BrowseDirectory(string currentPath)
    {
        var options = new FolderPickerOpenOptions
        {
            Title = "Select media root",
            AllowMultiple = false,
        };

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var fullPath = Path.IsPathFullyQualified(currentPath)
                ? currentPath
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", currentPath));
            if (Directory.Exists(fullPath))
            {
                options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(fullPath);
            }
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(options);
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    private Task<string?> BrowsePath(string currentPath, ValueKind valueKind)
    {
        return valueKind == ValueKind.ImageFilePath
            ? BrowseImageFile(currentPath, SelectedProjectMediaRoot())
            : BrowseDirectory(currentPath);
    }

    private string? SelectedProjectMediaRoot()
    {
        if (_selectedNode is null) return null;

        var project = ProjectAncestor(_selectedNode);
        return _database.GetProjectSettings(project.Id).MediaRoot;
    }

    private async Task<string?> BrowseImageFile(string currentPath, string? mediaRoot)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Select avatar image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.heic"],
                    AppleUniformTypeIdentifiers = ["public.image"],
                    MimeTypes = ["image/png", "image/jpeg", "image/webp"],
                },
            ],
        };

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var fullPath = Path.IsPathFullyQualified(currentPath)
                ? currentPath
                : !string.IsNullOrWhiteSpace(mediaRoot)
                    ? Path.GetFullPath(Path.Combine(mediaRoot, currentPath))
                    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", currentPath));
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            {
                options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(parent);
            }
        }

        var files = await StorageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0) return null;

        var selectedPath = files[0].Path.LocalPath;
        return MediaPathService.RelativePathIfInsideMediaRoot(selectedPath, mediaRoot);
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

    private static IBrush SafeColorBrush(string? hex, string fallback)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(string.IsNullOrWhiteSpace(hex) ? fallback : hex));
        }
        catch (FormatException)
        {
            return new SolidColorBrush(Color.Parse(fallback));
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
    ProductionDataRoot,
    SystemDataRoot,
    AppsRoot,
    PaletteRoot,
    IconThemesRoot,
    StatusBarsRoot,
    NavigationBarsRoot,
    ComponentClassesRoot,
    DevicesRoot,
    ActorsRoot,
    ThemesRoot,
    ProductionFontsRoot,
    EpisodesRoot,
    App,
    Module,
    Episode,
    Shot,
    PaletteColor,
    IconTheme,
    StatusBar,
    NavigationBar,
    ComponentClass,
    Device,
    Actor,
    Theme,
    ProductionFont,
}

internal sealed class ProjectTreeNode
{
    public ProjectTreeNode(
        ProjectTreeNodeKind kind,
        string id,
        string name,
        string notes,
        string recordClassId,
        ProjectTreeNode? parent = null,
        string? colorHex = null,
        bool isUsed = false)
    {
        Kind = kind;
        Id = id;
        Name = name;
        Notes = notes;
        RecordClassId = recordClassId;
        Parent = parent;
        ColorHex = colorHex;
        IsUsed = isUsed;
    }

    public ProjectTreeNodeKind Kind { get; }
    public string Id { get; }
    public string Name { get; set; }
    public string Notes { get; set; }
    public string RecordClassId { get; }
    public ProjectTreeNode? Parent { get; private set; }
    public string? ColorHex { get; set; }
    public bool IsUsed { get; }
    public List<ProjectTreeNode> Children { get; } = [];

    public int Level => Parent is null ? 0 : Parent.Level + 1;
    public bool CanAddChild => Kind is ProjectTreeNodeKind.AppsRoot
        or ProjectTreeNodeKind.App
        or ProjectTreeNodeKind.PaletteRoot
        or ProjectTreeNodeKind.IconThemesRoot
        or ProjectTreeNodeKind.StatusBarsRoot
        or ProjectTreeNodeKind.NavigationBarsRoot
        or ProjectTreeNodeKind.ComponentClassesRoot
        or ProjectTreeNodeKind.DevicesRoot
        or ProjectTreeNodeKind.ActorsRoot
        or ProjectTreeNodeKind.ThemesRoot
        or ProjectTreeNodeKind.ProductionFontsRoot
        or ProjectTreeNodeKind.EpisodesRoot
        or ProjectTreeNodeKind.Episode;
    public bool CanDuplicate => Kind is ProjectTreeNodeKind.App
        or ProjectTreeNodeKind.Module
        or ProjectTreeNodeKind.Episode
        or ProjectTreeNodeKind.Shot
        or ProjectTreeNodeKind.PaletteColor
        or ProjectTreeNodeKind.IconTheme
        or ProjectTreeNodeKind.StatusBar
        or ProjectTreeNodeKind.NavigationBar
        or ProjectTreeNodeKind.ComponentClass
        or ProjectTreeNodeKind.Device
        or ProjectTreeNodeKind.Actor
        or ProjectTreeNodeKind.Theme;
    public bool CanDelete => Kind is ProjectTreeNodeKind.App
        or ProjectTreeNodeKind.Module
        or ProjectTreeNodeKind.Episode
        or ProjectTreeNodeKind.Shot
        or ProjectTreeNodeKind.PaletteColor
        or ProjectTreeNodeKind.IconTheme
        or ProjectTreeNodeKind.StatusBar
        or ProjectTreeNodeKind.NavigationBar
        or ProjectTreeNodeKind.ComponentClass
        or ProjectTreeNodeKind.Device
        or ProjectTreeNodeKind.Actor
        or ProjectTreeNodeKind.Theme
        or ProjectTreeNodeKind.ProductionFont;
    public bool CanOpenEditor => Kind is not ProjectTreeNodeKind.ProductionDataRoot
        and not ProjectTreeNodeKind.SystemDataRoot
        and not ProjectTreeNodeKind.AppsRoot
        and not ProjectTreeNodeKind.PaletteRoot
        and not ProjectTreeNodeKind.IconThemesRoot
        and not ProjectTreeNodeKind.StatusBarsRoot
        and not ProjectTreeNodeKind.NavigationBarsRoot
        and not ProjectTreeNodeKind.ComponentClassesRoot
        and not ProjectTreeNodeKind.DevicesRoot
        and not ProjectTreeNodeKind.ActorsRoot
        and not ProjectTreeNodeKind.ThemesRoot
        and not ProjectTreeNodeKind.ProductionFontsRoot
        and not ProjectTreeNodeKind.EpisodesRoot;

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
            ProjectTreeNodeKind.ProductionDataRoot => "navigation.production_data",
            ProjectTreeNodeKind.SystemDataRoot => "navigation.system_data",
            ProjectTreeNodeKind.AppsRoot => "navigation.apps",
            ProjectTreeNodeKind.PaletteRoot => "navigation.palette",
            ProjectTreeNodeKind.IconThemesRoot => "navigation.icon_themes",
            ProjectTreeNodeKind.StatusBarsRoot => "navigation.status_bars",
            ProjectTreeNodeKind.NavigationBarsRoot => "navigation.navigation_bars",
            ProjectTreeNodeKind.ComponentClassesRoot => "navigation.component_classes",
            ProjectTreeNodeKind.DevicesRoot => "navigation.devices",
            ProjectTreeNodeKind.ActorsRoot => "navigation.actors",
            ProjectTreeNodeKind.ThemesRoot => "navigation.themes",
            ProjectTreeNodeKind.ProductionFontsRoot => "navigation.production_fonts",
            ProjectTreeNodeKind.EpisodesRoot => "navigation.episodes",
            ProjectTreeNodeKind.App => "app.generic",
            ProjectTreeNodeKind.Module => "module.generic",
            ProjectTreeNodeKind.Episode => "episode",
            ProjectTreeNodeKind.Shot => "shot",
            ProjectTreeNodeKind.PaletteColor => "palette_color",
            ProjectTreeNodeKind.IconTheme => "icon_theme",
            ProjectTreeNodeKind.StatusBar => "status_bar",
            ProjectTreeNodeKind.NavigationBar => "navigation_bar",
            ProjectTreeNodeKind.ComponentClass => "component.avatar",
            ProjectTreeNodeKind.Device => "device",
            ProjectTreeNodeKind.Actor => "actor",
            ProjectTreeNodeKind.Theme => "theme",
            ProjectTreeNodeKind.ProductionFont => "production_font",
            _ => throw new InvalidOperationException($"No record class for {kind}."),
        };
    }
}
