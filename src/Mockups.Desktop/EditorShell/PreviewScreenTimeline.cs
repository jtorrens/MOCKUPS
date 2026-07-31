using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record PreviewScreenTimelineItem(
    string Id,
    string Label,
    int StartFrame,
    int EndFrame);

internal sealed record PreviewScreenTimelineCollection(
    string Id,
    string Label,
    IReadOnlyList<PreviewScreenTimelineItem> Items);

internal sealed record PreviewScreenTimelineSnapshot(
    string ScreenId,
    string ScreenLabel,
    int PreRollFrames,
    int ContentDurationFrames,
    int PostRollFrames,
    IReadOnlyList<PreviewScreenTimelineCollection> Collections)
{
    public int MinimumFrame => -PreRollFrames;
    public int MaximumFrame => ContentDurationFrames + PostRollFrames - 1;
}

internal sealed record PreviewScreenTimelineRange(
    int PreRollFrames,
    int ContentDurationFrames,
    int PostRollFrames);

internal static class PreviewScreenTimelineMath
{
    public static double Fraction(
        int frame,
        int minimumFrame,
        int maximumFrame) =>
        maximumFrame <= minimumFrame
            ? 0
            : Math.Clamp(
                (frame - minimumFrame)
                / (double)(maximumFrame - minimumFrame),
                0,
                1);

    public static int Frame(
        double x,
        double width,
        int minimumFrame,
        int maximumFrame)
    {
        if (maximumFrame <= minimumFrame) return minimumFrame;
        return Math.Clamp(
            minimumFrame
            + (int)Math.Round(
                Math.Clamp(x / Math.Max(1, width), 0, 1)
                * (maximumFrame - minimumFrame),
                MidpointRounding.AwayFromZero),
            minimumFrame,
            maximumFrame);
    }

    public static (int StartFrame, int EndFrame) Move(
        int startFrame,
        int endFrame,
        int frameDelta,
        int contentDurationFrames)
    {
        var duration = Math.Max(1, endFrame - startFrame);
        var maximumStart = Math.Max(0, contentDurationFrames - duration);
        var nextStart = Math.Clamp(startFrame + frameDelta, 0, maximumStart);
        return (nextStart, nextStart + duration);
    }

    public static int ResizeEnd(
        int startFrame,
        int requestedEndFrame,
        int contentDurationFrames) =>
        Math.Clamp(
            requestedEndFrame,
            Math.Min(contentDurationFrames, startFrame + 1),
            contentDurationFrames);
}

internal static class PreviewScreenTimelineSnapshotFactory
{
    public static PreviewScreenTimelineSnapshot Create(
        RuntimeInputSurface surface,
        PreviewScreenTimelineRange range)
    {
        var animationSnapshot = surface.AnimationSnapshot
            ?? throw new InvalidOperationException(
                $"Screen Timeline '{surface.Owner.Node.Id}' requires its prepared animation snapshot.");
        var config = Parse(
            surface.Owner.ConfigJson,
            "Screen Timeline Variant config");
        var contract = RuntimeInputForwardingContract.EffectivePreview(
            Parse(
                animationSnapshot.Source.EffectiveContractJson,
                "Screen Timeline Runtime contract"),
            config);
        var runtime = surface.Preview;
        var animation = Parse(
            animationSnapshot.Source.AnimationJson,
            "Screen Timeline animation");
        var themeTokens = Parse(
            animationSnapshot.Source.ThemeTokensJson,
            "Screen Timeline Theme tokens");
        var collections = surface.Collections
            .Select((collection) =>
            {
                var items = DesignPreviewTestValues
                    .CollectionItems(runtime, collection)
                    .Select((item, itemIndex) =>
                    {
                        var itemId = JsonPath.RequiredString(
                            item,
                            "id",
                            $"Screen Timeline collection '{collection.Id}' item");
                        var start = Math.Clamp(
                            RuntimeAnimationFrameOrigin.ScreenFrameForOwnerFrame(
                                contract,
                                runtime,
                                animation,
                                itemId,
                                0,
                                themeTokens),
                            0,
                            Math.Max(0, range.ContentDurationFrames - 1));
                        var naturalDuration = RuntimeAnimationFrameOrigin.OwnerNaturalDuration(
                            contract,
                            runtime,
                            animation,
                            itemId,
                            themeTokens);
                        var naturalEnd = RuntimeAnimationFrameOrigin.ScreenFrameForOwnerFrame(
                            contract,
                            runtime,
                            animation,
                            itemId,
                            naturalDuration,
                            themeTokens);
                        var end = Math.Clamp(
                            Math.Max(start + 1, naturalEnd),
                            Math.Min(range.ContentDurationFrames, start + 1),
                            range.ContentDurationFrames);
                        var label = RuntimeCollectionItemPresentation.Resolve(
                            collection,
                            item,
                            itemIndex,
                            $"{collection.ItemLabel} {itemIndex + 1}",
                            "",
                            EditorIcons.Component).Title;
                        return new PreviewScreenTimelineItem(
                            itemId,
                            label,
                            start,
                            end);
                    })
                    .ToList();
                return new PreviewScreenTimelineCollection(
                    collection.Id,
                    collection.Label,
                    items);
            })
            .Where((collection) => collection.Items.Count > 0)
            .ToList();

        return new PreviewScreenTimelineSnapshot(
            surface.Owner.Node.Id,
            surface.Owner.Node.Name,
            range.PreRollFrames,
            Math.Max(1, range.ContentDurationFrames),
            range.PostRollFrames,
            collections);
    }

    private static JsonObject Parse(string json, string owner) =>
        JsonPath.ParseRequiredObject(json, owner);
}

internal sealed class PreviewScreenTimelineController : IDisposable
{
    private readonly ContentControl _host;
    private readonly Func<string, PreviewScreenTimelineRange> _screenRange;
    private readonly Func<string, int> _screenFrame;
    private readonly Action<string, int> _setScreenFrame;
    private readonly Action _togglePlayback;
    private readonly PreviewPlaybackState _playbackState;
    private readonly PreviewScreenTimelineSurface _surface;
    private string _screenId = "";

    public PreviewScreenTimelineController(
        ContentControl host,
        Func<string, PreviewScreenTimelineRange> screenRange,
        Func<string, int> screenFrame,
        Action<string, int> setScreenFrame,
        Action togglePlayback,
        PreviewPlaybackState playbackState)
    {
        _host = host;
        _screenRange = screenRange;
        _screenFrame = screenFrame;
        _setScreenFrame = setScreenFrame;
        _togglePlayback = togglePlayback;
        _playbackState = playbackState;
        _surface = new PreviewScreenTimelineSurface(
            SetFrame,
            StepFrame,
            TogglePlayback);
        _host.Content = _surface;
        _playbackState.Changed += RefreshFrame;
    }

    public void BeginScreen(string screenId)
    {
        _screenId = screenId;
        _surface.ShowLoading();
    }

    public void ShowPrepared(EditorPreparedPreviewAuthoringSurface prepared)
    {
        var screenId = prepared.Surface.Owner.Node.Id;
        if (!_screenId.Equals(screenId, StringComparison.Ordinal)) return;
        var snapshot = PreviewScreenTimelineSnapshotFactory.Create(
            prepared.Surface,
            _screenRange(screenId));
        _surface.SetSnapshot(
            snapshot,
            _screenFrame(screenId),
            _playbackState.IsPlaying);
    }

    public void Clear()
    {
        _screenId = "";
        _surface.Clear();
    }

    public void RefreshFrame()
    {
        if (string.IsNullOrWhiteSpace(_screenId)) return;
        _surface.SetFrame(
            _screenFrame(_screenId),
            _playbackState.IsPlaying);
    }

    public void Dispose()
    {
        _playbackState.Changed -= RefreshFrame;
        _host.Content = null;
    }

    private void SetFrame(int frame)
    {
        if (!string.IsNullOrWhiteSpace(_screenId))
            _setScreenFrame(_screenId, frame);
    }

    private void StepFrame(int delta)
    {
        if (!string.IsNullOrWhiteSpace(_screenId))
            SetFrame(_screenFrame(_screenId) + delta);
    }

    private void TogglePlayback() => _togglePlayback();
}

internal sealed class PreviewScreenTimelineSurface : Border
{
    private const double LabelWidth = 126;
    private readonly Action<int> _setFrame;
    private readonly Action<int> _stepFrame;
    private readonly Action _togglePlayback;
    private readonly StackPanel _content = new() { Spacing = 4 };
    private readonly TextBlock _frameText = new()
    {
        MinWidth = 84,
        VerticalAlignment = VerticalAlignment.Center,
        FontSize = 12,
        Opacity = 0.82,
    };
    private readonly Button _playButton;
    private readonly List<PreviewScreenTimelineLane> _lanes = [];
    private PreviewScreenTimelineRuler? _ruler;
    private PreviewScreenTimelineSnapshot? _snapshot;
    private int _frame;

    public PreviewScreenTimelineSurface(
        Action<int> setFrame,
        Action<int> stepFrame,
        Action togglePlayback)
    {
        _setFrame = setFrame;
        _stepFrame = stepFrame;
        _togglePlayback = togglePlayback;
        Padding = new Thickness(8);
        _playButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.Play, 15),
            "Play or pause Screen Timeline",
            38);
        _playButton.Click += (_, _) => _togglePlayback();
        Child = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = _content,
        };
        Clear();
    }

    public void Clear()
    {
        _snapshot = null;
        _lanes.Clear();
        _ruler = null;
        _content.Children.Clear();
        _content.Children.Add(new TextBlock
        {
            Text = "Selecciona una Screen para abrir su Timeline.",
            Margin = new Thickness(6),
            Opacity = 0.7,
        });
    }

    public void ShowLoading()
    {
        _snapshot = null;
        _lanes.Clear();
        _ruler = null;
        _content.Children.Clear();
        _content.Children.Add(new EditorLoadingScrim());
    }

    public void SetSnapshot(
        PreviewScreenTimelineSnapshot snapshot,
        int frame,
        bool isPlaying)
    {
        _snapshot = snapshot;
        _frame = Math.Clamp(frame, snapshot.MinimumFrame, snapshot.MaximumFrame);
        _lanes.Clear();
        _content.Children.Clear();
        _content.Children.Add(CreateTransport(snapshot, isPlaying));
        _ruler = new PreviewScreenTimelineRuler(snapshot, _frame);
        _ruler.FrameChanged += (_, nextFrame) => _setFrame(nextFrame);
        _content.Children.Add(CreateRow("", _ruler, 48));

        var general = new PreviewScreenTimelineLane(
            snapshot,
            _frame,
            0,
            snapshot.ContentDurationFrames,
            isGeneral: true);
        _lanes.Add(general);
        _content.Children.Add(CreateRow("General", general, 30));

        foreach (var collection in snapshot.Collections)
        {
            _content.Children.Add(CreateCollectionHeader(collection.Label));
            foreach (var item in collection.Items)
            {
                var lane = new PreviewScreenTimelineLane(
                    snapshot,
                    _frame,
                    item.StartFrame,
                    item.EndFrame,
                    isGeneral: false);
                _lanes.Add(lane);
                _content.Children.Add(CreateRow(item.Label, lane, 30));
            }
        }
        UpdateFrameText();
        UpdatePlayButton(isPlaying);
    }

    public void SetFrame(int frame, bool isPlaying)
    {
        if (_snapshot is null) return;
        _frame = Math.Clamp(
            frame,
            _snapshot.MinimumFrame,
            _snapshot.MaximumFrame);
        _ruler?.SetFrame(_frame);
        foreach (var lane in _lanes) lane.SetFrame(_frame);
        UpdateFrameText();
        UpdatePlayButton(isPlaying);
    }

    private Control CreateTransport(
        PreviewScreenTimelineSnapshot snapshot,
        bool isPlaying)
    {
        var start = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelineFirstFrame, 15),
            "First visible Screen Timeline frame");
        start.Click += (_, _) => _setFrame(snapshot.MinimumFrame);
        var previous = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelinePreviousFrame, 15),
            "Previous Screen Timeline frame");
        previous.Click += (_, _) => _stepFrame(-1);
        var next = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelineNextFrame, 15),
            "Next Screen Timeline frame");
        next.Click += (_, _) => _stepFrame(1);
        var end = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelineLastFrame, 15),
            "Last visible Screen Timeline frame");
        end.Click += (_, _) => _setFrame(snapshot.MaximumFrame);
        UpdatePlayButton(isPlaying);

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                start,
                previous,
                _playButton,
                next,
                end,
                new Border { Width = 6 },
                _frameText,
            },
        };
        Grid.SetColumn(controls, 1);
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{LabelWidth},*"),
            Margin = new Thickness(0, 0, 0, 2),
        };
        grid.Children.Add(controls);
        return grid;
    }

    private static Control CreateRow(
        string label,
        Control lane,
        double height)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{LabelWidth},*"),
            ColumnSpacing = 8,
            Height = height,
        };
        grid.Children.Add(new TextBlock
        {
            Text = label,
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 12,
            FontWeight = label == "General"
                ? FontWeight.SemiBold
                : FontWeight.Normal,
        });
        Grid.SetColumn(lane, 1);
        grid.Children.Add(lane);
        return grid;
    }

    private static Control CreateCollectionHeader(string label)
    {
        var border = new Border
        {
            Margin = new Thickness(0, 5, 0, 0),
            Padding = new Thickness(6, 4),
            Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Text = label.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Opacity = 0.76,
            },
        };
        return border;
    }

    private void UpdateFrameText()
    {
        if (_snapshot is null) return;
        _frameText.Text = _frame < 0
            ? $"{_frame} f · preroll"
            : _frame >= _snapshot.ContentDurationFrames
                ? $"+{_frame - _snapshot.ContentDurationFrames + 1} f · postroll"
                : $"{_frame}/{_snapshot.ContentDurationFrames - 1}";
    }

    private void UpdatePlayButton(bool isPlaying)
    {
        _playButton.Content = EditorIcons.Create(
            isPlaying ? EditorIcons.Pause : EditorIcons.Play,
            15);
        EditorTimelineTransport.ApplyPrimaryStyle(_playButton);
    }
}

internal abstract class PreviewScreenTimelineTrack : Control
{
    protected PreviewScreenTimelineTrack(
        PreviewScreenTimelineSnapshot snapshot,
        int frame)
    {
        Snapshot = snapshot;
        Frame = frame;
        MinWidth = 180;
        HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    protected PreviewScreenTimelineSnapshot Snapshot { get; }
    protected int Frame { get; private set; }

    public void SetFrame(int frame)
    {
        Frame = Math.Clamp(frame, Snapshot.MinimumFrame, Snapshot.MaximumFrame);
        InvalidateVisual();
    }

    protected double X(int frame) =>
        PreviewScreenTimelineMath.Fraction(
            frame,
            Snapshot.MinimumFrame,
            Snapshot.MaximumFrame)
        * Math.Max(1, Bounds.Width);

    protected int FrameAt(double x) =>
        PreviewScreenTimelineMath.Frame(
            x,
            Bounds.Width,
            Snapshot.MinimumFrame,
            Snapshot.MaximumFrame);

    protected void DrawZones(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.DrawRectangle(
            Brushes.Transparent,
            new Pen(EditorAnimationVisuals.TimelineBrush, 1),
            bounds);
        var zeroX = X(0);
        var endX = X(Snapshot.ContentDurationFrames);
        context.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(18, 47, 128, 237)),
            null,
            new Rect(
                zeroX,
                0,
                Math.Max(0, endX - zeroX),
                Bounds.Height));
        context.DrawLine(
            new Pen(EditorAnimationVisuals.ActiveTrackBrush, 1),
            new Point(zeroX, 0),
            new Point(zeroX, Bounds.Height));
        context.DrawLine(
            new Pen(EditorAnimationVisuals.TimelineBrush, 1),
            new Point(endX, 0),
            new Point(endX, Bounds.Height));
    }

    protected void DrawPlayhead(DrawingContext context)
    {
        var playheadX = X(Frame);
        context.DrawLine(
            new Pen(EditorSukiWindowTheme.AccentBrush(), 2),
            new Point(playheadX, 0),
            new Point(playheadX, Bounds.Height));
    }
}

internal sealed class PreviewScreenTimelineRuler : PreviewScreenTimelineTrack
{
    private bool _dragging;

    public PreviewScreenTimelineRuler(
        PreviewScreenTimelineSnapshot snapshot,
        int frame)
        : base(snapshot, frame)
    {
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => _dragging = false;
    }

    public event EventHandler<int>? FrameChanged;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        DrawZones(context);
        var tickCount = Math.Max(2, (int)Math.Floor(Bounds.Width / 24));
        var baselineY = 22d;
        context.DrawLine(
            new Pen(EditorAnimationVisuals.TimelineBrush, 1),
            new Point(0, baselineY),
            new Point(Bounds.Width, baselineY));
        for (var tick = 0; tick <= tickCount; tick++)
        {
            var x = Bounds.Width * tick / tickCount;
            var height = tick % 5 == 0 ? 10 : 6;
            context.DrawLine(
                new Pen(EditorAnimationVisuals.TimelineBrush, 1),
                new Point(x, baselineY - height),
                new Point(x, baselineY));
        }
        DrawPlayhead(context);
        var headX = X(Frame);
        var head = new StreamGeometry();
        using (var geometry = head.Open())
        {
            geometry.BeginFigure(new Point(headX - 5, 4), true);
            geometry.LineTo(new Point(headX + 5, 4));
            geometry.LineTo(new Point(headX, 10));
            geometry.EndFigure(true);
        }
        context.DrawGeometry(EditorSukiWindowTheme.AccentBrush(), null, head);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragging = true;
        args.Pointer.Capture(this);
        SetFromPointer(args.GetPosition(this).X);
        args.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        if (!_dragging) return;
        SetFromPointer(args.GetPosition(this).X);
        args.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (!_dragging) return;
        _dragging = false;
        args.Pointer.Capture(null);
        args.Handled = true;
    }

    private void SetFromPointer(double x)
    {
        var frame = FrameAt(x);
        SetFrame(frame);
        FrameChanged?.Invoke(this, frame);
    }
}

internal sealed class PreviewScreenTimelineLane : PreviewScreenTimelineTrack
{
    private const double ExitHandleWidth = 12;
    private readonly bool _isGeneral;
    private int _startFrame;
    private int _endFrame;
    private int _dragStartFrame;
    private int _dragStartValue;
    private int _dragEndValue;
    private DragMode _dragMode;

    public PreviewScreenTimelineLane(
        PreviewScreenTimelineSnapshot snapshot,
        int frame,
        int startFrame,
        int endFrame,
        bool isGeneral)
        : base(snapshot, frame)
    {
        _isGeneral = isGeneral;
        _startFrame = Math.Clamp(
            startFrame,
            0,
            Math.Max(0, snapshot.ContentDurationFrames - 1));
        _endFrame = Math.Clamp(
            Math.Max(_startFrame + 1, endFrame),
            _startFrame + 1,
            snapshot.ContentDurationFrames);
        if (!_isGeneral)
        {
            Cursor = new Cursor(StandardCursorType.SizeWestEast);
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerCaptureLost += (_, _) => _dragMode = DragMode.None;
        }
    }

    public int StartFrame => _startFrame;
    public int EndFrame => _endFrame;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        DrawZones(context);
        var left = X(_startFrame);
        var right = X(_endFrame);
        var block = new Rect(
            left,
            4,
            Math.Max(2, right - left),
            Math.Max(2, Bounds.Height - 8));
        context.DrawRectangle(
            _isGeneral
                ? new SolidColorBrush(Color.FromArgb(95, 47, 128, 237))
                : new SolidColorBrush(Color.FromArgb(150, 47, 128, 237)),
            new Pen(EditorSukiWindowTheme.AccentBrush(), 1),
            block,
            4,
            4);
        if (!_isGeneral)
        {
            context.DrawLine(
                new Pen(Brushes.White, 2),
                new Point(Math.Max(left, right - 5), 8),
                new Point(Math.Max(left, right - 5), Bounds.Height - 8));
        }
        DrawPlayhead(context);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var x = args.GetPosition(this).X;
        var left = X(_startFrame);
        var right = X(_endFrame);
        if (x < left - 4 || x > right + 4) return;
        _dragMode = right - x <= ExitHandleWidth
            ? DragMode.Exit
            : DragMode.Move;
        _dragStartFrame = FrameAt(x);
        _dragStartValue = _startFrame;
        _dragEndValue = _endFrame;
        args.Pointer.Capture(this);
        args.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        if (_dragMode == DragMode.None) return;
        var pointerFrame = FrameAt(args.GetPosition(this).X);
        if (_dragMode == DragMode.Move)
        {
            (_startFrame, _endFrame) = PreviewScreenTimelineMath.Move(
                _dragStartValue,
                _dragEndValue,
                pointerFrame - _dragStartFrame,
                Snapshot.ContentDurationFrames);
        }
        else
        {
            _endFrame = PreviewScreenTimelineMath.ResizeEnd(
                _startFrame,
                pointerFrame,
                Snapshot.ContentDurationFrames);
        }
        InvalidateVisual();
        args.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_dragMode == DragMode.None) return;
        _dragMode = DragMode.None;
        args.Pointer.Capture(null);
        args.Handled = true;
    }

    private enum DragMode
    {
        None,
        Move,
        Exit,
    }
}
