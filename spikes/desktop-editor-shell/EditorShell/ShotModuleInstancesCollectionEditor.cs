using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ShotModuleInstancesCollectionEditor
{
    private readonly SpikeDatabase _database;
    private readonly Action _onChanged;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;
    private readonly Func<string, Task<SpikeDatabase.ShotModuleInstanceDraft?>> _defineModuleInstance;
    private readonly Func<ProjectTreeNode, Task<bool>> _confirmDelete;
    private readonly Func<int> _shotFrame;
    private readonly PreviewPlaybackState _playbackState;
    private readonly ModuleInstanceTimelineDataSource _timelineDataSource;

    public ShotModuleInstancesCollectionEditor(
        SpikeDatabase database,
        Action onChanged,
        Action<ProjectTreeNode> reloadAndSelect,
        Func<string, Task<SpikeDatabase.ShotModuleInstanceDraft?>> defineModuleInstance,
        Func<ProjectTreeNode, Task<bool>> confirmDelete,
        Func<int> shotFrame,
        PreviewPlaybackState playbackState)
    {
        _database = database;
        _onChanged = onChanged;
        _reloadAndSelect = reloadAndSelect;
        _defineModuleInstance = defineModuleInstance;
        _confirmDelete = confirmDelete;
        _shotFrame = shotFrame;
        _playbackState = playbackState;
        _timelineDataSource = new ModuleInstanceTimelineDataSource(database);
    }

    public InstantEditorCard Create(ProjectTreeNode shot)
    {
        var slots = _database.GetShotModuleInstanceSlots(shot.Id);
        var body = new StackPanel { Spacing = 8 };
        var activeIndicators = new Dictionary<string, Control>(StringComparer.Ordinal);
        var frameRanges = ProductionScreenPlaybackState.FrameRanges(_timelineDataSource, shot.Id);
        for (var index = 0; index < slots.Count; index++)
        {
            body.Children.Add(CreateSlot(shot, slots[index], index, slots.Count, out var activeIndicator));
            activeIndicators[slots[index].Id] = activeIndicator;
        }
        void RefreshActiveScreen()
        {
            var activeId = ProductionScreenPlaybackState.ActiveScreenId(frameRanges, _shotFrame());
            foreach (var (screenId, indicator) in activeIndicators)
            {
                indicator.IsVisible = screenId.Equals(activeId, StringComparison.Ordinal);
            }
        }
        void OnPlaybackStateChanged() => RefreshActiveScreen();
        _playbackState.Changed += OnPlaybackStateChanged;
        body.DetachedFromVisualTree += (_, _) => _playbackState.Changed -= OnPlaybackStateChanged;
        RefreshActiveScreen();

        var add = EditorCollectionItemControls.CreateAddButton("Add Screen");
        add.Click += async (_, _) =>
        {
            var draft = await _defineModuleInstance(shot.Id);
            if (draft is null) return;
            var added = _database.AddModuleInstance(shot, draft);
            _onChanged();
            _reloadAndSelect(added);
        };

        return new InstantEditorCard(
            EditorCardHeader.Create("Modules", $"{EditorUiText.Count(slots.Count, "ordered slot")}", EditorIcons.CreateSemantic("Modules", EditorIcons.Module, 18)),
            new Border { Padding = new Thickness(10), Child = body },
            isExpanded: false,
            headerTrailing: add)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SessionStateId = "collection:shot-modules",
        };
    }

    private Control CreateSlot(
        ProjectTreeNode shot,
        SpikeDatabase.ModuleInstanceSlot slot,
        int index,
        int count,
        out Control activeIndicator)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("18,*,Auto,Auto,Auto,Auto"),
            ColumnSpacing = 6,
        };
        var activeIcon = EditorIcons.Create(EditorIcons.Play, 11);
        EditorIcons.ApplyBrush(activeIcon, EditorAnimationVisuals.ActiveTrackBrush);
        activeIndicator = new Border
        {
            Width = 18,
            Child = activeIcon,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        EditorAccessibility.Describe(
            activeIndicator,
            $"{slot.Name} is active at the current Preview frame");
        row.Children.Add(activeIndicator);
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
        open.Click += (_, _) => _reloadAndSelect(ScreenNode(shot, slot));
        Grid.SetColumn(open, 1);
        row.Children.Add(open);

        row.Children.Add(MoveButton(up: true, index == 0, -1));
        Grid.SetColumn(row.Children[^1], 2);
        row.Children.Add(MoveButton(up: false, index == count - 1, 1));
        Grid.SetColumn(row.Children[^1], 3);

        var duplicate = EditorCollectionItemControls.CreateDuplicateButton($"Duplicate {slot.Name}");
        duplicate.Click += (_, _) =>
        {
            var copy = _database.Duplicate(ScreenNode(shot, slot));
            _onChanged();
            _reloadAndSelect(copy);
        };
        Grid.SetColumn(duplicate, 4);
        row.Children.Add(duplicate);

        var delete = EditorCollectionItemControls.CreateDeleteButton($"Delete {slot.Name}");
        delete.Click += async (_, _) =>
        {
            var instance = ScreenNode(shot, slot);
            if (!await _confirmDelete(instance)) return;
            _database.Delete(instance);
            _onChanged();
            _reloadAndSelect(shot);
        };
        Grid.SetColumn(delete, 5);
        row.Children.Add(delete);

        return new Border { Padding = new Thickness(8), Child = row };

        Button MoveButton(bool up, bool disabled, int offset)
        {
            var button = EditorCollectionItemControls.CreateMoveButton(up, enabled: !disabled);
            button.Click += (_, _) =>
            {
                _database.MoveModuleInstance(slot.Id, offset);
                _onChanged();
                _reloadAndSelect(shot);
            };
            return button;
        }
    }

    private static ProjectTreeNode ScreenNode(
        ProjectTreeNode shot,
        SpikeDatabase.ModuleInstanceSlot slot) =>
        new(
            ProjectTreeNodeKind.ModuleInstance,
            slot.Id,
            slot.Name,
            $"{slot.ModuleName} · {slot.TransitionType}",
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ModuleInstance),
            shot);
}
