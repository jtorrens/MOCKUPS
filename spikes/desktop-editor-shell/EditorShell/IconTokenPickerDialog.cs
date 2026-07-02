using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
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
        var tokens = _database.GetIconTokenOptions(projectId, currentValue);
        var selected = currentValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var dialog = new SukiWindow
        {
            Title = allowMultiple ? "Select icon tokens" : "Select icon token",
            Width = 520,
            Height = 640,
            MinWidth = 460,
            MinHeight = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
        };

        var list = new StackPanel { Spacing = 5 };
        foreach (var option in tokens)
        {
            var optionContent = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("30,*"),
                ColumnSpacing = 10,
                MinHeight = 34,
            };
            optionContent.Children.Add(SvgIconPreview.CreateProjectIconTokenPreview(_database, projectId, option.Value, 20));
            var optionLabel = new TextBlock
            {
                Text = option.Label,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(optionLabel, 1);
            optionContent.Children.Add(optionLabel);

            var checkBox = new CheckBox
            {
                Content = optionContent,
                IsChecked = selected.Contains(option.Value),
            };
            checkBox.PropertyChanged += (_, change) =>
            {
                if (change.Property != CheckBox.IsCheckedProperty) return;
                if (checkBox.IsChecked == true)
                {
                    if (!allowMultiple)
                    {
                        selected.Clear();
                        foreach (var sibling in list.Children.OfType<CheckBox>().Where((item) => item != checkBox))
                        {
                            sibling.IsChecked = false;
                        }
                    }

                    selected.Add(option.Value);
                }
                else
                {
                    selected.Remove(option.Value);
                }
            };
            list.Children.Add(checkBox);
        }

        string? result = null;
        var cancelButton = new Button { Content = "Cancel", MinWidth = 90 };
        cancelButton.Click += (_, _) => dialog.Close();
        var okButton = new Button { Content = "OK", MinWidth = 90 };
        okButton.Click += (_, _) =>
        {
            result = string.Join(",", selected.OrderBy((token) => token, StringComparer.Ordinal));
            dialog.Close();
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancelButton, okButton },
        };
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 14,
            Children =
            {
                new ScrollViewer
                {
                    Content = list.Children.Count == 0
                        ? new TextBlock
                        {
                            Text = "No icon tokens available. Refresh icon themes first.",
                            Opacity = 0.72,
                        }
                        : list,
                },
                actions,
            },
        };
        Grid.SetRow(actions, 1);
        dialog.Content = new Border
        {
            Padding = new Thickness(18),
            Child = root,
        };

        await dialog.ShowDialog(_owner);
        return result;
    }
}
