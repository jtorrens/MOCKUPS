using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ShotModuleInstancesCollectionEditor
{
    private readonly SpikeDatabase _database;
    private readonly Action _onChanged;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;

    public ShotModuleInstancesCollectionEditor(
        SpikeDatabase database,
        Action onChanged,
        Action<ProjectTreeNode> reloadAndSelect)
    {
        _database = database;
        _onChanged = onChanged;
        _reloadAndSelect = reloadAndSelect;
    }

    public InstantEditorCard Create(ProjectTreeNode shot)
    {
        var slots = _database.GetShotModuleInstanceSlots(shot.Id);
        var body = new StackPanel { Spacing = 8 };
        for (var index = 0; index < slots.Count; index++)
        {
            body.Children.Add(CreateSlot(shot, slots[index], index, slots.Count));
        }

        var add = new Button
        {
            Content = "Add conversation",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        add.Click += (_, _) =>
        {
            var added = _database.AddChild(shot);
            _onChanged();
            _reloadAndSelect(added);
        };
        body.Children.Add(add);

        return new InstantEditorCard(
            EditorCardHeader.Create("Modules", $"{EditorUiText.Count(slots.Count, "ordered slot")}", EditorIcons.CreateSemantic("Modules", EditorIcons.Module, 18)),
            new Border { Padding = new Thickness(10), Child = body },
            isExpanded: true)
        { HorizontalAlignment = HorizontalAlignment.Stretch };
    }

    private Control CreateSlot(
        ProjectTreeNode shot,
        SpikeDatabase.ModuleInstanceSlot slot,
        int index,
        int count)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"),
            ColumnSpacing = 6,
        };
        var open = new Button
        {
            Content = new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    new TextBlock { Text = slot.Name, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                    new TextBlock { Text = $"{slot.ModuleName} · {slot.StoredDurationFrames} frames · {slot.TransitionType}", Opacity = 0.66, FontSize = 11 },
                },
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        open.Click += (_, _) => _reloadAndSelect(new ProjectTreeNode(
            ProjectTreeNodeKind.ModuleInstance,
            slot.Id,
            slot.Name,
            $"{slot.ModuleName} · {slot.TransitionType}",
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ModuleInstance),
            shot));
        row.Children.Add(open);

        row.Children.Add(MoveButton("Up", index == 0, -1));
        Grid.SetColumn(row.Children[^1], 1);
        row.Children.Add(MoveButton("Down", index == count - 1, 1));
        Grid.SetColumn(row.Children[^1], 2);

        var delete = new Button { Content = EditorIcons.Create(EditorIcons.Delete, 15) };
        delete.Click += (_, _) =>
        {
            _database.Delete(new ProjectTreeNode(
                ProjectTreeNodeKind.ModuleInstance,
                slot.Id,
                slot.Name,
                "",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ModuleInstance),
                shot));
            _onChanged();
            _reloadAndSelect(shot);
        };
        Grid.SetColumn(delete, 3);
        row.Children.Add(delete);

        return new Border { Padding = new Thickness(8), Child = row };

        Button MoveButton(string label, bool disabled, int offset)
        {
            var button = new Button { Content = label, IsEnabled = !disabled };
            button.Click += (_, _) =>
            {
                _database.MoveModuleInstance(slot.Id, offset);
                _onChanged();
                _reloadAndSelect(shot);
            };
            return button;
        }
    }
}
