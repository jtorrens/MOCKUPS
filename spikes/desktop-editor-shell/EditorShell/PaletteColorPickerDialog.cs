using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class PaletteColorPickerDialog
{
    private const int DefaultColumnCount = 3;
    private const double ItemWidth = 210;
    private const double ItemGap = 8;
    private const double DialogPadding = 18;
    private const double ScrollBarAllowance = 18;

    public static async Task<string?> Show(
        Window owner,
        string title,
        IReadOnlyList<FieldOption> options,
        string currentValue)
    {
        var selected = currentValue;
        var query = "";
        string? result = null;
        var listWidth = (DefaultColumnCount * ItemWidth) + ((DefaultColumnCount - 1) * ItemGap);
        var dialogWidth = listWidth + (DialogPadding * 2) + ScrollBarAllowance;

        var dialog = new SukiWindow
        {
            Title = title,
            Width = dialogWidth,
            Height = 620,
            MinWidth = dialogWidth,
            MaxWidth = dialogWidth,
            MinHeight = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };

        var searchBox = EditorTextBoxBehavior.Configure(new TextBox
        {
            PlaceholderText = "Search palette colors...",
            MinWidth = 260,
        });
        var selectedText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.78,
        };
        var listPanel = new Grid
        {
            Width = listWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            ColumnDefinitions = PaletteColumns(),
        };

        void RefreshSelectedText()
        {
            selectedText.Text = string.IsNullOrWhiteSpace(selected)
                ? "No palette color selected"
                : $"Selected: {selected}";
        }

        void RefreshList()
        {
            listPanel.Children.Clear();
            listPanel.RowDefinitions.Clear();
            RefreshSelectedText();

            var visibleOptions = options
                .Where((option) => EditorSearchMatcher.Matches(
                    query,
                    option.Value,
                    option.Label,
                    option.ColorHex,
                    option.IsNeutral ? "neutral" : ""))
                .OrderBy((option) => option.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var option in visibleOptions)
            {
                var optionIndex = listPanel.Children.Count;
                var row = optionIndex / DefaultColumnCount;
                var column = optionIndex % DefaultColumnCount;
                EnsureRow(listPanel, row);

                var isSelected = string.Equals(option.Value, selected, StringComparison.Ordinal);
                var content = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("34,150"),
                    ColumnSpacing = 10,
                    MinHeight = 38,
                };
                content.Children.Add(Swatch(option.ColorHex, 28, option.IsNeutral));

                var text = new StackPanel
                {
                    Spacing = 1,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = option.Label,
                            FontWeight = FontWeight.SemiBold,
                            Width = 150,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                        },
                        new TextBlock
                        {
                            Text = option.ColorHex ?? "",
                            FontSize = 11,
                            Opacity = 0.65,
                            Width = 150,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                        },
                    },
                };
                Grid.SetColumn(text, 1);
                content.Children.Add(text);

                var button = new Button
                {
                    Content = content,
                    Width = 210,
                    MinHeight = 52,
                    Margin = new Thickness(
                        0,
                        0,
                        column == DefaultColumnCount - 1 ? 0 : ItemGap,
                        ItemGap),
                    BorderThickness = isSelected ? new Thickness(2) : new Thickness(1),
                    BorderBrush = isSelected
                        ? new SolidColorBrush(Color.Parse("#3388FF"))
                        : new SolidColorBrush(Color.Parse("#4B5F7A")),
                };
                button.Click += (_, _) =>
                {
                    selected = option.Value;
                    RefreshList();
                };
                Grid.SetRow(button, row);
                Grid.SetColumn(button, column);
                listPanel.Children.Add(button);
            }

            if (listPanel.Children.Count == 0)
            {
                listPanel.Children.Add(new TextBlock { Text = "No palette colors match the current search.", Opacity = 0.72 });
            }
        }

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
            result = selected;
            dialog.Close();
        };

        var toolbar = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = "Search" },
                searchBox,
            },
        };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancelButton, applyButton },
        };
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            RowSpacing = 14,
            Children =
            {
                toolbar,
                selectedText,
                new ScrollViewer { Content = listPanel },
                actions,
            },
        };
        Grid.SetRow(selectedText, 1);
        Grid.SetRow((Control)root.Children[2], 2);
        Grid.SetRow(actions, 3);

        dialog.Content = new Border
        {
            Padding = new Thickness(DialogPadding),
            Child = root,
        };

        RefreshList();
        await dialog.ShowDialog(owner);
        return result;
    }

    public static Border Swatch(string? hex, double size, bool isNeutral = false)
    {
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(5),
            Background = DictionaryFieldColorValue.SafeBrush(hex, "#808080"),
            BorderBrush = new SolidColorBrush(Color.Parse("#667085")),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = isNeutral
                ? new TextBlock
                {
                    Text = "N",
                    FontSize = Math.Max(9, size * 0.42),
                    FontWeight = FontWeight.Bold,
                    Foreground = ContrastBrush(hex),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                }
                : null,
        };
    }

    private static IBrush ContrastBrush(string? hex)
    {
        return ColorValue.ContrastBrush(hex);
    }

    private static ColumnDefinitions PaletteColumns()
    {
        var columns = new ColumnDefinitions();
        for (var i = 0; i < DefaultColumnCount; i++)
        {
            columns.Add(new ColumnDefinition(new GridLength(ItemWidth + (i == DefaultColumnCount - 1 ? 0 : ItemGap))));
        }

        return columns;
    }

    private static void EnsureRow(Grid grid, int row)
    {
        while (grid.RowDefinitions.Count <= row)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
    }
}
