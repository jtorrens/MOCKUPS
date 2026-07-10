using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
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

    public Task<bool> ConfirmAction(
        string title,
        string headline,
        string body,
        string actionLabel,
        double width = 420,
        double height = 220)
    {
        return Confirm(
            title,
            headline,
            body,
            actionLabel,
            isDestructive: false,
            width,
            height);
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
            "Delete",
            isDestructive: true,
            width: 420,
            height: 220);
    }

    public Task<string?> PromptText(
        string title,
        string label,
        string currentValue,
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

        var textBox = EditorTextBoxBehavior.Configure(new TextBox
        {
            Text = currentValue,
            MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
        });
        var saveButton = new Button
        {
            Content = "Save",
            MinWidth = 92,
        };
        void RefreshSave()
        {
            saveButton.IsEnabled = !string.IsNullOrWhiteSpace(textBox.Text);
        }

        textBox.TextChanged += (_, _) => RefreshSave();
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || saveButton.IsEnabled != true)
            {
                return;
            }

            e.Handled = true;
            dialog.Close(textBox.Text?.Trim());
        };

        var content = new StackPanel
        {
            Spacing = 8,
        };
        content.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeight.Bold,
        });
        content.Children.Add(textBox);

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
        cancelButton.Click += (_, _) => dialog.Close(null);
        saveButton.Click += (_, _) => dialog.Close(textBox.Text?.Trim());

        actions.Children.Add(cancelButton);
        actions.Children.Add(saveButton);
        Grid.SetRow(content, 0);
        Grid.SetRow(actions, 1);
        ((Grid)root.Child).Children.Add(content);
        ((Grid)root.Child).Children.Add(actions);
        dialog.Content = root;
        dialog.Opened += (_, _) =>
        {
            RefreshSave();
            textBox.Focus();
            textBox.SelectAll();
        };

        return dialog.ShowDialog<string?>(_owner);
    }

    private Task<bool> Confirm(
        string title,
        string headline,
        string? body = null,
        string actionLabel = "Delete",
        bool isDestructive = true,
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

        var confirmButton = new Button
        {
            Content = actionLabel,
            MinWidth = 92,
            Background = new SolidColorBrush(Color.Parse(isDestructive
                ? (_isDark ? "#5a3435" : "#fff1f3")
                : (_isDark ? "#283247" : "#eef3ff"))),
            BorderBrush = new SolidColorBrush(Color.Parse(isDestructive
                ? (_isDark ? "#78565a" : "#ffd0d5")
                : (_isDark ? "#445675" : "#c9d8ff"))),
            Foreground = new SolidColorBrush(Color.Parse(isDestructive
                ? (_isDark ? "#e8a1a8" : "#b4232e")
                : (_isDark ? "#d7e2ff" : "#23477f"))),
        };
        confirmButton.Click += (_, _) => dialog.Close(true);

        actions.Children.Add(cancelButton);
        actions.Children.Add(confirmButton);

        Grid.SetRow(content, 0);
        Grid.SetRow(actions, 1);
        ((Grid)root.Child).Children.Add(content);
        ((Grid)root.Child).Children.Add(actions);
        dialog.Content = root;

        return dialog.ShowDialog<bool>(_owner);
    }

    private SukiWindow CreateDialog(string title, double width, double height)
    {
        var dialog = new SukiWindow
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
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);
        return dialog;
    }
}
