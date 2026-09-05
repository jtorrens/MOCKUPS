using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ShotModuleInstancesCollectionEditor
{
    private readonly IModuleInstanceCollectionStore _database;
    private readonly IModuleInstanceTimelineStore _timeline;
    private readonly EditorOperationCoordinator _operations;
    private readonly IEditorShellMessageSink _messages;
    private readonly Action _onChanged;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;
    private readonly Func<string, Task<ShotModuleInstanceDraft?>> _defineModuleInstance;
    private readonly Func<ProjectTreeNode, Task<bool>> _confirmDelete;
    public ShotModuleInstancesCollectionEditor(
        IModuleInstanceCollectionStore database,
        IModuleInstanceTimelineStore timeline,
        EditorOperationCoordinator operations,
        IEditorShellMessageSink messages,
        Action onChanged,
        Action<ProjectTreeNode> reloadAndSelect,
        Func<string, Task<ShotModuleInstanceDraft?>> defineModuleInstance,
        Func<ProjectTreeNode, Task<bool>> confirmDelete)
    {
        _database = database;
        _timeline = timeline;
        _operations = operations;
        _messages = messages;
        _onChanged = onChanged;
        _reloadAndSelect = reloadAndSelect;
        _defineModuleInstance = defineModuleInstance;
        _confirmDelete = confirmDelete;
    }

    public InstantEditorCard Create(ProjectTreeNode shot)
    {
        var add = EditorCollectionItemControls.CreateAddButton("Add Screen");
        add.Click += async (_, _) =>
        {
            await RunMutationAsync(
                "Add Screen",
                async () =>
                {
                    var draft = await _defineModuleInstance(shot.Id);
                    if (draft is null) return;
                    var added = await _operations.ExecuteAsync(
                        () => _database.AddModuleInstance(shot, draft));
                    _onChanged();
                    _reloadAndSelect(added);
                });
        };

        return DeferredEditorCard.Create(
            "Modules",
            "Load on expand",
            () => EditorIcons.CreateSemantic(
                    "Modules",
                    EditorIcons.Module,
                    18),
            "collection:shot-modules",
            (cancellationToken) => _operations.ExecuteAsync(
                () => new ShotModulesSnapshot(
                    _timeline.GetShotModuleInstanceSlots(shot.Id)),
                cancellationToken),
            (snapshot) => Present(shot, snapshot),
            add);
    }

    private DeferredEditorCardContent Present(
        ProjectTreeNode shot,
        ShotModulesSnapshot snapshot)
    {
        var slots = snapshot.Slots;
        var body = new StackPanel { Spacing = 8 };
        for (var index = 0; index < slots.Count; index++)
        {
            body.Children.Add(CreateSlot(
                shot,
                slots[index],
                index,
                slots.Count));
        }

        return new DeferredEditorCardContent(
            EditorUiText.Count(slots.Count, "ordered slot"),
            new Border
            {
                Padding = new Thickness(10),
                Child = body,
            });
    }

    private Control CreateSlot(
        ProjectTreeNode shot,
        ModuleInstanceSlot slot,
        int index,
        int count)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto"),
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
                    new TextBlock
                    {
                        Text =
                            $"{slot.ModuleName} · {slot.TransitionType}",
                        Opacity = 0.66,
                        FontSize = 11,
                    },
                },
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        open.Click += (_, _) => _reloadAndSelect(ScreenNode(shot, slot));
        row.Children.Add(open);

        row.Children.Add(MoveButton(up: true, index == 0, -1));
        Grid.SetColumn(row.Children[^1], 1);
        row.Children.Add(MoveButton(up: false, index == count - 1, 1));
        Grid.SetColumn(row.Children[^1], 2);

        var duplicate = EditorCollectionItemControls.CreateDuplicateButton($"Duplicate {slot.Name}");
        duplicate.Click += async (_, _) =>
        {
            await RunMutationAsync(
                "Duplicate Screen",
                async () =>
                {
                    var copy = await _operations.ExecuteAsync(
                        () => _database.Duplicate(
                            ScreenNode(shot, slot)));
                    _onChanged();
                    _reloadAndSelect(copy);
                });
        };
        Grid.SetColumn(duplicate, 3);
        row.Children.Add(duplicate);

        var delete = EditorCollectionItemControls.CreateDeleteButton($"Delete {slot.Name}");
        delete.Click += async (_, _) =>
        {
            await RunMutationAsync(
                "Delete Screen",
                async () =>
                {
                    var instance = ScreenNode(shot, slot);
                    if (!await _confirmDelete(instance)) return;
                    await _operations.ExecuteAsync(
                        () => _database.Delete(instance));
                    _onChanged();
                    _reloadAndSelect(shot);
                });
        };
        Grid.SetColumn(delete, 4);
        row.Children.Add(delete);

        return new Border { Padding = new Thickness(8), Child = row };

        Button MoveButton(bool up, bool disabled, int offset)
        {
            var button = EditorCollectionItemControls.CreateMoveButton(up, enabled: !disabled);
            button.Click += async (_, _) =>
            {
                await RunMutationAsync(
                    "Move Screen",
                    async () =>
                    {
                        await _operations.ExecuteAsync(
                            () => _database.MoveModuleInstance(
                                slot.Id,
                                offset));
                        _onChanged();
                        _reloadAndSelect(shot);
                    });
            };
            return button;
        }
    }

    private async Task RunMutationAsync(
        string operation,
        Func<Task> mutation)
    {
        try
        {
            await mutation();
        }
        catch (OperationCanceledException)
        {
            // The editor session no longer owns the queued operation.
        }
        catch (Exception exception)
        {
            _messages.Error(operation, exception);
        }
    }

    private static ProjectTreeNode ScreenNode(
        ProjectTreeNode shot,
        ModuleInstanceSlot slot) =>
        new(
            ProjectTreeNodeKind.ModuleInstance,
            slot.Id,
            slot.Name,
            $"{slot.ModuleName} · {slot.TransitionType}",
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ModuleInstance),
            shot);

    private sealed record ShotModulesSnapshot(
        System.Collections.Generic.IReadOnlyList<ModuleInstanceSlot> Slots);
}
