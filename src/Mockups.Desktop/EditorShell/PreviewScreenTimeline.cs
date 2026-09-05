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
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record PreviewScreenTimelineInterval(
    int StartFrame,
    int EndFrame,
    int? StartKeyframeFrame = null,
    int? EndKeyframeFrame = null,
    int MinimumStartFrame = 0,
    int MaximumEndFrame = int.MaxValue);

internal sealed record PreviewScreenTimelineSerialEdit(
    string StorageCollectionJsonKey,
    string DelayFieldJsonKey,
    string PresenceDurationFieldJsonKey,
    int PreviousEndFrame)
{
    public bool CanResizeEnd =>
        !string.IsNullOrWhiteSpace(
            PresenceDurationFieldJsonKey);
}

internal sealed record PreviewScreenTimelineStateEdit(
    string FieldId,
    string TargetId,
    Func<int, int> LocalFrameForScreenFrame);

internal sealed record PreviewScreenTimelineDurationEdit;

internal sealed record PreviewScreenTimelineItem(
    string Id,
    string Label,
    int StartFrame,
    int EndFrame,
    IReadOnlyList<PreviewScreenTimelineInterval> Intervals,
    PreviewScreenTimelineSerialEdit? SerialEdit = null,
    PreviewScreenTimelineStateEdit? StateEdit = null,
    PreviewScreenTimelineDurationEdit? DurationEdit = null);

internal sealed record PreviewScreenTimelineCollection(
    string Id,
    string Label,
    IReadOnlyList<PreviewScreenTimelineItem> Items);

internal sealed record PreviewScreenTimelineKeyframe(
    string FieldId,
    string TargetId,
    int LocalFrame,
    int ScreenFrame,
    bool IsProtected);

internal sealed record PreviewScreenTimelineReferenceMarker(
    string Id,
    int Frame,
    string Text);

internal sealed record PreviewScreenTimelineSnapshot(
    string ScreenId,
    string ScreenLabel,
    int PreRollFrames,
    int ContentDurationFrames,
    int PostRollFrames,
    IReadOnlyList<PreviewScreenTimelineCollection> Collections,
    IReadOnlyList<PreviewScreenTimelineKeyframe> Keyframes,
    RuntimeInputTimelineMutation? Mutation,
    bool IsDurationEditable = false)
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
    public const int AuthoringHorizonFrames = 10;

    public static int AuthoringMaximumFrame(
        PreviewScreenTimelineSnapshot snapshot) =>
        snapshot.MaximumFrame > int.MaxValue - AuthoringHorizonFrames
            ? int.MaxValue
            : snapshot.MaximumFrame + AuthoringHorizonFrames;

    public static int PreviewContentDuration(
        int confirmedContentDuration,
        IEnumerable<int> durationAffectingEndFrames) =>
        Math.Max(
            Math.Max(1, confirmedContentDuration),
            durationAffectingEndFrames.DefaultIfEmpty(1).Max());

    public static IReadOnlyList<int> NavigationFrames(
        PreviewScreenTimelineSnapshot snapshot,
        IEnumerable<int> visibleLayerBoundaries,
        IReadOnlySet<string> visibleTargetIds) =>
        visibleLayerBoundaries
            .Concat(snapshot.Keyframes
                .Where((keyframe) => visibleTargetIds.Contains(
                    keyframe.TargetId))
                .Select((keyframe) => keyframe.ScreenFrame))
            .Append(snapshot.MinimumFrame)
            .Append(snapshot.MaximumFrame)
            .Select((frame) => Math.Clamp(
                frame,
                snapshot.MinimumFrame,
                snapshot.MaximumFrame))
            .Distinct()
            .Order()
            .ToList();

    public static int AdjacentNavigationFrame(
        int currentFrame,
        int direction,
        IReadOnlyList<int> navigationFrames) =>
        direction < 0
            ? navigationFrames
                .Where((frame) => frame < currentFrame)
                .DefaultIfEmpty(currentFrame)
                .Max()
            : navigationFrames
                .Where((frame) => frame > currentFrame)
                .DefaultIfEmpty(currentFrame)
                .Min();

    public static PreviewScreenTimelineViewport EnsureAuthoringHorizon(
        PreviewScreenTimelineSnapshot snapshot,
        PreviewScreenTimelineViewport viewport,
        int previewContentDuration)
    {
        var contentMaximum = previewContentDuration > int.MaxValue - snapshot.PostRollFrames
            ? int.MaxValue
            : previewContentDuration + snapshot.PostRollFrames - 1;
        var requiredMaximum = contentMaximum > int.MaxValue - AuthoringHorizonFrames
            ? int.MaxValue
            : contentMaximum + AuthoringHorizonFrames;
        return requiredMaximum <= viewport.MaximumFrame
            ? viewport
            : viewport with { MaximumFrame = requiredMaximum };
    }

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
        var authoringMaximumFrame = AuthoringMaximumFrame(snapshot);
        var value = Math.Clamp(zoom, -1, 1);
        if (Math.Abs(value) < 0.0001)
            return new PreviewScreenTimelineViewport(
                snapshot.MinimumFrame,
                authoringMaximumFrame);
        var baseSpan = Math.Max(
            1,
            authoringMaximumFrame - snapshot.MinimumFrame);
        var scale = Math.Pow(8, value);
        var visibleSpan = Math.Max(8, baseSpan / scale);
        var anchorFraction = Fraction(
            anchorFrame,
            snapshot.MinimumFrame,
            authoringMaximumFrame);
        var minimum = anchorFrame - anchorFraction * visibleSpan;
        var maximum = minimum + visibleSpan;
        var requiredTrailingMaximum = Math.Min(
            authoringMaximumFrame,
            anchorFrame > int.MaxValue - AuthoringHorizonFrames
                ? int.MaxValue
                : anchorFrame + AuthoringHorizonFrames);
        if (maximum < requiredTrailingMaximum)
        {
            var correction = requiredTrailingMaximum - maximum;
            minimum += correction;
            maximum += correction;
        }
        return new PreviewScreenTimelineViewport(
            (int)Math.Floor(minimum),
            Math.Max(
                (int)Math.Floor(minimum) + 1,
                (int)Math.Ceiling(maximum)));
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
        var contract = Parse(
            animationSnapshot.Source.EffectiveContractJson,
            "Screen Timeline Runtime contract");
        var runtime = surface.Preview;
        var animation = Parse(
            animationSnapshot.Source.AnimationJson,
            "Screen Timeline animation");
        var themeTokens = Parse(
            animationSnapshot.Source.ThemeTokensJson,
            "Screen Timeline Theme tokens");
        var contractCollections = JsonPath.OptionalObjectArray(
            contract,
            "collections",
            "Screen Timeline Runtime contract");
        var tracks = JsonPath.OptionalObjectArray(
            animation,
            "tracks",
            "Screen Timeline animation");
        var collections = surface.Collections
            .Select((collection) => CreateCollection(
                collection,
                contractCollections,
                tracks,
                contract,
                runtime,
                animation,
                themeTokens,
                range.ContentDurationFrames,
                animationSnapshot.Source.FrameRate))
            .Where((collection) => collection.Items.Count > 0)
            .ToList();
        var keyframes = CreateKeyframes(
            tracks,
            collections
                .SelectMany((collection) => collection.Items)
                .SelectMany((item) => new[]
                {
                    item.Id,
                    item.StateEdit?.TargetId,
                })
                .Where((targetId) => targetId is not null)
                .Select((targetId) => targetId!)
                .Append("")
                .ToHashSet(StringComparer.Ordinal),
            contract,
            runtime,
            animation,
            themeTokens,
            animationSnapshot.Source.FrameRate);

        return new PreviewScreenTimelineSnapshot(
            surface.Owner.Node.Id,
            surface.Owner.Node.Name,
            range.PreRollFrames,
            Math.Max(1, range.ContentDurationFrames),
            range.PostRollFrames,
            collections,
            keyframes,
            surface.TimelineMutation,
            IsDurationEditable:
                RuntimeDurationContract.Policy(
                    animationSnapshot.Source.EffectiveContractJson)
                == RuntimeDurationPolicy.Explicit);
    }

    private static IReadOnlyList<PreviewScreenTimelineKeyframe> CreateKeyframes(
        IReadOnlyList<JsonObject> tracks,
        IReadOnlySet<string> visibleTargetIds,
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        JsonObject themeTokens,
        int frameRate) =>
        tracks.SelectMany((track) =>
        {
            var fieldId = JsonPath.RequiredString(
                track,
                "fieldId",
                "Screen Timeline animation track");
            var targetId = track["targetId"]?.GetValue<string>() ?? "";
            if (!visibleTargetIds.Contains(targetId))
                return [];
            return JsonPath.OptionalObjectArray(
                    track,
                    "keyframes",
                    $"Screen Timeline animation track '{fieldId}'")
                .Where((keyframe) => keyframe["enabled"]?.GetValue<bool>() != false)
                .Select((keyframe) =>
                {
                    var localFrame = keyframe["frame"] is JsonValue frameValue
                        && frameValue.TryGetValue<int>(out var parsedFrame)
                            ? parsedFrame
                            : throw new InvalidOperationException(
                                $"Screen Timeline animation track '{fieldId}' keyframe must contain an integer 'frame'.");
                    return new PreviewScreenTimelineKeyframe(
                        fieldId,
                        targetId,
                        localFrame,
                        RuntimeAnimationFrameOrigin.ScreenFrame(
                            contract,
                            runtime,
                            animation,
                            fieldId,
                            targetId,
                            localFrame,
                            themeTokens,
                            frameRate),
                        localFrame == 0);
                });
        }).ToList();

    private static PreviewScreenTimelineCollection CreateCollection(
        RuntimeInputCollectionDefinition collection,
        IReadOnlyList<JsonObject> contractCollections,
        IReadOnlyList<JsonObject> tracks,
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        JsonObject themeTokens,
        int contentDurationFrames,
        int frameRate)
    {
        var definition = contractCollections.FirstOrDefault((candidate) =>
            JsonPath.RequiredString(candidate, "jsonKey", "Runtime collection")
                .Equals(collection.JsonKey, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Screen Timeline collection '{collection.Id}' has no effective Runtime contract definition.");
        var timeline = definition["animationTimeline"] as JsonObject ?? new JsonObject();
        var ownerOrigin = timeline["ownerOrigin"] as JsonObject;
        var items = DesignPreviewTestValues.CollectionItems(runtime, collection).ToList();
        if (ownerOrigin?["kind"]?.GetValue<string>() == "firstMatchingValue")
        {
            return new PreviewScreenTimelineCollection(
                collection.Id,
                collection.Label,
                items.Select((item, index) => CreateStateItem(
                    collection,
                    item,
                    index,
                    ownerOrigin,
                    tracks,
                    contract,
                    runtime,
                    animation,
                    themeTokens,
                    contentDurationFrames,
                    frameRate)).ToList());
        }

        var sequenceItems = timeline["sequenceItems"]?.GetValue<bool>() != false;
        var preFieldIds = JsonPath.OptionalStringArray(
            timeline,
            "preDurationFieldIds",
            $"Screen Timeline collection '{collection.Id}'");
        var previousSequenceEnd = 0;
        var presenceDurationFieldId = timeline["presenceDurationFieldId"]?.GetValue<string>() ?? "";
        var presenceDurationField = string.IsNullOrWhiteSpace(presenceDurationFieldId)
            ? null
            : definition["fields"]!.AsArray().OfType<JsonObject>()
                .Single(field => field["id"]?.GetValue<string>() == presenceDurationFieldId);
        var projected = new List<PreviewScreenTimelineItem>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var itemId = JsonPath.RequiredString(item, "id", $"Screen Timeline collection '{collection.Id}' item");
            var start = sequenceItems
                ? Math.Max(0, RuntimeAnimationFrameOrigin.ScreenFrameForOwnerFrame(
                    contract, runtime, animation, itemId, 0, themeTokens, frameRate))
                : 0;
            var sequenceEnd = sequenceItems
                ? Math.Max(start + 1, RuntimeAnimationFrameOrigin.OwnerSequenceEndScreenFrame(
                    contract, runtime, animation, itemId, themeTokens, frameRate))
                : contentDurationFrames;
            var end = RuntimeAnimationFrameOrigin.OwnerPresenceEndScreenFrame(
                contract, runtime, animation, itemId, contentDurationFrames, themeTokens, frameRate);
            var label = ItemLabel(collection, item, index);
            PreviewScreenTimelineSerialEdit? serialEdit = null;
            if (sequenceItems && preFieldIds.Count == 1)
            {
                var field = definition["fields"]!.AsArray().OfType<JsonObject>()
                    .First(candidate => candidate["id"]?.GetValue<string>() == preFieldIds[0]);
                serialEdit = new PreviewScreenTimelineSerialEdit(
                    StorageCollectionKey(collection),
                    JsonPath.RequiredString(field, "jsonKey", "Timeline offset field"),
                    presenceDurationField is null
                        ? ""
                        : JsonPath.RequiredString(
                            presenceDurationField,
                            "jsonKey",
                            "Timeline presence duration field"),
                    previousSequenceEnd);
            }
            projected.Add(new PreviewScreenTimelineItem(
                itemId,
                label,
                start,
                end,
                [new PreviewScreenTimelineInterval(start, end)],
                serialEdit));
            if (sequenceItems) previousSequenceEnd = sequenceEnd;
        }
        return new PreviewScreenTimelineCollection(collection.Id, collection.Label, projected);
    }

    private static PreviewScreenTimelineItem CreateStateItem(
        RuntimeInputCollectionDefinition collection,
        JsonObject item,
        int itemIndex,
        JsonObject ownerOrigin,
        IReadOnlyList<JsonObject> tracks,
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        JsonObject themeTokens,
        int contentDurationFrames,
        int frameRate)
    {
        string Required(string key) => JsonPath.RequiredString(ownerOrigin, key, "State Timeline owner origin");
        var sourceCollectionKey = Required("sourceCollectionJsonKey");
        var sourceTargetIdKey = Required("sourceTargetIdJsonKey");
        var sourceFieldId = Required("sourceFieldId");
        var sourceValueKey = Required("sourceValueJsonKey");
        var matchValueKey = Required("matchValueJsonKey");
        var sourceTargetId = JsonPath.RequiredString(item, sourceTargetIdKey, "State Timeline item");
        var matchValue = JsonPath.RequiredString(item, matchValueKey, "State Timeline item");
        var sourceItem = JsonPath.OptionalObjectArray(runtime, sourceCollectionKey, "State Timeline source collection")
            .FirstOrDefault(candidate => candidate["id"]?.GetValue<string>() == sourceTargetId)
            ?? throw new InvalidOperationException($"State Timeline source item '{sourceTargetId}' is missing.");
        var currentValue = sourceItem[sourceValueKey] is JsonValue stateValue
            && stateValue.TryGetValue<string>(out var stateId)
                ? stateId
                : "";
        var track = tracks.FirstOrDefault(candidate =>
            candidate["fieldId"]?.GetValue<string>() == sourceFieldId
            && candidate["targetId"]?.GetValue<string>() == sourceTargetId);
        var events = (track?["keyframes"] as JsonArray)?.OfType<JsonObject>()
            .Where(candidate => candidate["enabled"]?.GetValue<bool>() != false)
            .Select(candidate => new
            {
                LocalFrame = candidate["frame"]?.GetValue<int>() ?? 0,
                Value = candidate["value"]?.GetValue<string>() ?? "",
            })
            .OrderBy(candidate => candidate.LocalFrame)
            .ToList() ?? [];
        var intervals = new List<PreviewScreenTimelineInterval>();
        var cursor = 0;
        int? activeStartLocal = currentValue == matchValue ? 0 : null;
        foreach (var stateEvent in events)
        {
            var screenFrame = Math.Clamp(
                RuntimeAnimationFrameOrigin.ScreenFrame(
                    contract, runtime, animation, sourceFieldId, sourceTargetId, stateEvent.LocalFrame, themeTokens, frameRate),
                0,
                contentDurationFrames);
            if (stateEvent.Value == matchValue && activeStartLocal is null)
            {
                cursor = screenFrame;
                activeStartLocal = stateEvent.LocalFrame;
            }
            else if (stateEvent.Value != matchValue && activeStartLocal is not null)
            {
                intervals.Add(new PreviewScreenTimelineInterval(
                    cursor,
                    Math.Max(cursor, screenFrame),
                    activeStartLocal == 0 ? null : activeStartLocal,
                    stateEvent.LocalFrame));
                activeStartLocal = null;
            }
        }
        if (activeStartLocal is not null)
        {
            intervals.Add(new PreviewScreenTimelineInterval(
                cursor,
                contentDurationFrames,
                activeStartLocal == 0 ? null : activeStartLocal,
                null));
        }
        var selectorBoundaries = events
            .Select(stateEvent => Math.Clamp(
                RuntimeAnimationFrameOrigin.ScreenFrame(
                    contract, runtime, animation, sourceFieldId, sourceTargetId, stateEvent.LocalFrame, themeTokens, frameRate),
                0,
                contentDurationFrames))
            .Distinct()
            .Order()
            .ToList();
        intervals = intervals.Select(interval => interval with
        {
            MinimumStartFrame = selectorBoundaries
                .Where(boundary => boundary < interval.StartFrame)
                .Select(boundary => boundary + 1)
                .DefaultIfEmpty(0)
                .Max(),
            MaximumEndFrame = selectorBoundaries
                .Where(boundary => boundary > interval.EndFrame)
                .Select(boundary => boundary - 1)
                .DefaultIfEmpty(contentDurationFrames)
                .Min(),
        }).ToList();
        int LocalFrameForScreenFrame(int screenFrame) => Math.Max(0, (int)Math.Round(
            RuntimeAnimationFrameOrigin.OwnerLocalFrame(
                contract, runtime, animation, sourceTargetId, screenFrame, themeTokens, frameRate),
            MidpointRounding.AwayFromZero));
        return new PreviewScreenTimelineItem(
            JsonPath.RequiredString(item, "id", "State Timeline item"),
            ItemLabel(collection, item, itemIndex),
            0,
            contentDurationFrames,
            intervals,
            StateEdit: new PreviewScreenTimelineStateEdit(
                sourceFieldId,
                sourceTargetId,
                LocalFrameForScreenFrame));
    }

    private static string ItemLabel(RuntimeInputCollectionDefinition collection, JsonObject item, int index) =>
        RuntimeCollectionItemPresentation.Resolve(
            collection,
            item,
            index,
            $"{collection.ItemLabel} {index + 1}",
            "",
            EditorIcons.Component).Title;

    private static string StorageCollectionKey(RuntimeInputCollectionDefinition collection) =>
        !string.IsNullOrWhiteSpace(collection.StorageCollectionJsonKey)
            ? collection.StorageCollectionJsonKey
            : string.IsNullOrWhiteSpace(collection.SourceCollectionJsonKey)
                ? collection.JsonKey
                : collection.SourceCollectionJsonKey;

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
    private readonly Func<string, IReadOnlyList<PreviewScreenTimelineReferenceMarker>>
        _referenceMarkers;
    private readonly PreviewScreenTimelineSurface _surface;
    private string _screenId = "";

    public PreviewScreenTimelineController(
        ContentControl host,
        Func<string, PreviewScreenTimelineRange> screenRange,
        Func<string, int> screenFrame,
        Action<string, int> setScreenFrame,
        Action togglePlayback,
        PreviewPlaybackState playbackState,
        Func<string, IReadOnlyList<PreviewScreenTimelineReferenceMarker>>
            referenceMarkers)
    {
        _host = host;
        _screenRange = screenRange;
        _screenFrame = screenFrame;
        _setScreenFrame = setScreenFrame;
        _togglePlayback = togglePlayback;
        _playbackState = playbackState;
        _referenceMarkers = referenceMarkers;
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
        try
        {
            var snapshot = PreviewScreenTimelineSnapshotFactory.Create(
                prepared.Surface,
                _screenRange(screenId));
            _surface.SetSnapshot(
                snapshot,
                (targetId) => prepared.Editor
                    .CreateScreenTimelineAnimationContent(
                        prepared.Surface,
                        targetId),
                _screenFrame(screenId),
                _playbackState.IsPlaying,
                _referenceMarkers(screenId));
        }
        catch (Exception exception)
        {
            _surface.ShowFailure(exception.Message);
        }
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
        _surface.SetReferenceMarkers(
            _referenceMarkers(_screenId));
    }

    public bool TryStepFrame(int delta) =>
        _surface.TryStepFrame(delta);

    public bool TryMoveToNavigationFrame(int direction) =>
        _surface.TryMoveToNavigationFrame(direction);

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
    private TextBlock? _frameText;
    private Button? _playButton;
    private readonly List<PreviewScreenTimelineLane> _lanes = [];
    private readonly Dictionary<string, bool>
        _collapsedCollections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double>
        _zoomByScreen = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string>
        _selectedLaneByScreen = new(StringComparer.Ordinal);
    private readonly Dictionary<PreviewScreenTimelineLane, TimelineRow>
        _rowsByLane = [];
    private readonly List<int> _keyframeFrames = [];
    private IReadOnlyList<PreviewScreenTimelineReferenceMarker>
        _referenceMarkers = [];
    private readonly ContentControl _animationHost = new();
    private PreviewScreenTimelineRuler? _ruler;
    private PreviewScreenTimelineBackdrop? _backdrop;
    private PreviewScreenTimelineOverlay? _overlay;
    private PreviewScreenTimelineSnapshot? _snapshot;
    private PreviewScreenTimelineViewport? _viewport;
    private int? _playheadSnapFrame;
    private int _frame;
    private int _previewContentDurationFrames;
    private Func<string, AnimationTargetEditorContent>? _animationContent;
    private PreviewScreenTimelineLane? _selectedLane;
    private PreviewScreenTimelineLane? _generalLane;

    public PreviewScreenTimelineSurface(
        Action<int> setFrame,
        Action<int> stepFrame,
        Action togglePlayback)
    {
        _setFrame = setFrame;
        _stepFrame = stepFrame;
        _togglePlayback = togglePlayback;
        Padding = new Thickness(8);
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
        _animationContent = null;
        _selectedLane = null;
        _generalLane = null;
        _previewContentDurationFrames = 0;
        _rowsByLane.Clear();
        _keyframeFrames.Clear();
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
        _animationContent = null;
        _selectedLane = null;
        _generalLane = null;
        _previewContentDurationFrames = 0;
        _rowsByLane.Clear();
        _keyframeFrames.Clear();
        _content.Children.Clear();
        _content.Children.Add(new EditorLoadingScrim());
    }

    public void ShowFailure(string message)
    {
        _snapshot = null;
        _lanes.Clear();
        _ruler = null;
        _backdrop = null;
        _overlay = null;
        _viewport = null;
        _playheadSnapFrame = null;
        _animationContent = null;
        _selectedLane = null;
        _generalLane = null;
        _previewContentDurationFrames = 0;
        _rowsByLane.Clear();
        _keyframeFrames.Clear();
        _content.Children.Clear();
        _content.Children.Add(new TextBlock
        {
            Text = $"Timeline unavailable: {message}",
            Margin = new Thickness(6),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.IndianRed,
        });
    }

    public void SetSnapshot(
        PreviewScreenTimelineSnapshot snapshot,
        Func<string, AnimationTargetEditorContent> animationContent,
        int frame,
        bool isPlaying,
        IReadOnlyList<PreviewScreenTimelineReferenceMarker>? referenceMarkers = null)
    {
        _snapshot = snapshot;
        _previewContentDurationFrames = snapshot.ContentDurationFrames;
        _animationContent = animationContent;
        _referenceMarkers = referenceMarkers ?? [];
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
        _rowsByLane.Clear();
        _keyframeFrames.Clear();
        _content.Children.Clear();
        _content.Children.Add(CreateTransport(snapshot, isPlaying, zoom));

        var timeline = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{LabelWidth},*"),
            ColumnSpacing = 8,
            RowSpacing = 2,
        };
        _ruler = new PreviewScreenTimelineRuler(
            snapshot,
            _viewport,
            _referenceMarkers,
            PlayheadSnapTargets,
            OnPlayheadDragged);
        AddTimelineRow(
            timeline,
            "",
            _ruler,
            28,
            FontWeight.Normal);
        var general = CreateLane(
            snapshot,
            "general",
            new PreviewScreenTimelineItem(
                "general",
                "General",
                0,
                snapshot.ContentDurationFrames,
                [new PreviewScreenTimelineInterval(0, snapshot.ContentDurationFrames)],
                DurationEdit: snapshot.IsDurationEditable
                    ? new PreviewScreenTimelineDurationEdit()
                    : null),
            isGeneral: true);
        _generalLane = general;
        var generalRow = AddTimelineRow(
            timeline,
            "General",
            general,
            28,
            FontWeight.SemiBold,
            () => SelectLane(general));
        _rowsByLane[general] = generalRow;

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
                    if (nextCollapsed
                        && itemRows.Any((row) => ReferenceEquals(
                            row.Lane,
                            _selectedLane)))
                    {
                        SelectLane(general);
                    }
                    _overlay?.InvalidateVisual();
                });
            foreach (var item in collection.Items)
            {
                var laneKey = LaneKey(
                    snapshot.ScreenId,
                    collection.Id,
                    item.Id);
                var lane = CreateLane(
                    snapshot,
                    laneKey,
                    item,
                    isGeneral: false);
                var row = AddTimelineRow(
                    timeline,
                    item.Label,
                    lane,
                    28,
                    FontWeight.Normal,
                    () => SelectLane(lane));
                row.SetVisible(!collapsed);
                itemRows.Add(row);
                _rowsByLane[lane] = row;
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
        _content.Children.Add(_animationHost);
        var selectedKey = _selectedLaneByScreen.TryGetValue(
                snapshot.ScreenId,
                out var storedLaneKey)
            ? storedLaneKey
            : general.Key;
        var selectedLane = _lanes.FirstOrDefault((lane) =>
                lane.IsVisible
                && lane.Key.Equals(selectedKey, StringComparison.Ordinal))
            ?? general;
        SelectLane(selectedLane);
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

    public void SetReferenceMarkers(
        IReadOnlyList<PreviewScreenTimelineReferenceMarker> markers)
    {
        if (_snapshot is null
            || _referenceMarkers.SequenceEqual(markers)) return;
        _referenceMarkers = markers;
        _ruler?.SetReferenceMarkers(markers);
    }

    public bool TryStepFrame(int delta)
    {
        if (_snapshot is null) return false;
        var next = Math.Clamp(
            _frame + delta,
            _snapshot.MinimumFrame,
            _snapshot.MaximumFrame);
        _playheadSnapFrame = null;
        if (next != _frame) _setFrame(next);
        return true;
    }

    public bool TryMoveToNavigationFrame(int direction)
    {
        if (_snapshot is null) return false;
        var next = PreviewScreenTimelineMath.AdjacentNavigationFrame(
            _frame,
            direction,
            NavigationFrames());
        _playheadSnapFrame = null;
        if (next != _frame) _setFrame(next);
        return true;
    }

    internal IReadOnlyList<int> NavigationFrames()
    {
        if (_snapshot is null) return [];
        var visibleLanes = _lanes
            .Where((lane) => lane.IsVisible)
            .ToList();
        return PreviewScreenTimelineMath.NavigationFrames(
            _snapshot,
            visibleLanes.SelectMany((lane) => lane.SnapFrames),
            visibleLanes
                .Select((lane) => lane.TargetId)
                .ToHashSet(StringComparer.Ordinal));
    }

    private PreviewScreenTimelineLane CreateLane(
        PreviewScreenTimelineSnapshot snapshot,
        string key,
        PreviewScreenTimelineItem item,
        bool isGeneral)
    {
        var lane = new PreviewScreenTimelineLane(
            snapshot,
            _viewport ?? throw new InvalidOperationException(
                "Screen Timeline viewport must be prepared before its lanes."),
            key,
            item,
            isGeneral,
            LaneSnapTargets);
        lane.EditCommitted += async (_, edit) => await CommitLaneEditAsync(item, edit);
        lane.AuthoringExtentChanged += (_, pointerX) =>
            RefreshPreviewContentDuration(lane, pointerX);
        lane.SelectionRequested += (_, _) => SelectLane(lane);
        lane.SnapGuideChanged += (_, snapFrame) =>
            _overlay?.SetSnapGuide(snapFrame);
        _lanes.Add(lane);
        return lane;
    }

    private void RefreshPreviewContentDuration(
        PreviewScreenTimelineLane activeLane,
        double pointerX)
    {
        if (_snapshot is null || _viewport is null) return;
        var next = PreviewScreenTimelineMath.PreviewContentDuration(
            activeLane.OwnsScreenDuration
                ? 1
                : _snapshot.ContentDurationFrames,
            activeLane.OwnsScreenDuration
                ? [activeLane.EndFrame]
                : _lanes
                    .Where((lane) => lane.AffectsScreenDuration)
                    .Select((lane) => lane.EndFrame));
        if (next == _previewContentDurationFrames) return;
        _previewContentDurationFrames = next;
        _generalLane?.SetAuthoringEndFrame(next);
        _backdrop?.SetContentEndFrame(next);
        var nextViewport = PreviewScreenTimelineMath.EnsureAuthoringHorizon(
            _snapshot,
            _viewport,
            next);
        if (nextViewport != _viewport)
        {
            _viewport = nextViewport;
            _ruler?.SetViewport(nextViewport);
            foreach (var lane in _lanes) lane.SetViewport(nextViewport);
            _backdrop?.SetViewport(nextViewport);
            _overlay?.SetViewport(nextViewport);
            activeLane.RebaseActiveDrag(pointerX);
        }
        UpdateFrameText();
    }

    private void SelectLane(PreviewScreenTimelineLane lane)
    {
        if (_snapshot is null || _animationContent is null) return;
        _selectedLane = lane;
        _selectedLaneByScreen[_snapshot.ScreenId] = lane.Key;
        var keyframes = _snapshot.Keyframes
            .Where((keyframe) => keyframe.TargetId.Equals(
                lane.TargetId,
                StringComparison.Ordinal))
            .ToList();
        _keyframeFrames.Clear();
        _keyframeFrames.AddRange(keyframes
            .Select((keyframe) => keyframe.ScreenFrame));
        foreach (var candidate in _lanes)
        {
            var isSelected = ReferenceEquals(candidate, lane);
            candidate.SetSelection(
                isSelected,
                isSelected ? keyframes : []);
            if (_rowsByLane.TryGetValue(candidate, out var row))
                row.SetSelected(isSelected);
        }
        var animation = _animationContent(lane.TargetId);
        _animationHost.Content = new StackPanel
        {
            Spacing = EditorUiDensity.Card(8),
            Margin = new Thickness(0, 8, 0, 0),
            Children =
            {
                EditorGroupBlock.CreateSeparator(),
                new TextBlock
                {
                    Text = $"{lane.Label} · Keyframes",
                    FontWeight = FontWeight.SemiBold,
                    Margin = new Thickness(4, 0),
                },
                animation.Content,
            },
        };
    }

    private async Task CommitLaneEditAsync(
        PreviewScreenTimelineItem item,
        PreviewScreenTimelineLaneEdit edit)
    {
        if (_snapshot?.Mutation is not { } mutation) return;
        if (item.DurationEdit is not null
            && edit.Mode == PreviewScreenTimelineLaneEditMode.Exit)
        {
            await mutation.UpdateDurationFramesAsync(
                edit.EndFrame);
            return;
        }
        if (item.SerialEdit is { } serial)
        {
            if (edit.Mode == PreviewScreenTimelineLaneEditMode.Move)
            {
                await mutation.UpdateCollectionValuesAsync(
                    serial.StorageCollectionJsonKey,
                    item.Id,
                    new Dictionary<string, JsonNode?>
                    {
                        [serial.DelayFieldJsonKey] = edit.StartFrame - serial.PreviousEndFrame,
                    });
                return;
            }
            if (edit.Mode == PreviewScreenTimelineLaneEditMode.Exit)
            {
                if (string.IsNullOrWhiteSpace(
                        serial.PresenceDurationFieldJsonKey))
                {
                    return;
                }
                await mutation.UpdateCollectionValuesAsync(
                    serial.StorageCollectionJsonKey,
                    item.Id,
                    new Dictionary<string, JsonNode?>
                    {
                        [serial.PresenceDurationFieldJsonKey] = Math.Max(1, edit.EndFrame - edit.StartFrame),
                    });
                return;
            }
        }
        if (item.StateEdit is not { } state
            || edit.Interval is not { } original
            || edit.UpdatedInterval is not { } updated) return;
        var changes = new Dictionary<int, int>();
        if (original.StartKeyframeFrame is { } startLocal
            && updated.StartFrame != original.StartFrame)
        {
            changes[startLocal] = state.LocalFrameForScreenFrame(updated.StartFrame);
        }
        if (original.EndKeyframeFrame is { } endLocal
            && updated.EndFrame != original.EndFrame)
        {
            changes[endLocal] = state.LocalFrameForScreenFrame(updated.EndFrame);
        }
        if (changes.Count == 0) return;
        await mutation.ExecuteAnimationMutationAsync(
            (animationDocument) =>
                animationDocument.TryMoveKeyframes(
                    state.FieldId,
                    state.TargetId,
                    changes));
    }

    private IReadOnlyList<int> PlayheadSnapTargets() =>
        _lanes
            .Where((lane) => lane.IsVisible)
            .SelectMany((lane) => lane.SnapFrames)
            .Concat(_keyframeFrames)
            .Concat(_referenceMarkers.Select((marker) => marker.Frame))
            .Distinct()
            .Order()
            .ToList();

    private IReadOnlyList<int> LaneSnapTargets(
        PreviewScreenTimelineLane active) =>
        _lanes
            .Where((lane) => !ReferenceEquals(lane, active) && lane.IsVisible)
            .SelectMany((lane) => lane.SnapFrames)
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
        bool isPlaying,
        double zoom)
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
        var playButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.Play, 15),
            "Play or pause Screen Timeline",
            38);
        playButton.Click += (_, _) => _togglePlayback();
        _playButton = playButton;
        var frameText = new TextBlock
        {
            MinWidth = 84,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Opacity = 0.82,
        };
        _frameText = frameText;
        UpdatePlayButton(isPlaying);
        UpdateFrameText();

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                start,
                previous,
                playButton,
                next,
                end,
                new Border { Width = 6 },
                frameText,
            },
        };
        Grid.SetColumn(controls, 1);
        var zoomControl = new PreviewScreenTimelineZoomControl(zoom);
        zoomControl.ValueChanged += (_, value) => SetZoom(value);
        Grid.SetColumn(zoomControl, 2);
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{LabelWidth},*,Auto"),
            ColumnSpacing = 8,
        };
        grid.Children.Add(controls);
        grid.Children.Add(zoomControl);
        return grid;
    }

    private static TimelineRow AddTimelineRow(
        Grid timeline,
        string label,
        Control lane,
        double height,
        FontWeight fontWeight,
        Action? select = null)
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
        var labelHost = new Border
        {
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(4),
            Child = labelControl,
        };
        if (select is not null)
        {
            labelHost.Cursor = new Cursor(StandardCursorType.Hand);
            labelHost.PointerPressed += (_, args) =>
            {
                if (!PreviewScreenTimelinePointer.IsPrimaryPress(labelHost, args)) return;
                select();
                args.Handled = true;
            };
        }
        Grid.SetRow(labelHost, rowIndex);
        timeline.Children.Add(labelHost);
        lane.Height = height;
        Grid.SetColumn(lane, 1);
        Grid.SetRow(lane, rowIndex);
        timeline.Children.Add(lane);
        return new TimelineRow(labelHost, lane as PreviewScreenTimelineLane, lane);
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
        Grid.SetColumnSpan(button, 2);
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
        if (_snapshot is null || _frameText is null) return;
        _frameText.Text = _frame < 0
            ? $"{_frame} f · preroll"
            : _frame >= _previewContentDurationFrames
                ? $"+{_frame - _previewContentDurationFrames + 1} f · postroll"
                : $"{_frame}/{_previewContentDurationFrames - 1}";
    }

    private void UpdatePlayButton(bool isPlaying)
    {
        if (_playButton is null) return;
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

    private sealed record TimelineRow(
        Border Label,
        PreviewScreenTimelineLane? Lane,
        Control LaneControl)
    {
        public void SetVisible(bool isVisible)
        {
            Label.IsVisible = isVisible;
            LaneControl.IsVisible = isVisible;
        }

        public void SetSelected(bool isSelected) =>
            Label.Background = isSelected
                ? EditorSukiWindowTheme.AccentBrush(0x24)
                : Brushes.Transparent;
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
        IReadOnlyList<PreviewScreenTimelineReferenceMarker> referenceMarkers,
        Func<IReadOnlyList<int>> snapTargets,
        Action<PreviewScreenTimelinePlayheadChange> setFrame)
    {
        _viewport = viewport;
        _snapTargets = snapTargets;
        _setFrame = setFrame;
        MinWidth = 180;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Background = Brushes.Transparent;
        _ticks = new PreviewScreenTimelineRulerTicks(
            snapshot,
            viewport,
            referenceMarkers);
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

    public void SetReferenceMarkers(
        IReadOnlyList<PreviewScreenTimelineReferenceMarker> markers) =>
        _ticks.SetReferenceMarkers(markers);

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
    private IReadOnlyList<PreviewScreenTimelineReferenceMarker>
        _referenceMarkers;

    public PreviewScreenTimelineRulerTicks(
        PreviewScreenTimelineSnapshot snapshot)
        : this(
            snapshot,
            new PreviewScreenTimelineViewport(
                snapshot.MinimumFrame,
                snapshot.MaximumFrame),
            [])
    {
    }

    public PreviewScreenTimelineRulerTicks(
        PreviewScreenTimelineSnapshot snapshot,
        PreviewScreenTimelineViewport viewport,
        IReadOnlyList<PreviewScreenTimelineReferenceMarker> referenceMarkers)
        : base(snapshot, viewport)
    {
        _referenceMarkers = referenceMarkers;
        IsHitTestVisible = false;
    }

    public void SetReferenceMarkers(
        IReadOnlyList<PreviewScreenTimelineReferenceMarker> markers)
    {
        _referenceMarkers = markers;
        InvalidateVisual();
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
        foreach (var marker in _referenceMarkers)
        {
            if (marker.Frame < Viewport.MinimumFrame
                || marker.Frame > Viewport.MaximumFrame) continue;
            var x = X(marker.Frame);
            context.DrawLine(
                new Pen(EditorAnimationVisuals.ActiveTrackBrush, 2),
                new Point(x, 1),
                new Point(x, baselineY));
        }
    }
}

internal enum PreviewScreenTimelineLaneEditMode
{
    Move,
    Exit,
    StateBoundary,
    StateInterval,
}

internal sealed record PreviewScreenTimelineLaneEdit(
    PreviewScreenTimelineLaneEditMode Mode,
    int StartFrame,
    int EndFrame,
    PreviewScreenTimelineInterval? Interval = null,
    PreviewScreenTimelineInterval? UpdatedInterval = null);

internal sealed class PreviewScreenTimelineLane : PreviewScreenTimelineTrack
{
    private const double ExitHandleWidth = 12;
    private readonly string _key;
    private readonly string _label;
    private readonly string _targetId;
    private readonly bool _isGeneral;
    private readonly bool _isStateLane;
    private readonly bool _isEditable;
    private readonly bool _canResizeEnd;
    private readonly bool _movesOwnerOrigin;
    private readonly bool _ownsScreenDuration;
    private readonly bool _canMove;
    private readonly int _authoredStartFrame;
    private readonly Func<PreviewScreenTimelineLane, IReadOnlyList<int>>
        _snapTargets;
    private readonly List<PreviewScreenTimelineInterval> _intervals;
    private int _startFrame;
    private int _endFrame;
    private double _dragStartFrame;
    private int _dragStartValue;
    private int _dragEndValue;
    private int _activeIntervalIndex = -1;
    private PreviewScreenTimelineInterval? _dragInterval;
    private IReadOnlyList<PreviewScreenTimelineKeyframe> _keyframes = [];
    private int? _activeSnapFrame;
    private DragMode _dragMode;
    private bool _isSelected;

    public PreviewScreenTimelineLane(
        PreviewScreenTimelineSnapshot snapshot,
        PreviewScreenTimelineViewport viewport,
        string key,
        PreviewScreenTimelineItem item,
        bool isGeneral,
        Func<PreviewScreenTimelineLane, IReadOnlyList<int>> snapTargets)
        : base(snapshot, viewport)
    {
        _key = key;
        _label = item.Label;
        _targetId = isGeneral ? "" : item.Id;
        _isGeneral = isGeneral;
        _isStateLane = item.StateEdit is not null;
        _isEditable = item.SerialEdit is not null
            || item.StateEdit is not null
            || item.DurationEdit is not null;
        _canResizeEnd = item.StateEdit is not null
            || item.SerialEdit?.CanResizeEnd == true
            || item.DurationEdit is not null;
        _movesOwnerOrigin = item.SerialEdit is not null;
        _ownsScreenDuration = item.DurationEdit is not null;
        _canMove = item.SerialEdit is not null || item.StateEdit is not null;
        _authoredStartFrame = item.StartFrame;
        _snapTargets = snapTargets;
        _startFrame = Math.Max(0, item.StartFrame);
        _endFrame = Math.Max(_startFrame + 1, item.EndFrame);
        _intervals = item.Intervals.ToList();
        Cursor = new Cursor(_isEditable
            ? StandardCursorType.SizeWestEast
            : StandardCursorType.Hand);
        PointerPressed += OnPointerPressed;
        if (_isEditable)
        {
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerCaptureLost += (_, _) => EndDrag();
        }
    }

    public string Key => _key;
    public string Label => _label;
    public string TargetId => _targetId;
    public bool IsSelected => _isSelected;
    public int StartFrame => _startFrame;
    public int EndFrame => _endFrame;
    public bool AffectsScreenDuration => _movesOwnerOrigin;
    public bool OwnsScreenDuration => _ownsScreenDuration;
    public IReadOnlyList<int> SnapFrames => _isGeneral
        ? [0, Snapshot.ContentDurationFrames]
        : _intervals
            .SelectMany(interval => new[] { interval.StartFrame, interval.EndFrame })
            .Distinct()
            .ToList();
    public event EventHandler<PreviewScreenTimelineLaneEdit>? EditCommitted;
    public event Action<object?, double>? AuthoringExtentChanged;
    public event EventHandler<int?>? SnapGuideChanged;
    public event EventHandler? SelectionRequested;

    public void RequestSelection() =>
        SelectionRequested?.Invoke(this, EventArgs.Empty);

    public void SetAuthoringEndFrame(int endFrame)
    {
        _endFrame = Math.Max(_startFrame + 1, endFrame);
        if (_intervals.Count == 1)
        {
            _intervals[0] = _intervals[0] with
            {
                EndFrame = _endFrame,
            };
        }
        InvalidateVisual();
    }

    public void RebaseActiveDrag(double pointerX)
    {
        if (_dragMode == DragMode.None) return;
        _dragStartFrame = RawFrameAt(pointerX);
        _dragStartValue = _startFrame;
        _dragEndValue = _endFrame;
    }

    public void SetSelection(
        bool isSelected,
        IReadOnlyList<PreviewScreenTimelineKeyframe> keyframes)
    {
        _isSelected = isSelected;
        _keyframes = keyframes;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var height = Math.Max(2, Bounds.Height - 6);
        if (_isSelected)
        {
            context.DrawRectangle(
                EditorSukiWindowTheme.AccentBrush(0x24),
                null,
                new Rect(0, 1, Bounds.Width, Math.Max(2, Bounds.Height - 2)),
                4,
                4);
        }
        if (_isStateLane)
        {
            PreviewScreenTimelineHatch.Draw(
                context,
                new Rect(X(0), 3, Math.Max(0, X(Snapshot.ContentDurationFrames) - X(0)), height));
        }
        for (var index = 0; index < _intervals.Count; index++)
        {
            var interval = _intervals[index];
            var left = X(interval.StartFrame);
            var right = X(interval.EndFrame);
            var block = new Rect(left, 3, Math.Max(2, right - left), height);
            context.DrawRectangle(
                _isGeneral
                    ? new SolidColorBrush(Color.FromArgb(95, 47, 128, 237))
                    : new SolidColorBrush(Color.FromArgb(150, 47, 128, 237)),
                new Pen(
                    _activeSnapFrame is null || index != _activeIntervalIndex
                        ? EditorSukiWindowTheme.AccentBrush()
                        : EditorAnimationVisuals.ActiveTrackBrush,
                    _activeSnapFrame is null || index != _activeIntervalIndex ? 1 : 2),
                block,
                4,
                4);
            if (!_isEditable) continue;
            if ((_isStateLane && interval.StartKeyframeFrame is not null)
                || (!_isStateLane && _canMove))
            {
                context.DrawLine(new Pen(Brushes.White, 2),
                    new Point(left + 4, 7), new Point(left + 4, Bounds.Height - 7));
            }
            if ((_isStateLane && interval.EndKeyframeFrame is not null)
                || (!_isStateLane && _canResizeEnd))
            {
                context.DrawLine(new Pen(Brushes.White, 2),
                    new Point(right - 4, 7), new Point(right - 4, Bounds.Height - 7));
            }
        }
        if (_isSelected)
        {
            var ownerOffset = _movesOwnerOrigin
                ? _startFrame - _authoredStartFrame
                : 0;
            foreach (var keyframe in _keyframes)
            {
                var projectedFrame = keyframe.ScreenFrame + ownerOffset;
                if (projectedFrame < Viewport.MinimumFrame
                    || projectedFrame > Viewport.MaximumFrame)
                {
                    continue;
                }
                var x = X(projectedFrame);
                var y = Bounds.Height / 2;
                var marker = new StreamGeometry();
                using (var geometry = marker.Open())
                {
                    geometry.BeginFigure(new Point(x, y - 4), true);
                    geometry.LineTo(new Point(x + 4, y));
                    geometry.LineTo(new Point(x, y + 4));
                    geometry.LineTo(new Point(x - 4, y));
                    geometry.EndFigure(true);
                }
                context.DrawGeometry(
                    keyframe.IsProtected
                        ? Brushes.Transparent
                        : EditorAnimationVisuals.ActiveTrackBrush,
                    keyframe.IsProtected
                        ? new Pen(EditorAnimationVisuals.ActiveTrackBrush, 1.4)
                        : null,
                    marker);
            }
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!PreviewScreenTimelinePointer.IsPrimaryPress(this, args)) return;
        RequestSelection();
        if (!_isEditable)
        {
            args.Handled = true;
            return;
        }
        var x = args.GetPosition(this).X;
        _activeIntervalIndex = _intervals.FindIndex(interval =>
            x >= X(interval.StartFrame) - 5 && x <= X(interval.EndFrame) + 5);
        if (_activeIntervalIndex < 0) return;
        var selected = _intervals[_activeIntervalIndex];
        var left = X(selected.StartFrame);
        var right = X(selected.EndFrame);
        if (_isStateLane)
        {
            if (TouchesDerivedBoundary(selected, x, left, right))
            {
                args.Handled = true;
                return;
            }
            _dragMode = Math.Abs(x - left) <= ExitHandleWidth && selected.StartKeyframeFrame is not null
                ? DragMode.StateStart
                : Math.Abs(right - x) <= ExitHandleWidth && selected.EndKeyframeFrame is not null
                    ? DragMode.StateEnd
                    : selected.StartKeyframeFrame is not null && selected.EndKeyframeFrame is not null
                        ? DragMode.StateMove
                        : DragMode.None;
        }
        else
        {
            _dragMode = _canResizeEnd
                && right - x <= ExitHandleWidth
                    ? DragMode.Exit
                    : _canMove
                        ? DragMode.Move
                        : DragMode.None;
        }
        if (_dragMode == DragMode.None) return;
        _dragStartFrame = RawFrameAt(x);
        _dragStartValue = selected.StartFrame;
        _dragEndValue = selected.EndFrame;
        _dragInterval = selected;
        args.Pointer.Capture(this);
        args.Handled = true;
    }

    internal static bool TouchesDerivedBoundary(
        PreviewScreenTimelineInterval interval,
        double pointerX,
        double left,
        double right) =>
        interval.StartKeyframeFrame is null
            && Math.Abs(pointerX - left) <= ExitHandleWidth
        || interval.EndKeyframeFrame is null
            && Math.Abs(right - pointerX) <= ExitHandleWidth;

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        if (_dragMode == DragMode.None) return;
        var pointerX = args.GetPosition(this).X;
        var pointerFrame = RawFrameAt(pointerX);
        int? snapFrame;
        if (_dragMode is DragMode.Move or DragMode.StateMove)
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
            if (_dragMode == DragMode.StateMove && _dragInterval is { } original)
            {
                if (_startFrame < original.MinimumStartFrame)
                {
                    var correction = original.MinimumStartFrame - _startFrame;
                    _startFrame += correction;
                    _endFrame += correction;
                }
                if (_endFrame > original.MaximumEndFrame)
                {
                    var correction = _endFrame - original.MaximumEndFrame;
                    _startFrame -= correction;
                    _endFrame -= correction;
                }
            }
            snapFrame = next.SnapFrame;
        }
        else if (_dragMode == DragMode.Exit)
        {
            var next = PreviewScreenTimelineMath.ResizeEndWithSnap(
                _startFrame,
                _dragEndValue + pointerFrame - _dragStartFrame,
                Bounds.Width,
                Viewport.MinimumFrame,
                Viewport.MaximumFrame,
                _snapTargets(this));
            _endFrame = next.EndFrame;
            snapFrame = next.SnapFrame;
        }
        else
        {
            var next = PreviewScreenTimelineMath.SnapFrame(
                pointerX,
                Bounds.Width,
                Viewport.MinimumFrame,
                Viewport.MaximumFrame,
                _snapTargets(this));
            if (_dragMode == DragMode.StateStart)
                _startFrame = Math.Clamp(
                    next.Frame,
                    _dragInterval?.MinimumStartFrame ?? 0,
                    _dragEndValue - 1);
            else
                _endFrame = Math.Clamp(
                    next.Frame,
                    _dragStartValue + 1,
                    _dragInterval?.MaximumEndFrame ?? Snapshot.ContentDurationFrames);
            snapFrame = next.IsSnapped ? next.Frame : null;
        }
        _intervals[_activeIntervalIndex] = (_dragInterval ?? _intervals[_activeIntervalIndex]) with
        {
            StartFrame = _startFrame,
            EndFrame = _endFrame,
        };
        SnapGuideChanged?.Invoke(this, snapFrame);
        _activeSnapFrame = snapFrame;
        InvalidateVisual();
        AuthoringExtentChanged?.Invoke(this, pointerX);
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
        var mode = _dragMode;
        var original = _dragInterval;
        var updated = _activeIntervalIndex >= 0 ? _intervals[_activeIntervalIndex] : null;
        _dragMode = DragMode.None;
        _activeSnapFrame = null;
        InvalidateVisual();
        SnapGuideChanged?.Invoke(this, null);
        EditCommitted?.Invoke(this, new PreviewScreenTimelineLaneEdit(
            mode switch
            {
                DragMode.Move => PreviewScreenTimelineLaneEditMode.Move,
                DragMode.Exit => PreviewScreenTimelineLaneEditMode.Exit,
                DragMode.StateMove => PreviewScreenTimelineLaneEditMode.StateInterval,
                _ => PreviewScreenTimelineLaneEditMode.StateBoundary,
            },
            _startFrame,
            _endFrame,
            original,
            updated));
        _dragInterval = null;
        _activeIntervalIndex = -1;
    }

    private enum DragMode
    {
        None,
        Move,
        Exit,
        StateStart,
        StateEnd,
        StateMove,
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
        Width = 168;
        Height = 38;
        Padding = new Thickness(6, 0);
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
            5.5,
            5.5);
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
    private int _contentEndFrame;

    public PreviewScreenTimelineBackdrop(
        PreviewScreenTimelineSnapshot snapshot,
        PreviewScreenTimelineViewport viewport)
        : base(snapshot, viewport)
    {
        _contentEndFrame = snapshot.ContentDurationFrames;
        IsHitTestVisible = false;
    }

    public void SetContentEndFrame(int frame)
    {
        _contentEndFrame = Math.Max(1, frame);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var zeroX = X(0);
        var endX = X(_contentEndFrame);
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
