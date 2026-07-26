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

internal sealed record ShotCreationDraft(
    string ActorId,
    int ShotNumber);

internal sealed class ShotCreationDialog
{
    private readonly Window _owner;
    private readonly IEditorChildStore _database;

    public ShotCreationDialog(Window owner, IEditorChildStore database)
    {
        _owner = owner;
        _database = database;
    }

    public Task<ShotCreationDraft?> Show(ProjectTreeNode episode)
    {
        if (episode.Kind != ProjectTreeNodeKind.Episode)
        {
            throw new InvalidOperationException("The Shot creation dialog requires an Episode.");
        }

        var project = ProjectAncestor(episode);
        var association = _database.GetShotManagerAssociation(project.Id);
        var episodeBinding = _database.GetShotManagerEpisodeBinding(episode.Id);
        if (association is not null && episodeBinding is null)
        {
            throw new InvalidOperationException(
                "Synchronize this Episode with Shot Manager before adding a Shot.");
        }
        var actors = _database.GetRequiredActorOptions(project.Id).ToList();
        var dialog = new SukiWindow
        {
            Title = "Add Shot",
            Width = 460,
            Height = 330,
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
        var shotNumber = _database.SuggestShotNumber(episode.Id).ToString();
        var definition = new FieldDefinition(
            "shot.creation.shotNumber",
            "Shot number",
            ValueKind.Integer,
            DefaultValue: shotNumber,
            Number: new NumberDefinition(
                Minimum: 1,
                Maximum: 99_999_999,
                Increment: 1,
                DecimalPlaces: 0));
        var shotNumberField = new DictionaryFieldControl(
            new FieldValue(definition, shotNumber));
        shotNumberField.ValueChanged += (_, value) => shotNumber = value;
        shotNumberField.ValueCommitted += (_, value) => shotNumber = value;
        addButton.Click += (_, _) =>
        {
            if (actorCombo.SelectedItem is null)
            {
                return;
            }
            dialog.Close(new ShotCreationDraft(
                actorCombo.SelectedItem.Value,
                int.Parse(shotNumber)));
        };

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
        content.Children.Add(shotNumberField);
        content.Children.Add(new TextBlock
        {
            Text = episodeBinding is null
                ? "The Shot number is stable and will identify its future production route."
                : "Shot Manager will use this stable number to calculate the technical name and output routes.",
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
        });
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
        return dialog.ShowDialog<ShotCreationDraft?>(_owner);
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
