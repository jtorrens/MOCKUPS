using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ShotCreationDialog
{
    private readonly Window _owner;
    private readonly SpikeDatabase _database;

    public ShotCreationDialog(Window owner, SpikeDatabase database)
    {
        _owner = owner;
        _database = database;
    }

    public Task<string?> Show(ProjectTreeNode episode)
    {
        if (episode.Kind != ProjectTreeNodeKind.Episode)
        {
            throw new InvalidOperationException("The Shot creation dialog requires an Episode.");
        }

        var project = ProjectAncestor(episode);
        var actors = _database.GetRequiredActorOptions(project.Id).ToList();
        var dialog = new SukiWindow
        {
            Title = "Add Shot",
            Width = 460,
            Height = 250,
            MinWidth = 420,
            MinHeight = 240,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);

        var actorCombo = new EditorInstantComboBox
        {
            ItemsSource = actors,
            SelectedItem = null,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = actors.Count > 0,
        };
        var addButton = new Button
        {
            Content = "Add",
            MinWidth = 92,
            IsEnabled = false,
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 92,
        };
        actorCombo.SelectionChanged += (_, _) => addButton.IsEnabled = actorCombo.SelectedItem is not null;
        cancelButton.Click += (_, _) => dialog.Close(null);
        addButton.Click += (_, _) => dialog.Close(actorCombo.SelectedItem?.Value);

        var actorLabel = new TextBlock
        {
            Text = "Actor",
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var fields = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("100,*"),
            ColumnSpacing = 12,
            Children = { actorLabel, actorCombo },
        };
        Grid.SetColumn(actorCombo, 1);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancelButton, addButton },
        };
        var content = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = actors.Count > 0
                        ? "Choose the Actor that owns this Shot. You can change it later, but a Shot can never be ownerless."
                        : "Create an Actor before adding a Shot.",
                    TextWrapping = TextWrapping.Wrap,
                },
                fields,
            },
        };
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 18,
            Children = { content, actions },
        };
        Grid.SetRow(actions, 1);
        dialog.Content = new Border
        {
            Padding = EditorUiDensity.CardThickness(18),
            Child = root,
        };
        return dialog.ShowDialog<string?>(_owner);
    }

    private static ProjectTreeNode ProjectAncestor(ProjectTreeNode node)
    {
        var current = node;
        while (current.Kind != ProjectTreeNodeKind.Project)
        {
            current = current.Parent
                ?? throw new InvalidOperationException($"{node.Kind} has no Project ancestor.");
        }
        return current;
    }
}
