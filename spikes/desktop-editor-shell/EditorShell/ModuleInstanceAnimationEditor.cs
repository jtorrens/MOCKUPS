using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ModuleInstanceAnimationEditor
{
    private readonly SpikeDatabase _database;
    private readonly EditorDictionaryFieldServices _dictionaryServices;
    private readonly Action _onChanged;
    private readonly EditorSessionUiState _sessionUiState;
    private readonly Func<int> _shotFrame;
    private readonly Action<int> _setShotFrame;
    private readonly PreviewPlaybackState _playbackState;
    private readonly Action _togglePlayback;

    public ModuleInstanceAnimationEditor(
        SpikeDatabase database,
        EditorDictionaryFieldServices dictionaryServices,
        Action onChanged,
        EditorSessionUiState sessionUiState,
        Func<int> shotFrame,
        Action<int> setShotFrame,
        PreviewPlaybackState playbackState,
        Action togglePlayback)
    {
        _database = database;
        _dictionaryServices = dictionaryServices;
        _onChanged = onChanged;
        _sessionUiState = sessionUiState;
        _shotFrame = shotFrame;
        _setShotFrame = setShotFrame;
        _playbackState = playbackState;
        _togglePlayback = togglePlayback;
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
        var preview = DesignPreviewTestValues.Parse(_database.GetModuleInstanceRuntimePreviewJson(node.Id));
        var animation = DesignPreviewTestValues.Parse(_database.GetModuleInstanceSettings(node.Id).AnimationJson);
        var track = new ModuleInstanceAnimationDocument(animation.ToJsonString()).Track(input.Id, targetId);
        if (track is null) return baseValue;
        var themeTokens = DesignPreviewTestValues.Parse(_database.GetModuleInstanceThemeTokensJson(node.Id));
        var screenFrame = _shotFrame() - ModuleInstanceTimeline.ScreenStartFrame(_database, node.Id);
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

    public AnimationTargetEditorContent CreateCollectionContent(
        ProjectTreeNode node,
        RuntimeInputCollectionDefinition collection)
    {
        var preview = DesignPreviewTestValues.Parse(_database.GetModuleInstanceRuntimePreviewJson(node.Id));
        var items = DesignPreviewTestValues.CollectionItems(preview, collection).ToList();
        var itemLabels = items
            .Select((item, index) => new
            {
                Id = item["id"]?.GetValue<string>() ?? "",
                Label = RuntimeCollectionItemPresentation.Resolve(
                    collection,
                    item,
                    index,
                    $"{collection.ItemLabel} {index + 1}",
                    $"Payload item {index + 1}",
                    EditorIcons.Component).Title,
            })
            .Where((item) => !string.IsNullOrWhiteSpace(item.Id))
            .ToDictionary((item) => item.Id, (item) => item.Label, StringComparer.Ordinal);
        var fieldIds = collection.Fields
            .Where((input) => input.Animation is not null)
            .Select((input) => input.Id)
            .ToHashSet(StringComparer.Ordinal);
        return CreateContent(
            node,
            $"collection:{collection.Id}",
            (target) => itemLabels.ContainsKey(target.TargetId) && fieldIds.Contains(target.FieldId),
            durationTargetId: null,
            (target) => target with { Label = $"{itemLabels[target.TargetId]} · {target.Label}" });
    }

    private AnimationTargetEditorContent CreateContent(
        ProjectTreeNode node,
        string scopeKey,
        Func<AnimationTarget, bool> includesTarget,
        string? durationTargetId,
        Func<AnimationTarget, AnimationTarget>? decorateTarget = null)
    {
        var instance = _database.GetModuleInstanceSettings(node.Id);
        var module = _database.GetModuleInstanceVariantSettings(node.Id);
        var preview = DesignPreviewTestValues.Parse(_database.GetModuleInstanceRuntimePreviewJson(node.Id));
        var config = DesignPreviewTestValues.Parse(module.ConfigJson);
        var animation = DesignPreviewTestValues.Parse(instance.AnimationJson);
        var themeTokens = DesignPreviewTestValues.Parse(_database.GetModuleInstanceThemeTokensJson(node.Id));
        var screenStartFrame = ModuleInstanceTimeline.ScreenStartFrame(_database, node.Id);
        List<AnimationTarget> ReadScopeTargets(JsonObject currentAnimation) => ReadTargets(
                node,
                preview,
                config,
                currentAnimation,
                themeTokens)
            .Where(includesTarget)
            .Select((target) => decorateTarget?.Invoke(target) ?? target)
            .ToList();
        var targets = ReadScopeTargets(animation);
        var targetKeys = targets
            .Select((target) => (target.FieldId, target.TargetId))
            .ToHashSet();
        var document = new ModuleInstanceAnimationDocument(instance.AnimationJson);
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
                ReadScopeTargets));
        }
        return new AnimationTargetEditorContent(content, activeTrackCount);
    }

    private Control CreateTimelineEditor(
        ProjectTreeNode node,
        ModuleInstanceAnimationDocument document,
        List<ResolvedAnimationTarget> targets,
        string scopeKey,
        string? durationTargetId,
        JsonObject preview,
        JsonObject animation,
        JsonObject themeTokens,
        Func<JsonObject, List<AnimationTarget>> readScopeTargets)
    {
        var screenStartFrame = ModuleInstanceTimeline.ScreenStartFrame(_database, node.Id);
        var actualScreenDuration = Math.Max(1, ModuleInstanceTimeline.DurationFrames(_database, node.Id));
        var durationPolicy = RuntimeDurationContract.Policy(
            _database.GetModuleInstanceEffectiveContractJson(node.Id));
        var currentAnimation = animation;
        int MaximumAuthoredScreenFrame() => targets
            .SelectMany((candidate) => (candidate.Track?.Keyframes ?? [])
                .Where((keyframe) => keyframe.Enabled)
                .Select((keyframe) => candidate.Target?.ScreenFrameForOwnerFrame(
                    (candidate.Target?.OwnerFrameOrigin ?? 0) + keyframe.Frame) ?? keyframe.Frame))
            .DefaultIfEmpty(-1)
            .Max();
        int CalculatedAuthoringDuration(int maximumAuthoredFrame) => Math.Max(
            Math.Max(actualScreenDuration, maximumAuthoredFrame + 1),
            targets
                .Where((candidate) => candidate.Target is { ReferenceDurationFrames: > 0 })
                .Select((candidate) => candidate.Target!.ScreenFrameForOwnerFrame(
                    candidate.Target.OwnerFrameOrigin + candidate.Target.ReferenceDurationFrames))
                .DefaultIfEmpty(actualScreenDuration)
                .Max());
        var maximumAuthoredScreenFrame = MaximumAuthoredScreenFrame();
        var calculatedAuthoringDuration = CalculatedAuthoringDuration(maximumAuthoredScreenFrame);
        var timelineDuration = durationPolicy == RuntimeDurationPolicy.Explicit
            ? actualScreenDuration
            : calculatedAuthoringDuration;
        var hasOutOfRangeKeyframes = maximumAuthoredScreenFrame >= actualScreenDuration;
        var ownerNaturalDuration = durationTargetId is null
            ? 1
            : RuntimeAnimationFrameOrigin.OwnerNaturalDuration(
                preview,
                preview,
                animation,
                durationTargetId,
                themeTokens);
        var referenceNaturalDuration = Math.Max(
            ownerNaturalDuration,
            targets
                .Where((candidate) => candidate.Target is not null)
                .Select((candidate) => (int)Math.Ceiling(
                    candidate.Target!.OwnerFrameOrigin + candidate.Target.ReferenceDurationFrames))
                .DefaultIfEmpty(1)
                .Max());
        var currentFrame = Math.Clamp(_shotFrame() - screenStartFrame, 0, timelineDuration - 1);
        int TimelineFrame() => Math.Clamp(currentFrame, 0, timelineDuration - 1);
        var selectionKey = $"{node.Id}:animation-properties:{scopeKey}";
        var selectedId = _sessionUiState.Selection(selectionKey);
        var selected = targets.FirstOrDefault((target) => TargetKey(target) == selectedId)
            ?? targets.FirstOrDefault((target) => target.Track is not null)
            ?? targets.First();
        double OwnerFrame() => RuntimeAnimationFrameOrigin.OwnerLocalFrame(
            preview,
            preview,
            currentAnimation,
            selected.Target?.TargetId ?? durationTargetId ?? "",
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
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = timelineDuration - 1,
            Value = TimelineFrame(),
            TickFrequency = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var sliderMagnet = new TimelineSliderMagnet(
            slider,
            () => targets
                .SelectMany((target) => (target.Track?.Keyframes ?? [])
                    .Where((keyframe) => keyframe.Enabled)
                    .Select((keyframe) => target.Target?.ScreenFrameForOwnerFrame(
                        (target.Target?.OwnerFrameOrigin ?? 0) + keyframe.Frame) ?? 0))
                .Distinct()
                .ToList());
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
        var changingFrame = false;

        void SetFrame(int screenFrame)
        {
            currentFrame = Math.Clamp(screenFrame, 0, timelineDuration - 1);
            changingFrame = true;
            slider.Value = TimelineFrame();
            changingFrame = false;
            if (currentFrame < actualScreenDuration) _setShotFrame(screenStartFrame + currentFrame);
            RefreshVisuals();
        }

        void SaveAndRefresh()
        {
            var selectedKey = TargetKey(selected);
            var authoringHorizon = timelineDuration;
            _database.UpdateModuleInstanceAnimationJson(node.Id, document.ToJson());
            currentAnimation = DesignPreviewTestValues.Parse(
                _database.GetModuleInstanceSettings(node.Id).AnimationJson);
            screenStartFrame = ModuleInstanceTimeline.ScreenStartFrame(_database, node.Id);
            actualScreenDuration = Math.Max(1, ModuleInstanceTimeline.DurationFrames(_database, node.Id));
            var refreshedTargets = readScopeTargets(currentAnimation)
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
            maximumAuthoredScreenFrame = MaximumAuthoredScreenFrame();
            calculatedAuthoringDuration = CalculatedAuthoringDuration(maximumAuthoredScreenFrame);
            hasOutOfRangeKeyframes = maximumAuthoredScreenFrame >= actualScreenDuration;
            timelineDuration = durationPolicy == RuntimeDurationPolicy.Explicit
                ? Math.Max(actualScreenDuration, authoringHorizon)
                : Math.Max(calculatedAuthoringDuration, authoringHorizon);
            currentFrame = Math.Clamp(currentFrame, 0, timelineDuration - 1);
            slider.Maximum = timelineDuration - 1;
            _onChanged();
            RefreshVisuals();
        }

        void RefreshVisuals()
        {
            frameText.Text = $"{TimelineFrame()}/{actualScreenDuration - 1}";
            authoringLimitText.Text = hasOutOfRangeKeyframes
                ? $"({maximumAuthoredScreenFrame} · keyframe outside Screen)"
                : timelineDuration > actualScreenDuration ? $"({timelineDuration - 1})" : "";
            authoringLimitText.Foreground = hasOutOfRangeKeyframes
                ? EditorAnimationVisuals.ActiveTrackBrush
                : null;
            var selectedLocalFrame = (int)Math.Round(OwnerFrame(), MidpointRounding.AwayFromZero) - (int)Math.Round(
                selected.Target?.OwnerFrameOrigin ?? 0,
                MidpointRounding.AwayFromZero);
            var hasCurrentKeyframe = selected.Track?.Keyframes.Any(
                (keyframe) => keyframe.Enabled && keyframe.Frame == selectedLocalFrame) == true;
            currentKeyframeButton.Content = EditorTimelineTransport.CreateKeyframeGlyph(
                filled: hasCurrentKeyframe,
                size: 16,
                brush: hasCurrentKeyframe
                    ? EditorAnimationVisuals.CurrentKeyframeBrush
                    : EditorAnimationVisuals.InactiveTrackBrush);
            EditorAccessibility.Describe(
                currentKeyframeButton,
                hasCurrentKeyframe ? "Keyframe at the current frame" : "No keyframe at the current frame");
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
                SetFrame);
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
                    Content = $"{(active ? "◆" : "◇")}  {target.Label}  ·  {EditorUiText.Count(target.Track?.Keyframes.Count ?? 0, "keyframe")}",
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
                document,
                selected,
                (int)Math.Round(OwnerFrame(), MidpointRounding.AwayFromZero),
                (ownerFrame) => SetFrame(selected.Target?.ScreenFrameForOwnerFrame(ownerFrame) ?? TimelineFrame()),
                SaveAndRefresh);
        }

        var firstFrameButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelineFirstFrame, 16),
            "First Screen frame");
        firstFrameButton.Click += (_, _) => SetFrame(0);
        var previousFrameButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelinePreviousFrame, 16),
            "Previous frame");
        previousFrameButton.Click += (_, _) => SetFrame(TimelineFrame() - 1);
        currentKeyframeButton.Click += (_, _) => SetFrame(TimelineFrame());
        playbackButton.Click += (_, _) => _togglePlayback();
        var nextFrameButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelineNextFrame, 16),
            "Next frame");
        nextFrameButton.Click += (_, _) => SetFrame(TimelineFrame() + 1);
        var lastFrameButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelineLastFrame, 16),
            "Last Screen frame");
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
        slider.ValueChanged += (_, args) =>
        {
            if (!changingFrame)
                SetFrame((int)Math.Round(
                    sliderMagnet.Resolve(args.NewValue),
                    MidpointRounding.AwayFromZero));
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
            slider.Maximum = timelineDuration - 1;
            RefreshVisuals();
        };
        var sliderRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = EditorUiDensity.Card(6),
        };
        sliderRow.Children.Add(slider);
        Grid.SetColumn(extendHorizonButton, 1);
        sliderRow.Children.Add(extendHorizonButton);
        root.Children.Add(sliderRow);
        root.Children.Add(timelineHost);
        if (durationTargetId is not null)
        {
            root.Children.Add(CreateTargetDurationEditor(
                node,
                document,
                durationTargetId,
                referenceNaturalDuration,
                SaveAndRefresh));
        }
        root.Children.Add(EditorGroupBlock.CreateSeparator());
        root.Children.Add(trackList);
        root.Children.Add(detailHost);
        void OnPlaybackChanged()
        {
            currentFrame = Math.Clamp(_shotFrame() - screenStartFrame, 0, timelineDuration - 1);
            changingFrame = true;
            slider.Value = TimelineFrame();
            changingFrame = false;
            RefreshVisuals();
        }
        _playbackState.Changed += OnPlaybackChanged;
        root.DetachedFromVisualTree += (_, _) => _playbackState.Changed -= OnPlaybackChanged;
        RefreshVisuals();
        return root;
    }

    private Control CreateTrackDetail(
        ProjectTreeNode node,
        ModuleInstanceAnimationDocument document,
        ResolvedAnimationTarget selected,
        int ownerFrame,
        Action<int> setFrame,
        Action saveAndReload)
    {
        if (selected.Target is null)
            return new TextBlock { Text = "El target de este track ya no existe.", Foreground = EditorAnimationVisuals.ActiveTrackBrush };
        var target = selected.Target;
        if (selected.Track is null)
        {
            var activate = new Button
            {
                Content = $"◇  Activate animation for {target.Label}",
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            activate.Click += (_, _) =>
            {
                document.AddTrack(
                    target.FieldId,
                    target.TargetId,
                    ValueNode(target.Input.ValueKind, target.BaseValue),
                    target.Input.Animation!.Interpolations.First());
                saveAndReload();
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
                filled: exact is not null,
                size: 16,
                brush: exact is not null
                    ? EditorAnimationVisuals.CurrentKeyframeBrush
                    : EditorAnimationVisuals.ActiveTrackBrush),
            Width = 34,
            Height = 30,
            Padding = EditorUiDensity.CardThickness(0),
        };
        EditorAccessibility.Describe(keyframeButton, exact is null ? "Create keyframe at current frame" : "Update current keyframe");
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
            new FieldValue(RuntimeInputFieldDefinitionFactory.Create(_database, node, target.Input), displayedValue),
            _dictionaryServices.ForNode(node, (_) => ""));
        var animation = target.Input.Animation!;
        var selectedInterpolation = exact?.Interpolation ?? animation.Interpolations.First();
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
            _dictionaryServices.ForNode(node, (_) => ""));
        void SaveValue(string value, string interpolation)
        {
            document.UpsertKeyframe(
                target.FieldId,
                target.TargetId,
                localFrame,
                ValueNode(target.Input.ValueKind, value),
                interpolation);
            saveAndReload();
        }
        keyframeButton.Click += (_, _) =>
        {
            if (exact is null) SaveValue(valueControl.Value, interpolationControl.Value);
            else if (localFrame > 0)
            {
                document.RemoveKeyframe(target.FieldId, target.TargetId, localFrame);
                saveAndReload();
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
        Action saveAndReload)
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
            document.SetTargetDurationFrames(targetId, toggle.IsChecked == true ? naturalDuration : null);
            saveAndReload();
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
                _dictionaryServices.ForNode(node, (_) => ""));
            control.ValueCommitted += (_, value) =>
            {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var duration)) return;
                document.SetTargetDurationFrames(targetId, Math.Max(1, duration));
                saveAndReload();
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
        int currentOwnerFrame,
        int timelineDuration,
        Action<int> setFrame)
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
            var referenceOrigin = active.Target?.ScreenFrameForOwnerFrame(active.Target.OwnerFrameOrigin) ?? 0;
            var referenceEnd = active.Target?.ScreenFrameForOwnerFrame(
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
                    var ownerKeyframe = target.Target?.ScreenFrameForOwnerFrame(
                        (target.Target?.OwnerFrameOrigin ?? 0) + keyframe.Frame) ?? keyframe.Frame;
                    var isActive = ReferenceEquals(target, active);
                    var isCurrent = isActive && ownerKeyframe == currentOwnerFrame;
                    var marker = new Button
                    {
                        Content = "◆",
                        FontSize = 13,
                        Width = 24,
                        Height = 24,
                        Padding = new Thickness(0),
                        Background = Brushes.Transparent,
                        BorderBrush = Brushes.Transparent,
                        Foreground = isCurrent
                            ? EditorAnimationVisuals.CurrentKeyframeBrush
                            : isActive
                                ? EditorAnimationVisuals.ActiveTrackBrush
                                : EditorAnimationVisuals.OtherKeyframeBrush,
                    };
                    marker.Click += (_, _) => setFrame(ownerKeyframe);
                    Canvas.SetLeft(marker, Math.Max(0, Math.Min(
                        width - 24,
                        ownerKeyframe / (double)markerScale * (width - 24))));
                    Canvas.SetTop(marker, 3);
                    canvas.Children.Add(marker);
                }
            }
        }
        canvas.SizeChanged += (_, args) => Render(args.NewSize.Width);
        return canvas;
    }

    private static string TargetKey(ResolvedAnimationTarget target)
    {
        var fieldId = target.Target?.FieldId ?? target.Track?.FieldId ?? "missing";
        var targetId = target.Target?.TargetId ?? target.Track?.TargetId ?? "";
        return $"{fieldId}\u001f{targetId}";
    }

    private IReadOnlyList<AnimationTarget> ReadTargets(
        ProjectTreeNode node,
        JsonObject preview,
        JsonObject config,
        JsonObject animation,
        JsonObject themeTokens)
    {
        var result = new List<AnimationTarget>();
        foreach (var input in ComponentPreviewInputSession.ReadRuntimeInputs(preview, config).Where((input) => input.Animation is not null))
            result.Add(new AnimationTarget(
                input.Id,
                "",
                input.Label,
                input,
                DesignPreviewTestValues.Value(preview, input),
                RuntimeAnimationFrameOrigin.FieldOwnerFrameOrigin(preview, preview, animation, input.Id, "", themeTokens),
                RuntimeAnimationFrameOrigin.FieldReferenceDurationFrames(preview, preview, animation, input.Id, "", themeTokens),
                (ownerFrame) => RuntimeAnimationFrameOrigin.ScreenFrameForOwnerFrame(
                    preview, preview, animation, "", ownerFrame, themeTokens)));
        foreach (var collection in ComponentPreviewInputSession.ReadRuntimeCollections(preview, config))
        {
            var items = DesignPreviewTestValues.CollectionItems(preview, collection);
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var targetId = item["id"]?.GetValue<string>() ?? "";
                foreach (var input in collection.Fields.Where((input) => input.Animation is not null))
                {
                    var targetInput = string.IsNullOrWhiteSpace(input.OptionsSourceCollectionJsonKey)
                        ? input
                        : input with { Options = RuntimeInputDynamicOptions.Resolve(_database, input, item) };
                    result.Add(new AnimationTarget(
                        targetInput.Id,
                        targetId,
                        targetInput.Label,
                        targetInput,
                        DesignPreviewTestValues.CollectionValue(item, targetInput),
                        RuntimeAnimationFrameOrigin.FieldOwnerFrameOrigin(preview, preview, animation, targetInput.Id, targetId, themeTokens),
                        RuntimeAnimationFrameOrigin.FieldReferenceDurationFrames(preview, preview, animation, targetInput.Id, targetId, themeTokens),
                        (ownerFrame) => RuntimeAnimationFrameOrigin.ScreenFrameForOwnerFrame(
                            preview, preview, animation, targetId, ownerFrame, themeTokens)));
                }
                if (!string.IsNullOrWhiteSpace(collection.ItemRuntimeContractJsonKey)
                    && item[collection.ItemRuntimeContractJsonKey] is JsonObject runtimeContract)
                {
                    foreach (var input in ComponentPreviewInputSession
                        .ReadRuntimeInputs(runtimeContract, new JsonObject())
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
                                preview, preview, animation, targetId, ownerFrame, themeTokens)));
                    }
                }
            }
        }
        return result;
    }

    private static JsonNode ValueNode(ValueKind kind, string value) => kind switch
    {
        ValueKind.Boolean => JsonValue.Create(bool.TryParse(value, out var boolean) && boolean)!,
        ValueKind.Integer => JsonValue.Create(int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) ? integer : 0)!,
        ValueKind.Decimal => JsonValue.Create(decimal.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0)!,
        _ => JsonValue.Create(value)!,
    };

    private static string InterpolationLabel(string interpolation) => interpolation switch
    {
        "writeOn" => "Write-on",
        "easeInOut" => "Ease in/out",
        "linear" => "Linear",
        _ => "Hold",
    };

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
        Func<double, int> ScreenFrameForOwnerFrame);

    private sealed record ResolvedAnimationTarget(
        AnimationTarget? Target,
        AnimationTrackView? Track)
    {
        public string Label => Target?.Label ?? $"Missing target · {Track!.FieldId}";
    }
}

internal sealed record AnimationTargetEditorContent(Control Content, int ActiveTrackCount);
