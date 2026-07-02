using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class IconThemeTokensCollectionEditor
{
    private readonly SpikeDatabase _database;
    private readonly bool _isDark;
    private readonly Func<string, string, Task> _showInfo;
    private readonly Func<string, Task<bool>> _confirmDelete;
    private readonly Func<ProjectTreeNode, Task> _showSearch;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;

    public IconThemeTokensCollectionEditor(
        SpikeDatabase database,
        bool isDark,
        Func<string, string, Task> showInfo,
        Func<string, Task<bool>> confirmDelete,
        Func<ProjectTreeNode, Task> showSearch,
        Action<ProjectTreeNode> reloadAndSelect)
    {
        _database = database;
        _isDark = isDark;
        _showInfo = showInfo;
        _confirmDelete = confirmDelete;
        _showSearch = showSearch;
        _reloadAndSelect = reloadAndSelect;
    }

    public Expander Create(ProjectTreeNode node)
    {
        var icon = EditorIcons.Create(EditorIcons.Icon, 18);
        var tokensPanel = new StackPanel
        {
            Spacing = 10,
        };
        tokensPanel.Children.Add(CreateToolbar(node));

        var tokens = _database.GetIconThemeTokens(node.Id);
        if (tokens.Count == 0)
        {
            tokensPanel.Children.Add(new TextBlock
            {
                Text = "No icon tokens yet. Use Refresh Sets first.",
                Opacity = 0.72,
            });
        }
        else
        {
            foreach (var token in tokens)
            {
                tokensPanel.Children.Add(CreateTokenRow(node, token));
            }
        }

        return new Expander
        {
            Header = EditorCardHeader.Create("Icon Tokens", $"{tokens.Count} semantic tokens", icon),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ExpandDirection = ExpandDirection.Down,
            IsExpanded = false,
            Content = new Border
            {
                Padding = new Thickness(10),
                Child = tokensPanel,
            },
        };
    }

    private Control CreateToolbar(ProjectTreeNode node)
    {
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var refreshButton = new Button
        {
            Content = "Refresh sets",
        };
        refreshButton.Click += async (_, _) =>
        {
            try
            {
                var result = _database.RefreshIconThemeSetsForTheme(node.Id);
                await _showInfo("Refresh complete", $"Refreshed {result.CommonTokenCount} common token(s) across {result.ThemeCount} icon set(s). Omitted {result.OmittedTokenCount} token(s).");
                _reloadAndSelect(node);
            }
            catch (Exception exception)
            {
                await _showInfo("Refresh failed", exception.Message);
            }
        };

        var searchButton = new Button
        {
            Content = "Search / add token",
        };
        searchButton.Click += async (_, _) => await _showSearch(node);

        toolbar.Children.Add(refreshButton);
        toolbar.Children.Add(searchButton);
        return toolbar;
    }

    private Control CreateTokenRow(ProjectTreeNode node, SpikeDatabase.IconThemeToken token)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("34,*,90,Auto"),
            ColumnSpacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var preview = SvgIconPreview.CreateIconThemePreview(_database, node.Id, token.File, 24);
        Grid.SetColumn(preview, 0);

        var text = new StackPanel
        {
            Spacing = 1,
            Children =
            {
                new TextBlock
                {
                    Text = token.Token,
                    FontWeight = FontWeight.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(token.Description) ? token.File : token.Description,
                    FontSize = 12,
                    Opacity = 0.72,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            },
        };
        Grid.SetColumn(text, 1);

        var category = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(token.Category) ? "misc" : token.Category,
            Opacity = 0.82,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(category, 2);

        var deleteButton = new Button
        {
            Content = EditorIcons.Create(EditorIcons.Delete, 14),
            Width = 30,
            Height = 30,
            Padding = new Thickness(0),
        };
        deleteButton.Click += async (_, _) =>
        {
            var confirmed = await _confirmDelete(token.Token);
            if (!confirmed) return;

            try
            {
                _database.DeleteIconThemeToken(node.Id, token.Token);
                _reloadAndSelect(node);
            }
            catch (Exception exception)
            {
                await _showInfo("Delete failed", exception.Message);
            }
        };
        Grid.SetColumn(deleteButton, 3);

        grid.Children.Add(preview);
        grid.Children.Add(text);
        grid.Children.Add(category);
        grid.Children.Add(deleteButton);
        return new Border
        {
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(10),
            BorderBrush = new SolidColorBrush(Color.Parse(_isDark ? "#44546A" : "#D0D7E2")),
            BorderThickness = new Thickness(1),
            Child = grid,
        };
    }
}
