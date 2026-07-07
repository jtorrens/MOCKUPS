using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ThemeTokenPickerDialog
{
    private readonly Window _owner;
    private readonly SpikeDatabase _database;

    public ThemeTokenPickerDialog(Window owner, SpikeDatabase database)
    {
        _owner = owner;
        _database = database;
    }

    public async Task<string?> Show(string projectId, string currentValue, IReadOnlyList<FieldOption>? allowedOptions)
    {
        var allowedTokens = allowedOptions is { Count: > 0 }
            ? allowedOptions.Select((option) => option.Value).ToHashSet(StringComparer.Ordinal)
            : null;
        var themes = _database.GetThemeOptions(projectId)
            .Where((option) => !string.IsNullOrWhiteSpace(option.Value))
            .ToList();
        var selectedThemeId = themes.FirstOrDefault()?.Value ?? "";
        var draft = currentValue;
        var query = "";
        string? result = null;

        var dialog = new SukiWindow
        {
            Title = "Select theme token",
            Width = 700,
            Height = 640,
            MinWidth = 540,
            MinHeight = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);

        var themeCombo = new EditorInstantComboBox
        {
            MinWidth = 220,
            ItemsSource = themes,
            SelectedItem = themes.FirstOrDefault((option) => option.Value == selectedThemeId),
        };
        var searchBox = EditorTextBoxBehavior.Configure(new TextBox
        {
            PlaceholderText = "Search tokens…",
            MinWidth = 220,
        });
        var selectedText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.78,
        };
        var listPanel = new StackPanel
        {
            Spacing = 7,
        };

        void RefreshSelectedText()
        {
            selectedText.Text = string.IsNullOrWhiteSpace(draft)
                ? "No theme token selected"
                : $"Selected: {draft}";
        }

        void RefreshList()
        {
            listPanel.Children.Clear();
            RefreshSelectedText();
            if (string.IsNullOrWhiteSpace(selectedThemeId))
            {
                listPanel.Children.Add(new TextBlock { Text = "No themes available.", Opacity = 0.72 });
                return;
            }

            var options = _database.GetThemeTokenOptions(projectId, selectedThemeId)
                .Where((option) => allowedTokens is null || allowedTokens.Contains(option.Token))
                .Where((option) => EditorSearchMatcher.Matches(
                    query,
                    option.Token,
                    option.Label,
                    option.Kind,
                    option.Value,
                    option.LightColorHex,
                    option.DarkColorHex))
                .ToList();

            foreach (var option in options)
            {
                var selected = string.Equals(option.Token, draft, StringComparison.Ordinal);
                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("72,260,150"),
                    ColumnSpacing = 14,
                    MinHeight = 46,
                };
                var previewZone = new Border
                {
                    Width = 72,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = PreviewFor(option),
                };
                row.Children.Add(previewZone);
                var text = new StackPanel
                {
                    Spacing = 1,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = option.Token,
                            FontWeight = FontWeight.SemiBold,
                            Width = 260,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                        },
                        new TextBlock
                        {
                            Text = option.Kind,
                            FontSize = 11,
                            Opacity = 0.65,
                            Width = 260,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                        },
                    },
                };
                Grid.SetColumn(text, 1);
                row.Children.Add(text);
                var value = new TextBlock
                {
                    Text = option.Value,
                    Opacity = 0.78,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = 150,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                Grid.SetColumn(value, 2);
                row.Children.Add(value);

                var button = new Button
                {
                    Content = row,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    BorderThickness = selected ? new Thickness(2) : new Thickness(1),
                    BorderBrush = selected
                        ? EditorSukiWindowTheme.AccentBrush()
                        : new SolidColorBrush(Color.Parse("#4B5F7A")),
                };
                button.Click += (_, _) =>
                {
                    draft = option.Token;
                    RefreshList();
                };
                listPanel.Children.Add(button);
            }

            if (listPanel.Children.Count == 0)
            {
                listPanel.Children.Add(new TextBlock { Text = "No theme tokens match the current search.", Opacity = 0.72 });
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
        var applyButton = new Button { Content = "Apply", MinWidth = 90 };
        applyButton.Click += (_, _) =>
        {
            result = draft;
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
                new StackPanel { Spacing = 4, Children = { new TextBlock { Text = "Theme" }, themeCombo } },
                new StackPanel { Spacing = 4, Children = { new TextBlock { Text = "Search" }, searchBox } },
            },
        };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancelButton, applyButton },
        };
        var scroll = new ScrollViewer { Content = listPanel };

        Grid.SetRow(toolbar, 0);
        root.Children.Add(toolbar);
        Grid.SetRow(selectedText, 1);
        root.Children.Add(selectedText);
        Grid.SetRow(scroll, 2);
        root.Children.Add(scroll);
        Grid.SetRow(actions, 3);
        root.Children.Add(actions);

        dialog.Content = new Border
        {
            Padding = new Thickness(18),
            Child = root,
        };

        RefreshList();
        await dialog.ShowDialog(_owner);
        return result;
    }

    private static Control PreviewFor(SpikeDatabase.ThemeTokenOption option)
    {
        if (option.Kind == "color")
        {
            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    Swatch(option.LightColorHex),
                    Swatch(option.DarkColorHex),
                },
            };
        }

        return new Border
        {
            Width = 40,
            Height = 24,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#4B5F7A")),
            Child = new TextBlock
            {
                Text = option.Value,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }

    private static Border Swatch(string? hex)
    {
        return new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(5),
            Background = SafeBrush(hex),
            BorderBrush = new SolidColorBrush(Color.Parse("#667085")),
            BorderThickness = new Thickness(1),
        };
    }

    private static IBrush SafeBrush(string? hex)
    {
        return ColorValue.SafeBrush(hex, "#808080");
    }
}
