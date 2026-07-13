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
    private readonly Action<ProjectTreeNode> _reloadAndSelect;
    private readonly EditorSessionUiState _sessionUiState;
    private readonly Func<string, int> _screenFrame;
    private readonly Action<string, int> _setScreenFrame;
    private readonly PreviewPlaybackState _playbackState;
    private readonly Action _togglePlayback;

    public ModuleInstanceAnimationEditor(
        SpikeDatabase database,
        EditorDictionaryFieldServices dictionaryServices,
        Action onChanged,
        Action<ProjectTreeNode> reloadAndSelect,
        EditorSessionUiState sessionUiState,
        Func<string, int> screenFrame,
        Action<string, int> setScreenFrame,
        PreviewPlaybackState playbackState,
        Action togglePlayback)
    {
        _database = database;
        _dictionaryServices = dictionaryServices;
        _onChanged = onChanged;
        _reloadAndSelect = reloadAndSelect;
        _sessionUiState = sessionUiState;
        _screenFrame = screenFrame;
        _setScreenFrame = setScreenFrame;
        _playbackState = playbackState;
        _togglePlayback = togglePlayback;
    }

    public AnimationTargetEditorContent CreateTargetContent(ProjectTreeNode node, string targetId)
    {
        var instance = _database.GetModuleInstanceSettings(node.Id);
        var module = _database.GetModuleSettings(instance.ModuleId);
        var preview = DesignPreviewTestValues.Parse(_database.GetModuleInstanceRuntimePreviewJson(node.Id));
        var config = DesignPreviewTestValues.Parse(module.ConfigJson);
        var targets = ReadTargets(node, preview, config)
            .Where((target) => target.TargetId == targetId)
            .ToList();
        var document = new ModuleInstanceAnimationDocument(instance.AnimationJson);
        var resolvedTargets = document.Tracks
            .Where((track) => track.TargetId == targetId)
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
            content.Children.Add(CreateTimelineEditor(node, document, resolvedTargets, targetId));
        }
        return new AnimationTargetEditorContent(content, activeTrackCount);
    }

    private Control CreateTimelineEditor(
        ProjectTreeNode node,
        ModuleInstanceAnimationDocument document,
        IReadOnlyList<ResolvedAnimationTarget> targets,
        string targetId)
    {
        var duration = Math.Max(1, ModuleInstanceTimeline.DurationFrames(_database, node.Id));
        var currentFrame = Math.Clamp(_screenFrame(node.Id), 0, duration - 1);
        var selectionKey = $"{node.Id}:animation-properties:{targetId}";
        var selectedId = _sessionUiState.Selection(selectionKey);
        var selected = targets.FirstOrDefault((target) => TargetKey(target) == selectedId)
            ?? targets.FirstOrDefault((target) => target.Track is not null)
            ?? targets.First();
        selectedId = TargetKey(selected);
        _sessionUiState.Select(selectionKey, selectedId);
        var root = new StackPanel { Spacing = EditorUiDensity.Card(12) };
        var frameText = new TextBlock { MinWidth = 76, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = duration - 1,
            Value = currentFrame,
            TickFrequency = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var timelineHost = new ContentControl();
        var trackList = new StackPanel { Spacing = EditorUiDensity.Card(4) };
        var detailHost = new ContentControl();
        var currentKeyframeButton = EditorTimelineTransport.CreateNavigationButton(
            new TextBlock { Text = "◇", FontSize = 16, VerticalAlignment = VerticalAlignment.Center },
            "No keyframe at the current frame",
            34);
        var playbackButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.Play, 16),
            "Play Screen animation",
            42);
        EditorTimelineTransport.ApplyPrimaryStyle(playbackButton);
        var changingFrame = false;

        void SetFrame(int frame)
        {
            currentFrame = Math.Clamp(frame, 0, duration - 1);
            changingFrame = true;
            slider.Value = currentFrame;
            changingFrame = false;
            _setScreenFrame(node.Id, currentFrame);
            RefreshVisuals();
        }

        void SaveAndReload()
        {
            _database.UpdateModuleInstanceAnimationJson(node.Id, document.ToJson());
            _onChanged();
            _reloadAndSelect(node);
        }

        void RefreshVisuals()
        {
            frameText.Text = $"{currentFrame}/{duration - 1}";
            var selectedLocalFrame = currentFrame - (selected.Target?.ScreenFrameOrigin ?? 0);
            var hasCurrentKeyframe = selected.Track?.Keyframes.Any(
                (keyframe) => keyframe.Enabled && keyframe.Frame == selectedLocalFrame) == true;
            currentKeyframeButton.Content = new TextBlock
            {
                Text = hasCurrentKeyframe ? "◆" : "◇",
                FontSize = 16,
                Foreground = hasCurrentKeyframe
                    ? EditorAnimationVisuals.CurrentKeyframeBrush
                    : EditorAnimationVisuals.InactiveTrackBrush,
                VerticalAlignment = VerticalAlignment.Center,
            };
            EditorAccessibility.Describe(
                currentKeyframeButton,
                hasCurrentKeyframe ? "Keyframe at the current frame" : "No keyframe at the current frame");
            playbackButton.Content = EditorIcons.Create(
                _playbackState.IsPlaying ? EditorIcons.Pause : EditorIcons.Play,
                16);
            EditorAccessibility.Describe(
                playbackButton,
                _playbackState.IsPlaying ? "Pause Screen animation" : "Play Screen animation");
            timelineHost.Content = CreateMiniTimeline(targets, selected, currentFrame, duration, SetFrame);
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
                    Foreground = selectedTarget && active ? EditorAnimationVisuals.ActiveTrackBrush : null,
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
                currentFrame,
                SetFrame,
                SaveAndReload);
        }

        var firstFrameButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelineFirstFrame, 16),
            "First Screen frame");
        firstFrameButton.Click += (_, _) => SetFrame(0);
        var previousFrameButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelinePreviousFrame, 16),
            "Previous frame");
        previousFrameButton.Click += (_, _) => SetFrame(currentFrame - 1);
        currentKeyframeButton.Click += (_, _) => SetFrame(currentFrame);
        playbackButton.Click += (_, _) => _togglePlayback();
        var nextFrameButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelineNextFrame, 16),
            "Next frame");
        nextFrameButton.Click += (_, _) => SetFrame(currentFrame + 1);
        var lastFrameButton = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.Create(EditorIcons.TimelineLastFrame, 16),
            "Last Screen frame");
        lastFrameButton.Click += (_, _) => SetFrame(duration - 1);
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
                frameText,
            },
        };
        root.Children.Add(transport);
        slider.ValueChanged += (_, args) =>
        {
            if (!changingFrame) SetFrame((int)Math.Round(args.NewValue, MidpointRounding.AwayFromZero));
        };
        root.Children.Add(slider);
        root.Children.Add(timelineHost);
        root.Children.Add(EditorGroupBlock.CreateSeparator());
        root.Children.Add(trackList);
        root.Children.Add(detailHost);
        void OnPlaybackChanged()
        {
            currentFrame = Math.Clamp(_screenFrame(node.Id), 0, duration - 1);
            changingFrame = true;
            slider.Value = currentFrame;
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
        int screenFrame,
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
        var localFrame = screenFrame - target.ScreenFrameOrigin;
        var enabledKeyframes = selected.Track.Keyframes.Where((keyframe) => keyframe.Enabled).ToList();
        var exact = enabledKeyframes.FirstOrDefault((keyframe) => keyframe.Frame == localFrame);
        var previous = enabledKeyframes.LastOrDefault((keyframe) => keyframe.Frame < localFrame);
        var next = enabledKeyframes.FirstOrDefault((keyframe) => keyframe.Frame > localFrame);
        var panel = new StackPanel { Spacing = EditorUiDensity.Card(9) };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*,Auto"), ColumnSpacing = EditorUiDensity.Card(6) };
        AddTransportButton(header, 0, "‹", "Previous keyframe", () => { if (previous is not null) setFrame(target.ScreenFrameOrigin + previous.Frame); }, previous is not null);
        var keyframeButton = new Button
        {
            Content = exact is null ? "◇" : "◆",
            Width = 34,
            Height = 30,
            Padding = EditorUiDensity.CardThickness(0),
            Foreground = exact is not null
                ? EditorAnimationVisuals.CurrentKeyframeBrush
                : EditorAnimationVisuals.ActiveTrackBrush,
        };
        EditorAccessibility.Describe(keyframeButton, exact is null ? "Create keyframe at current frame" : "Update current keyframe");
        Grid.SetColumn(keyframeButton, 1);
        header.Children.Add(keyframeButton);
        AddTransportButton(header, 2, "›", "Next keyframe", () => { if (next is not null) setFrame(target.ScreenFrameOrigin + next.Frame); }, next is not null);
        var count = new TextBlock
        {
            Text = EditorUiText.Count(enabledKeyframes.Count, "keyframe"),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.72,
        };
        Grid.SetColumn(count, 4);
        header.Children.Add(count);
        panel.Children.Add(header);

        var effective = exact ?? previous;
        var displayedValue = effective?.Value is null ? target.BaseValue : DisplayValue(effective.Value, target.Input.ValueKind);
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
        keyframeButton.Click += (_, _) => SaveValue(valueControl.Value, interpolationControl.Value);
        keyframeButton.IsEnabled = localFrame >= 0;
        valueControl.ValueCommitted += (_, value) => SaveValue(value, interpolationControl.Value);
        panel.Children.Add(valueControl);
        interpolationControl.ValueCommitted += (_, interpolation) =>
        {
            if (exact is not null) SaveValue(valueControl.Value, interpolation);
        };
        panel.Children.Add(interpolationControl);
        if (exact is not null)
        {
            var delete = new Button
            {
                Content = "Delete keyframe",
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            delete.Click += (_, _) =>
            {
                document.RemoveKeyframe(target.FieldId, target.TargetId, localFrame);
                saveAndReload();
            };
            panel.Children.Add(delete);
        }
        return panel;
    }

    private static Control CreateMiniTimeline(
        IReadOnlyList<ResolvedAnimationTarget> targets,
        ResolvedAnimationTarget active,
        int currentFrame,
        int duration,
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
            var line = new Border
            {
                Width = width,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = EditorAnimationVisuals.TimelineBrush,
            };
            Canvas.SetTop(line, 13);
            canvas.Children.Add(line);
            foreach (var target in targets.Where((candidate) => candidate.Track is not null))
            {
                foreach (var keyframe in target.Track!.Keyframes.Where((candidate) => candidate.Enabled))
                {
                    var screenKeyframe = (target.Target?.ScreenFrameOrigin ?? 0) + keyframe.Frame;
                    var isActive = ReferenceEquals(target, active);
                    var isCurrent = isActive && screenKeyframe == currentFrame;
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
                    marker.Click += (_, _) => setFrame(screenKeyframe);
                    Canvas.SetLeft(marker, Math.Max(0, Math.Min(
                        width - 24,
                        screenKeyframe / (double)Math.Max(1, duration - 1) * (width - 24))));
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

    private IReadOnlyList<AnimationTarget> ReadTargets(ProjectTreeNode node, JsonObject preview, JsonObject config)
    {
        var result = new List<AnimationTarget>();
        foreach (var input in ComponentPreviewInputSession.ReadRuntimeInputs(preview, config).Where((input) => input.Animation is not null))
            result.Add(new AnimationTarget(input.Id, "", input.Label, input, DesignPreviewTestValues.Value(preview, input), 0));
        foreach (var collection in ComponentPreviewInputSession.ReadRuntimeCollections(preview, config))
        {
            var items = DesignPreviewTestValues.CollectionItems(preview, collection);
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var targetId = item["id"]?.GetValue<string>() ?? "";
                foreach (var input in collection.Fields.Where((input) => input.Animation is not null))
                    result.Add(new AnimationTarget(
                        input.Id,
                        targetId,
                        input.Label,
                        input,
                        DesignPreviewTestValues.CollectionValue(item, input),
                        RuntimeAnimationFrameOrigin.ScreenFrame(preview, preview, input.Id, targetId)));
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

    private static string DisplayValue(JsonNode value, ValueKind kind)
    {
        if (kind == ValueKind.Boolean && value is JsonValue boolean && boolean.TryGetValue<bool>(out var flag)) return flag ? "true" : "false";
        if (value is JsonValue scalar && scalar.TryGetValue<string>(out var text)) return text;
        return value.ToJsonString().Trim('"');
    }

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
        string content,
        string accessibleName,
        Action action,
        bool enabled = true)
    {
        var button = new Button
        {
            Content = content,
            Width = 34,
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
        int ScreenFrameOrigin);

    private sealed record ResolvedAnimationTarget(
        AnimationTarget? Target,
        AnimationTrackView? Track)
    {
        public string Label => Target?.Label ?? $"Missing target · {Track!.FieldId}";
    }
}

internal sealed record AnimationTargetEditorContent(Control Content, int ActiveTrackCount);
