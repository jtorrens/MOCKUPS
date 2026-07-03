using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class IconTokenPickerDialog
{
    private readonly Window _owner;
    private readonly SpikeDatabase _database;

    public IconTokenPickerDialog(Window owner, SpikeDatabase database)
    {
        _owner = owner;
        _database = database;
    }

    public async Task<string?> Show(string projectId, string currentValue, bool allowMultiple)
    {
        var iconThemes = _database.GetIconThemeOptions(projectId)
            .Where((option) => !string.IsNullOrWhiteSpace(option.Value))
            .ToList();
        var selected = currentValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var selectedSet = selected.ToHashSet(StringComparer.Ordinal);
        var selectedThemeId = iconThemes.FirstOrDefault()?.Value ?? "";
        string query = "";
        string? result = null;

        var dialog = new SukiWindow
        {
            Title = allowMultiple ? "Select icon tokens" : "Select icon token",
            Width = 720,
            Height = 680,
            MinWidth = 560,
            MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
        };

        var themeCombo = new ComboBox
        {
            MinWidth = 220,
            ItemsSource = iconThemes,
            SelectedItem = iconThemes.FirstOrDefault((option) => option.Value == selectedThemeId),
        };
        EditorComboBoxBehavior.Configure(themeCombo);
        var searchBox = new TextBox
        {
            PlaceholderText = "Search icons…",
            MinWidth = 220,
        };
        var selectedText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.78,
        };
        var listPanel = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        void RefreshSelectedText()
        {
            selectedText.Text = selected.Count == 0
                ? "No icon selected"
                : string.Join(", ", selected);
        }

        void ToggleToken(string token)
        {
            if (!allowMultiple)
            {
                selected.Clear();
                selectedSet.Clear();
                selected.Add(token);
                selectedSet.Add(token);
                RefreshList();
                return;
            }

            if (selectedSet.Contains(token))
            {
                selectedSet.Remove(token);
                selected.RemoveAll((entry) => entry == token);
            }
            else
            {
                selectedSet.Add(token);
                selected.Add(token);
            }

            RefreshList();
        }

        void RefreshList()
        {
            listPanel.Children.Clear();
            RefreshSelectedText();
            if (string.IsNullOrWhiteSpace(selectedThemeId))
            {
                listPanel.Children.Add(new TextBlock { Text = "No icon themes available. Refresh icon sets first.", Opacity = 0.72 });
                return;
            }

            var normalizedQuery = query.Trim();
            var tokens = _database.GetIconThemeTokens(selectedThemeId)
                .Where((token) => string.IsNullOrWhiteSpace(normalizedQuery)
                    || token.Token.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || token.Category.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                .OrderBy((token) => token.Token, StringComparer.Ordinal)
                .ToList();

            foreach (var token in tokens)
            {
                var isSelected = selectedSet.Contains(token.Token);
                var content = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("72,180"),
                    ColumnSpacing = 10,
                    MinHeight = 40,
                };
                var iconZone = new Border
                {
                    Width = 72,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = SvgIconPreview.CreateIconThemePreview(_database, selectedThemeId, token.File, 24),
                };
                content.Children.Add(iconZone);
                var textZone = new StackPanel
                {
                    Spacing = 1,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                        {
                            new TextBlock
                            {
                                Text = token.Token,
                                FontWeight = FontWeight.SemiBold,
                                TextTrimming = TextTrimming.CharacterEllipsis,
                                Width = 180,
                            },
                            new TextBlock
                            {
                                Text = token.Category,
                                FontSize = 11,
                                Opacity = 0.65,
                                Width = 180,
                                TextTrimming = TextTrimming.CharacterEllipsis,
                            },
                        },
                };
                Grid.SetColumn(textZone, 1);
                content.Children.Add(textZone);

                var button = new Button
                {
                    Content = content,
                    Width = 285,
                    MinHeight = 52,
                    Margin = new Thickness(0, 0, 8, 8),
                    BorderThickness = isSelected ? new Thickness(2) : new Thickness(1),
                    BorderBrush = isSelected
                        ? new SolidColorBrush(Color.Parse("#3388FF"))
                        : new SolidColorBrush(Color.Parse("#4B5F7A")),
                };
                button.Click += (_, _) => ToggleToken(token.Token);
                listPanel.Children.Add(button);
            }

            if (listPanel.Children.Count == 0)
            {
                listPanel.Children.Add(new TextBlock { Text = "No icons match the current search.", Opacity = 0.72 });
            }
        }

        themeCombo.SelectionChanged += (_, _) =>
        {
            if (themeCombo.SelectedItem is FieldOption option)
            {
                selectedThemeId = option.Value;
                RefreshList();
            }
        };
        searchBox.TextChanged += (_, _) =>
        {
            query = searchBox.Text ?? "";
            RefreshList();
        };

        var cancelButton = new Button { Content = "Cancel", MinWidth = 90 };
        cancelButton.Click += (_, _) => dialog.Close();
        var okButton = new Button { Content = "Apply", MinWidth = 90 };
        okButton.Click += (_, _) =>
        {
            result = allowMultiple ? string.Join(",", selected) : selected.FirstOrDefault() ?? "";
            dialog.Close();
        };

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            RowSpacing = 14,
        };
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children =
            {
                new StackPanel { Spacing = 4, Children = { new TextBlock { Text = "Icon theme" }, themeCombo } },
                new StackPanel { Spacing = 4, Children = { new TextBlock { Text = "Search" }, searchBox } },
            },
        };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancelButton, okButton },
        };

        Grid.SetRow(toolbar, 0);
        root.Children.Add(toolbar);
        Grid.SetRow(selectedText, 1);
        root.Children.Add(selectedText);
        Grid.SetRow(actions, 3);
        root.Children.Add(actions);
        var scroll = new ScrollViewer
        {
            Content = listPanel,
        };
        Grid.SetRow(scroll, 2);
        root.Children.Add(scroll);

        dialog.Content = new Border
        {
            Padding = new Thickness(18),
            Child = root,
        };

        RefreshList();
        await dialog.ShowDialog(_owner);
        return result;
    }
}
