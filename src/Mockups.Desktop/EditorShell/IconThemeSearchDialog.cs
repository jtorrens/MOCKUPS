using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class IconThemeSearchDialog
{
    private readonly Window _owner;
    private readonly IIconThemeAssetStore _database;
    private readonly EditorOperationCoordinator _operations;
    private readonly Func<string, string, Task> _showInfo;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;

    public IconThemeSearchDialog(
        Window owner,
        IIconThemeAssetStore database,
        EditorOperationCoordinator operations,
        Func<string, string, Task> showInfo,
        Action<ProjectTreeNode> reloadAndSelect)
    {
        _owner = owner;
        _database = database;
        _operations = operations;
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
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);

        var queryBox = EditorTextBoxBehavior.Configure(new TextBox { PlaceholderText = "telephone" });
        var tokenBox = EditorTextBoxBehavior.Configure(new TextBox { PlaceholderText = "phone_call" });
        var categoryBox = EditorTextBoxBehavior.Configure(new TextBox { PlaceholderText = "phone" });
        var descriptionBox = EditorTextBoxBehavior.Configure(new TextBox
        {
            PlaceholderText = "Phone call icon",
            AcceptsReturn = true,
            MinHeight = 70,
        });
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
        var generateButton = new Button { Content = "Generate", MinWidth = 100, IsEnabled = false };
        var busyArea = EditorBusyOverlay.Create(new Border());
        CancellationTokenSource? activeOperation = null;
        var isDialogClosed = false;

        void UpdateGenerateAvailability() => generateButton.IsEnabled = activeOperation is null
            && !string.IsNullOrWhiteSpace(TokenFromText(tokenBox.Text ?? ""))
            && (lucideList.SelectedItem is IconThemeSearchCandidate || materialList.SelectedItem is IconThemeSearchCandidate);
        lucideList.SelectionChanged += (_, _) => UpdateGenerateAvailability();
        materialList.SelectionChanged += (_, _) => UpdateGenerateAvailability();
        tokenBox.TextChanged += (_, _) => UpdateGenerateAvailability();

        CancellationTokenSource BeginBusy(string message)
        {
            activeOperation?.Cancel();
            activeOperation?.Dispose();
            var cancellation = new CancellationTokenSource();
            activeOperation = cancellation;
            searchButton.IsEnabled = false;
            generateButton.IsEnabled = false;
            EditorBusyOverlay.SetBusy(busyArea, true, message);
            return cancellation;
        }

        void EndBusy(CancellationTokenSource cancellation)
        {
            if (activeOperation != cancellation)
            {
                return;
            }

            activeOperation = null;
            cancellation.Dispose();
            if (isDialogClosed)
            {
                return;
            }

            searchButton.IsEnabled = true;
            UpdateGenerateAvailability();
            EditorBusyOverlay.SetBusy(busyArea, false);
        }

        searchButton.Click += async (_, _) =>
        {
            SetError("");
            var query = queryBox.Text ?? "";
            var cancellation = BeginBusy("Searching icon providers...");
            PreviewDebugLog.Write("icon.search.start", ("query", query));
            try
            {
                var result = await _operations.ExecuteAsync(
                    () => _database.SearchIconThemeSources(query, cancellation.Token),
                    cancellation.Token);
                if (cancellation.IsCancellationRequested || isDialogClosed)
                {
                    return;
                }

                lucideList.ItemsSource = result.Lucide;
                materialList.ItemsSource = result.Material;
                lucideList.SelectedIndex = result.Lucide.Count > 0 ? 0 : -1;
                materialList.SelectedIndex = result.Material.Count > 0 ? 0 : -1;
                PreviewDebugLog.Write(
                    "icon.search.end",
                    ("query", query),
                    ("lucide", result.Lucide.Count),
                    ("material", result.Material.Count));
                if (string.IsNullOrWhiteSpace(tokenBox.Text))
                {
                    tokenBox.Text = TokenFromText(query);
                }
                if (string.IsNullOrWhiteSpace(categoryBox.Text))
                {
                    categoryBox.Text = CategoryFromToken(tokenBox.Text ?? "");
                }
            }
            catch (OperationCanceledException)
            {
                PreviewDebugLog.Write("icon.search.cancelled", ("query", query));
            }
            catch (Exception exception)
            {
                PreviewDebugLog.Write("icon.search.error", ("message", exception.Message));
                SetError(exception.Message);
            }
            finally
            {
                EndBusy(cancellation);
            }
        };

        generateButton.Click += async (_, _) =>
        {
            SetError("");
            var token = tokenBox.Text ?? "";
            var category = categoryBox.Text ?? "";
            var description = descriptionBox.Text ?? "";
            var cancellation = BeginBusy("Generating icon token in every icon set...");
            PreviewDebugLog.Write("icon.generate.start", ("token", token));
            try
            {
                var lucide = lucideList.SelectedItem as IconThemeSearchCandidate;
                var material = materialList.SelectedItem as IconThemeSearchCandidate;
                if (lucide is null && material is null)
                {
                    SetError("Select at least one source. Its SVG will be copied to collections without an equivalent.");
                    return;
                }

                var result = await _operations.ExecuteAsync(
                    () => _database.GenerateIconThemeToken(
                        node.Id,
                        TokenFromText(token),
                        TokenFromText(category),
                        description,
                        lucide?.SourceName ?? "",
                        material?.SourceName ?? "",
                        cancellation.Token),
                    cancellation.Token);
                if (cancellation.IsCancellationRequested || isDialogClosed)
                {
                    return;
                }

                PreviewDebugLog.Write(
                    "icon.generate.end",
                    ("token", result.Token),
                    ("written", result.WrittenFileCount),
                    ("themes", result.RefreshResult.ThemeCount),
                    ("tokens", result.RefreshResult.TokenCount));
                EditorModalWindowScope.Close(dialog);
                await _showInfo("Generate complete", $"Saved “{result.Token}” in {result.WrittenFileCount} set(s). {result.CopiedFileCount} explicit copy/copies for collections without an equivalent.");
                _reloadAndSelect(node);
            }
            catch (OperationCanceledException)
            {
                PreviewDebugLog.Write("icon.generate.cancelled", ("token", token));
            }
            catch (Exception exception)
            {
                PreviewDebugLog.Write("icon.generate.error", ("message", exception.Message));
                SetError(exception.Message);
            }
            finally
            {
                EndBusy(cancellation);
            }
        };

        var cancelButton = new Button { Content = "Cancel", MinWidth = 92 };
        cancelButton.Click += (_, _) =>
        {
            activeOperation?.Cancel();
            EditorModalWindowScope.Close(dialog);
        };
        EditorModalWindowScope.OnClosed(dialog, () =>
        {
            isDialogClosed = true;
            activeOperation?.Cancel();
        });

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
                    Text = "Select one or both providers. Without an equivalent, the available SVG is copied into the other collections using the same token.",
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
        busyArea.Content = new ScrollViewer
        {
            Content = contentStack,
        };
        var dialogGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 14,
            Children =
            {
                busyArea,
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

        await EditorModalWindowScope.ShowDialog(dialog, _owner);
    }

    private static Control CandidateColumn(string title, ListBox listBox, int column = 0)
    {
        listBox.ItemTemplate = new FuncDataTemplate<IconThemeSearchCandidate>((candidate, _) =>
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
        var clear = new Button { Content = "No equivalent — copy other source" };
        clear.Click += (_, _) => listBox.SelectedIndex = -1;
        var panel = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = title, FontWeight = FontWeight.SemiBold },
                listBox,
                clear,
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
        return IconTokenRules.TokenFromText(value);
    }

    private static string CategoryFromToken(string token)
    {
        return IconTokenRules.CategoryFromToken(token);
    }
}
