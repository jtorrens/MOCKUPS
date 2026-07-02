using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class IconThemeSearchDialog
{
    private readonly Window _owner;
    private readonly SpikeDatabase _database;
    private readonly Func<string, string, Task> _showInfo;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;

    public IconThemeSearchDialog(
        Window owner,
        SpikeDatabase database,
        Func<string, string, Task> showInfo,
        Action<ProjectTreeNode> reloadAndSelect)
    {
        _owner = owner;
        _database = database;
        _showInfo = showInfo;
        _reloadAndSelect = reloadAndSelect;
    }

    public async Task Show(ProjectTreeNode node)
    {
        var dialog = new SukiWindow
        {
            Title = "Search / add icon token",
            Width = 760,
            Height = 660,
            MinWidth = 720,
            MinHeight = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
        };

        var queryBox = new TextBox { PlaceholderText = "telephone" };
        var tokenBox = new TextBox { PlaceholderText = "phone_call" };
        var categoryBox = new TextBox { PlaceholderText = "phone" };
        var descriptionBox = new TextBox
        {
            PlaceholderText = "Phone call icon",
            AcceptsReturn = true,
            MinHeight = 70,
        };
        var lucideList = new ListBox { MinHeight = 190, MaxHeight = 230 };
        var materialList = new ListBox { MinHeight = 190, MaxHeight = 230 };
        var errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#E8A1A8")),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };

        void SetError(string message)
        {
            errorText.Text = message;
            errorText.IsVisible = !string.IsNullOrWhiteSpace(message);
        }

        var searchButton = new Button { Content = "Search", MinWidth = 90 };
        searchButton.Click += (_, _) =>
        {
            SetError("");
            try
            {
                var result = _database.SearchIconThemeSources(queryBox.Text ?? "");
                lucideList.ItemsSource = result.Lucide;
                materialList.ItemsSource = result.Material;
                lucideList.SelectedIndex = result.Lucide.Count > 0 ? 0 : -1;
                materialList.SelectedIndex = result.Material.Count > 0 ? 0 : -1;
                if (string.IsNullOrWhiteSpace(tokenBox.Text))
                {
                    tokenBox.Text = TokenFromText(queryBox.Text ?? "");
                }
                if (string.IsNullOrWhiteSpace(categoryBox.Text))
                {
                    categoryBox.Text = CategoryFromToken(tokenBox.Text ?? "");
                }
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
            }
        };

        var generateButton = new Button { Content = "Generate", MinWidth = 100 };
        generateButton.Click += async (_, _) =>
        {
            SetError("");
            try
            {
                var lucide = lucideList.SelectedItem as SpikeDatabase.IconThemeSearchCandidate;
                var material = materialList.SelectedItem as SpikeDatabase.IconThemeSearchCandidate;
                if (lucide is null || material is null)
                {
                    SetError("Select one Lucide source and one Material source.");
                    return;
                }

                var result = _database.GenerateIconThemeToken(
                    node.Id,
                    TokenFromText(tokenBox.Text ?? ""),
                    TokenFromText(categoryBox.Text ?? ""),
                    descriptionBox.Text ?? "",
                    lucide.SourceName,
                    material.SourceName);
                dialog.Close();
                await _showInfo("Generate complete", $"Generated “{result.Token}” in {result.WrittenFileCount} set(s). Refreshed {result.RefreshResult.CommonTokenCount} common token(s).");
                _reloadAndSelect(node);
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
            }
        };

        var cancelButton = new Button { Content = "Cancel", MinWidth = 92 };
        cancelButton.Click += (_, _) => dialog.Close();

        var actionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancelButton, generateButton },
        };
        var contentStack = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = "Search provider icons, select one Lucide and one Material source, then generate a shared MOCKUPS token.",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.8,
                },
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 8,
                    Children =
                    {
                        queryBox,
                        searchButton,
                    },
                },
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    ColumnSpacing = 12,
                    Children =
                    {
                        CandidateColumn("Lucide", lucideList),
                        CandidateColumn("Material", materialList, column: 1),
                    },
                },
                LabeledControl("MOCKUPS token", tokenBox),
                LabeledControl("Category", categoryBox),
                LabeledControl("Description", descriptionBox),
                errorText,
            },
        };
        var dialogGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 14,
            Children =
            {
                new ScrollViewer
                {
                    Content = contentStack,
                },
                actionRow,
            },
        };
        Grid.SetColumn(searchButton, 1);
        Grid.SetRow(actionRow, 1);

        dialog.Content = new Border
        {
            Padding = new Thickness(18),
            Child = dialogGrid,
        };

        await dialog.ShowDialog(_owner);
    }

    private static Control CandidateColumn(string title, ListBox listBox, int column = 0)
    {
        listBox.ItemTemplate = new FuncDataTemplate<SpikeDatabase.IconThemeSearchCandidate>((candidate, _) =>
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("34,*"),
                ColumnSpacing = 10,
            };
            row.Children.Add(SvgIconPreview.CreateSearchPreview(candidate?.PreviewUrl ?? "", 22));
            var text = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock
                    {
                        Text = candidate?.SourceName ?? "",
                        FontWeight = FontWeight.SemiBold,
                    },
                    new TextBlock
                    {
                        Text = candidate?.Provider ?? "",
                        FontSize = 11,
                        Opacity = 0.65,
                    },
                },
            };
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            return row;
        });
        var panel = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = title, FontWeight = FontWeight.SemiBold },
                listBox,
            },
        };
        Grid.SetColumn(panel, column);
        return panel;
    }

    private static Control LabeledControl(string label, Control control)
    {
        return new StackPanel
        {
            Spacing = 5,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 12,
                    Opacity = 0.78,
                },
                control,
            },
        };
    }

    private static string TokenFromText(string value)
    {
        var token = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9_]+", "_");
        token = Regex.Replace(token, "_+", "_").Trim('_');
        return token;
    }

    private static string CategoryFromToken(string token)
    {
        var index = token.IndexOf('_', StringComparison.Ordinal);
        return index <= 0 ? "misc" : token[..index];
    }
}
