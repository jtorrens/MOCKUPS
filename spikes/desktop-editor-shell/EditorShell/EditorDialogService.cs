using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SukiUI.Controls;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorDialogService
{
    private readonly Window _owner;
    private readonly bool _isDark;

    public EditorDialogService(Window owner, bool isDark)
    {
        _owner = owner;
        _isDark = isDark;
    }

    public async Task ShowInfo(string title, string message)
    {
        var dialog = CreateDialog(title, 440, 220);
        var okButton = new Button
        {
            Content = "OK",
            MinWidth = 92,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        okButton.Click += (_, _) => dialog.Close();

        dialog.Content = new Border
        {
            Padding = new Thickness(22),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                RowSpacing = 18,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    okButton,
                },
            },
        };
        Grid.SetRow(okButton, 1);

        await dialog.ShowDialog(_owner);
    }

    public Task<bool> ConfirmIconTokenDelete(string token)
    {
        return Confirm(
            "Delete icon token",
            $"Delete \"{token}\" from every icon set?",
            width: 430,
            height: 220);
    }

    public Task<bool> ConfirmDelete(ProjectTreeNode node)
    {
        var message = node.Kind == ProjectTreeNodeKind.Episode
            ? "This will also remove the shots inside this episode in the current in-memory spike."
            : node.Kind == ProjectTreeNodeKind.App
                ? "This will also remove the modules inside this app in the current spike database."
                : "This removes this item from the current spike database.";

        return Confirm(
            $"Delete {node.Kind}",
            $"Delete {node.Name}?",
            message,
            width: 420,
            height: 220);
    }

    private Task<bool> Confirm(
        string title,
        string headline,
        string? body = null,
        double width = 420,
        double height = 220)
    {
        var dialog = CreateDialog(title, width, height);
        var root = new Border
        {
            Padding = new Thickness(22),
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
            Text = headline,
            FontSize = 17,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
        });
        if (!string.IsNullOrWhiteSpace(body))
        {
            content.Children.Add(new TextBlock
            {
                Text = body,
                TextWrapping = TextWrapping.Wrap,
            });
        }

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

        return dialog.ShowDialog<bool>(_owner);
    }

    private static SukiWindow CreateDialog(string title, double width, double height)
    {
        return new SukiWindow
        {
            Title = title,
            Width = width,
            Height = height,
            MinWidth = width,
            MinHeight = height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };
    }
}
