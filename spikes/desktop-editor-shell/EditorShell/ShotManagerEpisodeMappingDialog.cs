using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Integrations.ShotManager;
using SukiUI.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ShotManagerEpisodeMappingDialog
{
    private readonly Window _owner;
    private readonly SpikeDatabase _database;
    private readonly string _projectId;

    public ShotManagerEpisodeMappingDialog(
        Window owner,
        SpikeDatabase database,
        string projectId)
    {
        _owner = owner;
        _database = database;
        _projectId = projectId;
    }

    public Task<IReadOnlyList<ShotManagerEpisodeAssociationChoice>?> Show(
        ShotManagerProductionSnapshot snapshot,
        string seasonId)
    {
        var editor = new ShotManagerEpisodeMappingEditor(
            _database,
            _projectId,
            snapshot,
            seasonId);
        if (editor.IsComplete)
        {
            return Task.FromResult<
                IReadOnlyList<ShotManagerEpisodeAssociationChoice>?>(
                editor.Choices());
        }

        var dialog = new SukiWindow
        {
            Title = "Associate Shot Manager Episodes",
            Width = 760,
            Height = 620,
            MinWidth = 680,
            MinHeight = 480,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);
        var apply = new Button
        {
            Content = "Synchronize",
            MinWidth = 120,
            IsEnabled = editor.IsComplete,
        };
        editor.StateChanged += (_, _) =>
            apply.IsEnabled = editor.IsComplete;
        var cancel = new Button
        {
            Content = "Cancel",
            MinWidth = 92,
        };
        cancel.Click += (_, _) => dialog.Close(null);
        apply.Click += (_, _) =>
        {
            if (editor.IsComplete)
            {
                dialog.Close(editor.Choices());
            }
        };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancel, apply },
        };
        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = "Shot Manager contains Episodes that are not associated yet. Choose an existing local Episode or explicitly create a new one.",
                    TextWrapping = TextWrapping.Wrap,
                },
                new ScrollViewer
                {
                    HorizontalScrollBarVisibility =
                        Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility =
                        Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    Content = editor.Content,
                },
                actions,
            },
        };
        Grid.SetRow(content.Children[1], 1);
        Grid.SetRow(actions, 2);
        dialog.Content = new Border
        {
            Padding = EditorUiDensity.CardThickness(18),
            Child = content,
        };
        return dialog.ShowDialog<
            IReadOnlyList<ShotManagerEpisodeAssociationChoice>?>(_owner);
    }
}
