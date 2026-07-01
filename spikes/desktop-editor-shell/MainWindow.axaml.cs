using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell;

public partial class MainWindow : SukiWindow
{
    private bool _isDark = true;
    private readonly SpikeDatabase _database = new(SpikeDatabase.DefaultDatabasePath());
    private readonly EditorFieldCommitCoordinator _fieldCommitCoordinator = new();
    private readonly List<Expander> _editorCards = [];
    private readonly HashSet<string> _expandedNodeIds = [];
    private List<ProjectTreeNode> _treeRoots = [];
    private ProjectTreeNode? _selectedNode;
    private string? _selectedPreviewDeviceId;

    public MainWindow()
    {
        InitializeComponent();
        RestoreShellState();
        Closing += (_, _) => SaveShellState();
        ApplyTheme();
        LoadProjectTree();
        InitializePreviewDevices();
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
            RefreshPreviewDevice();
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
        RefreshPreviewDevice();
    }

    private void SetBrush(string key, string hex)
    {
        Resources[key] = new SolidColorBrush(Color.Parse(hex));
    }

    private void InitializePreviewDevices()
    {
        var project = _treeRoots.FirstOrDefault((node) => node.Kind == ProjectTreeNodeKind.Project);
        if (project is null) return;

        var options = _database.GetDeviceOptions(project.Id);
        PreviewDeviceComboBox.ItemsSource = options;
        var selected = !string.IsNullOrWhiteSpace(_selectedPreviewDeviceId)
            ? options.FirstOrDefault((option) => option.Value == _selectedPreviewDeviceId)
            : null;
        selected ??= options.FirstOrDefault();
        PreviewDeviceComboBox.SelectedItem = selected;
        _selectedPreviewDeviceId = selected?.Value;
        RefreshPreviewDevice();
    }

    private void OnPreviewDeviceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PreviewDeviceComboBox.SelectedItem is not FieldOption option) return;

        _selectedPreviewDeviceId = option.Value;
        RefreshPreviewDevice();
    }

    private void RefreshPreviewDevice()
    {
        if (PreviewDeviceHost is null || string.IsNullOrWhiteSpace(_selectedPreviewDeviceId)) return;

        var metrics = _database.GetDevicePreviewMetrics(_selectedPreviewDeviceId);
        PreviewDeviceHost.Content = CreateDevicePreview(metrics);
    }

    private Control CreateDevicePreview(SpikeDatabase.DevicePreviewMetrics metrics)
    {
        var canvas = new Canvas
        {
            Width = metrics.CanvasWidth,
            Height = metrics.CanvasHeight,
        };

        var device = new Border
        {
            Width = metrics.CanvasWidth,
            Height = metrics.CanvasHeight,
            CornerRadius = new CornerRadius(Math.Max(0, metrics.CornerRadius)),
            Background = new SolidColorBrush(Color.Parse(_isDark ? "#20242D" : "#F2F4F7")),
            BorderBrush = (IBrush)Resources["PreviewDeviceBorderBrush"]!,
            BorderThickness = new Avalonia.Thickness(Math.Max(6, Math.Min(metrics.CanvasWidth, metrics.CanvasHeight) * 0.012)),
            ClipToBounds = true,
            BoxShadow = BoxShadows.Parse("0 10 28 0 #33000000"),
        };
        Canvas.SetLeft(device, 0);
        Canvas.SetTop(device, 0);

        var screen = new Border
        {
            Width = metrics.ScreenWidth,
            Height = metrics.ScreenHeight,
            CornerRadius = new CornerRadius(Math.Max(0, metrics.CornerRadius * 0.72)),
            Background = (IBrush)Resources["PreviewScreenBackground"]!,
            ClipToBounds = true,
            Child = CreatePreviewScreenContent(metrics),
        };
        Canvas.SetLeft(screen, metrics.ScreenX);
        Canvas.SetTop(screen, metrics.ScreenY);

        canvas.Children.Add(device);
        canvas.Children.Add(screen);
        return canvas;
    }

    private Control CreatePreviewScreenContent(SpikeDatabase.DevicePreviewMetrics metrics)
    {
        var headerHeight = Math.Max(56, metrics.ScreenHeight * 0.11);
        return new Grid
        {
            RowDefinitions = new RowDefinitions($"{headerHeight},*"),
            Children =
            {
                new Border
                {
                    Background = (IBrush)Resources["PreviewHeaderBackground"]!,
                    Padding = new Avalonia.Thickness(18, 14),
                    Child = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock
                            {
                                Foreground = (IBrush)Resources["PreviewHeaderTextBrush"]!,
                                FontSize = 18,
                                FontWeight = FontWeight.Bold,
                                Text = "Alex",
                            },
                            new TextBlock
                            {
                                Foreground = (IBrush)Resources["PreviewHeaderMutedBrush"]!,
                                FontSize = 12,
                                Text = "offline",
                            },
                        },
                    },
                },
                PreviewMessagesLayer(),
            },
        };
    }

    private Control PreviewMessagesLayer()
    {
        var layer = new Grid
        {
            Background = Brushes.Transparent,
            Children =
            {
                new StackPanel
                {
                    Margin = new Avalonia.Thickness(18, 34, 18, 0),
                    Spacing = 12,
                    Children =
                    {
                        new Border
                        {
                            HorizontalAlignment = HorizontalAlignment.Right,
                            MaxWidth = 210,
                            CornerRadius = new CornerRadius(18),
                            Background = (IBrush)Resources["PreviewOutgoingBubble"]!,
                            Padding = new Avalonia.Thickness(14, 10),
                            Child = new TextBlock
                            {
                                Foreground = (IBrush)Resources["PreviewBubbleTextBrush"]!,
                                TextWrapping = TextWrapping.Wrap,
                                Text = "Are you close?",
                            },
                        },
                        new Border
                        {
                            HorizontalAlignment = HorizontalAlignment.Left,
                            MaxWidth = 220,
                            CornerRadius = new CornerRadius(18),
                            Background = (IBrush)Resources["PreviewIncomingBubble"]!,
                            Padding = new Avalonia.Thickness(14, 10),
                            Child = new TextBlock
                            {
                                Foreground = (IBrush)Resources["PreviewIncomingTextBrush"]!,
                                TextWrapping = TextWrapping.Wrap,
                                Text = "Two minutes away.",
                            },
                        },
                    },
                },
            },
        };
        Grid.SetRow(layer, 1);
        return layer;
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
                .Where((child) => child.Kind is ProjectTreeNodeKind.AppsRoot
                    or ProjectTreeNodeKind.ProductionDataRoot
                    or ProjectTreeNodeKind.SystemDataRoot)
                .OrderBy(NavigationRootOrder))
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

        var iconName = sectionRoot.Kind switch
        {
            ProjectTreeNodeKind.ProductionDataRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.ProductionDataRoot),
            ProjectTreeNodeKind.SystemDataRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.SystemDataRoot),
            ProjectTreeNodeKind.EpisodesRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.Episode),
            ProjectTreeNodeKind.PaletteRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.PaletteColor),
            ProjectTreeNodeKind.DevicesRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.Device),
            ProjectTreeNodeKind.ActorsRoot => EditorIcons.ForTreeNode(ProjectTreeNodeKind.Actor),
            _ => EditorIcons.ForTreeNode(ProjectTreeNodeKind.App),
        };
        AddNavigationCard(parent, sectionRoot, content, iconName);
    }

    private static int NavigationRootOrder(ProjectTreeNode node)
    {
        return node.Kind switch
        {
            ProjectTreeNodeKind.AppsRoot => 10,
            ProjectTreeNodeKind.EpisodesRoot => 20,
            ProjectTreeNodeKind.ProductionDataRoot => 30,
            ProjectTreeNodeKind.SystemDataRoot => 40,
            _ => 100,
        };
    }

    private void AddNavigationNode(StackPanel parent, ProjectTreeNode node, int level)
    {
        if (node.Children.Count > 0)
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
        var body = new Border
        {
            Padding = new Avalonia.Thickness(7, 0, 7, 7),
            IsVisible = isExpanded,
            Child = content,
        };

        var card = new Border
        {
            Margin = new Avalonia.Thickness(0, 0, 0, 4),
            CornerRadius = new CornerRadius(10),
            BoxShadow = BoxShadows.Parse("0 4 10 0 #18000000"),
            Child = new GlassCard
            {
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        CreateNavigationHeader(node, iconName, isExpanded),
                        body,
                    },
                },
            },
        };

        parent.Children.Add(card);
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
                ? new ColumnDefinitions("*,Auto")
                : new ColumnDefinitions("20,*,Auto"),
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

        var usedDot = new Avalonia.Controls.Shapes.Ellipse
        {
            Width = 7,
            Height = 7,
            Fill = node.IsUsed ? new SolidColorBrush(Color.Parse("#D6A638")) : Brushes.Transparent,
            Stroke = new SolidColorBrush(Color.Parse(_isDark ? "#8d96a6" : "#667085")),
            StrokeThickness = 1,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
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

    private Button CreateNavigationToggleButton(
        bool isExpanded,
        string tooltip,
        EventHandler<RoutedEventArgs> onClick)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = isExpanded ? "−" : "+",
                FontSize = 17,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
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

    private Button CreateNavigationSelectButton(ProjectTreeNode node, bool includeSubtitle)
    {
        var textPanel = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
        };
        textPanel.Children.Add(new TextBlock
        {
            Text = NavigationTitle(node),
            FontWeight = FontWeight.SemiBold,
            Foreground = NavigationTextBrush(node),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var subtitle = NavigationSubtitle(node);
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
            SelectTreeNode(node);
            if (node.Children.Count > 0)
            {
                CollapseVisibleNavigationPeers(node);
                _expandedNodeIds.Add(node.Id);
                RebuildNavigationCards();
            }
        };
        return button;
    }

    private static string NavigationTitle(ProjectTreeNode node)
    {
        return node.Kind switch
        {
            ProjectTreeNodeKind.Project => "Project",
            ProjectTreeNodeKind.ProductionDataRoot => "Production data",
            ProjectTreeNodeKind.SystemDataRoot => "System data",
            _ => node.Name,
        };
    }

    private static string NavigationSubtitle(ProjectTreeNode node)
    {
        return node.Kind switch
        {
            ProjectTreeNodeKind.Project => "Episodes, shots, screens and modules",
            ProjectTreeNodeKind.AppsRoot => "Apps and module defaults",
            ProjectTreeNodeKind.ProductionDataRoot => "Actors, devices and production themes",
            ProjectTreeNodeKind.SystemDataRoot => "Icon sets, bars, palette, fonts, media and presets",
            _ => node.Notes,
        };
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
            ApplyIconBrush(control, NavigationTextBrush(node));
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
            actions.Children.Add(CreateTreeActionButton(EditorIcons.Create(EditorIcons.Add, 14), "Add child", (_, e) =>
            {
                e.Handled = true;
                AddChild(node);
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
        if (rebuildTree)
        {
            RebuildNavigationCards();
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
        var controlsByFieldId = new Dictionary<string, DictionaryFieldControl>();
        var headerIcon = EditorIcons.Create(layoutCard.Icon, 18);
        ContentControl? actorAvatarPreviewHost = null;

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

            if (node.Kind == ProjectTreeNodeKind.Actor && layoutCard.Id == "avatar")
            {
                actorAvatarPreviewHost = new ContentControl
                {
                    Content = CreateActorAvatarPreview(node.Id),
                };
                groupPanel.Children.Add(actorAvatarPreviewHost);
            }

            foreach (var layoutField in group.VisibleFields)
            {
                var field = CreateFieldValue(node, layoutField.Id);
                var control = new DictionaryFieldControl(field, BrowsePath);
                controls.Add(control);
                controlsByFieldId[field.Definition.Id] = control;
                control.ValueCommitted += (_, value) =>
                {
                    _fieldCommitCoordinator.Commit(
                        control,
                        value,
                        (draftValue) => StoredFieldValue(node, field.Definition.Id, draftValue),
                        () => CurrentStoredFieldValue(node, field.Definition.Id),
                        (storedValue) => PersistFieldValue(node, field.Definition.Id, storedValue));
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
            Header = CreateEditorCardHeader(layoutCard.Label, EditorCardSubtitle(layoutCard), headerIcon),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ExpandDirection = ExpandDirection.Down,
            IsExpanded = layoutCard.DefaultOpen,
            Content = new Border
            {
                Padding = new Avalonia.Thickness(10),
                Child = body,
            },
        };
        UpdateEditorCardHeaderState(headerIcon, controls);
        foreach (var control in controls)
        {
            control.ValueChanged += (_, _) =>
            {
                UpdateEditorCardHeaderState(headerIcon, controls);
                if (actorAvatarPreviewHost is not null)
                {
                    actorAvatarPreviewHost.Content = CreateActorAvatarPreview(node.Id, CurrentDraftFieldValues(controlsByFieldId));
                }
            };
        }

        return card;
    }

    private static IReadOnlyDictionary<string, string> CurrentDraftFieldValues(
        IReadOnlyDictionary<string, DictionaryFieldControl> controlsByFieldId)
    {
        return controlsByFieldId.ToDictionary(
            (pair) => pair.Key,
            (pair) => pair.Value.Value);
    }

    private static string EditorCardSubtitle(EditorLayoutCard layoutCard)
    {
        if (!string.IsNullOrWhiteSpace(layoutCard.Subtitle))
        {
            return layoutCard.Subtitle;
        }

        return layoutCard.VisibleGroups
            .Select((group) => group.Label)
            .FirstOrDefault((label) => !string.IsNullOrWhiteSpace(label)) ?? "";
    }

    private static Control CreateEditorCardHeader(string label, string subtitle, Control icon)
    {
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Children.Add(icon);
        var textPanel = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
        };
        textPanel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            textPanel.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                Opacity = 0.72,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        header.Children.Add(textPanel);
        return header;
    }

    private static void UpdateEditorCardHeaderState(Control headerIcon, IEnumerable<DictionaryFieldControl> controls)
    {
        var hasOverrides = controls.Any((control) => !control.IsDefault);
        var brush = hasOverrides ? new SolidColorBrush(Color.Parse("#D6A638")) : null;
        ApplyIconBrush(headerIcon, brush);
    }

    private static void ApplyIconBrush(Control control, IBrush? brush)
    {
        switch (control)
        {
            case PathIcon pathIcon:
                if (brush is null)
                {
                    pathIcon.ClearValue(PathIcon.ForegroundProperty);
                }
                else
                {
                    pathIcon.Foreground = brush;
                }
                break;
            case TextBlock textBlock:
                if (brush is null)
                {
                    textBlock.ClearValue(TextBlock.ForegroundProperty);
                }
                else
                {
                    textBlock.Foreground = brush;
                }
                break;
            case Panel panel:
                foreach (var child in panel.Children.OfType<Control>())
                {
                    ApplyIconBrush(child, brush);
                }
                break;
        }
    }

    private FieldValue CreateFieldValue(ProjectTreeNode node, string fieldId)
    {
        var persisted = node.Kind is ProjectTreeNodeKind.Project
            or ProjectTreeNodeKind.App
            or ProjectTreeNodeKind.Module
            or ProjectTreeNodeKind.Episode
            or ProjectTreeNodeKind.Shot
            or ProjectTreeNodeKind.PaletteColor
            or ProjectTreeNodeKind.Device
            or ProjectTreeNodeKind.Actor;

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
                    node.Kind switch
                    {
                        ProjectTreeNodeKind.Project => "Production Notes",
                        ProjectTreeNodeKind.Episode => "Episode Notes",
                        _ => "Notes",
                    },
                    ValueKind.StringMultiline,
                    IsEditable: persisted,
                    DefaultValue: node.Notes),
                node.Notes),
            "project.slug" when node.Kind == ProjectTreeNodeKind.Project => CreateProjectFieldValue(
                node.Id,
                "project.slug",
                "Slug",
                ValueKind.StringSingleLine),
            "project.defaultFps" when node.Kind == ProjectTreeNodeKind.Project => CreateProjectFieldValue(
                node.Id,
                "project.defaultFps",
                "Default FPS",
                ValueKind.Integer),
            "project.mediaRoot" when node.Kind == ProjectTreeNodeKind.Project => CreateProjectFieldValue(
                node.Id,
                "project.mediaRoot",
                "Media Root",
                ValueKind.DirectoryPath),
            "episode.slug" when node.Kind == ProjectTreeNodeKind.Episode => CreateEpisodeFieldValue(
                node.Id,
                "episode.slug",
                "Slug",
                ValueKind.StringSingleLine),
            "episode.sortOrder" when node.Kind == ProjectTreeNodeKind.Episode => CreateEpisodeFieldValue(
                node.Id,
                "episode.sortOrder",
                "Sort Order",
                ValueKind.Integer),
            "palette.token" when node.Kind == ProjectTreeNodeKind.PaletteColor => CreatePaletteColorFieldValue(
                node.Id,
                "palette.token",
                "Token",
                ValueKind.StringSingleLine),
            "palette.valueHex" when node.Kind == ProjectTreeNodeKind.PaletteColor => CreatePaletteColorFieldValue(
                node.Id,
                "palette.valueHex",
                "Hex",
                ValueKind.HexColor),
            "palette.isNeutral" when node.Kind == ProjectTreeNodeKind.PaletteColor => CreatePaletteColorFieldValue(
                node.Id,
                "palette.isNeutral",
                "Neutral",
                ValueKind.Boolean),
            "palette.source" when node.Kind == ProjectTreeNodeKind.PaletteColor => CreatePaletteColorFieldValue(
                node.Id,
                "palette.source",
                "Source",
                ValueKind.StringSingleLine),
            "palette.protected" when node.Kind == ProjectTreeNodeKind.PaletteColor => CreatePaletteColorFieldValue(
                node.Id,
                "palette.protected",
                "Protected",
                ValueKind.Boolean),
            "palette.hiddenFromPickers" when node.Kind == ProjectTreeNodeKind.PaletteColor => CreatePaletteColorFieldValue(
                node.Id,
                "palette.hiddenFromPickers",
                "Hidden From Pickers",
                ValueKind.Boolean),
            "palette.note" when node.Kind == ProjectTreeNodeKind.PaletteColor => CreatePaletteColorFieldValue(
                node.Id,
                "palette.note",
                "Note",
                ValueKind.StringMultiline),
            "device.manufacturer" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.manufacturer", "Manufacturer", ValueKind.StringSingleLine),
            "device.model" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.model", "Model", ValueKind.StringSingleLine),
            "device.osFamily" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.osFamily", "OS Family", ValueKind.StringSingleLine),
            "device.metrics.designSpace.size" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.designSpace.size", "Design space", ValueKind.IntegerPair),
            "device.metrics.renderSize" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.renderSize", "Render size", ValueKind.IntegerPair),
            "device.metrics.scaleToPixels" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.scaleToPixels", "Scale to pixels", ValueKind.Integer),
            "device.metrics.pixelRatio" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.pixelRatio", "Pixel ratio", ValueKind.Integer),
            "device.metrics.defaultScreenScale" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.defaultScreenScale", "Default screen scale", ValueKind.Integer),
            "device.metrics.canvas.size" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.canvas.size", "Canvas size", ValueKind.IntegerPair),
            "device.metrics.screen.position" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.screen.position", "Screen position", ValueKind.IntegerPair),
            "device.metrics.screen.size" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.screen.size", "Screen size", ValueKind.IntegerPair),
            "device.metrics.cornerRadius" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.cornerRadius", "Corner radius", ValueKind.Integer),
            "device.metrics.viewport.position" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.viewport.position", "Viewport position", ValueKind.IntegerPair),
            "device.metrics.viewport.size" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.viewport.size", "Viewport size", ValueKind.IntegerPair),
            "device.metrics.safeArea.vertical" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.safeArea.vertical", "Safe vertical", ValueKind.IntegerPair),
            "device.metrics.safeArea.horizontal" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.safeArea.horizontal", "Safe horizontal", ValueKind.IntegerPair),
            "device.metrics.statusBar.position" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.statusBar.position", "Status bar position", ValueKind.IntegerPair),
            "device.metrics.statusBar.size" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.statusBar.size", "Status bar size", ValueKind.IntegerPair),
            "device.metrics.dynamicIsland.position" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.dynamicIsland.position", "Dynamic island position", ValueKind.IntegerPair),
            "device.metrics.dynamicIsland.size" when node.Kind == ProjectTreeNodeKind.Device => CreateDeviceFieldValue(node.Id, "device.metrics.dynamicIsland.size", "Dynamic island size", ValueKind.IntegerPair),
            "actor.shortName" when node.Kind == ProjectTreeNodeKind.Actor => CreateActorFieldValue(node.Id, "actor.shortName", "Short name", ValueKind.StringSingleLine),
            "actor.defaultDeviceId" when node.Kind == ProjectTreeNodeKind.Actor => CreateActorFieldValue(node.Id, "actor.defaultDeviceId", "Default device", ValueKind.OptionToken),
            "actor.defaultThemeId" when node.Kind == ProjectTreeNodeKind.Actor => CreateActorFieldValue(node.Id, "actor.defaultThemeId", "Default theme", ValueKind.OptionToken),
            "actor.color.modes" when node.Kind == ProjectTreeNodeKind.Actor => CreateActorFieldValue(node.Id, "actor.color.modes", "Actor Color", ValueKind.PaletteColorPair),
            "actor.avatarTextColor.modes" when node.Kind == ProjectTreeNodeKind.Actor => CreateActorFieldValue(node.Id, "actor.avatarTextColor.modes", "Actor Text Color", ValueKind.PaletteColorPair),
            "actor.avatar.filePath" when node.Kind == ProjectTreeNodeKind.Actor => CreateActorFieldValue(node.Id, "actor.avatar.filePath", "Avatar image", ValueKind.ImageFilePath),
            "actor.avatar.scale" when node.Kind == ProjectTreeNodeKind.Actor => CreateActorFieldValue(node.Id, "actor.avatar.scale", "Avatar scale", ValueKind.StringSingleLine),
            "actor.avatar.offset" when node.Kind == ProjectTreeNodeKind.Actor => CreateActorFieldValue(node.Id, "actor.avatar.offset", "Avatar offset", ValueKind.IntegerPair),
            "actor.avatar.useInitials" when node.Kind == ProjectTreeNodeKind.Actor => CreateActorFieldValue(node.Id, "actor.avatar.useInitials", "Use initials", ValueKind.Boolean),
            "actor.avatar.initialsPadding" when node.Kind == ProjectTreeNodeKind.Actor => CreateActorFieldValue(node.Id, "actor.avatar.initialsPadding", "Initials padding", ValueKind.Integer),
            _ => throw new InvalidOperationException($"Unknown field '{fieldId}' for record class '{node.RecordClassId}'."),
        };
    }

    private FieldValue CreateProjectFieldValue(
        string projectId,
        string fieldId,
        string label,
        ValueKind valueKind)
    {
        var settings = _database.GetProjectSettings(projectId);
        var value = fieldId switch
        {
            "project.slug" => settings.Slug,
            "project.defaultFps" => settings.DefaultFps.ToString(),
            "project.mediaRoot" => settings.MediaRoot,
            _ => "",
        };

        return new FieldValue(
            new FieldDefinition(
                fieldId,
                label,
                valueKind,
                IsEditable: true,
                DefaultValue: value),
            value);
    }

    private FieldValue CreateEpisodeFieldValue(
        string episodeId,
        string fieldId,
        string label,
        ValueKind valueKind)
    {
        var settings = _database.GetEpisodeSettings(episodeId);
        var value = fieldId switch
        {
            "episode.slug" => settings.Slug,
            "episode.sortOrder" => settings.SortOrder.ToString(),
            _ => "",
        };

        return new FieldValue(
            new FieldDefinition(
                fieldId,
                label,
                valueKind,
                IsEditable: true,
                DefaultValue: value),
            value);
    }

    private FieldValue CreatePaletteColorFieldValue(
        string colorId,
        string fieldId,
        string label,
        ValueKind valueKind)
    {
        var settings = _database.GetPaletteColorSettings(colorId);
        var value = fieldId switch
        {
            "palette.token" => settings.Token,
            "palette.valueHex" => settings.ValueHex,
            "palette.isNeutral" => BoolToString(settings.IsNeutral),
            "palette.source" => settings.Source,
            "palette.protected" => BoolToString(settings.IsProtected),
            "palette.hiddenFromPickers" => BoolToString(settings.HiddenFromPickers),
            "palette.note" => settings.Note,
            _ => "",
        };

        return new FieldValue(
            new FieldDefinition(
                fieldId,
                label,
                valueKind,
                IsEditable: true,
                DefaultValue: value),
            value);
    }

    private FieldValue CreateDeviceFieldValue(
        string deviceId,
        string fieldId,
        string label,
        ValueKind valueKind)
    {
        var settings = _database.GetDeviceSettings(deviceId);
        var value = fieldId switch
        {
            "device.manufacturer" => settings.Manufacturer,
            "device.model" => settings.Model,
            "device.osFamily" => settings.OsFamily,
            _ => _database.GetDeviceMetricFieldValue(deviceId, fieldId),
        };

        return new FieldValue(
            new FieldDefinition(
                fieldId,
                label,
                valueKind,
                IsEditable: true,
                DefaultValue: value),
            value);
    }

    private FieldValue CreateActorFieldValue(
        string actorId,
        string fieldId,
        string label,
        ValueKind valueKind)
    {
        var settings = _database.GetActorSettings(actorId);
        var value = _database.GetActorFieldValue(actorId, fieldId);
        var options = fieldId switch
        {
            "actor.defaultDeviceId" => _database.GetDeviceOptions(settings.ProjectId),
            "actor.defaultThemeId" => _database.GetThemeOptions(settings.ProjectId),
            _ => valueKind is ValueKind.PaletteColorToken or ValueKind.PaletteColorPair
                ? _database.GetPaletteColorOptions(settings.ProjectId)
                : null,
        };

        return new FieldValue(
            new FieldDefinition(
                fieldId,
                label,
                valueKind,
                IsEditable: true,
                DefaultValue: ActorFieldDefaultValue(fieldId, value),
                CommitAsDefault: !ActorFieldKeepsDefault(fieldId, valueKind),
                Options: options),
            value);
    }

    private static bool ActorFieldKeepsDefault(string fieldId, ValueKind valueKind)
    {
        return fieldId.StartsWith("actor.avatar.", StringComparison.Ordinal)
            || valueKind == ValueKind.PaletteColorPair;
    }

    private static string ActorFieldDefaultValue(string fieldId, string currentValue)
    {
        return fieldId switch
        {
            "actor.avatar.filePath" => "",
            "actor.avatar.scale" => "1",
            "actor.avatar.offset" => "0|0",
            "actor.avatar.useInitials" => "false",
            "actor.avatar.initialsPadding" => "96",
            _ => currentValue,
        };
    }

    private void PersistFieldValue(ProjectTreeNode node, string fieldId, string value)
    {
        var persisted = node.Kind is ProjectTreeNodeKind.Project
            or ProjectTreeNodeKind.App
            or ProjectTreeNodeKind.Module
            or ProjectTreeNodeKind.Episode
            or ProjectTreeNodeKind.Shot
            or ProjectTreeNodeKind.PaletteColor
            or ProjectTreeNodeKind.Device
            or ProjectTreeNodeKind.Actor;

        if (fieldId == "core.name")
        {
            node.Name = value;
            EditorTitle.Text = value;
        }
        else if (fieldId == "core.notes")
        {
            node.Notes = value;
        }

        if (node.Kind == ProjectTreeNodeKind.Project && fieldId.StartsWith("project.", StringComparison.Ordinal))
        {
            _database.UpdateProjectField(node.Id, fieldId, value);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Episode && fieldId.StartsWith("episode.", StringComparison.Ordinal))
        {
            _database.UpdateEpisodeField(node.Id, fieldId, value);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.PaletteColor && fieldId.StartsWith("palette.", StringComparison.Ordinal))
        {
            _database.UpdatePaletteColorField(node.Id, fieldId, value);
            if (fieldId == "palette.token")
            {
                node.Name = value;
                EditorTitle.Text = value;
                RebuildNavigationCards();
            }
            else if (fieldId == "palette.valueHex")
            {
                node.ColorHex = value;
                RebuildNavigationCards();
            }
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Device && fieldId.StartsWith("device.", StringComparison.Ordinal))
        {
            _database.UpdateDeviceField(node.Id, fieldId, value);
            if (_selectedPreviewDeviceId == node.Id)
            {
                RefreshPreviewDevice();
            }
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Actor && fieldId.StartsWith("actor.", StringComparison.Ordinal))
        {
            _database.UpdateActorField(node.Id, fieldId, value);
            if (fieldId == "actor.shortName")
            {
                node.Notes = value;
                RebuildNavigationCards();
            }
            return;
        }

        if (persisted && fieldId is "core.name" or "core.notes")
        {
            _database.UpdateNode(node);
        }
    }

    private string StoredFieldValue(ProjectTreeNode node, string fieldId, string value)
    {
        if (node.Kind == ProjectTreeNodeKind.Actor && fieldId == "actor.avatar.filePath")
        {
            return RelativeActorMediaPath(node.Id, value) ?? value;
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
            ? BrowseImageFile(currentPath, CurrentMediaRoot())
            : BrowseDirectory(currentPath);
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
        return RelativePathIfInsideMediaRoot(selectedPath, mediaRoot);
    }

    private Control CreateActorAvatarPreview(
        string actorId,
        IReadOnlyDictionary<string, string>? draftValues = null)
    {
        var settings = _database.GetActorSettings(actorId);
        var imagePath = ActorPreviewField(actorId, "actor.avatar.filePath", draftValues);
        var useInitials = StringToBool(ActorPreviewField(actorId, "actor.avatar.useInitials", draftValues));
        var scale = ParseDouble(ActorPreviewField(actorId, "actor.avatar.scale", draftValues), 1);
        var offset = SplitPair(ActorPreviewField(actorId, "actor.avatar.offset", draftValues));
        var offsetX = ParseDouble(offset.First, 0) / 4;
        var offsetY = ParseDouble(offset.Second, 0) / 4;
        var colorPair = SplitPair(ActorPreviewField(actorId, "actor.color.modes", draftValues));
        var textColorPair = SplitPair(ActorPreviewField(actorId, "actor.avatarTextColor.modes", draftValues));
        var paletteOptions = _database.GetPaletteColorOptions(settings.ProjectId);
        var background = PaletteBrush(paletteOptions, colorPair.First, "#808080");
        var foreground = PaletteBrush(paletteOptions, textColorPair.First, "#1A1A1A");

        var viewport = new Border
        {
            Width = 160,
            Height = 160,
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            BorderThickness = new Avalonia.Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse(_isDark ? "#8FA0B8" : "#667085")),
            Background = background,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var fullPath = ResolveLocalPath(imagePath, CurrentMediaRoot());
        if (!useInitials && !string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
        {
            try
            {
                viewport.Child = new Image
                {
                    Source = new Bitmap(fullPath),
                    Width = 160,
                    Height = 160,
                    Stretch = Stretch.UniformToFill,
                    RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                    RenderTransform = new TransformGroup
                    {
                        Children =
                        {
                            new ScaleTransform(scale, scale),
                            new TranslateTransform(offsetX, offsetY),
                        },
                    },
                };
                return WrapAvatarPreview(viewport);
            }
            catch (Exception)
            {
                // If an image format is unsupported in this spike shell, fall back to initials.
            }
        }

        viewport.Child = new TextBlock
        {
            Text = ActorInitials(settings.ShortName, settings.DisplayName),
            Foreground = foreground,
            FontSize = 52,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        return WrapAvatarPreview(viewport);
    }

    private string ActorPreviewField(
        string actorId,
        string fieldId,
        IReadOnlyDictionary<string, string>? draftValues)
    {
        return draftValues is not null && draftValues.TryGetValue(fieldId, out var value)
            ? value
            : _database.GetActorFieldValue(actorId, fieldId);
    }

    private static Control WrapAvatarPreview(Control viewport)
    {
        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Avatar preview · 640×640 crop",
                    FontSize = 12,
                    Opacity = 0.72,
                },
                viewport,
            },
        };
    }

    private string? RelativeActorMediaPath(string actorId, string path)
    {
        var settings = _database.GetActorSettings(actorId);
        var mediaRoot = _database.GetProjectSettings(settings.ProjectId).MediaRoot;
        return RelativePathIfInsideMediaRoot(path, mediaRoot);
    }

    private string? CurrentMediaRoot()
    {
        var node = _selectedNode;
        if (node is null) return null;

        var current = node;
        while (current.Kind != ProjectTreeNodeKind.Project)
        {
            current = current.Parent;
            if (current is null) return null;
        }

        return _database.GetProjectSettings(current.Id).MediaRoot;
    }

    private static string RelativePathIfInsideMediaRoot(string path, string? mediaRoot)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(mediaRoot)) return path;
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(mediaRoot);
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(relative)
            ? path
            : relative;
    }

    private static string? ResolveLocalPath(string path, string? mediaRoot)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        return Path.IsPathFullyQualified(path)
            ? path
            : !string.IsNullOrWhiteSpace(mediaRoot)
                ? Path.GetFullPath(Path.Combine(mediaRoot, path))
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", path));
    }

    private static IBrush PaletteBrush(IReadOnlyList<FieldOption> paletteOptions, string token, string fallback)
    {
        var hex = paletteOptions.FirstOrDefault((option) => option.Value == token)?.ColorHex;
        return SafeColorBrush(hex, fallback);
    }

    private static string ActorInitials(string shortName, string displayName)
    {
        var source = string.IsNullOrWhiteSpace(shortName) ? displayName : shortName;
        var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Concat(parts.Take(2).Select((part) => part[0])).ToUpperInvariant();
    }

    private static double ParseDouble(string value, double fallback)
    {
        return double.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static (string First, string Second) SplitPair(string value)
    {
        var parts = value.Split('|', 2);
        return (parts.ElementAtOrDefault(0) ?? "", parts.ElementAtOrDefault(1) ?? "");
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

    private static string BoolToString(bool value)
    {
        return value ? "true" : "false";
    }

    private static bool StringToBool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
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
    ProductionDataRoot,
    SystemDataRoot,
    AppsRoot,
    PaletteRoot,
    DevicesRoot,
    ActorsRoot,
    EpisodesRoot,
    App,
    Module,
    Episode,
    Shot,
    PaletteColor,
    Device,
    Actor,
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
        or ProjectTreeNodeKind.DevicesRoot
        or ProjectTreeNodeKind.ActorsRoot
        or ProjectTreeNodeKind.EpisodesRoot
        or ProjectTreeNodeKind.Episode;
    public bool CanDuplicate => Kind is ProjectTreeNodeKind.App
        or ProjectTreeNodeKind.Module
        or ProjectTreeNodeKind.Episode
        or ProjectTreeNodeKind.Shot
        or ProjectTreeNodeKind.PaletteColor
        or ProjectTreeNodeKind.Device
        or ProjectTreeNodeKind.Actor;
    public bool CanDelete => Kind is ProjectTreeNodeKind.App
        or ProjectTreeNodeKind.Module
        or ProjectTreeNodeKind.Episode
        or ProjectTreeNodeKind.Shot
        or ProjectTreeNodeKind.PaletteColor
        or ProjectTreeNodeKind.Device
        or ProjectTreeNodeKind.Actor;
    public bool CanOpenEditor => Kind is not ProjectTreeNodeKind.ProductionDataRoot
        and not ProjectTreeNodeKind.SystemDataRoot
        and not ProjectTreeNodeKind.AppsRoot
        and not ProjectTreeNodeKind.PaletteRoot
        and not ProjectTreeNodeKind.DevicesRoot
        and not ProjectTreeNodeKind.ActorsRoot
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
            ProjectTreeNodeKind.DevicesRoot => "navigation.devices",
            ProjectTreeNodeKind.ActorsRoot => "navigation.actors",
            ProjectTreeNodeKind.EpisodesRoot => "navigation.episodes",
            ProjectTreeNodeKind.App => "app.generic",
            ProjectTreeNodeKind.Module => "module.generic",
            ProjectTreeNodeKind.Episode => "episode",
            ProjectTreeNodeKind.Shot => "shot",
            ProjectTreeNodeKind.PaletteColor => "palette_color",
            ProjectTreeNodeKind.Device => "device",
            ProjectTreeNodeKind.Actor => "actor",
            _ => throw new InvalidOperationException($"No record class for {kind}."),
        };
    }
}
