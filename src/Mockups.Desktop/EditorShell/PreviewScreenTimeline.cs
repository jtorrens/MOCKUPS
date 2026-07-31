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
    int DelayFrames,
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

internal sealed record PreviewScreenTimelineViewport(
    int MinimumFrame,
    int MaximumFrame);

internal static class PreviewScreenTimelineMath
{
    public const double SnapDistancePixels = 7;

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
        int frameDelta)
    {
        var duration = Math.Max(1, endFrame - startFrame);
        var nextStart = Math.Max(0, startFrame + frameDelta);
        return (nextStart, nextStart + duration);
    }

    public static int ResizeEnd(
        int startFrame,
        int requestedEndFrame) =>
        Math.Max(requestedEndFrame, startFrame + 1);

    public static (int Frame, bool IsSnapped) SnapFrame(
        double x,
        double width,
        int minimumFrame,
        int maximumFrame,
        IReadOnlyList<int> candidates)
    {
        var raw = RawFrame(
            x,
            width,
            minimumFrame,
            maximumFrame);
        var snap = ClosestSnap(
            raw,
            SnapThresholdFrames(
                width,
                minimumFrame,
                maximumFrame),
            candidates);
        return snap is { } snapped
            ? (snapped, true)
            : (Math.Clamp(
                (int)Math.Round(raw, MidpointRounding.AwayFromZero),
                minimumFrame,
                maximumFrame), false);
    }

    public static (int StartFrame, int EndFrame, int? SnapFrame) MoveWithSnap(
        int startFrame,
        int endFrame,
        double frameDelta,
        double laneWidth,
        int minimumTimelineFrame,
        int maximumTimelineFrame,
        IReadOnlyList<int> candidates)
    {
        var duration = Math.Max(1, endFrame - startFrame);
        var desiredStart = Math.Max(0, startFrame + frameDelta);
        var desiredEnd = desiredStart + duration;
        var threshold = SnapThresholdFrames(
            laneWidth,
            minimumTimelineFrame,
            maximumTimelineFrame);
        var match = candidates
            .Distinct()
            .SelectMany((candidate) => new[]
            {
                (Candidate: candidate, Offset: candidate - desiredStart),
                (Candidate: candidate, Offset: candidate - desiredEnd),
            })
            .Where((match) => Math.Abs(match.Offset) <= threshold)
            .Where((match) => desiredStart + match.Offset >= 0)
            .OrderBy((match) => Math.Abs(match.Offset))
            .ThenBy((match) => match.Candidate)
            .Select((match) => new PreviewScreenTimelineSnapMatch(
                match.Candidate,
                match.Offset))
            .FirstOrDefault();
        if (match is not null)
        {
            var snappedStart = (int)Math.Round(
                desiredStart + match.Offset,
                MidpointRounding.AwayFromZero);
            return (
                snappedStart,
                snappedStart + duration,
                match.Candidate);
        }
        var rounded = Move(
            startFrame,
            endFrame,
            (int)Math.Round(frameDelta, MidpointRounding.AwayFromZero));
        return (rounded.StartFrame, rounded.EndFrame, null);
    }

    public static (int EndFrame, int? SnapFrame) ResizeEndWithSnap(
        int startFrame,
        double requestedEndFrame,
        double laneWidth,
        int minimumTimelineFrame,
        int maximumTimelineFrame,
        IReadOnlyList<int> candidates)
    {
        var desired = Math.Max(requestedEndFrame, startFrame + 1);
        var snap = ClosestSnap(
            desired,
            SnapThresholdFrames(
                laneWidth,
                minimumTimelineFrame,
                maximumTimelineFrame),
            candidates.Where((candidate) => candidate > startFrame));
        return snap is { } snapped
            ? (snapped, snapped)
            : (ResizeEnd(
                startFrame,
                (int)Math.Round(desired, MidpointRounding.AwayFromZero)), null);
    }

    public static double RawFrame(
        double x,
        double width,
        int minimumFrame,
        int maximumFrame) =>
        minimumFrame
        + Math.Clamp(x / Math.Max(1, width), 0, 1)
        * Math.Max(0, maximumFrame - minimumFrame);

    public static double UnboundedFrame(
        double x,
        double width,
        int minimumFrame,
        int maximumFrame) =>
        minimumFrame
        + x / Math.Max(1, width)
        * Math.Max(0, maximumFrame - minimumFrame);

    public static PreviewScreenTimelineViewport Viewport(
        PreviewScreenTimelineSnapshot snapshot,
        int anchorFrame,
        double zoom)
    {
        var value = Math.Clamp(zoom, -1, 1);
        if (Math.Abs(value) < 0.0001)
            return new PreviewScreenTimelineViewport(
                snapshot.MinimumFrame,
                snapshot.MaximumFrame);
        var baseSpan = Math.Max(
            1,
            snapshot.MaximumFrame - snapshot.MinimumFrame);
        var scale = Math.Pow(8, value);
        var visibleSpan = Math.Max(8, baseSpan / scale);
        var anchorFraction = Fraction(
            anchorFrame,
            snapshot.MinimumFrame,
            snapshot.MaximumFrame);
        var minimum = anchorFrame - anchorFraction * visibleSpan;
        return new PreviewScreenTimelineViewport(
            (int)Math.Floor(minimum),
            Math.Max(
                (int)Math.Floor(minimum) + 1,
                (int)Math.Ceiling(minimum + visibleSpan)));
    }

    private static double SnapThresholdFrames(
        double width,
        int minimumFrame,
        int maximumFrame) =>
        SnapDistancePixels
        * Math.Max(1, maximumFrame - minimumFrame)
        / Math.Max(1, width);

    private static int? ClosestSnap(
        double rawFrame,
        double thresholdFrames,
        IEnumerable<int> candidates)
    {
        var ordered = candidates
            .Distinct()
            .Select((candidate) => new
            {
                Frame = candidate,
                Distance = Math.Abs(candidate - rawFrame),
            })
            .Where((candidate) => candidate.Distance <= thresholdFrames)
            .OrderBy((candidate) => candidate.Distance)
            .ThenBy((candidate) => candidate.Frame)
            .FirstOrDefault();
        return ordered?.Frame;
    }

    private sealed record PreviewScreenTimelineSnapMatch(
        int Candidate,
        double Offset);
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
                        var ownerZero = Math.Clamp(
                            RuntimeAnimationFrameOrigin.ScreenFrameForOwnerFrame(
                                contract,
                                runtime,
                                animation,
                                itemId,
                                0,
                                themeTokens),
                            0,
                            Math.Max(0, range.ContentDurationFrames - 1));
                        var start = Math.Clamp(
                            RuntimeAnimationFrameOrigin.OwnerAppearanceScreenFrame(
                                contract,
                                runtime,
                                animation,
                                itemId,
                                themeTokens),
                            0,
                            ownerZero);
                        var delayFrames = Math.Max(0, ownerZero - start);
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
                            delayFrames,
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
    private readonly StackPanel _content = new() { Spacing = 2 };
    private readonly TextBlock _frameText = new()
    {
        MinWidth = 84,
        VerticalAlignment = VerticalAlignment.Center,
        FontSize = 12,
        Opacity = 0.82,
    };
    private readonly Button _playButton;
    private readonly List<PreviewScreenTimelineLane> _lanes = [];
    private readonly Dictionary<string, (int StartFrame, int EndFrame)>
        _laneState = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool>
        _collapsedCollections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double>
        _zoomByScreen = new(StringComparer.Ordinal);
    private readonly List<int> _keyframeFrames = [];
    private PreviewScreenTimelineRuler? _ruler;
    private PreviewScreenTimelineBackdrop? _backdrop;
    private PreviewScreenTimelineOverlay? _overlay;
    private PreviewScreenTimelineSnapshot? _snapshot;
    private PreviewScreenTimelineViewport? _viewport;
    private int? _playheadSnapFrame;
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
        _backdrop = null;
        _overlay = null;
        _viewport = null;
        _playheadSnapFrame = null;
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
        _backdrop = null;
        _overlay = null;
        _viewport = null;
        _playheadSnapFrame = null;
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
        var zoom = _zoomByScreen.TryGetValue(snapshot.ScreenId, out var storedZoom)
            ? storedZoom
            : 0;
        _viewport = PreviewScreenTimelineMath.Viewport(
            snapshot,
            _frame,
            zoom);
        _playheadSnapFrame = null;
        _lanes.Clear();
        _content.Children.Clear();
        _content.Children.Add(CreateTransport(snapshot, isPlaying));

        var timeline = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{LabelWidth},*,96"),
            ColumnSpacing = 8,
            RowSpacing = 2,
        };
        _ruler = new PreviewScreenTimelineRuler(
            snapshot,
            _viewport,
            PlayheadSnapTargets,
            OnPlayheadDragged);
        AddTimelineRow(
            timeline,
            "",
            _ruler,
            28,
            FontWeight.Normal);
        var zoomControl = new PreviewScreenTimelineZoomControl(zoom);
        zoomControl.ValueChanged += (_, value) => SetZoom(value);
        Grid.SetColumn(zoomControl, 2);
        Grid.SetRow(zoomControl, 0);
        timeline.Children.Add(zoomControl);

        var general = CreateLane(
            snapshot,
            "general",
            0,
            snapshot.ContentDurationFrames,
            isGeneral: true);
        AddTimelineRow(
            timeline,
            "General",
            general,
            28,
            FontWeight.SemiBold);

        foreach (var collection in snapshot.Collections)
        {
            var collectionKey = CollectionKey(snapshot.ScreenId, collection.Id);
            var collapsed = _collapsedCollections.TryGetValue(
                    collectionKey,
                    out var storedCollapsed)
                && storedCollapsed;
            var itemRows = new List<TimelineRow>();
            AddCollectionHeader(
                timeline,
                collection.Label,
                collapsed,
                (nextCollapsed) =>
                {
                    _collapsedCollections[collectionKey] = nextCollapsed;
                    foreach (var row in itemRows) row.SetVisible(!nextCollapsed);
                    _overlay?.InvalidateVisual();
                });
            foreach (var item in collection.Items)
            {
                var laneKey = LaneKey(
                    snapshot.ScreenId,
                    collection.Id,
                    item.Id);
                var initial = _laneState.TryGetValue(laneKey, out var stored)
                    ? stored
                    : (item.StartFrame, item.EndFrame);
                var lane = CreateLane(
                    snapshot,
                    laneKey,
                    initial.StartFrame,
                    initial.EndFrame,
                    isGeneral: false,
                    delayFrames: item.DelayFrames);
                var row = AddTimelineRow(
                    timeline,
                    item.Label,
                    lane,
                    28,
                    FontWeight.Normal);
                row.SetVisible(!collapsed);
                itemRows.Add(row);
            }
        }

        var rowCount = timeline.RowDefinitions.Count;
        _backdrop = new PreviewScreenTimelineBackdrop(snapshot, _viewport);
        Grid.SetColumn(_backdrop, 1);
        Grid.SetRowSpan(_backdrop, rowCount);
        timeline.Children.Insert(0, _backdrop);
        _overlay = new PreviewScreenTimelineOverlay(
            snapshot,
            _viewport,
            _frame,
            isPlayheadSnapped: false);
        Grid.SetColumn(_overlay, 1);
        Grid.SetRowSpan(_overlay, rowCount);
        timeline.Children.Add(_overlay);
        _content.Children.Add(timeline);
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
        if (_playheadSnapFrame != _frame) _playheadSnapFrame = null;
        _overlay?.SetPlayhead(
            _frame,
            _playheadSnapFrame is not null);
        UpdateFrameText();
        UpdatePlayButton(isPlaying);
    }

    private PreviewScreenTimelineLane CreateLane(
        PreviewScreenTimelineSnapshot snapshot,
        string key,
        int startFrame,
        int endFrame,
        bool isGeneral,
        int delayFrames = 0)
    {
        var lane = new PreviewScreenTimelineLane(
            snapshot,
            _viewport ?? throw new InvalidOperationException(
                "Screen Timeline viewport must be prepared before its lanes."),
            key,
            startFrame,
            endFrame,
            isGeneral,
            delayFrames,
            LaneSnapTargets);
        lane.BoundsChanged += (_, bounds) =>
        {
            if (!isGeneral)
                _laneState[key] = (bounds.StartFrame, bounds.EndFrame);
        };
        lane.SnapGuideChanged += (_, snapFrame) =>
            _overlay?.SetSnapGuide(snapFrame);
        _lanes.Add(lane);
        return lane;
    }

    private IReadOnlyList<int> PlayheadSnapTargets() =>
        _lanes
            .Where((lane) => lane.IsVisible)
            .SelectMany((lane) => new[] { lane.StartFrame, lane.EndFrame })
            .Concat(_keyframeFrames)
            .Distinct()
            .Order()
            .ToList();

    private IReadOnlyList<int> LaneSnapTargets(
        PreviewScreenTimelineLane active) =>
        _lanes
            .Where((lane) => !ReferenceEquals(lane, active) && lane.IsVisible)
            .SelectMany((lane) => new[] { lane.StartFrame, lane.EndFrame })
            .Append(_frame)
            .Distinct()
            .Order()
            .ToList();

    private void OnPlayheadDragged(
        PreviewScreenTimelinePlayheadChange change)
    {
        _frame = change.Frame;
        _playheadSnapFrame = change.IsSnapped
            ? change.Frame
            : null;
        _overlay?.SetPlayhead(change.Frame, change.IsSnapped);
        UpdateFrameText();
        _setFrame(change.Frame);
    }

    private void SetZoom(double value)
    {
        if (_snapshot is null) return;
        var zoom = Math.Abs(value) < 0.04 ? 0 : Math.Clamp(value, -1, 1);
        _zoomByScreen[_snapshot.ScreenId] = zoom;
        _viewport = PreviewScreenTimelineMath.Viewport(
            _snapshot,
            _frame,
            zoom);
        _ruler?.SetViewport(_viewport);
        foreach (var lane in _lanes) lane.SetViewport(_viewport);
        _backdrop?.SetViewport(_viewport);
        _overlay?.SetViewport(_viewport);
    }

    private Control CreateTransport(
        PreviewScreenTimelineSnapshot snapshot,
        bool isPlaying)
    {
        var start = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelineFirstFrame, 15),
            "First visible Screen Timeline frame");
        start.Click += (_, _) => SetTransportFrame(snapshot.MinimumFrame);
        var previous = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelinePreviousFrame, 15),
            "Previous Screen Timeline frame");
        previous.Click += (_, _) => StepTransportFrame(-1);
        var next = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelineNextFrame, 15),
            "Next Screen Timeline frame");
        next.Click += (_, _) => StepTransportFrame(1);
        var end = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelineLastFrame, 15),
            "Last visible Screen Timeline frame");
        end.Click += (_, _) => SetTransportFrame(snapshot.MaximumFrame);
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
            ColumnSpacing = 8,
        };
        grid.Children.Add(controls);
        return grid;
    }

    private static TimelineRow AddTimelineRow(
        Grid timeline,
        string label,
        Control lane,
        double height,
        FontWeight fontWeight)
    {
        var rowIndex = timeline.RowDefinitions.Count;
        timeline.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var labelControl = new TextBlock
        {
            Text = label,
            Height = height,
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 12,
            FontWeight = fontWeight,
        };
        Grid.SetRow(labelControl, rowIndex);
        timeline.Children.Add(labelControl);
        lane.Height = height;
        Grid.SetColumn(lane, 1);
        Grid.SetRow(lane, rowIndex);
        timeline.Children.Add(lane);
        return new TimelineRow(labelControl, lane);
    }

    private static void AddCollectionHeader(
        Grid timeline,
        string label,
        bool collapsed,
        Action<bool> setCollapsed)
    {
        var rowIndex = timeline.RowDefinitions.Count;
        timeline.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var button = new Button
        {
            Height = 28,
            Margin = new Thickness(0, 4, 0, 0),
            Padding = new Thickness(6, 2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        void Render(bool isCollapsed)
        {
            var content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            };
            content.Children.Add(new TextBlock
            {
                Text = label.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Opacity = 0.76,
                VerticalAlignment = VerticalAlignment.Center,
            });
            var indicator = new TextBlock
            {
                Text = isCollapsed ? ">" : "v",
                Width = 22,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                Opacity = 0.78,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(indicator, 1);
            content.Children.Add(indicator);
            button.Content = content;
            EditorAccessibility.Describe(
                button,
                $"{(isCollapsed ? "Expand" : "Collapse")} Timeline collection {label}");
        }
        var current = collapsed;
        Render(current);
        button.Click += (_, _) =>
        {
            current = !current;
            Render(current);
            setCollapsed(current);
        };
        Grid.SetColumnSpan(button, 3);
        Grid.SetRow(button, rowIndex);
        timeline.Children.Add(button);
    }

    private void SetTransportFrame(int frame)
    {
        _playheadSnapFrame = null;
        _setFrame(frame);
    }

    private void StepTransportFrame(int delta)
    {
        _playheadSnapFrame = null;
        _stepFrame(delta);
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

    private static string CollectionKey(
        string screenId,
        string collectionId) =>
        $"{screenId}\u001f{collectionId}";

    private static string LaneKey(
        string screenId,
        string collectionId,
        string itemId) =>
        $"{screenId}\u001f{collectionId}\u001f{itemId}";

    private sealed record TimelineRow(Control Label, Control Lane)
    {
        public void SetVisible(bool isVisible)
        {
            Label.IsVisible = isVisible;
            Lane.IsVisible = isVisible;
        }
    }
}

internal abstract class PreviewScreenTimelineTrack : Control
{
    protected PreviewScreenTimelineTrack(
        PreviewScreenTimelineSnapshot snapshot,
        PreviewScreenTimelineViewport viewport)
    {
        Snapshot = snapshot;
        Viewport = viewport;
        MinWidth = 180;
        HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    protected PreviewScreenTimelineSnapshot Snapshot { get; }
    protected PreviewScreenTimelineViewport Viewport { get; private set; }

    public void SetViewport(PreviewScreenTimelineViewport viewport)
    {
        Viewport = viewport;
        InvalidateVisual();
    }

    protected double X(int frame) =>
        PreviewScreenTimelineMath.Fraction(
            frame,
            Viewport.MinimumFrame,
            Viewport.MaximumFrame)
        * Math.Max(1, Bounds.Width);

    protected double RawFrameAt(double x) =>
        PreviewScreenTimelineMath.UnboundedFrame(
            x,
            Bounds.Width,
            Viewport.MinimumFrame,
            Viewport.MaximumFrame);
}

internal sealed record PreviewScreenTimelinePlayheadChange(
    int Frame,
    bool IsSnapped);

internal sealed class PreviewScreenTimelineRuler : Border
{
    private PreviewScreenTimelineViewport _viewport;
    private readonly PreviewScreenTimelineRulerTicks _ticks;
    private readonly Func<IReadOnlyList<int>> _snapTargets;
    private readonly Action<PreviewScreenTimelinePlayheadChange> _setFrame;
    private bool _dragging;

    public PreviewScreenTimelineRuler(
        PreviewScreenTimelineSnapshot snapshot,
        PreviewScreenTimelineViewport viewport,
        Func<IReadOnlyList<int>> snapTargets,
        Action<PreviewScreenTimelinePlayheadChange> setFrame)
    {
        _viewport = viewport;
        _snapTargets = snapTargets;
        _setFrame = setFrame;
        MinWidth = 180;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Background = Brushes.Transparent;
        _ticks = new PreviewScreenTimelineRulerTicks(snapshot, viewport);
        Child = _ticks;
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => _dragging = false;
    }

    public void SetViewport(PreviewScreenTimelineViewport viewport)
    {
        _viewport = viewport;
        _ticks.SetViewport(viewport);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!PreviewScreenTimelinePointer.IsPrimaryPress(this, args)) return;
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
        var change = PreviewScreenTimelineMath.SnapFrame(
            x,
            Bounds.Width,
            _viewport.MinimumFrame,
            _viewport.MaximumFrame,
            _snapTargets());
        _setFrame(new PreviewScreenTimelinePlayheadChange(
            change.Frame,
            change.IsSnapped));
    }
}

internal sealed class PreviewScreenTimelineRulerTicks
    : PreviewScreenTimelineTrack
{
    public PreviewScreenTimelineRulerTicks(
        PreviewScreenTimelineSnapshot snapshot)
        : this(
            snapshot,
            new PreviewScreenTimelineViewport(
                snapshot.MinimumFrame,
                snapshot.MaximumFrame))
    {
    }

    public PreviewScreenTimelineRulerTicks(
        PreviewScreenTimelineSnapshot snapshot,
        PreviewScreenTimelineViewport viewport)
        : base(snapshot, viewport)
    {
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var tickCount = Math.Max(2, (int)Math.Floor(Bounds.Width / 24));
        var baselineY = Math.Max(10, Bounds.Height - 3);
        for (var tick = 0; tick <= tickCount; tick++)
        {
            var x = Bounds.Width * tick / tickCount;
            var height = tick % 5 == 0 ? 10 : 6;
            context.DrawLine(
                new Pen(EditorAnimationVisuals.TimelineBrush, 1),
                new Point(x, baselineY - height),
                new Point(x, baselineY));
        }
    }
}

internal sealed record PreviewScreenTimelineLaneBounds(
    int StartFrame,
    int EndFrame);

internal sealed class PreviewScreenTimelineLane : PreviewScreenTimelineTrack
{
    private const double ExitHandleWidth = 12;
    private readonly string _key;
    private readonly bool _isGeneral;
    private readonly int _delayFrames;
    private readonly Func<PreviewScreenTimelineLane, IReadOnlyList<int>>
        _snapTargets;
    private int _startFrame;
    private int _endFrame;
    private double _dragStartFrame;
    private int _dragStartValue;
    private int _dragEndValue;
    private int? _activeSnapFrame;
    private DragMode _dragMode;

    public PreviewScreenTimelineLane(
        PreviewScreenTimelineSnapshot snapshot,
        PreviewScreenTimelineViewport viewport,
        string key,
        int startFrame,
        int endFrame,
        bool isGeneral,
        int delayFrames,
        Func<PreviewScreenTimelineLane, IReadOnlyList<int>> snapTargets)
        : base(snapshot, viewport)
    {
        _key = key;
        _isGeneral = isGeneral;
        _delayFrames = Math.Max(0, delayFrames);
        _snapTargets = snapTargets;
        _startFrame = Math.Max(0, startFrame);
        _endFrame = Math.Max(_startFrame + 1, endFrame);
        if (!_isGeneral)
        {
            Cursor = new Cursor(StandardCursorType.SizeWestEast);
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerCaptureLost += (_, _) => EndDrag();
        }
    }

    public string Key => _key;
    public int StartFrame => _startFrame;
    public int DelayFrames => _delayFrames;
    public int EndFrame => _endFrame;
    public event EventHandler<PreviewScreenTimelineLaneBounds>? BoundsChanged;
    public event EventHandler<int?>? SnapGuideChanged;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var left = X(_startFrame);
        var right = X(_endFrame);
        var block = new Rect(
            left,
            3,
            Math.Max(2, right - left),
            Math.Max(2, Bounds.Height - 6));
        context.DrawRectangle(
            _isGeneral
                ? new SolidColorBrush(Color.FromArgb(95, 47, 128, 237))
                : new SolidColorBrush(Color.FromArgb(150, 47, 128, 237)),
            new Pen(
                _activeSnapFrame is null
                    ? EditorSukiWindowTheme.AccentBrush()
                    : EditorAnimationVisuals.ActiveTrackBrush,
                _activeSnapFrame is null ? 1 : 2),
            block,
            4,
            4);
        if (!_isGeneral && _delayFrames > 0)
        {
            var delayRight = Math.Min(right, X(_startFrame + _delayFrames));
            PreviewScreenTimelineHatch.Draw(
                context,
                new Rect(
                    left,
                    block.Top,
                    Math.Max(0, delayRight - left),
                    block.Height));
        }
        if (!_isGeneral)
        {
            context.DrawLine(
                new Pen(Brushes.White, 2),
                new Point(Math.Max(left, right - 5), 7),
                new Point(Math.Max(left, right - 5), Bounds.Height - 7));
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!PreviewScreenTimelinePointer.IsPrimaryPress(this, args)) return;
        var x = args.GetPosition(this).X;
        var left = X(_startFrame);
        var right = X(_endFrame);
        if (x < left - 4 || x > right + 4) return;
        _dragMode = right - x <= ExitHandleWidth
            ? DragMode.Exit
            : DragMode.Move;
        _dragStartFrame = RawFrameAt(x);
        _dragStartValue = _startFrame;
        _dragEndValue = _endFrame;
        args.Pointer.Capture(this);
        args.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        if (_dragMode == DragMode.None) return;
        var pointerFrame = RawFrameAt(args.GetPosition(this).X);
        int? snapFrame;
        if (_dragMode == DragMode.Move)
        {
            var next = PreviewScreenTimelineMath.MoveWithSnap(
                _dragStartValue,
                _dragEndValue,
                pointerFrame - _dragStartFrame,
                Bounds.Width,
                Viewport.MinimumFrame,
                Viewport.MaximumFrame,
                _snapTargets(this));
            _startFrame = next.StartFrame;
            _endFrame = next.EndFrame;
            snapFrame = next.SnapFrame;
        }
        else
        {
            var next = PreviewScreenTimelineMath.ResizeEndWithSnap(
                _startFrame,
                pointerFrame,
                Bounds.Width,
                Viewport.MinimumFrame,
                Viewport.MaximumFrame,
                _snapTargets(this));
            _endFrame = next.EndFrame;
            snapFrame = next.SnapFrame;
        }
        BoundsChanged?.Invoke(
            this,
            new PreviewScreenTimelineLaneBounds(
                _startFrame,
                _endFrame));
        SnapGuideChanged?.Invoke(this, snapFrame);
        _activeSnapFrame = snapFrame;
        InvalidateVisual();
        args.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_dragMode == DragMode.None) return;
        EndDrag();
        args.Pointer.Capture(null);
        args.Handled = true;
    }

    private void EndDrag()
    {
        if (_dragMode == DragMode.None) return;
        _dragMode = DragMode.None;
        _activeSnapFrame = null;
        InvalidateVisual();
        SnapGuideChanged?.Invoke(this, null);
    }

    private enum DragMode
    {
        None,
        Move,
        Exit,
    }
}

internal static class PreviewScreenTimelinePointer
{
    public static bool IsPrimaryPress(
        Control control,
        PointerPressedEventArgs args)
    {
        var properties = args.GetCurrentPoint(control).Properties;
        if (args.Pointer.Type != PointerType.Pen)
            return properties.IsLeftButtonPressed;
        return !properties.IsRightButtonPressed
            && !properties.IsMiddleButtonPressed
            && !properties.IsBarrelButtonPressed
            && !properties.IsEraser;
    }
}

internal sealed class PreviewScreenTimelineZoomControl : Border
{
    private const double CenterDetentPixels = 6;
    private readonly PreviewScreenTimelineZoomVisual _visual;
    private bool _dragging;
    private double _value;

    public PreviewScreenTimelineZoomControl(double value)
    {
        Width = 88;
        Height = 28;
        Padding = new Thickness(4, 0);
        Background = Brushes.Transparent;
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
        _value = Math.Clamp(value, -1, 1);
        _visual = new PreviewScreenTimelineZoomVisual(_value)
        {
            IsHitTestVisible = false,
        };
        Child = _visual;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => _dragging = false;
        ToolTip.SetTip(this, "Timeline scale · center is 1:1");
        EditorAccessibility.Describe(this, "Timeline scale; center is 1 to 1");
    }

    public event Action<object?, double>? ValueChanged;
    public double Value => _value;

    public static double ValueAt(double x, double width)
    {
        const double trackInset = 14;
        var trackWidth = Math.Max(1, width - trackInset * 2);
        var localX = Math.Clamp(x - trackInset, 0, trackWidth);
        var centerX = trackWidth / 2;
        return Math.Abs(localX - centerX) <= CenterDetentPixels
            ? 0
            : localX / trackWidth * 2 - 1;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!PreviewScreenTimelinePointer.IsPrimaryPress(this, args)) return;
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
        SetFromPointer(args.GetPosition(this).X);
        _dragging = false;
        args.Pointer.Capture(null);
        args.Handled = true;
    }

    private void SetFromPointer(double x)
    {
        var next = ValueAt(x, Bounds.Width);
        if (Math.Abs(next - _value) < 0.0001) return;
        _value = next;
        _visual.SetValue(_value);
        ValueChanged?.Invoke(this, _value);
    }
}

internal sealed class PreviewScreenTimelineZoomVisual : Control
{
    private double _value;

    public PreviewScreenTimelineZoomVisual(double value) => _value = value;

    public void SetValue(double value)
    {
        _value = Math.Clamp(value, -1, 1);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var centerY = Bounds.Height / 2 + 3;
        var left = 10d;
        var right = Math.Max(left + 1, Bounds.Width - 10);
        var center = (left + right) / 2;
        var brush = EditorAnimationVisuals.TimelineBrush;
        context.DrawLine(
            new Pen(brush, 1),
            new Point(left, centerY),
            new Point(right, centerY));
        context.DrawLine(
            new Pen(EditorAnimationVisuals.ActiveTrackBrush, 1.5),
            new Point(center, centerY - 5),
            new Point(center, centerY + 5));
        var thumbX = left + (_value + 1) / 2 * (right - left);
        context.DrawEllipse(
            new SolidColorBrush(Color.Parse("#20252D")),
            new Pen(
                Math.Abs(_value) < 0.0001
                    ? EditorAnimationVisuals.ActiveTrackBrush
                    : brush,
                2),
            new Point(thumbX, centerY),
            4,
            4);
        DrawScaleMark(context, left - 7, centerY, 3, brush);
        DrawScaleMark(context, right + 7, centerY, 6, brush);
    }

    private static void DrawScaleMark(
        DrawingContext context,
        double centerX,
        double baselineY,
        double size,
        IBrush brush)
    {
        var geometry = new StreamGeometry();
        using (var drawing = geometry.Open())
        {
            drawing.BeginFigure(
                new Point(centerX - size, baselineY + 2),
                true);
            drawing.LineTo(new Point(centerX, baselineY - size));
            drawing.LineTo(new Point(centerX + size, baselineY + 2));
            drawing.EndFigure(true);
        }
        context.DrawGeometry(brush, null, geometry);
    }
}

internal sealed class PreviewScreenTimelineBackdrop : PreviewScreenTimelineTrack
{
    public PreviewScreenTimelineBackdrop(
        PreviewScreenTimelineSnapshot snapshot,
        PreviewScreenTimelineViewport viewport)
        : base(snapshot, viewport)
    {
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var zeroX = X(0);
        var endX = X(Snapshot.ContentDurationFrames);
        PreviewScreenTimelineHatch.Draw(
            context,
            new Rect(0, 0, zeroX, Bounds.Height));
        PreviewScreenTimelineHatch.Draw(
            context,
            new Rect(
                endX,
                0,
                Math.Max(0, Bounds.Width - endX),
                Bounds.Height));
    }

}

internal static class PreviewScreenTimelineHatch
{
    private static readonly Pen HatchPen = new(
        new SolidColorBrush(Color.FromArgb(35, 143, 152, 168)),
        1);

    public static void Draw(
        DrawingContext context,
        Rect region)
    {
        if (region.Width <= 0 || region.Height <= 0) return;
        using (context.PushClip(region))
        {
            const double spacing = 11;
            for (var x = region.Left - region.Height;
                 x < region.Right;
                 x += spacing)
            {
                context.DrawLine(
                    HatchPen,
                    new Point(x, region.Bottom),
                    new Point(x + region.Height, region.Top));
            }
        }
    }
}

internal sealed class PreviewScreenTimelineOverlay : PreviewScreenTimelineTrack
{
    private int _frame;
    private bool _isPlayheadSnapped;
    private int? _snapGuideFrame;

    public PreviewScreenTimelineOverlay(
        PreviewScreenTimelineSnapshot snapshot,
        PreviewScreenTimelineViewport viewport,
        int frame,
        bool isPlayheadSnapped)
        : base(snapshot, viewport)
    {
        _frame = frame;
        _isPlayheadSnapped = isPlayheadSnapped;
        IsHitTestVisible = false;
    }

    public void SetPlayhead(int frame, bool isSnapped)
    {
        _frame = Math.Clamp(
            frame,
            Snapshot.MinimumFrame,
            Snapshot.MaximumFrame);
        _isPlayheadSnapped = isSnapped;
        InvalidateVisual();
    }

    public void SetSnapGuide(int? frame)
    {
        _snapGuideFrame = frame;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_snapGuideFrame is { } guideFrame)
        {
            var guideX = X(guideFrame);
            context.DrawLine(
                new Pen(EditorAnimationVisuals.ActiveTrackBrush, 1),
                new Point(guideX, 0),
                new Point(guideX, Bounds.Height));
        }

        var playheadBrush = _isPlayheadSnapped
            || _snapGuideFrame == _frame
            ? EditorAnimationVisuals.ActiveTrackBrush
            : EditorSukiWindowTheme.AccentBrush();
        var playheadX = X(_frame);
        context.DrawLine(
            new Pen(playheadBrush, 2),
            new Point(playheadX, 0),
            new Point(playheadX, Bounds.Height));
        var head = new StreamGeometry();
        using (var geometry = head.Open())
        {
            geometry.BeginFigure(new Point(playheadX - 5, 0), true);
            geometry.LineTo(new Point(playheadX + 5, 0));
            geometry.LineTo(new Point(playheadX, 6));
            geometry.EndFigure(true);
        }
        context.DrawGeometry(playheadBrush, null, head);
    }
}
