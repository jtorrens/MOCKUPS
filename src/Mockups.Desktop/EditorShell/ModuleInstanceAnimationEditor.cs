using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ModuleInstanceAnimationEditor
{
    private readonly ModuleInstanceAnimationDocumentStore _animationDocuments;
    private readonly RuntimeInputOptionsDataSource _runtimeInputOptions;
    private readonly EditorDictionaryFieldServices _dictionaryServices;
    private readonly IEditorShellMessageSink _messages;
    private readonly Action _onChanged;
    private readonly EditorSessionUiState _sessionUiState;
    private readonly Func<int> _shotFrame;
    private readonly Action<int> _setShotFrame;
    private readonly PreviewPlaybackState _playbackState;
    private readonly Action _togglePlayback;
    private EditorDictionaryContextSnapshot?
        _preparedDictionaryContext;
    private ModuleInstanceAnimationSnapshot?
        _preparedAnimationSnapshot;
    private IRuntimeInputOptionsDataSource ActiveInputOptions =>
        _preparedDictionaryContext is null
            ? _runtimeInputOptions
            : new PreparedRuntimeInputOptionsDataSource(
                _preparedDictionaryContext);

    public ModuleInstanceAnimationEditor(
        IModuleInstanceAnimationStore animation,
        IModuleInstanceTimelineStore timeline,
        IModuleInstanceThemeTokenQuery moduleInstanceThemes,
        IDictionaryFieldContextRepository dictionary,
        IActorPreviewRepository actors,
        EditorOperationCoordinator operations,
        EditorDictionaryFieldServices dictionaryServices,
        IEditorShellMessageSink messages,
        Action onChanged,
        EditorSessionUiState sessionUiState,
        Func<int> shotFrame,
        Action<int> setShotFrame,
        PreviewPlaybackState playbackState,
        Action togglePlayback)
    {
        var timelineDataSource =
            new ModuleInstanceTimelineDataSource(
                timeline,
                moduleInstanceThemes);
        _animationDocuments = new ModuleInstanceAnimationDocumentStore(
            animation,
            timeline,
            moduleInstanceThemes,
            timelineDataSource,
            operations);
        _runtimeInputOptions =
            new RuntimeInputOptionsDataSource(dictionary, actors);
        _dictionaryServices = dictionaryServices;
        _messages = messages;
        _onChanged = onChanged;
        _sessionUiState = sessionUiState;
        _shotFrame = shotFrame;
        _setShotFrame = setShotFrame;
        _playbackState = playbackState;
        _togglePlayback = togglePlayback;
    }

    public ModuleInstanceAnimationSnapshot PrepareSnapshot(
        ProjectTreeNode node)
    {
        return _animationDocuments.LoadSnapshot(node.Id);
    }

    public void UsePreparedContext(
        EditorDictionaryContextSnapshot? dictionaryContext,
        ModuleInstanceAnimationSnapshot? animationSnapshot)
    {
        _preparedDictionaryContext = dictionaryContext;
        _preparedAnimationSnapshot = animationSnapshot;
    }

    public AnimationTargetEditorContent CreateTargetContent(ProjectTreeNode node, string targetId)
    {
        return CreateContent(
            node,
            $"target:{targetId}",
            (target) => target.TargetId == targetId,
            targetId);
    }

    public string ResolveRuntimeValue(
        ProjectTreeNode node,
        ComponentInputDefinition input,
        string targetId,
        string baseValue)
    {
        var snapshot = PreparedSnapshot(node);
        var source = snapshot.Source;
        var preview = DesignPreviewTestValues.Parse(source.RuntimePreviewJson);
        var animation = DesignPreviewTestValues.Parse(source.AnimationJson);
        var track = new ModuleInstanceAnimationDocument(animation.ToJsonString()).Track(input.Id, targetId);
        if (track is null) return baseValue;
        var themeTokens = DesignPreviewTestValues.Parse(source.ThemeTokensJson);
        var screenFrame =
            _shotFrame()
            - snapshot.ActionStartFrame;
        var ownerFrame = RuntimeAnimationFrameOrigin.OwnerLocalFrame(
            preview,
            preview,
            animation,
            targetId,
            screenFrame,
            themeTokens);
        var fieldOrigin = RuntimeAnimationFrameOrigin.FieldOwnerFrameOrigin(
            preview,
            preview,
            animation,
            input.Id,
            targetId,
            themeTokens);
        return ModuleInstanceAnimationValueResolver.ResolveDisplayValue(
            track,
            ownerFrame - fieldOrigin,
            ValueNode(input.ValueKind, baseValue),
            input.ValueKind);
    }

    public void AddInitialTrack(
        ModuleInstanceAnimationDocument document,
        ProjectTreeNode node,
        ComponentInputDefinition input,
        string targetId,
        string baseValue)
    {
        var animationDefinition = input.Animation
            ?? throw new InvalidOperationException($"Input '{input.Id}' is not animatable.");
        var value = ValueNode(input.ValueKind, baseValue);
        if (string.IsNullOrWhiteSpace(animationDefinition.BaseDurationFieldId)
            || !animationDefinition.Interpolations.Contains("writeOn", StringComparer.Ordinal)
            || input.ValueKind is not (ValueKind.StringSingleLine or ValueKind.StringMultiline))
        {
            document.AddTrack(
                input.Id,
                targetId,
                value,
                animationDefinition.Interpolations.First());
            return;
        }

        var snapshot = PreparedSnapshot(node);
        var source = snapshot.Source;
        var preview = DesignPreviewTestValues.Parse(source.RuntimePreviewJson);
        var animation = DesignPreviewTestValues.Parse(document.ToJson());
        var themeTokens = DesignPreviewTestValues.Parse(source.ThemeTokensJson);
        var completionFrame = RuntimeAnimationFrameOrigin.FieldReferenceDurationFrames(
            preview,
            preview,
            animation,
            input.Id,
            targetId,
            themeTokens);
        document.AddWriteOnTrack(
            input.Id,
            targetId,
            value,
            completionFrame);
    }

    public AnimationTargetEditorContent CreateCollectionContent(
        ProjectTreeNode node,
        RuntimeInputCollectionDefinition collection)
    {
        var preview = DesignPreviewTestValues.Parse(
            PreparedSnapshot(node).Source.RuntimePreviewJson);
        var items = DesignPreviewTestValues.CollectionItems(preview, collection).ToList();
        var content = new StackPanel
        {
            Spacing = EditorUiDensity.Card(12),
        };
        var activeTrackCount = 0;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var itemId = item["id"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(itemId))
            {
                continue;
            }
            var itemAnimation = CreateTargetContent(node, itemId);
            if (itemAnimation.ActiveTrackCount == 0)
            {
                continue;
            }
            var label = RuntimeCollectionItemPresentation.Resolve(
                collection,
                item,
                index,
                $"{collection.ItemLabel} {index + 1}",
                $"Payload item {index + 1}",
                EditorIcons.Component).Title;
            content.Children.Add(EditorGroupBlock.CreateInlineSection(label));
            content.Children.Add(itemAnimation.Content);
            activeTrackCount += itemAnimation.ActiveTrackCount;
        }
        if (activeTrackCount == 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = "Activa el rombo de un Runtime Value para crear su track de animación.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.72,
            });
        }
        return new AnimationTargetEditorContent(content, activeTrackCount);
    }

    private AnimationTargetEditorContent CreateContent(
        ProjectTreeNode node,
        string scopeKey,
        Func<AnimationTarget, bool> includesTarget,
        string durationTargetId)
    {
        var snapshot = PreparedSnapshot(node);
        var source = snapshot.Source;
        var preview = DesignPreviewTestValues.Parse(source.RuntimePreviewJson);
        var config = DesignPreviewTestValues.Parse(source.VariantConfigJson);
        var animation = DesignPreviewTestValues.Parse(source.AnimationJson);
        var themeTokens = DesignPreviewTestValues.Parse(source.ThemeTokensJson);
        List<AnimationTarget> ReadScopeTargets(JsonObject currentAnimation) => ReadTargets(
                preview,
                config,
                currentAnimation,
                themeTokens)
            .Where(includesTarget)
            .ToList();
        var targets = ReadScopeTargets(animation);
        var targetKeys = targets
            .Select((target) => (target.FieldId, target.TargetId))
            .ToHashSet();
        var document = new ModuleInstanceAnimationDocument(source.AnimationJson);
        var resolvedTargets = document.Tracks
            .Where((track) => targetKeys.Contains((track.FieldId, track.TargetId)))
            .Select((track) => new ResolvedAnimationTarget(
                targets.FirstOrDefault((target) => target.FieldId == track.FieldId && target.TargetId == track.TargetId),
                track))
            .ToList();
        var activeTrackCount = resolvedTargets.Count;
        var content = new StackPanel { Spacing = EditorUiDensity.Card(12) };
        if (resolvedTargets.Count == 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = "Activa el rombo de un Runtime Value para crear su track de animación.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.72,
            });
        }
        else
        {
            content.Children.Add(CreateTimelineEditor(
                node,
                document,
                resolvedTargets,
                scopeKey,
                durationTargetId,
                preview,
                animation,
                themeTokens,
                source.EffectiveContractJson,
                snapshot,
                ReadScopeTargets));
        }
        return new AnimationTargetEditorContent(content, activeTrackCount);
    }

    private Control CreateTimelineEditor(
        ProjectTreeNode node,
        ModuleInstanceAnimationDocument document,
        List<ResolvedAnimationTarget> targets,
        string scopeKey,
        string durationTargetId,
        JsonObject preview,
        JsonObject animation,
        JsonObject themeTokens,
        string effectiveContractJson,
        ModuleInstanceAnimationSnapshot preparedSnapshot,
        Func<JsonObject, List<AnimationTarget>> readScopeTargets)
    {
        var screenStartFrame =
            preparedSnapshot.ActionStartFrame;
        var actualScreenDuration = preparedSnapshot.DurationFrames;
        var durationPolicy = RuntimeDurationContract.Policy(effectiveContractJson);
        var currentAnimation = animation;
        var usesOwnerTimeline = !string.IsNullOrWhiteSpace(durationTargetId);
        var commands =
            new ModuleInstanceAnimationCommandCoordinator(
                preparedSnapshot.Source.AnimationJson,
                (candidateJson) =>
                    _animationDocuments
                        .SaveAnimationSnapshotAsync(
                            node.Id,
                            candidateJson));
        int TimelineFrameForScreenFrame(int screenFrame) =>
            AnimationTimelineCoordinateSpace.TimelineFrameForScreenFrame(
                usesOwnerTimeline,
                screenFrame,
                (candidateScreenFrame) => RuntimeAnimationFrameOrigin.OwnerLocalFrame(
                    preview,
                    preview,
                    currentAnimation,
                    durationTargetId,
                    candidateScreenFrame,
                    themeTokens));
        int ScreenFrameForTimelineFrame(double timelineFrame) =>
            AnimationTimelineCoordinateSpace.ScreenFrameForTimelineFrame(
                usesOwnerTimeline,
                timelineFrame,
                (candidateOwnerFrame) => RuntimeAnimationFrameOrigin.ScreenFrameForOwnerFrame(
                    preview,
                    preview,
                    currentAnimation,
                    durationTargetId,
                    candidateOwnerFrame,
                    themeTokens));
        int MarkerTimelineFrame(
            ResolvedAnimationTarget candidate,
            AnimationKeyframeView keyframe)
        {
            return AnimationTimelineCoordinateSpace.MarkerFrame(
                usesOwnerTimeline,
                candidate.Target?.OwnerFrameOrigin ?? 0,
                keyframe.Frame,
                candidate.Target?.ScreenFrameForOwnerFrame ?? ((_) => keyframe.Frame));
        }
        int MaximumAuthoredTimelineFrame() => targets
            .SelectMany((candidate) => (candidate.Track?.Keyframes ?? [])
                .Where((keyframe) => keyframe.Enabled)
                .Select((keyframe) => MarkerTimelineFrame(candidate, keyframe)))
            .DefaultIfEmpty(-1)
            .Max();
        int OwnerNaturalDuration() => usesOwnerTimeline
            ? RuntimeAnimationFrameOrigin.OwnerNaturalDuration(
                preview,
                preview,
                currentAnimation,
                durationTargetId,
                themeTokens)
            : actualScreenDuration;
        int ReferenceNaturalDuration() => Math.Max(
            OwnerNaturalDuration(),
            targets
                .Where((candidate) => candidate.Target is not null)
                .Select((candidate) => (int)Math.Ceiling(
                    candidate.Target!.OwnerFrameOrigin + candidate.Target.ReferenceDurationFrames))
                .DefaultIfEmpty(1)
                .Max());
        int CalculatedAuthoringDuration(int maximumAuthoredFrame)
        {
            var referenceDuration = usesOwnerTimeline
                ? ReferenceNaturalDuration()
                : targets
                    .Where((candidate) => candidate.Target is { ReferenceDurationFrames: > 0 })
                    .Select((candidate) => candidate.Target!.ScreenFrameForOwnerFrame(
                        candidate.Target.OwnerFrameOrigin + candidate.Target.ReferenceDurationFrames))
                    .DefaultIfEmpty(actualScreenDuration)
                    .Max();
            return Math.Max(
                Math.Max(
                    usesOwnerTimeline ? ReferenceNaturalDuration() : actualScreenDuration,
                    maximumAuthoredFrame + 1),
                referenceDuration);
        }
        var maximumAuthoredTimelineFrame = MaximumAuthoredTimelineFrame();
        var calculatedAuthoringDuration = CalculatedAuthoringDuration(maximumAuthoredTimelineFrame);
        var timelineDuration = !usesOwnerTimeline && durationPolicy == RuntimeDurationPolicy.Explicit
            ? actualScreenDuration
            : calculatedAuthoringDuration;
        var naturalTimelineDuration = usesOwnerTimeline
            ? ReferenceNaturalDuration()
            : actualScreenDuration;
        var hasOutOfRangeKeyframes = maximumAuthoredTimelineFrame >= naturalTimelineDuration;
        var currentFrame = Math.Clamp(
            TimelineFrameForScreenFrame(_shotFrame() - screenStartFrame),
            0,
            timelineDuration - 1);
        int TimelineFrame() => Math.Clamp(currentFrame, 0, timelineDuration - 1);
        var selectionKey = $"{node.Id}:animation-properties:{scopeKey}";
        var selectedId = _sessionUiState.Selection(selectionKey);
        var selected = targets.FirstOrDefault((target) => TargetKey(target) == selectedId)
            ?? targets.FirstOrDefault((target) => target.Track is not null)
            ?? targets.First();
        double OwnerFrame() => usesOwnerTimeline
            ? TimelineFrame()
            : RuntimeAnimationFrameOrigin.OwnerLocalFrame(
                preview,
                preview,
                currentAnimation,
                selected.Target?.TargetId ?? "",
                TimelineFrame(),
                themeTokens);
        selectedId = TargetKey(selected);
        _sessionUiState.Select(selectionKey, selectedId);
        var root = new StackPanel { Spacing = EditorUiDensity.Card(12) };
        var frameText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var authoringLimitText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.56,
        };
        var frameCounter = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            MinWidth = 96,
            Children = { frameText, authoringLimitText },
        };
        var playhead = new AnimationTimelinePlayhead(
            TimelineFrame(),
            timelineDuration);
        var timelineHost = new ContentControl();
        var trackList = new StackPanel { Spacing = EditorUiDensity.Card(4) };
        var detailHost = new ContentControl();
        var currentKeyframeButton = EditorTimelineTransport.CreateNavigationButton(
            EditorTimelineTransport.CreateKeyframeGlyph(
                filled: false,
                size: 16,
                brush: EditorAnimationVisuals.InactiveTrackBrush),
            "No keyframe at the current frame",
            34);
        var playbackButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.Play, 16),
            "Play Screen animation",
            42);
        EditorTimelineTransport.ApplyPrimaryStyle(playbackButton);
        var frameUpdateGate = new TimelineFrameUpdateGate();

        void SetFrame(int timelineFrame)
        {
            frameUpdateGate.Run(() =>
            {
                currentFrame = Math.Clamp(timelineFrame, 0, timelineDuration - 1);
                playhead.SetFrame(TimelineFrame());
                var screenFrame = ScreenFrameForTimelineFrame(currentFrame);
                if (screenFrame < actualScreenDuration)
                {
                    _setShotFrame(screenStartFrame + screenFrame);
                }
            });
            RefreshVisuals();
        }

        void PreviewDraggedFrame(int timelineFrame)
        {
            frameUpdateGate.Run(() =>
            {
                currentFrame = Math.Clamp(timelineFrame, 0, timelineDuration - 1);
                playhead.SetFrame(TimelineFrame());
                frameText.Text = $"{TimelineFrame()}/{timelineDuration - 1}";
                var screenFrame = ScreenFrameForTimelineFrame(currentFrame);
                if (screenFrame < actualScreenDuration)
                {
                    _setShotFrame(screenStartFrame + screenFrame);
                }
            });
        }

        async Task SaveAndRefresh(
            Func<ModuleInstanceAnimationDocument, bool>
                mutation)
        {
            var selectedKey = TargetKey(selected);
            var authoringHorizon = timelineDuration;
            var result = await commands.ExecuteAsync(
                mutation);
            document =
                new ModuleInstanceAnimationDocument(
                    result.ConfirmedAnimationJson);
            if (!result.Succeeded)
            {
                currentAnimation =
                    DesignPreviewTestValues.Parse(
                        result.ConfirmedAnimationJson);
                RefreshTargetBindings(
                    selectedKey);
                _messages.Error(
                    "Save animation",
                    result.Error
                    ?? new InvalidOperationException(
                        "Animation persistence failed."));
                RefreshVisuals();
                return;
            }
            if (result.Snapshot is null)
            {
                return;
            }

            preparedSnapshot = result.Snapshot;
            _preparedAnimationSnapshot =
                preparedSnapshot;
            currentAnimation = DesignPreviewTestValues.Parse(
                preparedSnapshot.Source.AnimationJson);
            screenStartFrame =
                preparedSnapshot.ActionStartFrame;
            actualScreenDuration =
                preparedSnapshot.DurationFrames;
            RefreshTargetBindings(
                selectedKey);
            maximumAuthoredTimelineFrame = MaximumAuthoredTimelineFrame();
            naturalTimelineDuration = usesOwnerTimeline
                ? ReferenceNaturalDuration()
                : actualScreenDuration;
            calculatedAuthoringDuration = CalculatedAuthoringDuration(maximumAuthoredTimelineFrame);
            hasOutOfRangeKeyframes = maximumAuthoredTimelineFrame >= naturalTimelineDuration;
            timelineDuration = !usesOwnerTimeline && durationPolicy == RuntimeDurationPolicy.Explicit
                ? Math.Max(actualScreenDuration, authoringHorizon)
                : Math.Max(calculatedAuthoringDuration, authoringHorizon);
            currentFrame = Math.Clamp(currentFrame, 0, timelineDuration - 1);
            playhead.SetDuration(timelineDuration);
            _onChanged();
            RefreshVisuals();
        }

        void RefreshTargetBindings(
            string selectedKey)
        {
            var refreshedTargets =
                readScopeTargets(currentAnimation)
                .ToDictionary((candidate) => (candidate.FieldId, candidate.TargetId));
            for (var index = 0; index < targets.Count; index++)
            {
                var candidate = targets[index];
                var fieldId = candidate.Target?.FieldId ?? candidate.Track?.FieldId ?? "";
                var targetId = candidate.Target?.TargetId ?? candidate.Track?.TargetId ?? "";
                refreshedTargets.TryGetValue((fieldId, targetId), out var refreshedTarget);
                targets[index] = candidate with
                {
                    Target = refreshedTarget,
                    Track = document.Track(fieldId, targetId),
                };
            }
            selected = targets.FirstOrDefault((candidate) => TargetKey(candidate) == selectedKey)
                ?? targets[0];
        }

        void RefreshVisuals()
        {
            frameText.Text = $"{TimelineFrame()}/{timelineDuration - 1}";
            authoringLimitText.Text = hasOutOfRangeKeyframes
                ? $"({maximumAuthoredTimelineFrame} · keyframe outside {(usesOwnerTimeline ? "item" : "Screen")})"
                : timelineDuration > naturalTimelineDuration ? $"({timelineDuration - 1})" : "";
            authoringLimitText.Foreground = hasOutOfRangeKeyframes
                ? EditorAnimationVisuals.ActiveTrackBrush
                : null;
            var selectedLocalFrame = (int)Math.Round(OwnerFrame(), MidpointRounding.AwayFromZero) - (int)Math.Round(
                selected.Target?.OwnerFrameOrigin ?? 0,
                MidpointRounding.AwayFromZero);
            var currentKeyframe = selected.Track?.Keyframes.FirstOrDefault(
                (keyframe) => keyframe.Enabled && keyframe.Frame == selectedLocalFrame);
            var hasCurrentKeyframe = currentKeyframe is not null;
            currentKeyframeButton.Content = EditorTimelineTransport.CreateKeyframeGlyph(
                filled: hasCurrentKeyframe && currentKeyframe!.Frame > 0,
                size: 16,
                brush: hasCurrentKeyframe
                    ? EditorAnimationVisuals.CurrentKeyframeBrush
                    : EditorAnimationVisuals.InactiveTrackBrush);
            EditorAccessibility.Describe(
                currentKeyframeButton,
                hasCurrentKeyframe
                    ? currentKeyframe!.Frame == 0
                        ? "Protected origin keyframe at the current frame"
                        : "Keyframe at the current frame"
                    : "No keyframe at the current frame");
            playbackButton.Content = EditorIcons.Create(
                _playbackState.IsPlaying ? EditorIcons.Pause : EditorIcons.Play,
                16);
            EditorAccessibility.Describe(
                playbackButton,
                _playbackState.IsPlaying ? "Pause Screen animation" : "Play Screen animation");
            timelineHost.Content = CreateMiniTimeline(
                targets,
                selected,
                TimelineFrame(),
                timelineDuration,
                usesOwnerTimeline,
                SetFrame,
                PreviewDraggedFrame,
                (target, keyframe, destinationFrame) =>
                    SaveAndRefresh(
                        (candidate) =>
                            candidate.TryMoveKeyframe(
                                target.Track!.FieldId,
                                target.Track.TargetId,
                                keyframe.Frame,
                                destinationFrame)));
            trackList.Children.Clear();
            foreach (var target in targets)
            {
                var selectedTarget = ReferenceEquals(target, selected);
                var active = target.Track is not null;
                var button = new Button
                {
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = selectedTarget ? EditorSukiWindowTheme.AccentBrush(0x18) : Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    Foreground = selectedTarget
                        ? EditorAnimationVisuals.ActiveTrackBrush
                        : EditorAnimationVisuals.OtherKeyframeBrush,
                    Content = CreateTrackSummary(
                        active,
                        target.Label,
                        target.Track?.Keyframes.Count ?? 0,
                        selectedTarget
                            ? EditorAnimationVisuals.ActiveTrackBrush
                            : EditorAnimationVisuals.OtherKeyframeBrush),
                };
                button.Click += (_, _) =>
                {
                    selected = target;
                    _sessionUiState.Select(selectionKey, TargetKey(target));
                    RefreshVisuals();
                };
                trackList.Children.Add(button);
            }
            detailHost.Content = CreateTrackDetail(
                node,
                selected,
                (int)Math.Round(OwnerFrame(), MidpointRounding.AwayFromZero),
                (ownerFrame) => SetFrame(usesOwnerTimeline
                    ? ownerFrame
                    : selected.Target?.ScreenFrameForOwnerFrame(ownerFrame) ?? TimelineFrame()),
                SaveAndRefresh);
        }

        var firstFrameButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelineFirstFrame, 16),
            usesOwnerTimeline ? "First item frame" : "First Screen frame");
        firstFrameButton.Click += (_, _) => SetFrame(0);
        var previousFrameButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelinePreviousFrame, 16),
            usesOwnerTimeline ? "Previous item frame" : "Previous Screen frame");
        previousFrameButton.Click += (_, _) => SetFrame(TimelineFrame() - 1);
        currentKeyframeButton.Click += (_, _) => SetFrame(TimelineFrame());
        playbackButton.Click += (_, _) =>
        {
            if (usesOwnerTimeline && !_playbackState.IsPlaying)
            {
                var screenFrame = ScreenFrameForTimelineFrame(TimelineFrame());
                if (screenFrame < actualScreenDuration)
                {
                    _setShotFrame(screenStartFrame + screenFrame);
                }
            }
            _togglePlayback();
        };
        var nextFrameButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelineNextFrame, 16),
            usesOwnerTimeline ? "Next item frame" : "Next Screen frame");
        nextFrameButton.Click += (_, _) => SetFrame(TimelineFrame() + 1);
        var lastFrameButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelineLastFrame, 16),
            usesOwnerTimeline ? "Last item frame" : "Last Screen frame");
        lastFrameButton.Click += (_, _) => SetFrame(timelineDuration - 1);
        var transport = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = EditorUiDensity.Card(4),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                EditorTimelineTransport.CreateSeparator(EditorSukiWindowTheme.IsDark(null)),
                currentKeyframeButton,
                firstFrameButton,
                previousFrameButton,
                playbackButton,
                nextFrameButton,
                lastFrameButton,
                EditorTimelineTransport.CreateSeparator(EditorSukiWindowTheme.IsDark(null)),
                frameCounter,
            },
        };
        root.Children.Add(transport);
        playhead.FrameChanged += (_, frame) =>
        {
            if (!frameUpdateGate.IsActive)
                SetFrame((int)Math.Round(frame, MidpointRounding.AwayFromZero));
        };
        var extendHorizonButton = EditorTimelineTransport.CreateNavigationButton(
            new TextBlock
            {
                Text = "+",
                FontSize = 18,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            },
            "Extend the animation authoring horizon",
            34);
        extendHorizonButton.Click += (_, _) =>
        {
            timelineDuration += 10;
            playhead.SetDuration(timelineDuration);
            RefreshVisuals();
        };
        var timelineLane = new Grid
        {
            Height = 54,
        };
        timelineLane.Children.Add(playhead);
        timelineHost.VerticalAlignment = VerticalAlignment.Bottom;
        timelineLane.Children.Add(timelineHost);
        var timelineControl = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = EditorUiDensity.Card(6),
        };
        timelineControl.Children.Add(timelineLane);
        extendHorizonButton.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(extendHorizonButton, 1);
        timelineControl.Children.Add(extendHorizonButton);
        root.Children.Add(timelineControl);
        if (usesOwnerTimeline)
        {
            root.Children.Add(CreateTargetDurationEditor(
                node,
                document,
                durationTargetId,
                ReferenceNaturalDuration(),
                SaveAndRefresh));
        }
        root.Children.Add(EditorGroupBlock.CreateSeparator());
        root.Children.Add(trackList);
        root.Children.Add(detailHost);
        void OnPlaybackChanged()
        {
            if (frameUpdateGate.IsActive) return;
            frameUpdateGate.Run(() =>
            {
                currentFrame = Math.Clamp(
                    TimelineFrameForScreenFrame(_shotFrame() - screenStartFrame),
                    0,
                    timelineDuration - 1);
                playhead.SetFrame(TimelineFrame());
            });
            RefreshVisuals();
        }
        PreviewPlaybackStateBinding.Attach(root, _playbackState, OnPlaybackChanged);
        RefreshVisuals();
        return root;
    }

    private Control CreateTrackDetail(
        ProjectTreeNode node,
        ResolvedAnimationTarget selected,
        int ownerFrame,
        Action<int> setFrame,
        Func<
            Func<ModuleInstanceAnimationDocument, bool>,
            Task> saveMutation)
    {
        if (selected.Target is null)
            return new TextBlock { Text = "El target de este track ya no existe.", Foreground = EditorAnimationVisuals.ActiveTrackBrush };
        var target = selected.Target;
        if (selected.Track is null)
        {
            var activate = new Button
            {
                Content = CreateAnimationActivationLabel(target.Label),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            activate.Click += (_, _) =>
            {
                _ = saveMutation((candidate) =>
                {
                    AddInitialTrack(
                        candidate,
                        node,
                        target.Input,
                        target.TargetId,
                        target.BaseValue);
                    return true;
                });
            };
            return activate;
        }
        var localFrame = ownerFrame - (int)Math.Round(target.OwnerFrameOrigin, MidpointRounding.AwayFromZero);
        var enabledKeyframes = selected.Track.Keyframes.Where((keyframe) => keyframe.Enabled).ToList();
        var exact = enabledKeyframes.FirstOrDefault((keyframe) => keyframe.Frame == localFrame);
        var previous = enabledKeyframes.LastOrDefault((keyframe) => keyframe.Frame < localFrame);
        var next = enabledKeyframes.FirstOrDefault((keyframe) => keyframe.Frame > localFrame);
        var panel = new StackPanel { Spacing = EditorUiDensity.Card(9) };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*,Auto"), ColumnSpacing = EditorUiDensity.Card(6) };
        AddTransportButton(
            header,
            0,
            EditorTimelineTransport.CreateKeyframeStepIcon(next: false),
            "Previous keyframe",
            () => { if (previous is not null) setFrame((int)Math.Round(target.OwnerFrameOrigin) + previous.Frame); },
            previous is not null,
            width: 38);
        var keyframeButton = new Button
        {
            Content = EditorTimelineTransport.CreateKeyframeGlyph(
                filled: exact is not null && exact.Frame > 0,
                size: 16,
                brush: exact is not null
                    ? EditorAnimationVisuals.CurrentKeyframeBrush
                    : EditorAnimationVisuals.ActiveTrackBrush),
            Width = 34,
            Height = 30,
            Padding = EditorUiDensity.CardThickness(0),
        };
        EditorAccessibility.Describe(
            keyframeButton,
            exact is null
                ? "Create keyframe at current frame"
                : exact.Frame == 0
                    ? "Protected origin keyframe"
                    : "Update current keyframe");
        if (exact?.Frame == 0)
        {
            ToolTip.SetTip(keyframeButton, "Local frame 0 · Protected");
        }
        Grid.SetColumn(keyframeButton, 1);
        header.Children.Add(keyframeButton);
        AddTransportButton(
            header,
            2,
            EditorTimelineTransport.CreateKeyframeStepIcon(next: true),
            "Next keyframe",
            () => { if (next is not null) setFrame((int)Math.Round(target.OwnerFrameOrigin) + next.Frame); },
            next is not null,
            width: 38);
        var count = new TextBlock
        {
            Text = EditorUiText.Count(enabledKeyframes.Count, "keyframe"),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.72,
        };
        Grid.SetColumn(count, 4);
        header.Children.Add(count);
        panel.Children.Add(header);

        var displayedValue = ModuleInstanceAnimationValueResolver.ResolveDisplayValue(
            selected.Track,
            localFrame,
            ValueNode(target.Input.ValueKind, target.BaseValue),
            target.Input.ValueKind);
        var valueControl = new DictionaryFieldControl(
            new FieldValue(RuntimeInputFieldDefinitionFactory.Create(ActiveInputOptions, node, target.Input), displayedValue),
            DictionaryServices(node));
        var animation = target.Input.Animation!;
        var selectedInterpolation = exact?.Interpolation
            ?? next?.Interpolation
            ?? animation.Interpolations.First();
        var interpolationControl = new DictionaryFieldControl(
            new FieldValue(
                new FieldDefinition(
                    $"animation.interpolation.{target.FieldId}",
                    "Interpolation",
                    ValueKind.OptionToken,
                    DefaultValue: animation.Interpolations.First(),
                    Options: animation.Interpolations
                        .Select((value) => new FieldOption(value, InterpolationLabel(value)))
                        .ToList()),
                selectedInterpolation),
            DictionaryServices(node));
        void SaveValue(string value, string interpolation)
        {
            _ = saveMutation((candidate) =>
            {
                candidate.UpsertKeyframe(
                    target.FieldId,
                    target.TargetId,
                    localFrame,
                    ValueNode(
                        target.Input.ValueKind,
                        value),
                    interpolation);
                return true;
            });
        }
        keyframeButton.Click += (_, _) =>
        {
            if (exact is null) SaveValue(valueControl.Value, interpolationControl.Value);
            else if (localFrame > 0)
            {
                _ = saveMutation((candidate) =>
                {
                    candidate.RemoveKeyframe(
                        target.FieldId,
                        target.TargetId,
                        localFrame);
                    return true;
                });
            }
        };
        keyframeButton.IsEnabled = localFrame >= 0 && (exact is null || localFrame > 0);
        valueControl.ValueCommitted += (_, value) => SaveValue(value, interpolationControl.Value);
        panel.Children.Add(valueControl);
        interpolationControl.ValueCommitted += (_, interpolation) =>
        {
            if (exact is not null) SaveValue(valueControl.Value, interpolation);
        };
        panel.Children.Add(interpolationControl);
        return panel;
    }

    private Control CreateTargetDurationEditor(
        ProjectTreeNode node,
        ModuleInstanceAnimationDocument document,
        string targetId,
        int naturalDuration,
        Func<
            Func<ModuleInstanceAnimationDocument, bool>,
            Task> saveMutation)
    {
        var stored = document.TargetDurationFrames(targetId);
        var enabled = stored is not null;
        var toggle = new ToggleSwitch
        {
            IsChecked = enabled,
            VerticalAlignment = VerticalAlignment.Center,
        };
        EditorAccessibility.Describe(toggle, "Enable target-duration retime");
        toggle.PropertyChanged += (_, change) =>
        {
            if (change.Property != ToggleSwitch.IsCheckedProperty) return;
            _ = saveMutation((candidate) =>
            {
                candidate.SetTargetDurationFrames(
                    targetId,
                    toggle.IsChecked == true
                        ? naturalDuration
                        : null);
                return true;
            });
        };
        var switchRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = EditorUiDensity.Card(8),
        };
        switchRow.Children.Add(new TextBlock
        {
            Text = "Retime",
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(toggle, 1);
        switchRow.Children.Add(toggle);
        var panel = new StackPanel
        {
            Spacing = EditorUiDensity.Card(6),
        };
        panel.Children.Add(EditorGroupBlock.CreateSeparator());
        panel.Children.Add(switchRow);
        panel.Children.Add(new TextBlock { Text = $"Natural duration: {naturalDuration} frames", Opacity = 0.76 });
        if (enabled)
        {
            var definition = new FieldDefinition(
                $"animation.targetDuration.{(string.IsNullOrWhiteSpace(targetId) ? "screen" : targetId)}",
                "Target duration",
                ValueKind.Integer,
                DefaultValue: naturalDuration.ToString(CultureInfo.InvariantCulture),
                Number: new NumberDefinition(1, 100000, 1, 0),
                Unit: "frames");
            var control = new DictionaryFieldControl(
                new FieldValue(definition, stored!.Value.ToString(CultureInfo.InvariantCulture)),
                DictionaryServices(node));
            control.ValueCommitted += (_, value) =>
            {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var duration)) return;
                _ = saveMutation((candidate) =>
                {
                    candidate.SetTargetDurationFrames(
                        targetId,
                        Math.Max(1, duration));
                    return true;
                });
            };
            panel.Children.Add(control);
            panel.Children.Add(new TextBlock
            {
                Text = "Scales all keyframes and actions without rewriting their authored frames.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.62,
                FontSize = 11,
            });
        }
        return panel;
    }

    private static Control CreateMiniTimeline(
        IReadOnlyList<ResolvedAnimationTarget> targets,
        ResolvedAnimationTarget active,
        int currentTimelineFrame,
        int timelineDuration,
        bool usesOwnerTimeline,
        Action<int> setFrame,
        Action<int> previewFrame,
        Func<
            ResolvedAnimationTarget,
            AnimationKeyframeView,
            int,
            Task> moveKeyframe)
    {
        var canvas = new Canvas
        {
            Height = 30,
            MinWidth = 180,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        void Render(double availableWidth)
        {
            if (availableWidth <= 0) return;
            var width = Math.Max(180, availableWidth);
            canvas.Children.Clear();
            var referenceDuration = Math.Max(0, active.Target?.ReferenceDurationFrames ?? 0);
            var referenceOrigin = usesOwnerTimeline
                ? active.Target?.OwnerFrameOrigin ?? 0
                : active.Target?.ScreenFrameForOwnerFrame(active.Target.OwnerFrameOrigin) ?? 0;
            var referenceEnd = usesOwnerTimeline
                ? referenceOrigin + referenceDuration
                : active.Target?.ScreenFrameForOwnerFrame(
                    active.Target.OwnerFrameOrigin + referenceDuration) ?? referenceOrigin;
            var displayDuration = timelineDuration;
            var markerScale = Math.Max(1, displayDuration - 1);
            var intervalScale = Math.Max(1, displayDuration);
            var lane = new Border
            {
                Width = width,
                Height = 18,
                CornerRadius = new CornerRadius(5),
                Background = Brushes.Transparent,
                BorderBrush = EditorAnimationVisuals.TimelineBrush,
                BorderThickness = new Thickness(1),
            };
            Canvas.SetTop(lane, 6);
            canvas.Children.Add(lane);
            if (referenceDuration > 0)
            {
                var start = Math.Min(width, referenceOrigin / intervalScale * width);
                var end = Math.Min(width, referenceEnd / (double)intervalScale * width);
                var durationBand = new Border
                {
                    Width = Math.Max(2, end - start),
                    Height = 18,
                    CornerRadius = new CornerRadius(5),
                    Background = EditorAnimationVisuals.ReferenceDurationBrush,
                };
                ToolTip.SetTip(durationBand, $"Reference duration: {referenceDuration} frames");
                Canvas.SetLeft(durationBand, start);
                Canvas.SetTop(durationBand, 6);
                canvas.Children.Add(durationBand);
            }
            foreach (var target in targets.Where((candidate) => candidate.Track is not null))
            {
                foreach (var keyframe in target.Track!.Keyframes.Where((candidate) => candidate.Enabled))
                {
                    var ownerFrame = (target.Target?.OwnerFrameOrigin ?? 0) + keyframe.Frame;
                    var timelineKeyframe = AnimationTimelineCoordinateSpace.MarkerFrame(
                        usesOwnerTimeline,
                        target.Target?.OwnerFrameOrigin ?? 0,
                        keyframe.Frame,
                        target.Target?.ScreenFrameForOwnerFrame ?? ((_) => keyframe.Frame));
                    var screenKeyframe = target.Target?.ScreenFrameForOwnerFrame(ownerFrame)
                        ?? timelineKeyframe;
                    var isActive = ReferenceEquals(target, active);
                    var isCurrent = timelineKeyframe == currentTimelineFrame;
                    var isProtected = keyframe.Frame == 0;
                    var markerBrush = isCurrent
                        ? EditorAnimationVisuals.CurrentKeyframeBrush
                        : isActive
                            ? EditorAnimationVisuals.ActiveTrackBrush
                            : EditorAnimationVisuals.OtherKeyframeBrush;
                    Control glyph = isActive
                        ? new Polygon
                        {
                            Points = new Points { new Point(9, 0), new Point(18, 9), new Point(9, 18), new Point(0, 9) },
                            Fill = isProtected ? Brushes.Transparent : markerBrush,
                            Stroke = isProtected ? markerBrush : null,
                            StrokeThickness = isProtected ? 1.6 : 0,
                        }
                        : new Ellipse
                        {
                            Width = 8,
                            Height = 8,
                            Fill = isProtected ? Brushes.Transparent : markerBrush,
                            Stroke = isProtected ? markerBrush : null,
                            StrokeThickness = isProtected ? 1.4 : 0,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                        };
                    var canDrag = keyframe.Frame > 0 && target.Target is not null;
                    var marker = new Border
                    {
                        Width = isActive ? 18 : 12,
                        Height = isActive ? 18 : 12,
                        Background = Brushes.Transparent,
                        Child = glyph,
                        Focusable = canDrag,
                        Cursor = new Cursor(canDrag
                            ? StandardCursorType.SizeWestEast
                            : StandardCursorType.Arrow),
                    };
                    ToolTip.SetTip(
                        marker,
                        isProtected
                            ? $"Local frame 0 · Screen frame {screenKeyframe} · Protected"
                            : $"Local frame {keyframe.Frame} · Screen frame {screenKeyframe}");
                    var originalLeft = Math.Max(0, Math.Min(
                        width - marker.Width,
                        timelineKeyframe / (double)markerScale * (width - marker.Width)));
                    Canvas.SetLeft(marker, originalLeft);
                    Canvas.SetTop(marker, isActive ? 6 : 9);
                    canvas.Children.Add(marker);
                    var dragging = false;
                    var moved = false;
                    var released = false;
                    var validDestination = false;
                    var candidateLocalFrame = keyframe.Frame;
                    var candidateTimelineFrame = timelineKeyframe;
                    var pressPosition = default(Point);
                    IPointer? capturedPointer = null;

                    void Restore()
                    {
                        dragging = false;
                        Canvas.SetLeft(marker, originalLeft);
                        SetMarkerBrush(glyph, isCurrent
                            ? EditorAnimationVisuals.CurrentKeyframeBrush
                            : isActive
                                ? EditorAnimationVisuals.ActiveTrackBrush
                                : EditorAnimationVisuals.OtherKeyframeBrush);
                        previewFrame(timelineKeyframe);
                    }

                    marker.PointerPressed += (_, args) =>
                    {
                        if (!args.GetCurrentPoint(marker).Properties.IsLeftButtonPressed) return;
                        if (!canDrag)
                        {
                            setFrame(timelineKeyframe);
                            args.Handled = true;
                            return;
                        }
                        released = false;
                        dragging = true;
                        moved = false;
                        validDestination = false;
                        capturedPointer = args.Pointer;
                        pressPosition = args.GetPosition(canvas);
                        marker.Focus();
                        args.Pointer.Capture(marker);
                        args.Handled = true;
                    };
                    marker.PointerMoved += (_, args) =>
                    {
                        if (!dragging) return;
                        var position = args.GetPosition(canvas);
                        if (!moved && Math.Abs(position.X - pressPosition.X) < 2) return;
                        moved = true;
                        var rawLeft = originalLeft + position.X - pressPosition.X;
                        var laneWidth = Math.Max(1, width - marker.Width);
                        var rawTimelineFrame = Math.Clamp(rawLeft / laneWidth * markerScale, 0, markerScale);
                        var otherTimelineFrames = targets
                            .SelectMany((candidate) => (candidate.Track?.Keyframes ?? [])
                                .Where((candidateKeyframe) => candidateKeyframe.Enabled
                                    && candidateKeyframe.Id != keyframe.Id)
                                .Select((candidateKeyframe) =>
                                    AnimationTimelineCoordinateSpace.MarkerFrame(
                                        usesOwnerTimeline,
                                        candidate.Target?.OwnerFrameOrigin ?? 0,
                                        candidateKeyframe.Frame,
                                        candidate.Target?.ScreenFrameForOwnerFrame
                                            ?? ((_) => candidateKeyframe.Frame))))
                            .ToList();
                        var snappedTimelineFrame = TimelineKeyframeDrag.ResolveScreenFrame(
                            rawTimelineFrame,
                            args.KeyModifiers.HasFlag(KeyModifiers.Alt),
                            markerScale,
                            laneWidth,
                            otherTimelineFrames);
                        var candidateOwnerFrame =
                            AnimationTimelineCoordinateSpace.OwnerFrameForTimelineFrame(
                                usesOwnerTimeline,
                                snappedTimelineFrame,
                                target.Target?.OwnerFrameForScreenFrame
                                    ?? ((frame) => frame));
                        candidateLocalFrame = (int)Math.Round(
                            candidateOwnerFrame - (target.Target?.OwnerFrameOrigin ?? 0),
                            MidpointRounding.AwayFromZero);
                        var isOriginalDestination = candidateLocalFrame == keyframe.Frame;
                        var isOccupied = target.Track!.Keyframes.Any((candidate) =>
                            candidate.Id != keyframe.Id && candidate.Frame == candidateLocalFrame);
                        validDestination = candidateLocalFrame > 0
                            && !isOriginalDestination
                            && !isOccupied;
                        candidateTimelineFrame = usesOwnerTimeline
                            ? (int)Math.Round(
                                (target.Target?.OwnerFrameOrigin ?? 0) + Math.Max(0, candidateLocalFrame),
                                MidpointRounding.AwayFromZero)
                            : target.Target?.ScreenFrameForOwnerFrame(
                                (target.Target?.OwnerFrameOrigin ?? 0) + Math.Max(0, candidateLocalFrame))
                                ?? snappedTimelineFrame;
                        Canvas.SetLeft(marker, Math.Clamp(
                            candidateTimelineFrame / (double)markerScale * laneWidth,
                            0,
                            laneWidth));
                        SetMarkerBrush(glyph, validDestination
                            ? EditorAnimationVisuals.CurrentKeyframeBrush
                            : isOriginalDestination
                                ? EditorAnimationVisuals.ActiveTrackBrush
                                : Brushes.IndianRed);
                        previewFrame(candidateTimelineFrame);
                        args.Handled = true;
                    };
                    marker.PointerReleased += (_, args) =>
                    {
                        if (!dragging) return;
                        released = true;
                        dragging = false;
                        args.Pointer.Capture(null);
                        capturedPointer = null;
                        args.Handled = true;
                        if (!moved)
                        {
                            setFrame(timelineKeyframe);
                            return;
                        }
                        if (validDestination)
                        {
                            _ = moveKeyframe(
                                target,
                                keyframe,
                                candidateLocalFrame);
                            return;
                        }
                        Restore();
                    };
                    marker.PointerCaptureLost += (_, _) =>
                    {
                        if (!released && dragging) Restore();
                        released = false;
                    };
                    marker.KeyDown += (_, args) =>
                    {
                        if (args.Key != Key.Escape || !dragging) return;
                        released = true;
                        capturedPointer?.Capture(null);
                        capturedPointer = null;
                        Restore();
                        args.Handled = true;
                    };
                }
            }
        }
        canvas.SizeChanged += (_, args) => Render(args.NewSize.Width);
        return canvas;
    }

    private static void SetMarkerBrush(Control marker, IBrush brush)
    {
        switch (marker)
        {
            case Polygon diamond:
                if (diamond.StrokeThickness > 0)
                {
                    diamond.Stroke = brush;
                }
                else
                {
                    diamond.Fill = brush;
                }
                break;
            case Ellipse circle:
                if (circle.StrokeThickness > 0)
                {
                    circle.Stroke = brush;
                }
                else
                {
                    circle.Fill = brush;
                }
                break;
        }
    }

    private static string TargetKey(ResolvedAnimationTarget target)
    {
        var fieldId = target.Target?.FieldId ?? target.Track?.FieldId ?? "missing";
        var targetId = target.Target?.TargetId ?? target.Track?.TargetId ?? "";
        return $"{fieldId}\u001f{targetId}";
    }

    private IReadOnlyList<AnimationTarget> ReadTargets(
        JsonObject preview,
        JsonObject config,
        JsonObject animation,
        JsonObject themeTokens)
    {
        var result = new List<AnimationTarget>();
        foreach (var input in RuntimeInputDefinitionReader.ReadInputs(preview, config).Where((input) => input.Animation is not null))
            result.Add(new AnimationTarget(
                input.Id,
                "",
                input.Label,
                input,
                DesignPreviewTestValues.Value(preview, input),
                RuntimeAnimationFrameOrigin.FieldOwnerFrameOrigin(preview, preview, animation, input.Id, "", themeTokens),
                RuntimeAnimationFrameOrigin.FieldReferenceDurationFrames(preview, preview, animation, input.Id, "", themeTokens),
                (ownerFrame) => RuntimeAnimationFrameOrigin.ScreenFrameForOwnerFrame(
                    preview, preview, animation, "", ownerFrame, themeTokens),
                (screenFrame) => RuntimeAnimationFrameOrigin.OwnerLocalFrame(
                    preview, preview, animation, "", screenFrame, themeTokens)));
        foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(preview, config))
        {
            var items = DesignPreviewTestValues.CollectionItems(preview, collection);
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var targetId = JsonPath.RequiredString(
                    item,
                    "id",
                    $"Animation collection '{collection.Id}' item at index {index}");
                foreach (var input in collection.Fields.Where((input) => input.Animation is not null))
                {
                    var targetInput = string.IsNullOrWhiteSpace(input.OptionsSourceCollectionJsonKey)
                        ? input
                        : input with { Options = RuntimeInputDynamicOptions.Resolve(ActiveInputOptions, input, item) };
                    result.Add(new AnimationTarget(
                        targetInput.Id,
                        targetId,
                        targetInput.Label,
                        targetInput,
                        DesignPreviewTestValues.CollectionValue(item, targetInput),
                        RuntimeAnimationFrameOrigin.FieldOwnerFrameOrigin(preview, preview, animation, targetInput.Id, targetId, themeTokens),
                        RuntimeAnimationFrameOrigin.FieldReferenceDurationFrames(preview, preview, animation, targetInput.Id, targetId, themeTokens),
                        (ownerFrame) => RuntimeAnimationFrameOrigin.ScreenFrameForOwnerFrame(
                            preview, preview, animation, targetId, ownerFrame, themeTokens),
                        (screenFrame) => RuntimeAnimationFrameOrigin.OwnerLocalFrame(
                            preview, preview, animation, targetId, screenFrame, themeTokens)));
                }
                if (!string.IsNullOrWhiteSpace(collection.ItemRuntimeContractJsonKey))
                {
                    var runtimeContract = JsonPath.RequiredObject(
                        item,
                        collection.ItemRuntimeContractJsonKey,
                        $"Animation collection '{collection.Id}' item '{targetId}'");
                    foreach (var input in RuntimeInputDefinitionReader
                        .ReadInputs(runtimeContract, new JsonObject())
                        .Where((input) => input.Animation is not null))
                    {
                        result.Add(new AnimationTarget(
                            input.Id,
                            targetId,
                            input.Label,
                            input,
                            DesignPreviewTestValues.Value(runtimeContract, input),
                            RuntimeAnimationFrameOrigin.FieldOwnerFrameOrigin(preview, preview, animation, input.Id, targetId, themeTokens),
                            RuntimeAnimationFrameOrigin.FieldReferenceDurationFrames(preview, preview, animation, input.Id, targetId, themeTokens),
                            (ownerFrame) => RuntimeAnimationFrameOrigin.ScreenFrameForOwnerFrame(
                                preview, preview, animation, targetId, ownerFrame, themeTokens),
                            (screenFrame) => RuntimeAnimationFrameOrigin.OwnerLocalFrame(
                                preview, preview, animation, targetId, screenFrame, themeTokens)));
                    }
                }
            }
        }
        return result;
    }

    private DictionaryFieldServices DictionaryServices(
        ProjectTreeNode node)
    {
        return _preparedDictionaryContext is null
            ? _dictionaryServices.ForNode(
                node,
                (_) => "")
            : _dictionaryServices.ForPreparedNode(
                node,
                _preparedDictionaryContext,
                (_) => "");
    }

    private ModuleInstanceAnimationSnapshot PreparedSnapshot(
        ProjectTreeNode node)
    {
        if (_preparedAnimationSnapshot is not { } snapshot
            || !snapshot.ModuleInstanceId.Equals(
                node.Id,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Animation editor for '{node.Id}' requires its prepared snapshot.");
        }

        return snapshot;
    }

    private static JsonNode ValueNode(ValueKind kind, string value) =>
        RuntimeInputValueKindContract.ParseValue(kind, value, "Animation keyframe value");

    private static string InterpolationLabel(string interpolation) => interpolation switch
    {
        "writeOn" => "Write-on",
        "easeInOut" => "Ease in/out",
        "linear" => "Linear",
        _ => "Hold",
    };

    private static Control CreateTrackSummary(
        bool active,
        string label,
        int keyframeCount,
        IBrush brush)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = EditorUiDensity.Card(7),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                EditorTimelineTransport.CreateAnimationActivationGlyph(
                    active,
                    extendsOwnerDuration: true,
                    size: 14,
                    brush: brush),
                new TextBlock
                {
                    Text = $"{label}  ·  {EditorUiText.Count(keyframeCount, "keyframe")}",
                    VerticalAlignment = VerticalAlignment.Center,
                },
            },
        };
    }

    private static Control CreateAnimationActivationLabel(string label)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = EditorUiDensity.Card(7),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                EditorTimelineTransport.CreateAnimationActivationGlyph(
                    filled: false,
                    extendsOwnerDuration: true,
                    size: 14,
                    brush: EditorAnimationVisuals.InactiveTrackBrush),
                new TextBlock
                {
                    Text = $"Activate animation for {label}",
                    VerticalAlignment = VerticalAlignment.Center,
                },
            },
        };
    }

    private static void AddTransportButton(
        Grid host,
        int column,
        object content,
        string accessibleName,
        Action action,
        bool enabled = true,
        double width = 34)
    {
        var button = new Button
        {
            Content = content,
            Width = width,
            Height = 30,
            Padding = new Thickness(0),
            IsEnabled = enabled,
        };
        EditorAccessibility.Describe(button, accessibleName);
        button.Click += (_, _) => action();
        Grid.SetColumn(button, column);
        host.Children.Add(button);
    }

    private sealed record AnimationTarget(
        string FieldId,
        string TargetId,
        string Label,
        ComponentInputDefinition Input,
        string BaseValue,
        double OwnerFrameOrigin,
        int ReferenceDurationFrames,
        Func<double, int> ScreenFrameForOwnerFrame,
        Func<int, double> OwnerFrameForScreenFrame);

    private sealed record ResolvedAnimationTarget(
        AnimationTarget? Target,
        AnimationTrackView? Track)
    {
        public string Label => Target?.Label ?? $"Missing target · {Track!.FieldId}";
    }

    private sealed class AnimationTimelinePlayhead : Canvas
    {
        private int _frame;
        private int _duration;
        private bool _dragging;

        public AnimationTimelinePlayhead(int frame, int duration)
        {
            _frame = frame;
            _duration = duration;
            Height = 54;
            MinWidth = 180;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            Cursor = new Cursor(StandardCursorType.SizeWestEast);
            Background = Brushes.Transparent;
            SizeChanged += (_, _) => Render();
            PointerPressed += (_, args) =>
            {
                if (!args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
                _dragging = true;
                args.Pointer.Capture(this);
                SetFromPointer(args.GetPosition(this).X);
                args.Handled = true;
            };
            PointerMoved += (_, args) =>
            {
                if (!_dragging) return;
                SetFromPointer(args.GetPosition(this).X);
                args.Handled = true;
            };
            PointerReleased += (_, args) =>
            {
                if (!_dragging) return;
                _dragging = false;
                args.Pointer.Capture(null);
                args.Handled = true;
            };
            PointerCaptureLost += (_, _) => _dragging = false;
        }

        public event Action<object?, double>? FrameChanged;

        public void SetFrame(int frame)
        {
            _frame = Math.Clamp(frame, 0, Math.Max(0, _duration - 1));
            Render();
        }

        public void SetDuration(int duration)
        {
            _duration = Math.Max(1, duration);
            SetFrame(_frame);
        }

        private void SetFromPointer(double x)
        {
            var width = Math.Max(1, Bounds.Width - 14);
            var frame = (int)Math.Round(
                Math.Clamp((x - 7) / width, 0, 1) * Math.Max(0, _duration - 1),
                MidpointRounding.AwayFromZero);
            if (frame == _frame) return;
            _frame = frame;
            Render();
            FrameChanged?.Invoke(this, frame);
        }

        private void Render()
        {
            if (Bounds.Width <= 0) return;
            var width = Math.Max(14, Bounds.Width);
            var trackWidth = width - 14;
            var fraction = _duration <= 1 ? 0 : _frame / (double)(_duration - 1);
            Children.Clear();
            var baseline = new Border
            {
                Width = trackWidth,
                Height = 1,
                Background = EditorAnimationVisuals.TimelineBrush,
            };
            Canvas.SetLeft(baseline, 7);
            Canvas.SetTop(baseline, 20);
            Children.Add(baseline);
            var tickCount = Math.Max(2, (int)Math.Floor(trackWidth / 28));
            for (var tick = 0; tick <= tickCount; tick++)
            {
                var major = tick % 5 == 0;
                var mark = new Border
                {
                    Width = 1,
                    Height = major ? 10 : 6,
                    Background = EditorAnimationVisuals.TimelineBrush,
                };
                Canvas.SetLeft(mark, 7 + (trackWidth * tick / tickCount));
                Canvas.SetTop(mark, 20 - mark.Height);
                Children.Add(mark);
            }
            var playheadX = 7 + (trackWidth * fraction);
            var playhead = new Border
            {
                Width = 2,
                Height = Math.Max(1, Bounds.Height - 20),
                Background = EditorSukiWindowTheme.AccentBrush(),
            };
            Canvas.SetLeft(playhead, Math.Clamp(playheadX - 1, 0, width - 2));
            Canvas.SetTop(playhead, 20);
            Children.Add(playhead);
            var head = new Polygon
            {
                Points = new Points { new Point(0, 0), new Point(10, 0), new Point(5, 6) },
                Fill = EditorSukiWindowTheme.AccentBrush(),
            };
            Canvas.SetLeft(head, Math.Clamp(playheadX - 5, 0, width - 10));
            Canvas.SetTop(head, 14);
            Children.Add(head);
            var grip = new Border
            {
                Width = 28,
                Height = Math.Max(32, Bounds.Height),
                Background = Brushes.Transparent,
            };
            Canvas.SetLeft(grip, Math.Clamp(playheadX - 14, 0, width - 28));
            Canvas.SetTop(grip, 0);
            Children.Add(grip);
        }
    }
}

internal sealed record AnimationTargetEditorContent(Control Content, int ActiveTrackCount);
