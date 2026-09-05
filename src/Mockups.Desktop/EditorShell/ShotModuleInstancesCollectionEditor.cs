using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ShotModuleInstancesCollectionEditor
{
    private readonly IModuleInstanceCollectionStore _database;
    private readonly IModuleInstanceTimelineStore _timeline;
    private readonly IProductionRecordFieldStore _productionFields;
    private readonly EditorOperationCoordinator _operations;
    private readonly IEditorShellMessageSink _messages;
    private readonly Action _onChanged;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;
    private readonly Func<string, Task<ShotModuleInstanceDraft?>> _defineModuleInstance;
    private readonly Func<ProjectTreeNode, Task<bool>> _confirmDelete;
    private readonly Func<int> _shotFrame;
    private readonly PreviewPlaybackState _playbackState;
    private readonly ModuleInstanceTimelineDataSource _timelineDataSource;

    public ShotModuleInstancesCollectionEditor(
        IModuleInstanceCollectionStore database,
        IModuleInstanceTimelineStore timeline,
        IProductionRecordFieldStore productionFields,
        IModuleInstanceThemeTokenQuery moduleInstanceThemes,
        EditorOperationCoordinator operations,
        IEditorShellMessageSink messages,
        Action onChanged,
        Action<ProjectTreeNode> reloadAndSelect,
        Func<string, Task<ShotModuleInstanceDraft?>> defineModuleInstance,
        Func<ProjectTreeNode, Task<bool>> confirmDelete,
        Func<int> shotFrame,
        PreviewPlaybackState playbackState)
    {
        _database = database;
        _timeline = timeline;
        _productionFields = productionFields;
        _operations = operations;
        _messages = messages;
        _onChanged = onChanged;
        _reloadAndSelect = reloadAndSelect;
        _defineModuleInstance = defineModuleInstance;
        _confirmDelete = confirmDelete;
        _shotFrame = shotFrame;
        _playbackState = playbackState;
        _timelineDataSource =
            new ModuleInstanceTimelineDataSource(
                timeline,
                moduleInstanceThemes);
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
                    _timeline.GetShotModuleInstanceSlots(shot.Id),
                    ProductionScreenPlaybackState.FrameRanges(
                        _timelineDataSource,
                        shot.Id),
                    _timeline.GetShotModuleInstanceSlots(shot.Id)
                        .ToDictionary(
                            (slot) => slot.Id,
                            (slot) => _timeline.GetModuleInstanceSettings(slot.Id),
                            StringComparer.Ordinal),
                    _productionFields.GetShotSettings(shot.Id).DurationFrames),
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
        body.Children.Add(CreateTimeline(shot, snapshot));
        body.Children.Add(new Separator { Margin = new Thickness(0, 4) });
        var activeIndicators = new Dictionary<string, Control>(StringComparer.Ordinal);
        var frameRanges = snapshot.FrameRanges;
        for (var index = 0; index < slots.Count; index++)
        {
            body.Children.Add(CreateSlot(
                shot,
                slots[index],
                frameRanges[index],
                index,
                slots.Count,
                out var activeIndicator));
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
        PreviewPlaybackStateBinding.Attach(body, _playbackState, RefreshActiveScreen);

        return new DeferredEditorCardContent(
            EditorUiText.Count(slots.Count, "ordered slot"),
            new Border
            {
                Padding = new Thickness(10),
                Child = body,
            });
    }

    private Control CreateTimeline(
        ProjectTreeNode shot,
        ShotModulesSnapshot snapshot)
    {
        if (snapshot.FrameRanges.Count == 0)
        {
            return new TextBlock
            {
                Text = "Add a Screen to create the Shot timeline.",
                Opacity = 0.66,
                Margin = new Thickness(8),
            };
        }

        var minimumFrame = Math.Min(
            0,
            snapshot.FrameRanges.Min((range) => range.StartFrame));
        var maximumFrame = Math.Max(
            snapshot.ShotDurationFrames,
            snapshot.FrameRanges.Max((range) =>
                range.StartFrame + range.DurationFrames));
        var visibleFrames = Math.Max(1, maximumFrame - minimumFrame);
        var pixelsPerFrame = Math.Clamp(760d / visibleFrames, 0.75, 6d);
        var trackWidth = Math.Max(760d, visibleFrames * pixelsPerFrame);
        var lanes = new StackPanel { Spacing = 4 };

        lanes.Children.Add(new TextBlock
        {
            Text =
                $"Shot window 0–{snapshot.ShotDurationFrames - 1} · top lane wins overlaps",
            FontSize = 11,
            Opacity = 0.66,
            Margin = new Thickness(184, 0, 0, 4),
        });

        for (var index = 0; index < snapshot.FrameRanges.Count; index++)
        {
            var range = snapshot.FrameRanges[index];
            var slot = snapshot.Slots[index];
            var settings = snapshot.Settings[slot.Id];
            var lane = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("176,*"),
                ColumnSpacing = 8,
                Height = 38,
            };
            lane.Children.Add(new TextBlock
            {
                Text = slot.Name,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeight.SemiBold,
            });

            var canvas = new Canvas
            {
                Width = trackWidth,
                Height = 34,
                Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            };
            var shotWindow = new Border
            {
                Width = Math.Max(1, snapshot.ShotDurationFrames * pixelsPerFrame),
                Height = 34,
                Background = new SolidColorBrush(Color.FromArgb(24, 47, 128, 237)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 47, 128, 237)),
                BorderThickness = new Thickness(1, 0),
            };
            Canvas.SetLeft(shotWindow, (0 - minimumFrame) * pixelsPerFrame);
            canvas.Children.Add(shotWindow);

            var bar = new Border
            {
                Width = Math.Max(18, range.DurationFrames * pixelsPerFrame),
                Height = 26,
                CornerRadius = new CornerRadius(5),
                Background = index == 0
                    ? EditorAnimationVisuals.ActiveTrackBrush
                    : new SolidColorBrush(Color.Parse("#536273")),
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = $"{range.StartFrame} → {range.StartFrame + range.DurationFrames}",
                    FontSize = 10,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(8, 0),
                },
            };
            Canvas.SetLeft(bar, (range.StartFrame - minimumFrame) * pixelsPerFrame);
            Canvas.SetTop(bar, 4);
            canvas.Children.Add(bar);

            var move = new Thumb
            {
                Width = bar.Width,
                Height = bar.Height,
                Background = Brushes.Transparent,
                Cursor = new Avalonia.Input.Cursor(
                    Avalonia.Input.StandardCursorType.SizeAll),
            };
            Canvas.SetLeft(move, Canvas.GetLeft(bar));
            Canvas.SetTop(move, 4);
            canvas.Children.Add(move);
            var pendingStart = range.StartFrame;
            var movePixels = 0d;
            move.DragDelta += (_, args) =>
            {
                movePixels += args.Vector.X;
                pendingStart = range.StartFrame
                    + (int)Math.Round(movePixels / pixelsPerFrame);
                var left = (pendingStart - minimumFrame) * pixelsPerFrame;
                Canvas.SetLeft(bar, left);
                Canvas.SetLeft(move, left);
            };
            move.DragCompleted += async (_, _) =>
            {
                if (pendingStart == range.StartFrame) return;
                await CommitTimelineValue(
                    shot,
                    slot.Id,
                    "moduleInstance.startFrame",
                    pendingStart);
            };

            if (RuntimeDurationContract.ParsePolicy(settings.DurationPolicy)
                == RuntimeDurationPolicy.Explicit)
            {
                var resize = new Thumb
                {
                    Width = 10,
                    Height = bar.Height,
                    Background = new SolidColorBrush(
                        Color.FromArgb(120, 255, 255, 255)),
                    Cursor = new Avalonia.Input.Cursor(
                        Avalonia.Input.StandardCursorType.SizeWestEast),
                };
                Canvas.SetLeft(
                    resize,
                    Canvas.GetLeft(bar) + bar.Width - resize.Width);
                Canvas.SetTop(resize, 4);
                canvas.Children.Add(resize);
                var pendingDuration = range.DurationFrames;
                var resizePixels = 0d;
                resize.DragDelta += (_, args) =>
                {
                    resizePixels += args.Vector.X;
                    pendingDuration = Math.Max(
                        1,
                        range.DurationFrames
                        + (int)Math.Round(resizePixels / pixelsPerFrame));
                    bar.Width = Math.Max(18, pendingDuration * pixelsPerFrame);
                    move.Width = bar.Width;
                    Canvas.SetLeft(
                        resize,
                        Canvas.GetLeft(bar) + bar.Width - resize.Width);
                };
                resize.DragCompleted += async (_, _) =>
                {
                    if (pendingDuration == range.DurationFrames) return;
                    await CommitTimelineValue(
                        shot,
                        slot.Id,
                        "moduleInstance.durationFrames",
                        pendingDuration);
                };
                EditorAccessibility.Describe(
                    resize,
                    $"Resize free-duration Screen {slot.Name}");
            }
            EditorAccessibility.Describe(
                move,
                $"Move Screen {slot.Name} in time");
            Grid.SetColumn(canvas, 1);
            lane.Children.Add(canvas);
            lanes.Children.Add(lane);
        }

        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = lanes,
            MaxHeight = 320,
        };
    }

    private async Task CommitTimelineValue(
        ProjectTreeNode shot,
        string screenId,
        string fieldId,
        int value)
    {
        await RunMutationAsync(
            "Update Screen timeline",
            async () =>
            {
                await _operations.ExecuteAsync(
                    () => _productionFields.UpdateModuleInstanceField(
                        screenId,
                        fieldId,
                        value.ToString(
                            System.Globalization.CultureInfo.InvariantCulture)));
                _onChanged();
                _reloadAndSelect(shot);
            });
    }

    private Control CreateSlot(
        ProjectTreeNode shot,
        ModuleInstanceSlot slot,
        ProductionScreenFrameRange frameRange,
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
                    new TextBlock
                    {
                        Text =
                            $"{slot.ModuleName} · {frameRange.DurationFrames} frames · {slot.TransitionType} · {slot.ActionDelayFrames} delay",
                        Opacity = 0.66,
                        FontSize = 11,
                    },
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
        Grid.SetColumn(duplicate, 4);
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
        Grid.SetColumn(delete, 5);
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
        IReadOnlyList<ModuleInstanceSlot> Slots,
        IReadOnlyList<ProductionScreenFrameRange> FrameRanges,
        IReadOnlyDictionary<string, ModuleInstanceSettings> Settings,
        int ShotDurationFrames);
}
